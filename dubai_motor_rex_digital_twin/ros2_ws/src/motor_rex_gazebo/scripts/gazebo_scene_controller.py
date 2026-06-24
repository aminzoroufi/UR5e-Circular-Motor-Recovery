#!/usr/bin/env python3
from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterable, Tuple

from ament_index_python.packages import get_package_share_directory
import rclpy
from geometry_msgs.msg import Pose
from rclpy.node import Node
from ros_gz_interfaces.msg import Entity
from ros_gz_interfaces.srv import DeleteEntity, SetEntityPose, SpawnEntity
from std_msgs.msg import String
from tf2_ros import Buffer, TransformException, TransformListener


Vector3 = Tuple[float, float, float]
Color = Tuple[float, float, float, float]
MOTOR_MESH_DIR = Path(get_package_share_directory("motor_rex_description")) / "meshes" / "motor" / "detailed"
MOTOR_MESH_SCALE = 0.70
GRIPPER_TCP_OFFSET = 0.17
HELD_PROXY_PREFIX = "rex_held_"
HIDDEN_PROXY_POSE = (0.0, 0.0, -2.0)
CATEGORY_TABLE_TOP = 0.45


@dataclass(frozen=True)
class PartSpec:
    component_id: str
    model_name: str
    shape: str
    size: Tuple[float, ...]
    mass: float
    initial_pose: Vector3
    color: Color


PART_SPECS: Dict[str, PartSpec] = {
    "terminal_box": PartSpec("terminal_box", "rex_part_terminal_box", "box", (0.16, 0.12, 0.10), 0.9, (0.02, 0.12, 0.54), (0.72, 0.76, 0.78, 1.0)),
    "bolts": PartSpec("bolts", "rex_part_bolts", "box", (0.12, 0.08, 0.04), 0.2, (0.29, 0.12, 0.50), (0.08, 0.30, 0.88, 1.0)),
    "bearing_front": PartSpec("bearing_front", "rex_part_bearing_front", "cylinder_x", (0.045, 0.04), 0.35, (0.52, 0.12, 0.52), (0.92, 0.22, 0.10, 1.0)),
    "bearing_rear": PartSpec("bearing_rear", "rex_part_bearing_rear", "cylinder_x", (0.045, 0.04), 0.35, (0.74, 0.12, 0.52), (0.92, 0.22, 0.10, 1.0)),
    "front_cover": PartSpec("front_cover", "rex_part_front_cover", "cylinder_x", (0.12, 0.08), 1.4, (0.02, -0.08, 0.60), (0.52, 0.54, 0.55, 1.0)),
    "housing": PartSpec("housing", "rex_part_housing", "box", (0.30, 0.20, 0.20), 8.0, (0.30, -0.08, 0.58), (0.78, 0.82, 0.84, 1.0)),
    "stator": PartSpec("stator", "rex_part_stator", "cylinder_x", (0.11, 0.16), 6.3, (0.60, -0.08, 0.59), (0.50, 0.52, 0.54, 1.0)),
    "rear_cover": PartSpec("rear_cover", "rex_part_rear_cover", "cylinder_x", (0.12, 0.08), 1.4, (0.86, -0.08, 0.60), (0.52, 0.54, 0.55, 1.0)),
    "fan": PartSpec("fan", "rex_part_fan", "box", (0.04, 0.24, 0.24), 0.5, (0.04, -0.28, 0.60), (0.08, 0.30, 0.88, 1.0)),
    "rotor": PartSpec("rotor", "rex_part_rotor", "cylinder_x", (0.055, 0.26), 4.2, (0.40, -0.28, 0.53), (0.95, 0.43, 0.16, 1.0)),
    "shaft": PartSpec("shaft", "rex_part_shaft", "cylinder_x", (0.025, 0.42), 1.2, (0.76, -0.28, 0.50), (0.12, 0.13, 0.14, 1.0)),
}

STATION_CENTERS: Dict[str, Vector3] = {
    "reuse_bin": (1.02, -0.72, CATEGORY_TABLE_TOP),
    "repair_bin": (0.72, -1.20, CATEGORY_TABLE_TOP),
    "replace_bin": (0.08, -1.20, CATEGORY_TABLE_TOP),
    "recycle_bin": (-0.22, -0.70, CATEGORY_TABLE_TOP),
}

