import csv
import json
from pathlib import Path
from typing import Dict, List

import rclpy
from rclpy.node import Node
from std_msgs.msg import String


def _default_project_root() -> Path:
    return Path(__file__).resolve().parents[4]


class MockSensorNode(Node):
    def __init__(self) -> None:
        super().__init__("mock_sensor_node")
        project_root = _default_project_root()
        self.declare_parameter("sensor_path", str(project_root / "data" / "mock_sensor_stream.csv"))
        self.publisher = self.create_publisher(String, "/motor_rex/mock_sensor_data", 10)
        self.rows = self._load_rows()
        self.index = 0
        self.timer = self.create_timer(0.75, self.publish_next_row)

    def _load_rows(self) -> List[Dict[str, str]]:
        path = Path(str(self.get_parameter("sensor_path").value))
        if not path.exists():
            self.get_logger().warning(f"Sensor CSV not found: {path}")
            return []
        with path.open("r", encoding="utf-8", newline="") as handle:
            return list(csv.DictReader(handle))

    def publish_next_row(self) -> None:
        if not self.rows:
            return
        row = self.rows[self.index % len(self.rows)]
        self.index += 1
        msg = String()
        msg.data = json.dumps(row)
        self.publisher.publish(msg)


def main() -> None:
    rclpy.init()
    node = MockSensorNode()
    try:
        rclpy.spin(node)
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    main()
