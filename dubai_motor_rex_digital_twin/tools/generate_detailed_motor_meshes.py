#!/usr/bin/env python3
from __future__ import annotations

import math
from pathlib import Path
from typing import Callable, Iterable, List, Sequence, Tuple


Point = Tuple[float, float, float]
Face = Tuple[int, int, int]


PROJECT_ROOT = Path(__file__).resolve().parents[1]
UNITY_DIR = PROJECT_ROOT / "unity_digital_twin" / "Assets" / "Models" / "Motors" / "DetailedCoolingMotor"
GAZEBO_DIR = PROJECT_ROOT / "ros2_ws" / "src" / "motor_rex_description" / "meshes" / "motor" / "detailed"
MATERIAL_COLORS = {
    "housing": (0.78, 0.82, 0.84),
    "front_cover": (0.52, 0.54, 0.55),
    "rear_cover": (0.52, 0.54, 0.55),
    "rotor": (0.95, 0.43, 0.16),
    "stator": (0.50, 0.52, 0.54),
    "shaft": (0.18, 0.19, 0.20),
    "bearing_front": (0.92, 0.22, 0.10),
    "bearing_rear": (0.92, 0.22, 0.10),
    "fan": (0.08, 0.30, 0.88),
    "terminal_box": (0.72, 0.76, 0.78),
    "bolts": (0.08, 0.30, 0.88),
}


def identity(point: Point) -> Point:
    return point


def unity_to_gazebo(point: Point) -> Point:
    x, y, z = point
    return x, z, y


class Mesh:
    def __init__(self, name: str) -> None:
        self.name = name
        self.vertices: List[Point] = []
        self.faces: List[Face] = []

    def add_vertex(self, point: Point) -> int:
        self.vertices.append(point)
        return len(self.vertices)

    def add_tri(self, a: Point, b: Point, c: Point) -> None:
        self.faces.append((self.add_vertex(a), self.add_vertex(b), self.add_vertex(c)))

    def add_quad(self, a: Point, b: Point, c: Point, d: Point) -> None:
        self.add_tri(a, b, c)
        self.add_tri(a, c, d)

    def extend(self, other: "Mesh") -> None:
        offset = len(self.vertices)
        self.vertices.extend(other.vertices)
        self.faces.extend((a + offset, b + offset, c + offset) for a, b, c in other.faces)

    def write_obj(self, path: Path, transform: Callable[[Point], Point] = identity) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        transformed_vertices = [transform(vertex) for vertex in self.vertices]
        material_name = f"{self.name}_material"
        with path.open("w", encoding="utf-8") as handle:
            handle.write(f"# Detailed separable motor component: {self.name}\n")
            handle.write("# Generated from the MVP mesh generator; OpenMotor STEP CAD is kept as source reference.\n")
            handle.write(f"mtllib {path.with_suffix('.mtl').name}\n")
            handle.write(f"o {self.name}\n")
            handle.write(f"usemtl {material_name}\n")
            for vertex in transformed_vertices:
                x, y, z = vertex
                handle.write(f"v {x:.6f} {y:.6f} {z:.6f}\n")
            normals: List[Point] = []
            for face in self.faces:
                normals.append(face_normal(
                    transformed_vertices[face[0] - 1],
                    transformed_vertices[face[1] - 1],
                    transformed_vertices[face[2] - 1],
                ))
            for normal in normals:
                handle.write(f"vn {normal[0]:.6f} {normal[1]:.6f} {normal[2]:.6f}\n")
            for index, face in enumerate(self.faces, start=1):
                handle.write(f"f {face[0]}//{index} {face[1]}//{index} {face[2]}//{index}\n")
        write_mtl(path.with_suffix(".mtl"), material_name, MATERIAL_COLORS.get(self.name, (0.65, 0.68, 0.70)))


def write_mtl(path: Path, material_name: str, color: Tuple[float, float, float]) -> None:
    r, g, b = color
    with path.open("w", encoding="utf-8") as handle:
        handle.write(f"newmtl {material_name}\n")
        handle.write(f"Ka {r * 0.55:.6f} {g * 0.55:.6f} {b * 0.55:.6f}\n")
        handle.write(f"Kd {r:.6f} {g:.6f} {b:.6f}\n")
        handle.write("Ks 0.850000 0.850000 0.850000\n")
        handle.write(f"Ke {r * 0.18:.6f} {g * 0.18:.6f} {b * 0.18:.6f}\n")
        handle.write("Ns 96.000000\n")
        handle.write("illum 2\n")


