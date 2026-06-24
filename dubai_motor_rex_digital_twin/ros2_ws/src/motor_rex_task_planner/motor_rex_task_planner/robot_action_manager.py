from dataclasses import dataclass
from typing import Dict, List


POSE_BY_STATION = {
    "reuse_bin": "reuse_bin_pose",
    "repair_bin": "repair_bin_pose",
    "replace_bin": "replace_bin_pose",
    "recycle_bin": "recycle_bin_pose",
}

PICK_POSE_BY_COMPONENT = {
    "terminal_box": "pickup_terminal_box_pose",
    "fan": "pickup_fan_pose",
    "bearing_front": "pickup_bearing_front_pose",
    "bearing_rear": "pickup_bearing_rear_pose",
    "rotor": "pickup_rotor_pose",
    "stator": "pickup_stator_pose",
    "shaft": "pickup_shaft_pose",
    "housing": "pickup_housing_pose",
    "front_cover": "pickup_front_cover_pose",
    "rear_cover": "pickup_rear_cover_pose",
    "bolts": "pickup_bolts_pose",
}

COMPONENT_TASK_ORDER = [
    "terminal_box",
    "fan",
    "bearing_front",
    "bearing_rear",
    "rotor",
    "stator",
    "shaft",
    "housing",
    "front_cover",
    "rear_cover",
    "bolts",
]

@dataclass
class RobotTask:
    task_id: int
    component_id: str
    decision: str
    source_pose: Dict
    target_station: str
    source_named_pose: str
    named_pose: str
    action: str
    robot_role: str = "UR5e sorting robot"


class RobotActionManager:
    """MVP action layer.

    This class currently creates and logs simulated pick/place commands. It is the
    replacement point for MoveIt action calls, gripper actions, and real robot
    drivers in the next phase.
    """

    def create_component_tasks(self, decisions: List[Dict], poses: Dict[str, Dict]) -> List[RobotTask]:
        tasks: List[RobotTask] = []
        ordered_decisions = sorted(
            decisions,
            key=lambda component: COMPONENT_TASK_ORDER.index(component["component_id"])
            if component["component_id"] in COMPONENT_TASK_ORDER
            else len(COMPONENT_TASK_ORDER),
        )
        for index, component in enumerate(ordered_decisions, start=1):
            component_id = component["component_id"]
            target_station = component.get("target_station", "test_station")
            action = component.get("robot_action", "inspect_component")
            tasks.append(
                RobotTask(
                    task_id=index,
                    component_id=component_id,
                    decision=component["decision"],
                    source_pose=poses.get(component_id, {}),
                    target_station=target_station,
                    source_named_pose=PICK_POSE_BY_COMPONENT.get(component_id, "pickup_pose"),
                    named_pose=POSE_BY_STATION.get(target_station, "inspection_pose"),
                    action=action,
                )
            )
        return tasks

    def simulate_execution(self, task: RobotTask) -> Dict:
        return {
            "task_id": task.task_id,
            "component_id": task.component_id,
            "decision": task.decision,
            "action": task.action,
            "target_station": task.target_station,
            "source_named_pose": task.source_named_pose,
            "moveit_named_pose": task.named_pose,
            "status": "completed",
        }
