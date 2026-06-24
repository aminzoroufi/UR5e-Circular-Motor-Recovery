import json

import rclpy
from rclpy.node import Node
from std_msgs.msg import String


COMPONENT_POSES = {
    "terminal_box": {"frame_id": "world", "position": [0.02, 0.12, 0.52], "rpy": [0.0, 0.0, 0.0]},
    "bolts": {"frame_id": "world", "position": [0.29, 0.12, 0.48], "rpy": [0.0, 0.0, 0.0]},
    "bearing_front": {"frame_id": "world", "position": [0.52, 0.12, 0.50], "rpy": [0.0, 1.5708, 0.0]},
    "bearing_rear": {"frame_id": "world", "position": [0.74, 0.12, 0.50], "rpy": [0.0, 1.5708, 0.0]},
    "front_cover": {"frame_id": "world", "position": [0.02, -0.08, 0.58], "rpy": [0.0, 1.5708, 0.0]},
    "housing": {"frame_id": "world", "position": [0.30, -0.08, 0.56], "rpy": [0.0, 0.0, 0.0]},
    "stator": {"frame_id": "world", "position": [0.60, -0.08, 0.57], "rpy": [0.0, 1.5708, 0.0]},
    "rear_cover": {"frame_id": "world", "position": [0.86, -0.08, 0.58], "rpy": [0.0, 1.5708, 0.0]},
    "fan": {"frame_id": "world", "position": [0.04, -0.28, 0.58], "rpy": [0.0, 0.0, 0.0]},
    "rotor": {"frame_id": "world", "position": [0.40, -0.28, 0.51], "rpy": [0.0, 1.5708, 0.0]},
    "shaft": {"frame_id": "world", "position": [0.76, -0.28, 0.48], "rpy": [0.0, 1.5708, 0.0]},
}


class ComponentPosePublisher(Node):
    def __init__(self) -> None:
        super().__init__("component_pose_publisher")
        self.publisher = self.create_publisher(String, "/motor_rex/component_poses", 10)
        self.timer = self.create_timer(1.0, self.publish_poses)

    def publish_poses(self) -> None:
        msg = String()
        msg.data = json.dumps(COMPONENT_POSES)
        self.publisher.publish(msg)


def main() -> None:
    rclpy.init()
    node = ComponentPosePublisher()
    try:
        rclpy.spin(node)
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    main()