def face_normal(a: Point, b: Point, c: Point) -> Point:
    ux, uy, uz = b[0] - a[0], b[1] - a[1], b[2] - a[2]
    vx, vy, vz = c[0] - a[0], c[1] - a[1], c[2] - a[2]
    nx = uy * vz - uz * vy
    ny = uz * vx - ux * vz
    nz = ux * vy - uy * vx
    length = math.sqrt(nx * nx + ny * ny + nz * nz)
    if length <= 1e-9:
        return 0.0, 1.0, 0.0
    return nx / length, ny / length, nz / length


def rotate_x(point: Point, angle: float) -> Point:
    x, y, z = point
    c = math.cos(angle)
    s = math.sin(angle)
    return x, y * c - z * s, y * s + z * c


def translate(point: Point, center: Point) -> Point:
    return point[0] + center[0], point[1] + center[1], point[2] + center[2]


def add_box(mesh: Mesh, center: Point, size: Point, angle_x: float = 0.0) -> None:
    sx, sy, sz = size[0] * 0.5, size[1] * 0.5, size[2] * 0.5
    corners = [
        (-sx, -sy, -sz),
        (sx, -sy, -sz),
        (sx, sy, -sz),
        (-sx, sy, -sz),
        (-sx, -sy, sz),
        (sx, -sy, sz),
        (sx, sy, sz),
        (-sx, sy, sz),
    ]
    points = [translate(rotate_x(point, angle_x), center) for point in corners]
    faces = [
        (0, 1, 2, 3),
        (4, 7, 6, 5),
        (0, 4, 5, 1),
        (1, 5, 6, 2),
        (2, 6, 7, 3),
        (3, 7, 4, 0),
    ]
    for face in faces:
        mesh.add_quad(points[face[0]], points[face[1]], points[face[2]], points[face[3]])


def add_cylinder_x(mesh: Mesh, center: Point, radius: float, length: float, segments: int = 48, cap: bool = True) -> None:
    half = length * 0.5
    left_center = (center[0] - half, center[1], center[2])
    right_center = (center[0] + half, center[1], center[2])
    for index in range(segments):
        a0 = 2.0 * math.pi * index / segments
        a1 = 2.0 * math.pi * (index + 1) / segments
        left0 = (center[0] - half, center[1] + radius * math.cos(a0), center[2] + radius * math.sin(a0))
        left1 = (center[0] - half, center[1] + radius * math.cos(a1), center[2] + radius * math.sin(a1))
        right0 = (center[0] + half, center[1] + radius * math.cos(a0), center[2] + radius * math.sin(a0))
        right1 = (center[0] + half, center[1] + radius * math.cos(a1), center[2] + radius * math.sin(a1))
        mesh.add_quad(left0, right0, right1, left1)
        if cap:
            mesh.add_tri(left_center, left1, left0)
            mesh.add_tri(right_center, right0, right1)


def add_cylinder_z(mesh: Mesh, center: Point, radius: float, length: float, segments: int = 32, cap: bool = True) -> None:
    half = length * 0.5
    bottom_center = (center[0], center[1], center[2] - half)
    top_center = (center[0], center[1], center[2] + half)
    for index in range(segments):
        a0 = 2.0 * math.pi * index / segments
        a1 = 2.0 * math.pi * (index + 1) / segments
        bottom0 = (center[0] + radius * math.cos(a0), center[1] + radius * math.sin(a0), center[2] - half)
        bottom1 = (center[0] + radius * math.cos(a1), center[1] + radius * math.sin(a1), center[2] - half)
        top0 = (center[0] + radius * math.cos(a0), center[1] + radius * math.sin(a0), center[2] + half)
        top1 = (center[0] + radius * math.cos(a1), center[1] + radius * math.sin(a1), center[2] + half)
        mesh.add_quad(bottom0, bottom1, top1, top0)
        if cap:
            mesh.add_tri(bottom_center, bottom0, bottom1)
            mesh.add_tri(top_center, top1, top0)