STATION_OFFSETS: Dict[str, Dict[str, Tuple[float, float]]] = {
    "reuse_bin": {
        "terminal_box": (-0.29, -0.13),
        "rotor": (-0.04, -0.13),
        "housing": (0.24, -0.12),
        "front_cover": (-0.13, 0.14),
        "rear_cover": (0.13, 0.14),
    },
    "repair_bin": {
        "stator": (-0.14, 0.0),
        "shaft": (0.15, 0.0),
    },
    "replace_bin": {
        "bearing_front": (-0.12, 0.0),
        "bearing_rear": (0.12, 0.0),
    },
    "recycle_bin": {
        "fan": (-0.12, 0.0),
        "bolts": (0.12, 0.0),
    },
}


class GazeboSceneController(Node):
    """Spawns gravity-enabled separable parts and follows the real UR5e tool."""

    def __init__(self) -> None:
        super().__init__("gazebo_scene_controller")
        self.declare_parameter("world_name", "motor_rex_recovery_cell")
        self.world_name = str(self.get_parameter("world_name").value)
        self.spawn_client = self.create_client(SpawnEntity, f"/world/{self.world_name}/create")
        self.pose_client = self.create_client(SetEntityPose, f"/world/{self.world_name}/set_pose")
        self.delete_client = self.create_client(DeleteEntity, f"/world/{self.world_name}/remove")
        self.action_sub = self.create_subscription(
            String,
            "/motor_rex/current_robot_action",
            self.on_action,
            10,
        )
        self.spawn_order = list(PART_SPECS)
        self.spawn_index = 0
        self.spawned = set()
        self.proxy_requested = set()
        self.proxy_spawned = set()
        self.held_component: str | None = None
        self.tf_buffer = Buffer()
        self.tf_listener = TransformListener(self.tf_buffer, self)
        self.startup_timer = self.create_timer(0.5, self._startup_tick)
        self.attachment_timer = self.create_timer(0.04, self._update_attached_part)
        self.get_logger().info("Waiting for Gazebo services; motor parts will use gravity and collision")

    def _startup_tick(self) -> None:
        if (
            not self.spawn_client.service_is_ready()
            or not self.pose_client.service_is_ready()
            or not self.delete_client.service_is_ready()
        ):
            self.spawn_client.wait_for_service(timeout_sec=0.0)
            self.pose_client.wait_for_service(timeout_sec=0.0)
            self.delete_client.wait_for_service(timeout_sec=0.0)
            return
        if self.spawn_index >= len(self.spawn_order):
            self.startup_timer.cancel()
            self.get_logger().info(f"Spawned {len(self.spawned)} dynamic motor parts from {MOTOR_MESH_DIR}")
            return
        component_id = self.spawn_order[self.spawn_index]
        self.spawn_index += 1
        self._spawn_part(PART_SPECS[component_id])

    def _spawn_part(self, spec: PartSpec) -> None:
        request = SpawnEntity.Request()
        request.entity_factory.name = spec.model_name
        request.entity_factory.allow_renaming = False
        request.entity_factory.sdf = self._sdf_for_part(spec)
        request.entity_factory.pose = self._pose(*spec.initial_pose)
        request.entity_factory.relative_to = "world"
        future = self.spawn_client.call_async(request)
        future.add_done_callback(lambda done, component_id=spec.component_id: self._on_spawn_done(component_id, done))

    def _on_spawn_done(self, component_id: str, future) -> None:
        try:
            response = future.result()
        except Exception as exc:  # pragma: no cover
            self.get_logger().warning(f"Failed to spawn {component_id}: {exc}")
            return
        if response.success:
            self.spawned.add(component_id)
        else:
            self.get_logger().warning(f"Gazebo rejected dynamic model for {component_id}")

    def on_action(self, msg: String) -> None:
        try:
            payload = json.loads(msg.data)
        except json.JSONDecodeError:
            return
        component_id = payload.get("component_id")
        if component_id not in PART_SPECS or payload.get("status") != "executing":
            return
        action = payload.get("action")
        if action == "grasp_component":
            if component_id not in self.proxy_requested:
                self.proxy_requested.add(component_id)
                self._spawn_held_proxy(PART_SPECS[component_id])
            self._delete_source_part(component_id)
            self.held_component = component_id
            self.get_logger().info(
                f"Gripper contact accepted; carrying collision-free {component_id} proxy at tool0"
            )
        elif action == "release_component":
            self.held_component = None
            target_station = str(payload.get("target_station", "reuse_bin"))
            self._move_entity(
                f"{HELD_PROXY_PREFIX}{component_id}",
                self._station_pose(target_station, component_id),
                f"{component_id} held proxy",
            )
            self.get_logger().info(
                f"Placed {component_id} on {target_station}; stable proxy preserves the sorted layout"
            )

    def _update_attached_part(self) -> None:
        component_id = self.held_component
        if component_id is None:
            return
        try:
            transform = self.tf_buffer.lookup_transform("world", "tool0", rclpy.time.Time())
        except TransformException:
            return
        translation = transform.transform.translation
        self._move_entity(
            f"{HELD_PROXY_PREFIX}{component_id}",
            (translation.x, translation.y, translation.z - GRIPPER_TCP_OFFSET),
            f"{component_id} held proxy",
        )

    def _spawn_held_proxy(self, spec: PartSpec) -> None:
        request = SpawnEntity.Request()
        request.entity_factory.name = f"{HELD_PROXY_PREFIX}{spec.component_id}"
        request.entity_factory.allow_renaming = False
        request.entity_factory.sdf = self._sdf_for_held_proxy(spec)
        request.entity_factory.pose = self._pose(*HIDDEN_PROXY_POSE)
        request.entity_factory.relative_to = "world"
        future = self.spawn_client.call_async(request)
        future.add_done_callback(
            lambda done, component_id=spec.component_id: self._on_proxy_spawn_done(component_id, done)
        )

    def _on_proxy_spawn_done(self, component_id: str, future) -> None:
        try:
            response = future.result()
        except Exception as exc:  # pragma: no cover
            self.get_logger().warning(f"Failed to spawn held proxy for {component_id}: {exc}")
            return
        if response.success:
            self.proxy_spawned.add(component_id)
        else:
            self.get_logger().warning(f"Gazebo rejected held proxy for {component_id}")

    def _station_pose(self, station: str, component_id: str) -> Vector3:
        center = STATION_CENTERS.get(station, STATION_CENTERS["reuse_bin"])
        offset = STATION_OFFSETS.get(station, {}).get(component_id, (0.0, 0.0))
        return (
            center[0] + offset[0],
            center[1] + offset[1],
            self._resting_z(PART_SPECS[component_id], center[2]),
        )

    @staticmethod
    def _resting_z(spec: PartSpec, surface_z: float) -> float:
        if spec.shape == "box":
            return surface_z + spec.size[2] * 0.5
        return surface_z + spec.size[0]

    def _delete_source_part(self, component_id: str) -> None:
        request = DeleteEntity.Request()
        request.entity.name = PART_SPECS[component_id].model_name
        request.entity.type = Entity.MODEL
        future = self.delete_client.call_async(request)
        future.add_done_callback(
            lambda done, name=component_id: self._on_delete_done(name, done)
        )

    def _on_delete_done(self, component_id: str, future) -> None:
        try:
            response = future.result()
        except Exception as exc:  # pragma: no cover
            self.get_logger().warning(f"Failed to remove source model for {component_id}: {exc}")
            return
        if response.success:
            self.spawned.discard(component_id)
        else:
            self.get_logger().warning(f"Gazebo rejected source-model removal for {component_id}")

    def _move_entity(self, entity_name: str, target: Vector3, log_name: str) -> None:
        request = SetEntityPose.Request()
        request.entity.name = entity_name
        request.entity.type = Entity.MODEL
        request.pose = self._pose(*target)
        future = self.pose_client.call_async(request)
        future.add_done_callback(lambda done, name=log_name: self._on_pose_done(name, done))

    def _on_pose_done(self, component_id: str, future) -> None:
        try:
            response = future.result()
        except Exception as exc:  # pragma: no cover
            self.get_logger().warning(f"Failed to move {component_id}: {exc}")
            return
        if not response.success:
            self.get_logger().warning(f"Gazebo rejected pose update for {component_id}")

    def _sdf_for_part(self, spec: PartSpec) -> str:
        visual_geometry, collision_geometry, geometry_pose = self._geometries(spec)
        inertia = max(0.0001, spec.mass * 0.002)
        return (
            '<?xml version="1.0"?>'
            '<sdf version="1.10">'
            f'<model name="{spec.model_name}">'
            "<static>false</static><self_collide>false</self_collide>"
            '<link name="part_link">'
            "<gravity>true</gravity><velocity_decay><linear>0.08</linear><angular>0.08</angular></velocity_decay>"
            f"<inertial><mass>{spec.mass}</mass><inertia>"
            f"<ixx>{inertia}</ixx><iyy>{inertia}</iyy><izz>{inertia}</izz>"
            "<ixy>0</ixy><ixz>0</ixz><iyz>0</iyz></inertia></inertial>"
            '<visual name="visual">'
            f"{geometry_pose}<cast_shadows>true</cast_shadows>"
            f"<geometry>{visual_geometry}</geometry>{self._material_for_part(spec)}"
            "</visual>"
            '<collision name="collision">'
            f"{geometry_pose}<geometry>{collision_geometry}</geometry>"
            "<surface><friction><ode><mu>0.9</mu><mu2>0.8</mu2></ode></friction>"
            "<contact><ode><kp>500000</kp><kd>50</kd></ode></contact></surface>"
            "</collision>"
            "</link></model></sdf>"
        )

    def _sdf_for_held_proxy(self, spec: PartSpec) -> str:
        visual_geometry, _, geometry_pose = self._geometries(spec)
        return (
            '<?xml version="1.0"?>'
            '<sdf version="1.10">'
            f'<model name="{HELD_PROXY_PREFIX}{spec.component_id}">'
            "<static>true</static><self_collide>false</self_collide>"
            '<link name="proxy_link"><visual name="visual">'
            f"{geometry_pose}<cast_shadows>true</cast_shadows>"
            f"<geometry>{visual_geometry}</geometry>{self._material_for_part(spec)}"
            "</visual></link></model></sdf>"
        )

    def _geometries(self, spec: PartSpec) -> Tuple[str, str, str]:
        mesh_path = MOTOR_MESH_DIR / f"{spec.component_id}.obj"
        if mesh_path.exists():
            visual = (
                f"<mesh><uri>file://{mesh_path}</uri>"
                f"<scale>{MOTOR_MESH_SCALE} {MOTOR_MESH_SCALE} {MOTOR_MESH_SCALE}</scale></mesh>"
            )
        else:
            visual = self._primitive_geometry(spec)
        return visual, self._stable_collision_geometry(spec), self._geometry_pose(spec)

    @staticmethod
    def _primitive_geometry(spec: PartSpec) -> str:
        if spec.shape == "box":
            x, y, z = spec.size
            return f"<box><size>{x} {y} {z}</size></box>"
        radius, length = spec.size
        return f"<cylinder><radius>{radius}</radius><length>{length}</length></cylinder>"

    @staticmethod
    def _stable_collision_geometry(spec: PartSpec) -> str:
        if spec.shape == "box":
            return GazeboSceneController._primitive_geometry(spec)
        radius, length = spec.size
        diameter = radius * 2.0
        return f"<box><size>{diameter} {diameter} {length}</size></box>"

    @staticmethod
    def _geometry_pose(spec: PartSpec) -> str:
        return "<pose>0 0 0 0 1.57079632679 0</pose>" if spec.shape == "cylinder_x" else ""

    @staticmethod
    def _material_for_part(spec: PartSpec) -> str:
        r, g, b, a = spec.color
        metalness = 1.0 if spec.component_id not in {"fan", "bolts"} else 0.55
        roughness = 0.26 if spec.component_id in {"rotor", "shaft", "front_cover", "rear_cover"} else 0.38
        return (
            "<material>"
            f"<ambient>{r * 0.65:.3f} {g * 0.65:.3f} {b * 0.65:.3f} {a}</ambient>"
            f"<diffuse>{r:.3f} {g:.3f} {b:.3f} {a}</diffuse>"
            "<specular>0.9 0.9 0.9 1</specular><pbr><metal>"
            f"<metalness>{metalness:.2f}</metalness><roughness>{roughness:.2f}</roughness>"
            "</metal></pbr></material>"
        )

    @staticmethod
    def _pose(x: float, y: float, z: float) -> Pose:
        pose = Pose()
        pose.position.x = float(x)
        pose.position.y = float(y)
        pose.position.z = float(z)
        pose.orientation.w = 1.0
        return pose


def main(args: Iterable[str] | None = None) -> None:
    rclpy.init(args=args)
    node = GazeboSceneController()
    try:
        rclpy.spin(node)
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    main()
