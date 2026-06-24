import json
from pathlib import Path

import rclpy
from rclpy.node import Node
from std_msgs.msg import String

from motor_rex_decision_engine.health_model import evaluate_passport, load_sensor_rows
from motor_rex_decision_engine.passport_loader import load_passport, save_results


def _default_project_root() -> Path:
    return Path(__file__).resolve().parents[4]


class DecisionEngineNode(Node):
    def __init__(self) -> None:
        super().__init__("motor_rex_decision_engine")
        project_root = _default_project_root()

        self.declare_parameter("passport_path", str(project_root / "data" / "sample_motor_passport.json"))
        self.declare_parameter("sensor_path", str(project_root / "data" / "mock_sensor_stream.csv"))
        self.declare_parameter("results_path", str(project_root / "data" / "processed_motor_results.json"))
        self.declare_parameter("publish_period_sec", 2.0)

        self.passport_pub = self.create_publisher(String, "/motor_rex/passport", 10)
        self.decisions_pub = self.create_publisher(String, "/motor_rex/component_decisions", 10)
        self.summary_pub = self.create_publisher(String, "/motor_rex/summary", 10)

        self.results = self._evaluate()
        period = float(self.get_parameter("publish_period_sec").value)
        self.timer = self.create_timer(period, self._publish_results)
        self._publish_results()

    def _evaluate(self) -> dict:
        passport_path = Path(str(self.get_parameter("passport_path").value))
        sensor_path = Path(str(self.get_parameter("sensor_path").value))
        results_path = Path(str(self.get_parameter("results_path").value))

        passport = load_passport(passport_path)
        sensor_rows = load_sensor_rows(sensor_path)
        results = evaluate_passport(passport, sensor_rows)
        save_results(results_path, results)
        self.get_logger().info(f"Processed Re-X decisions for {results['product_id']}")
        self.get_logger().info(f"Saved results to {results_path}")
        return results

    def _publish_results(self) -> None:
        passport_msg = String()
        passport_msg.data = json.dumps(
            {
                "product_id": self.results["product_id"],
                "source_site": self.results["source_site"],
                "asset_type": self.results["asset_type"],
                "operating_hours": self.results["operating_hours"],
                "failure_symptoms": self.results["failure_symptoms"],
            }
        )
        self.passport_pub.publish(passport_msg)

        decisions_msg = String()
        decisions_msg.data = json.dumps(self.results["components"])
        self.decisions_pub.publish(decisions_msg)

        summary_msg = String()
        summary_msg.data = json.dumps(self.results["summary"])
        self.summary_pub.publish(summary_msg)


def main() -> None:
    rclpy.init()
    node = DecisionEngineNode()
    try:
        rclpy.spin(node)
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    main()
