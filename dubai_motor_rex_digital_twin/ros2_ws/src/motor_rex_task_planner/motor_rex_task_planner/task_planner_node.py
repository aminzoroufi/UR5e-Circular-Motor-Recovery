import json
from pathlib import Path
from typing import Dict, List

from ament_index_python.packages import get_package_share_directory
from builtin_interfaces.msg import Duration
import rclpy
from rclpy.node import Node
from std_msgs.msg import String
from trajectory_msgs.msg import JointTrajectory, JointTrajectoryPoint

from motor_rex_task_planner.moveit_named_pose_executor import MoveItNamedPoseExecutor
from motor_rex_task_planner.rex_decision_client import (
    parse_component_decisions,
    parse_component_poses,
)
from motor_rex_task_planner.robot_action_manager import RobotActionManager, RobotTask


WORKFLOW_PREFIX = [
    {"type": "system", "description": "Load motor passport"},
    {"type": "moveit_pose", "description": "Scan motor", "pose_name": "inspection_pose"},
    {"type": "system", "description": "Evaluate health"},
    {"type": "system", "description": "Generate Re-X decisions"},
    {"type": "moveit_pose", "description": "Move robot to home", "pose_name": "home"},
]

WORKFLOW_SUFFIX = [{"type": "system", "description": "Publish final summary"}]

SORT_STATION_CONTACTS = {
    "reuse_bin": [1.02, -0.58, 0.64],
    "repair_bin": [0.72, -1.08, 0.64],
    "replace_bin": [0.08, -1.08, 0.64],
    "recycle_bin": [-0.22, -0.52, 0.64],
}

TOOL_TO_PART_OFFSET = 0.17
APPROACH_CLEARANCE = 0.10
CARRY_CLEARANCE = 0.16
APPROACH_TOLERANCE = 0.06
MAX_MOTION_RETRIES = 5
GRIPPER_CLOSE_DWELL_SEC = 2.00
GRIPPER_OPEN_DWELL_SEC = 0.75


def _default_project_root() -> Path:
    return Path(__file__).resolve().parents[4]


def _default_pose_config() -> Path:
    return Path(get_package_share_directory("motor_rex_task_planner")) / "config" / "ur5e_named_poses.yaml"


