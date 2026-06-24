import json

import rclpy
from rclpy.node import Node
from std_msgs.msg import String


DETECTED_COMPONENTS = [
    "housing",
    "front_cover",
    "rear_cover",
    "rotor",
    "stator",
    "shaft",
    "bearing_front",
    "bearing_rear",
    "fan",
    "terminal_box",
    "bolts",
]


class MockCameraNode(Node):
    def __init__(self) -> None:
        super().__init__("mock_camera_node")
        self.publisher = self.create_publisher(String, "/motor_rex/detected_components", 10)
        self.timer = self.create_timer(1.5, self.publish_detected_components)

    def publish_detected_components(self) -> None:
        msg = String()
        msg.data = json.dumps(
            {
                "camera_id": "mock_overhead_depth_camera",
                "frame_id": "camera_placeholder",
                "detected_components": DETECTED_COMPONENTS,
            }
        )
        self.publisher.publish(msg)


def main() -> None:
    rclpy.init()
    node = MockCameraNode()
    try:
        rclpy.spin(node)
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    main()

