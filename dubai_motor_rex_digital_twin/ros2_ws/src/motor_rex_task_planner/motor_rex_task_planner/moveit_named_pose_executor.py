from __future__ import annotations

from pathlib import Path
from typing import Callable, Dict, List, Optional

from builtin_interfaces.msg import Duration
from geometry_msgs.msg import Pose
import yaml
from moveit_msgs.action import ExecuteTrajectory, MoveGroup
from moveit_msgs.msg import (
    BoundingVolume,
    Constraints,
    JointConstraint,
    MoveItErrorCodes,
    PositionConstraint,
)
from rclpy.action import ActionClient
from shape_msgs.msg import SolidPrimitive


MoveDoneCallback = Callable[[Dict], None]


class MoveItNamedPoseExecutor:
    """Small MoveIt action wrapper for MVP named-pose execution.

    The first physical-ready workflow uses known UR5e-compatible joint targets.
    Component-specific Cartesian approach and grasp synthesis can replace this
    class later without changing the decision engine or dashboard contract.
    """

    def __init__(
        self,
        node,
        pose_config_path: str | Path,
        action_name: str = "/move_action",
        group_name: str = "arm",
        planning_time_sec: float = 5.0,
        velocity_scale: float = 0.35,
        acceleration_scale: float = 0.35,
        waypoint_time_step_sec: float = 1.0,
        post_execute_settle_sec: float = 0.35,
        controller_name: str = "arm_controller",
        end_effector_link: str = "tool0",
    ) -> None:
        self.node = node
        self.group_name = group_name
        self.planning_time_sec = planning_time_sec
        self.velocity_scale = velocity_scale
        self.acceleration_scale = acceleration_scale
        self.waypoint_time_step_sec = waypoint_time_step_sec
        self.post_execute_settle_sec = post_execute_settle_sec
        self.controller_name = controller_name
        self.end_effector_link = end_effector_link
        self.action_client = ActionClient(node, MoveGroup, action_name)
        self.execute_client = ActionClient(node, ExecuteTrajectory, "/execute_trajectory")

        config = self._load_pose_config(Path(pose_config_path))
        self.joint_names: List[str] = config["joint_names"]
        self.named_poses: Dict[str, List[float]] = config["named_poses"]
        self.active_pose: Optional[str] = None
        self.done_callback: Optional[MoveDoneCallback] = None
        self.last_planning_time_sec = 0.0
        self.settle_timer = None

    def start_named_pose(self, pose_name: str, done_callback: MoveDoneCallback) -> bool:
        if pose_name not in self.named_poses:
            done_callback(
                {
                    "pose_name": pose_name,
                    "status": "failed",
                    "error": f"Unknown named pose: {pose_name}",
                }
            )
            return False

        if not self.action_client.wait_for_server(timeout_sec=0.2):
            return False

        self.active_pose = pose_name
        self.done_callback = done_callback
        goal = self._build_goal(pose_name)
        send_future = self.action_client.send_goal_async(goal)
        send_future.add_done_callback(self._on_goal_response)
        return True

    def start_cartesian_pose(
        self,
        target_name: str,
        position: List[float],
        done_callback: MoveDoneCallback,
        tolerance: float = 0.04,
    ) -> bool:
        if not self.action_client.wait_for_server(timeout_sec=0.2):
            return False

        self.active_pose = target_name
        self.done_callback = done_callback
        goal = self._build_cartesian_goal(target_name, position, tolerance)
        send_future = self.action_client.send_goal_async(goal)
        send_future.add_done_callback(self._on_goal_response)
        return True

    def _build_goal(self, pose_name: str) -> MoveGroup.Goal:
        goal = MoveGroup.Goal()
        request = goal.request
        request.group_name = self.group_name
        request.pipeline_id = "ompl"
        request.num_planning_attempts = 3
        request.allowed_planning_time = self.planning_time_sec
        request.max_velocity_scaling_factor = self.velocity_scale
        request.max_acceleration_scaling_factor = self.acceleration_scale
        request.start_state.is_diff = True
        request.goal_constraints = [self._constraints_for_pose(pose_name)]

        goal.planning_options.plan_only = True
        goal.planning_options.look_around = False
        goal.planning_options.replan = True
        goal.planning_options.replan_attempts = 1
        goal.planning_options.planning_scene_diff.is_diff = True
        return goal

    def _build_cartesian_goal(
        self,
        target_name: str,
        position: List[float],
        tolerance: float,
    ) -> MoveGroup.Goal:
        goal = MoveGroup.Goal()
        request = goal.request
        request.group_name = self.group_name
        request.pipeline_id = "ompl"
        request.num_planning_attempts = 12
        request.allowed_planning_time = max(self.planning_time_sec, 10.0)
        request.max_velocity_scaling_factor = self.velocity_scale
        request.max_acceleration_scaling_factor = self.acceleration_scale
        request.start_state.is_diff = True

        constraints = Constraints()
        constraints.name = target_name

        position_constraint = PositionConstraint()
        position_constraint.header.frame_id = "world"
        position_constraint.link_name = self.end_effector_link
        position_constraint.weight = 1.0
        position_constraint.constraint_region = BoundingVolume()
        primitive = SolidPrimitive()
        primitive.type = SolidPrimitive.SPHERE
        primitive.dimensions = [float(tolerance)]
        region_pose = Pose()
        region_pose.position.x = float(position[0])
        region_pose.position.y = float(position[1])
        region_pose.position.z = float(position[2])
        region_pose.orientation.w = 1.0
        position_constraint.constraint_region.primitives = [primitive]
        position_constraint.constraint_region.primitive_poses = [region_pose]
        constraints.position_constraints = [position_constraint]

        request.goal_constraints = [constraints]
        goal.planning_options.plan_only = True
        goal.planning_options.look_around = False
        goal.planning_options.replan = True
        goal.planning_options.replan_attempts = 2
        goal.planning_options.planning_scene_diff.is_diff = True
        return goal

    def _constraints_for_pose(self, pose_name: str) -> Constraints:
        constraints = Constraints()
        constraints.name = pose_name
        positions = self.named_poses[pose_name]
        for joint_name, position in zip(self.joint_names, positions):
            constraint = JointConstraint()
            constraint.joint_name = joint_name
            constraint.position = float(position)
            constraint.tolerance_above = 0.01
            constraint.tolerance_below = 0.01
            constraint.weight = 1.0
            constraints.joint_constraints.append(constraint)
        return constraints

    def _on_goal_response(self, future) -> None:
        try:
            goal_handle = future.result()
        except Exception as exc:  # pragma: no cover - defensive ROS callback guard
            self._finish(False, f"MoveIt goal send failed: {exc}")
            return

        if not goal_handle.accepted:
            self._finish(False, "MoveIt rejected the goal")
            return

        result_future = goal_handle.get_result_async()
        result_future.add_done_callback(self._on_result)

    def _on_result(self, future) -> None:
        try:
            wrapped_result = future.result()
            result = wrapped_result.result
            error_code = result.error_code
            success = error_code.val == MoveItErrorCodes.SUCCESS
            self.last_planning_time_sec = float(result.planning_time)
            if not success:
                message = error_code.message or f"MoveIt planning error {error_code.val}"
                self._finish(
                    False,
                    message,
                    {
                        "planning_time_sec": round(self.last_planning_time_sec, 3),
                        "error_code": int(error_code.val),
                    },
                )
                return

            trajectory = result.planned_trajectory
            self._ensure_increasing_waypoint_times(trajectory)
            if not self.execute_client.wait_for_server(timeout_sec=0.5):
                self._finish(False, "MoveIt execute_trajectory action server unavailable")
                return

            execute_goal = ExecuteTrajectory.Goal()
            execute_goal.trajectory = trajectory
            execute_goal.controller_names = [self.controller_name]
            execute_future = self.execute_client.send_goal_async(execute_goal)
            execute_future.add_done_callback(self._on_execute_goal_response)
        except Exception as exc:  # pragma: no cover - defensive ROS callback guard
            self._finish(False, f"MoveIt result failed: {exc}")

    def _on_execute_goal_response(self, future) -> None:
        try:
            goal_handle = future.result()
        except Exception as exc:  # pragma: no cover - defensive ROS callback guard
            self._finish(False, f"MoveIt execute goal send failed: {exc}")
            return

        if not goal_handle.accepted:
            self._finish(False, "MoveIt execute_trajectory rejected the goal")
            return

        result_future = goal_handle.get_result_async()
        result_future.add_done_callback(self._on_execute_result)

    def _on_execute_result(self, future) -> None:
        try:
            wrapped_result = future.result()
            error_code = wrapped_result.result.error_code
            success = error_code.val == MoveItErrorCodes.SUCCESS
            message = error_code.message or ("success" if success else f"MoveIt execution error {error_code.val}")
            extra = {
                "planning_time_sec": round(self.last_planning_time_sec, 3),
                "error_code": int(error_code.val),
            }
            if success and self.post_execute_settle_sec > 0.0:
                self.settle_timer = self.node.create_timer(
                    self.post_execute_settle_sec,
                    lambda: self._finish_after_settle(success, message, extra),
                )
            else:
                self._finish(success, message, extra)
        except Exception as exc:  # pragma: no cover - defensive ROS callback guard
            self._finish(False, f"MoveIt execute result failed: {exc}")

    def _finish_after_settle(self, success: bool, message: str, extra: Dict) -> None:
        if self.settle_timer is not None:
            self.settle_timer.cancel()
            self.settle_timer = None
        self._finish(success, message, extra)

    def _ensure_increasing_waypoint_times(self, trajectory) -> None:
        points = trajectory.joint_trajectory.points
        minimum_step_ns = int(self.waypoint_time_step_sec * 1_000_000_000)
        previous_ns = 0
        for point in points:
            current_ns = (
                int(point.time_from_start.sec) * 1_000_000_000
                + int(point.time_from_start.nanosec)
            )
            if current_ns <= previous_ns:
                current_ns = previous_ns + minimum_step_ns
                point.time_from_start = Duration(
                    sec=current_ns // 1_000_000_000,
                    nanosec=current_ns % 1_000_000_000,
                )
            previous_ns = current_ns

    def _finish(self, success: bool, message: str, extra: Optional[Dict] = None) -> None:
        callback = self.done_callback
        pose_name = self.active_pose
        self.active_pose = None
        self.done_callback = None
        if callback is None:
            return
        payload = {
            "pose_name": pose_name,
            "status": "completed" if success else "failed",
            "message": message,
        }
        if extra:
            payload.update(extra)
        callback(payload)

    @staticmethod
    def _load_pose_config(path: Path) -> Dict:
        if not path.exists():
            raise FileNotFoundError(f"MoveIt pose config not found: {path}")
        with path.open("r", encoding="utf-8") as handle:
            config = yaml.safe_load(handle)
        if not config.get("joint_names") or not config.get("named_poses"):
            raise ValueError(f"Invalid MoveIt pose config: {path}")
        return config