class TaskPlannerNode(Node):
    def __init__(self) -> None:
        super().__init__("motor_rex_task_planner")
        project_root = _default_project_root()
        self.declare_parameter("execution_mode", "simulated")
        self.declare_parameter("moveit_pose_config", str(_default_pose_config()))
        self.declare_parameter("moveit_action_name", "/move_action")
        self.declare_parameter("moveit_group_name", "arm")
        self.declare_parameter("moveit_planning_time_sec", 5.0)
        self.declare_parameter("moveit_velocity_scale", 0.60)
        self.declare_parameter("moveit_acceleration_scale", 0.60)
        self.declare_parameter("moveit_waypoint_time_step_sec", 0.25)
        self.declare_parameter("moveit_post_execute_settle_sec", 0.35)
        self.declare_parameter("step_period_sec", 1.0)
        self.declare_parameter("task_log_path", str(project_root / "data" / "task_progress_latest.json"))

        self.decisions_sub = self.create_subscription(
            String,
            "/motor_rex/component_decisions",
            self.on_decisions,
            10,
        )
        self.poses_sub = self.create_subscription(
            String,
            "/motor_rex/component_poses",
            self.on_poses,
            10,
        )

        self.task_progress_pub = self.create_publisher(String, "/motor_rex/task_progress", 10)
        self.current_action_pub = self.create_publisher(String, "/motor_rex/current_robot_action", 10)
        self.final_report_pub = self.create_publisher(String, "/motor_rex/final_report", 10)
        self.gripper_command_pub = self.create_publisher(
            JointTrajectory,
            "/gripper_controller/joint_trajectory",
            10,
        )

        self.action_manager = RobotActionManager()
        self.execution_mode = str(self.get_parameter("execution_mode").value)
        self.task_log_path = Path(str(self.get_parameter("task_log_path").value))
        self.moveit_executor = (
            self._create_moveit_executor("arm", "arm_controller", "tool0")
            if self.execution_mode == "moveit"
            else None
        )
        self.decisions: List[Dict] = []
        self.poses: Dict[str, Dict] = {}
        self.component_tasks: List[RobotTask] = []
        self.workflow: List[Dict] = []
        self.task_history: List[Dict] = []
        self.current_index = 0
        self.started = False
        self.waiting_for_moveit = False
        self.motion_retry_counts: Dict[str, int] = {}
        self.timer = self.create_timer(float(self.get_parameter("step_period_sec").value), self.tick)
        self.gripper_dwell_timer = None
        self.get_logger().info(f"Task planner execution_mode={self.execution_mode}")

    def _create_moveit_executor(
        self,
        group_name: str,
        controller_name: str,
        end_effector_link: str,
    ) -> MoveItNamedPoseExecutor:
        return MoveItNamedPoseExecutor(
            node=self,
            pose_config_path=str(self.get_parameter("moveit_pose_config").value),
            action_name=str(self.get_parameter("moveit_action_name").value),
            group_name=group_name,
            planning_time_sec=float(self.get_parameter("moveit_planning_time_sec").value),
            velocity_scale=float(self.get_parameter("moveit_velocity_scale").value),
            acceleration_scale=float(self.get_parameter("moveit_acceleration_scale").value),
            waypoint_time_step_sec=float(self.get_parameter("moveit_waypoint_time_step_sec").value),
            post_execute_settle_sec=float(self.get_parameter("moveit_post_execute_settle_sec").value),
            controller_name=controller_name,
            end_effector_link=end_effector_link,
        )

    def on_decisions(self, msg: String) -> None:
        self.decisions = parse_component_decisions(msg.data)
        self.try_start()

    def on_poses(self, msg: String) -> None:
        self.poses = parse_component_poses(msg.data)
        self.try_start()

    def try_start(self) -> None:
        if self.started or not self.decisions or not self.poses:
            return
        self.component_tasks = self.action_manager.create_component_tasks(self.decisions, self.poses)
        workflow: List[Dict] = [dict(task) for task in WORKFLOW_PREFIX]
        workflow.extend({"type": "robot", "task": task} for task in self.component_tasks)
        workflow.extend(dict(task) for task in WORKFLOW_SUFFIX)
        self.workflow = workflow
        self.started = True
        self.get_logger().info(f"Started task workflow with {len(self.workflow)} steps")

    def tick(self) -> None:
        if not self.started or self.waiting_for_moveit or self.current_index >= len(self.workflow):
            return

        item = self.workflow[self.current_index]
        if item["type"] == "system":
            self._complete_step(item["description"], {"action": item["description"], "status": "completed"})
        elif item["type"] == "moveit_pose":
            self._start_pose_step(item)
        elif item["type"] == "robot":
            self._start_robot_step(item["task"])
        else:
            self._complete_step(
                f"Unknown workflow item: {item['type']}",
                {"action": "unknown_workflow_item", "status": "failed"},
                status="failed",
            )

    def _start_pose_step(self, item: Dict) -> None:
        pose_name = item["pose_name"]
        if self.moveit_executor is None:
            payload = {
                "action": item["description"],
                "moveit_named_pose": pose_name,
                "execution_mode": self.execution_mode,
                "status": "completed",
            }
            self._complete_step(item["description"], payload)
            return

        started = self.moveit_executor.start_named_pose(
            pose_name,
            lambda result: self._on_pose_step_done(item, result),
        )
        if not started:
            self._publish_action(
                {
                    "action": item["description"],
                    "moveit_named_pose": pose_name,
                    "execution_mode": "moveit",
                    "status": "waiting_for_moveit_action_server",
                }
            )
            return

        self.waiting_for_moveit = True
        self._publish_action(
            {
                "action": item["description"],
                "moveit_named_pose": pose_name,
                "execution_mode": "moveit",
                "status": "executing",
            }
        )

    def _on_pose_step_done(self, item: Dict, result: Dict) -> None:
        self.waiting_for_moveit = False
        status = result["status"]
        payload = {
            "action": item["description"],
            "moveit_named_pose": item["pose_name"],
            "execution_mode": "moveit",
            **result,
        }
        self._complete_step(item["description"], payload, status=status)

    def _start_robot_step(self, task: RobotTask) -> None:
        if self.moveit_executor is None:
            result = self.action_manager.simulate_execution(task)
            description = self._task_description(task)
            self._complete_step(description, result)
            return

        source = self._pose_position(task.source_pose)
        self._command_gripper(task.robot_role, opened=True)
        self.waiting_for_moveit = True
        self._publish_motion_action(task, "approach_component", source, "open")
        self._start_task_motion(
            task,
            "source_approach",
            self._tool_target(source, APPROACH_CLEARANCE),
            lambda result: self._on_source_approach_done(task, result),
        )

    def _on_source_approach_done(self, task: RobotTask, result: Dict) -> None:
        if not self._motion_succeeded(task, "source approach", result):
            return
        source = self._pose_position(task.source_pose)
        self._publish_motion_action(task, "contact_component", source, "open")
        self._start_task_motion(
            task,
            "source_contact",
            self._tool_target(source, 0.0),
            lambda contact_result: self._on_source_contact_done(task, contact_result),
        )

    def _on_source_contact_done(self, task: RobotTask, result: Dict) -> None:
        if not self._motion_succeeded(task, "source contact", result):
            return

        self._command_gripper(task.robot_role, opened=False)
        self._publish_action(
            {
                "action": "grasp_component",
                "component_id": task.component_id,
                "execution_mode": "moveit",
                "robot_role": task.robot_role,
                "gripper": "closed",
                "status": "executing",
            }
        )
        self.gripper_dwell_timer = self.create_timer(
            GRIPPER_CLOSE_DWELL_SEC,
            lambda: self._on_grasp_dwell_done(task),
        )

    def _on_grasp_dwell_done(self, task: RobotTask) -> None:
        if self.gripper_dwell_timer is not None:
            self.gripper_dwell_timer.cancel()
            self.gripper_dwell_timer = None
        source = self._pose_position(task.source_pose)
        self._start_task_motion(
            task,
            "source_lift",
            self._tool_target(source, CARRY_CLEARANCE),
            lambda lift_result: self._on_source_lift_done(task, lift_result),
        )

    def _on_source_lift_done(self, task: RobotTask, result: Dict) -> None:
        if not self._motion_succeeded(task, "source lift", result):
            return

        self._publish_action(
            {
                "action": "transfer_via_home",
                "component_id": task.component_id,
                "execution_mode": "moveit",
                "robot_role": task.robot_role,
                "gripper": "closed",
                "status": "executing",
            }
        )
        self._start_task_named_motion(
            task,
            "transfer_home",
            "home",
            lambda home_result: self._on_transfer_home_done(task, home_result),
        )

    def _on_transfer_home_done(self, task: RobotTask, result: Dict) -> None:
        if not self._motion_succeeded(task, "transfer waypoint", result):
            return

        target = self._task_target_position(task)
        self._publish_motion_action(task, "approach_target", target, "closed")
        self._start_task_motion(
            task,
            "target_approach",
            self._tool_target(target, CARRY_CLEARANCE),
            lambda approach_result: self._on_target_approach_done(task, approach_result),
        )

    def _on_target_approach_done(self, task: RobotTask, result: Dict) -> None:
        if not self._motion_succeeded(task, "target approach", result):
            return
        target = self._task_target_position(task)
        self._publish_motion_action(task, "contact_target", target, "closed")
        self._start_task_motion(
            task,
            "target_contact",
            self._tool_target(target, 0.0),
            lambda contact_result: self._on_target_contact_done(task, contact_result),
        )

    def _on_target_contact_done(self, task: RobotTask, result: Dict) -> None:
        if not self._motion_succeeded(task, "target contact", result):
            return

        self._publish_action(
            {
                "action": "release_component",
                "component_id": task.component_id,
                "target_station": task.target_station,
                "execution_mode": "moveit",
                "robot_role": task.robot_role,
                "gripper": "open",
                "status": "executing",
            }
        )
        self._command_gripper(task.robot_role, opened=True)
        self.gripper_dwell_timer = self.create_timer(
            GRIPPER_OPEN_DWELL_SEC,
            lambda: self._on_release_dwell_done(task),
        )

    def _on_release_dwell_done(self, task: RobotTask) -> None:
        if self.gripper_dwell_timer is not None:
            self.gripper_dwell_timer.cancel()
            self.gripper_dwell_timer = None
        target = self._task_target_position(task)
        self._start_task_motion(
            task,
            "target_retreat",
            self._tool_target(target, CARRY_CLEARANCE),
            lambda retreat_result: self._on_target_retreat_done(task, retreat_result),
        )

    def _on_target_retreat_done(self, task: RobotTask, result: Dict) -> None:
        if not self._motion_succeeded(task, "target retreat", result):
            return
        self._start_task_named_motion(
            task,
            "post_task_home",
            "home",
            lambda home_result: self._on_post_task_home_done(task, home_result),
        )

    def _on_post_task_home_done(self, task: RobotTask, result: Dict) -> None:
        if not self._motion_succeeded(task, "post-task home", result):
            return
        self.waiting_for_moveit = False
        payload = self.action_manager.simulate_execution(task)
        payload.update(
            {
                "execution_mode": "moveit",
                "robot_role": task.robot_role,
                "motion_profile": "approach-contact-grasp-carry-contact-release",
                "status": "completed",
                "gripper": "open",
            }
        )
        self._complete_step(self._task_description(task), payload)

    def _start_task_motion(
        self,
        task: RobotTask,
        phase: str,
        position: List[float],
        done_callback,
    ) -> None:
        executor = self.moveit_executor
        tolerance = (
            APPROACH_TOLERANCE
            if phase in {"source_approach", "source_lift", "target_approach", "target_retreat"}
            else 0.04
        )
        if executor is None or not executor.start_cartesian_pose(
            f"{task.component_id}_{phase}",
            position,
            lambda result: self._on_task_motion_result(
                task,
                phase,
                position,
                done_callback,
                result,
            ),
            tolerance=tolerance,
        ):
            self._fail_robot_task(
                task,
                phase,
                {"status": "failed", "message": "MoveIt action server unavailable"},
            )

    def _start_task_named_motion(
        self,
        task: RobotTask,
        phase: str,
        pose_name: str,
        done_callback,
    ) -> None:
        executor = self.moveit_executor
        if executor is None or not executor.start_named_pose(
            pose_name,
            lambda result: self._on_task_named_motion_result(
                task,
                phase,
                pose_name,
                done_callback,
                result,
            ),
        ):
            self._fail_robot_task(
                task,
                phase,
                {"status": "failed", "message": "MoveIt action server unavailable"},
            )

    def _on_task_named_motion_result(
        self,
        task: RobotTask,
        phase: str,
        pose_name: str,
        done_callback,
        result: Dict,
    ) -> None:
        retry_key = f"{task.component_id}:{phase}"
        if result.get("status") != "completed":
            retries = self.motion_retry_counts.get(retry_key, 0)
            if retries < MAX_MOTION_RETRIES:
                self.motion_retry_counts[retry_key] = retries + 1
                self.get_logger().warning(
                    f"Retry {retries + 1}/{MAX_MOTION_RETRIES} for "
                    f"{task.component_id} {phase}: "
                    f"{result.get('message', 'planning failed')}"
                )
                self._start_task_named_motion(task, phase, pose_name, done_callback)
                return
        self.motion_retry_counts.pop(retry_key, None)
        done_callback(result)

    def _on_task_motion_result(
        self,
        task: RobotTask,
        phase: str,
        position: List[float],
        done_callback,
        result: Dict,
    ) -> None:
        retry_key = f"{task.component_id}:{phase}"
        if result.get("status") != "completed":
            retries = self.motion_retry_counts.get(retry_key, 0)
            if retries < MAX_MOTION_RETRIES:
                self.motion_retry_counts[retry_key] = retries + 1
                self.get_logger().warning(
                    f"Retry {retries + 1}/{MAX_MOTION_RETRIES} for "
                    f"{task.component_id} {phase}: "
                    f"{result.get('message', 'planning failed')}"
                )
                self._start_task_motion(task, phase, position, done_callback)
                return
        self.motion_retry_counts.pop(retry_key, None)
        done_callback(result)

    def _motion_succeeded(self, task: RobotTask, phase: str, result: Dict) -> bool:
        if result.get("status") == "completed":
            return True
        self._fail_robot_task(task, phase, result)
        return False

    def _fail_robot_task(self, task: RobotTask, phase: str, result: Dict) -> None:
        self.waiting_for_moveit = False
        self._command_gripper(task.robot_role, opened=True)
        if phase in {
            "source lift",
            "transfer waypoint",
            "target approach",
            "target contact",
            "target retreat",
        }:
            self._publish_action(
                {
                    "action": "release_component",
                    "component_id": task.component_id,
                    "target_station": task.target_station,
                    "execution_mode": "moveit",
                    "robot_role": task.robot_role,
                    "gripper": "open",
                    "status": "executing",
                }
            )
        payload = self.action_manager.simulate_execution(task)
        payload.update(
            {
                "execution_mode": "moveit",
                "status": "failed",
                "failed_phase": phase,
                "moveit_result": result,
                "robot_role": task.robot_role,
            }
        )
        self._complete_step(f"{self._task_description(task)} failed at {phase}", payload, status="failed")

    def _publish_motion_action(
        self,
        task: RobotTask,
        action: str,
        part_position: List[float],
        gripper: str,
    ) -> None:
        self._publish_action(
            {
                "action": action,
                "component_id": task.component_id,
                "target_station": task.target_station,
                "target_part_position": part_position,
                "execution_mode": "moveit",
                "robot_role": task.robot_role,
                "gripper": gripper,
                "status": "executing",
            }
        )

    def _task_target_position(self, task: RobotTask) -> List[float]:
        return list(SORT_STATION_CONTACTS.get(task.target_station, SORT_STATION_CONTACTS["reuse_bin"]))

    @staticmethod
    def _pose_position(pose: Dict) -> List[float]:
        position = pose.get("position", [0.0, 0.0, 0.6])
        return [float(position[0]), float(position[1]), float(position[2])]

    @staticmethod
    def _tool_target(part_position: List[float], clearance: float) -> List[float]:
        return [
            float(part_position[0]),
            float(part_position[1]),
            float(part_position[2]) + TOOL_TO_PART_OFFSET + clearance,
        ]

    @staticmethod
    def _task_description(task: RobotTask) -> str:
        return f"Pick {task.component_id} -> {task.target_station} ({task.decision})"

    def _complete_step(self, description: str, action_payload: Dict, status: str = "completed") -> None:
        self.current_index += 1
        progress = {
            "step": self.current_index,
            "total_steps": len(self.workflow),
            "description": description,
            "status": status,
        }
        self.task_history.append(progress)
        self._publish_action(action_payload)

        progress_msg = String()
        progress_msg.data = json.dumps(progress)
        self.task_progress_pub.publish(progress_msg)
        self.get_logger().info(description)
        self._write_task_log()

        if self.current_index == len(self.workflow):
            self.publish_final_report()

    def _command_gripper(self, robot_role: str, opened: bool) -> None:
        trajectory = JointTrajectory()
        trajectory.joint_names = ["robotiq_85_left_knuckle_joint"]
        point = JointTrajectoryPoint()
        point.positions = [0.0] if opened else [0.7929]
        point.time_from_start = Duration(sec=0, nanosec=450_000_000)
        trajectory.points.append(point)
        self.gripper_command_pub.publish(trajectory)

    def _publish_action(self, action_payload: Dict) -> None:
        action_msg = String()
        action_msg.data = json.dumps(action_payload)
        self.current_action_pub.publish(action_msg)

    def _write_task_log(self) -> None:
        payload = {
            "workflow_status": "running"
            if self.current_index < len(self.workflow)
            else "completed",
            "execution_mode": self.execution_mode,
            "target_robot": "Universal Robots UR5e",
            "target_gripper": "Robotiq 2F-85 style parallel gripper",
            "current_step": self.current_index,
            "total_steps": len(self.workflow),
            "tasks": self.task_history,
        }
        self.task_log_path.parent.mkdir(parents=True, exist_ok=True)
        self.task_log_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

    def publish_final_report(self) -> None:
        counts: Dict[str, int] = {}
        for component in self.decisions:
            counts[component["decision"]] = counts.get(component["decision"], 0) + 1
        failed_steps = sum(1 for task in self.task_history if task["status"] != "completed")
        report = {
            "workflow_status": "completed" if failed_steps == 0 else "completed_with_failures",
            "execution_mode": self.execution_mode,
            "target_robot": "Universal Robots UR5e",
            "target_gripper": "Robotiq 2F-85 style parallel gripper",
            "components_processed": len(self.decisions),
            "decision_counts": counts,
            "failed_steps": failed_steps,
            "claim": (
                "Hardware-ready digital twin architecture for robotic electric motor recovery; "
                "the MVP uses UR5e-compatible named poses and can replace placeholder "
                "motion, gripper, sensors, and motor primitives with physical systems."
            ),
        }
        msg = String()
        msg.data = json.dumps(report)
        self.final_report_pub.publish(msg)
        self._write_task_log()


def main() -> None:
    rclpy.init()
    node = TaskPlannerNode()
    try:
        rclpy.spin(node)
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    main()
