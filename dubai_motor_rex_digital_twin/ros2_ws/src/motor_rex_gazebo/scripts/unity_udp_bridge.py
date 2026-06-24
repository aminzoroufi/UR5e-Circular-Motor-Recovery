#!/usr/bin/env python3
from __future__ import annotations

import json
import socket
import time

import rclpy
from rclpy.node import Node
from sensor_msgs.msg import JointState
from std_msgs.msg import String
from tf2_ros import Buffer, TransformException, TransformListener


ARM_JOINTS = [
    "shoulder_pan_joint",
    "shoulder_lift_joint",
    "elbow_joint",
    "wrist_1_joint",
    "wrist_2_joint",
    "wrist_3_joint",
]
GRIPPER_JOINT = "robotiq_85_left_knuckle_joint"


class UnityUdpBridge(Node):
    def __init__(self) -> None:
        super().__init__("unity_udp_bridge")
        self.declare_parameter("unity_host", "host.docker.internal")
        self.declare_parameter("unity_port", 15000)
        self.declare_parameter("max_joint_rate_hz", 30.0)
        self.target = (
            str(self.get_parameter("unity_host").value),
            int(self.get_parameter("unity_port").value),
        )
        self.minimum_period = 1.0 / float(self.get_parameter("max_joint_rate_hz").value)
        self.last_joint_send = 0.0
        self.socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.tf_buffer = Buffer()
        self.tf_listener = TransformListener(self.tf_buffer, self)
        self.create_subscription(JointState, "/joint_states", self.on_joint_state, 20)
        self.create_subscription(
            String,
            "/motor_rex/current_robot_action",
            self.on_action,
            20,
        )
        self.get_logger().info(
            f"Streaming Gazebo/MoveIt state to Unity UDP {self.target[0]}:{self.target[1]}"
        )

    def on_joint_state(self, message: JointState) -> None:
        now = time.monotonic()
        if now - self.last_joint_send < self.minimum_period:
            return

        by_name = dict(zip(message.name, message.position))
        if any(name not in by_name for name in ARM_JOINTS):
            return

        self.last_joint_send = now
        payload = {
            "type": "joint_state",
            "names": ARM_JOINTS,
            "positions": [float(by_name[name]) for name in ARM_JOINTS],
            "gripper_position": float(by_name.get(GRIPPER_JOINT, 0.0)),
        }
        try:
            transform = self.tf_buffer.lookup_transform(
                "world",
                "tool0",
                rclpy.time.Time(),
            )
            translation = transform.transform.translation
            payload["tool_position"] = [
                float(translation.x),
                float(translation.y),
                float(translation.z),
            ]
        except TransformException:
            pass
        self._send(payload)

    def on_action(self, message: String) -> None:
        try:
            payload = json.loads(message.data)
        except json.JSONDecodeError:
            return
        payload["type"] = "action"
        self._send(payload)

    def _send(self, payload: dict) -> None:
        try:
            self.socket.sendto(
                json.dumps(payload, separators=(",", ":")).encode("utf-8"),
                self.target,
            )
        except OSError as exception:
            self.get_logger().warning(f"Unity UDP send failed: {exception}")

    def destroy_node(self) -> bool:
        self.socket.close()
        return super().destroy_node()


def main() -> None:
    rclpy.init()
    node = UnityUdpBridge()
    try:
        rclpy.spin(node)
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    main()