def add_ring_x(mesh: Mesh, center: Point, outer_radius: float, inner_radius: float, length: float, segments: int = 64) -> None:
    half = length * 0.5
    x_left = center[0] - half
    x_right = center[0] + half
    for index in range(segments):
        a0 = 2.0 * math.pi * index / segments
        a1 = 2.0 * math.pi * (index + 1) / segments
        o0_l = (x_left, center[1] + outer_radius * math.cos(a0), center[2] + outer_radius * math.sin(a0))
        o1_l = (x_left, center[1] + outer_radius * math.cos(a1), center[2] + outer_radius * math.sin(a1))
        o0_r = (x_right, center[1] + outer_radius * math.cos(a0), center[2] + outer_radius * math.sin(a0))
        o1_r = (x_right, center[1] + outer_radius * math.cos(a1), center[2] + outer_radius * math.sin(a1))
        i0_l = (x_left, center[1] + inner_radius * math.cos(a0), center[2] + inner_radius * math.sin(a0))
        i1_l = (x_left, center[1] + inner_radius * math.cos(a1), center[2] + inner_radius * math.sin(a1))
        i0_r = (x_right, center[1] + inner_radius * math.cos(a0), center[2] + inner_radius * math.sin(a0))
        i1_r = (x_right, center[1] + inner_radius * math.cos(a1), center[2] + inner_radius * math.sin(a1))
        mesh.add_quad(o0_l, o0_r, o1_r, o1_l)
        mesh.add_quad(i1_l, i1_r, i0_r, i0_l)
        mesh.add_quad(o1_l, i1_l, i0_l, o0_l)
        mesh.add_quad(o0_r, i0_r, i1_r, o1_r)


def add_radial_box(mesh: Mesh, radius: float, angle: float, size: Point, x_offset: float = 0.0) -> None:
    center = (x_offset, radius * math.cos(angle), radius * math.sin(angle))
    add_box(mesh, center, size, angle_x=angle)


def add_cover_bosses(mesh: Mesh, x_offset: float = 0.0) -> None:
    for angle in evenly_spaced(6):
        center = (x_offset, 0.105 * math.cos(angle), 0.105 * math.sin(angle))
        add_cylinder_x(mesh, center, 0.013, 0.014, segments=20)


def evenly_spaced(count: int) -> Iterable[float]:
    for index in range(count):
        yield 2.0 * math.pi * index / count


def housing() -> Mesh:
    mesh = Mesh("housing")
    add_ring_x(mesh, (0.0, 0.0, 0.0), 0.145, 0.085, 0.36)
    for angle in evenly_spaced(18):
        add_radial_box(mesh, 0.158, angle, (0.335, 0.020, 0.020))
    add_box(mesh, (-0.065, -0.155, -0.055), (0.20, 0.030, 0.070))
    add_box(mesh, (0.065, -0.155, 0.055), (0.20, 0.030, 0.070))
    return mesh


def cover(name: str) -> Mesh:
    mesh = Mesh(name)
    add_ring_x(mesh, (0.0, 0.0, 0.0), 0.135, 0.030, 0.055)
    add_cylinder_x(mesh, (0.0, 0.0, 0.0), 0.072, 0.075)
    add_ring_x(mesh, (0.0, 0.0, 0.0), 0.095, 0.030, 0.082)
    add_cover_bosses(mesh)
    return mesh


def rotor() -> Mesh:
    mesh = Mesh("rotor")
    add_cylinder_x(mesh, (0.0, 0.0, 0.0), 0.055, 0.30)
    add_cylinder_x(mesh, (0.0, 0.0, 0.0), 0.030, 0.34)
    for angle in evenly_spaced(12):
        add_radial_box(mesh, 0.060, angle, (0.255, 0.008, 0.010))
    return mesh


def stator() -> Mesh:
    mesh = Mesh("stator")
    add_ring_x(mesh, (0.0, 0.0, 0.0), 0.128, 0.072, 0.20)
    for angle in evenly_spaced(18):
        add_radial_box(mesh, 0.095, angle, (0.17, 0.052, 0.014))
    for angle in evenly_spaced(9):
        add_radial_box(mesh, 0.121, angle + math.pi / 18.0, (0.11, 0.020, 0.030))
    return mesh


def shaft() -> Mesh:
    mesh = Mesh("shaft")
    add_cylinder_x(mesh, (0.0, 0.0, 0.0), 0.020, 0.56)
    add_cylinder_x(mesh, (-0.19, 0.0, 0.0), 0.026, 0.055)
    add_cylinder_x(mesh, (0.19, 0.0, 0.0), 0.026, 0.055)
    return mesh


def bearing(name: str) -> Mesh:
    mesh = Mesh(name)
    add_ring_x(mesh, (0.0, 0.0, 0.0), 0.055, 0.026, 0.034)
    for angle in evenly_spaced(12):
        add_cylinder_x(mesh, (0.0, 0.040 * math.cos(angle), 0.040 * math.sin(angle)), 0.0065, 0.010, segments=12)
    return mesh


def fan() -> Mesh:
    mesh = Mesh("fan")
    add_cylinder_x(mesh, (0.0, 0.0, 0.0), 0.032, 0.045)
    add_ring_x(mesh, (0.0, 0.0, 0.0), 0.145, 0.135, 0.018, segments=64)
    for angle in evenly_spaced(7):
        add_radial_box(mesh, 0.088, angle + 0.18, (0.020, 0.090, 0.026))
    return mesh


def terminal_box() -> Mesh:
    mesh = Mesh("terminal_box")
    add_box(mesh, (0.0, 0.0, 0.0), (0.165, 0.105, 0.120))
    add_box(mesh, (0.0, 0.060, 0.0), (0.175, 0.018, 0.130))
    add_cylinder_z(mesh, (0.052, 0.010, 0.080), 0.016, 0.060)
    add_cylinder_z(mesh, (-0.052, 0.010, -0.080), 0.016, 0.060)
    return mesh


def bolts() -> Mesh:
    mesh = Mesh("bolts")
    # Keep bolts as loose table parts instead of a circular in-assembly pattern.
    # This makes the disassembly tray readable in both Unity and Gazebo.
    for row, z_offset in enumerate((-0.045, 0.0, 0.045)):
        for col, x_offset in enumerate((-0.075, -0.025, 0.025, 0.075)):
            length = 0.038 if row < 2 else 0.030
            radius = 0.008 if col % 2 == 0 else 0.007
            add_cylinder_x(mesh, (x_offset, 0.0, z_offset), radius, length, segments=6)
            add_cylinder_x(mesh, (x_offset - length * 0.52, 0.0, z_offset), radius * 1.25, 0.010, segments=6)
    return mesh


def build_meshes() -> Sequence[Mesh]:
    return [
        housing(),
        cover("front_cover"),
        cover("rear_cover"),
        rotor(),
        stator(),
        shaft(),
        bearing("bearing_front"),
        bearing("bearing_rear"),
        fan(),
        terminal_box(),
        bolts(),
    ]


def write_readme() -> None:
    text = (
        "# DetailedCoolingMotor\n\n"
        "OBJ meshes generated for the Dubai Motor Re-X MVP.\n\n"
        "Source reference: `external_models/sources/OpenMotor-Hardware`, which keeps the "
        "downloaded OpenMotor STEP CAD files under CERN-OHL-W-2.0. These runtime meshes "
        "are separated into the exact MVP component IDs so Unity, Gazebo, the task "
        "planner, and the dashboard can swap in CAD-derived parts later without API changes.\n"
    )
    UNITY_DIR.mkdir(parents=True, exist_ok=True)
    GAZEBO_DIR.mkdir(parents=True, exist_ok=True)
    (UNITY_DIR / "README.md").write_text(text, encoding="utf-8")
    (GAZEBO_DIR / "README.md").write_text(text, encoding="utf-8")


def main() -> None:
    write_readme()
    for mesh in build_meshes():
        mesh.write_obj(UNITY_DIR / f"{mesh.name}.obj")
        mesh.write_obj(GAZEBO_DIR / f"{mesh.name}.obj", transform=unity_to_gazebo)
    print(f"Generated {len(build_meshes())} motor component meshes")
    print(f"Unity: {UNITY_DIR}")
    print(f"Gazebo: {GAZEBO_DIR}")


if __name__ == "__main__":
    main()
