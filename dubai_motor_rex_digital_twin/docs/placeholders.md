# Placeholder Ledger

This file records every intentional MVP placeholder so the project can move quickly without hiding what still needs to be replaced for production or sim-to-real work.

## Robot And Gripper

| Area | Current MVP Asset | Target Replacement | Stable Contract |
| --- | --- | --- | --- |
| Robot model | Official Universal Robots UR5e description is now staged and included in `motor_rex_cell.urdf.xacro`; earlier primitive robot remains only as a Unity fallback | Production UR5e config/driver with calibrated robot-specific kinematics | Keep planning group `arm`, UR-style joint names, named poses, controller interface, and task-planner pose names |
| Robot base placement | Fixed mount in the workcell | Calibrated robot base frame from cell measurement | Keep world/base transform explicit in launch/URDF |
| Gripper | Robotiq 2F-85 description is now staged and included; Unity keeps a fallback primitive if model import fails | Robotiq driver feedback and calibrated TCP/grasp frames | Keep planning group/control abstraction `gripper` and open/close command semantics |
| Grasping | Position-based approach/contact targets plus kinematic attachment after contact | Orientation-constrained grasp synthesis, attached collision objects, and force-confirmed contact | Keep component IDs and target station names stable |
| Gripper execution | Open/close commands sent to the placeholder `gripper_controller` during MoveIt pick/place | Robotiq action/driver feedback and grasp confirmation | Keep open/close command semantics and task action payloads stable |

## Motor Assembly

The motor uses detailed, separable OBJ runtime meshes in Unity and Gazebo, generated from the MVP mesh generator and kept aligned with the downloaded OpenMotor STEP CAD source reference in `external_models/sources/OpenMotor-Hardware`. Every Gazebo part is a separate dynamic model with a lightweight primitive collision shape. The component IDs below must stay stable:

| Component ID | Current Link/Frame | Current Geometry | Replacement Target |
| --- | --- | --- | --- |
| `housing` | `rex_part_housing` | Detailed finned OBJ plus dynamic box collision | CAD-derived housing mesh/link |
| `front_cover` | `rex_part_front_cover` | Detailed flanged OBJ plus dynamic cylinder collision | CAD-derived front cover |
| `rear_cover` | `rex_part_rear_cover` | Detailed flanged OBJ plus dynamic cylinder collision | CAD-derived rear cover |
| `rotor` | `rex_part_rotor` | Detailed slotted OBJ plus dynamic cylinder collision | CAD-derived rotor |
| `stator` | `rex_part_stator` | Detailed toothed OBJ plus dynamic cylinder collision | CAD-derived stator |
| `shaft` | `rex_part_shaft` | Detailed OBJ plus dynamic cylinder collision | CAD-derived shaft |
| `bearing_front` | `rex_part_bearing_front` | Detailed bearing OBJ plus dynamic cylinder collision | CAD-derived bearing |
| `bearing_rear` | `rex_part_bearing_rear` | Detailed bearing OBJ plus dynamic cylinder collision | CAD-derived bearing |
| `fan` | `rex_part_fan` | Detailed fan OBJ plus dynamic box collision | CAD-derived fan |
| `terminal_box` | `rex_part_terminal_box` | Detailed terminal-box OBJ plus dynamic box collision | CAD-derived terminal box |
| `bolts` | `rex_part_bolts` | Grouped detailed bolt OBJ plus dynamic box collision | Individual or grouped CAD bolt meshes |

Replacement rule: preserve the component IDs above in the passport, decision output, task planner, Unity objects, and dashboard. The geometry can change from generated OBJ to STEP-derived mesh without changing the decision engine or task planner.

Gazebo MVP note: `gazebo_scene_controller.py` spawns dynamic models named `rex_part_*` and lets gravity settle them on the source table. At grasp, the dynamic source model is removed; a collision-free `rex_held_*` visual proxy then follows `tool0` and is placed at a deterministic, non-overlapping category-table pose. This stabilized placement is intentional for the MVP; a force-confirmed detachable joint and fully dynamic destination placement remain future work.

## Workcell And Simulation

| Area | Current MVP Placeholder | Future Replacement |
| --- | --- | --- |
| Cameras | Mock detections without camera geometry | Calibrated RGB-D/depth camera, TF frames, and perception pipeline |
| Category tables | Four static colored tables | Physical tables/trays, safety zones, and inventory events |
| Contact physics | Gravity, friction, primitive collision, and kinematic attachment | Tuned contacts, compliance, detachable joints, and force/torque checks |

## Data And Logic

| Area | Current MVP Placeholder | Future Replacement |
| --- | --- | --- |
| Digital Product Passport | Sample JSON for `DXB-HVAC-MOTOR-001` | Real asset passport from maintenance/ERP/CMMS systems |
| Sensor stream | CSV mock vibration, thermal, current, insulation, and alignment values | Live sensors or historical plant data |
| Perception | Mock component pose publisher | Vision model and calibrated pose estimation |
| Health/Re-X rules | Deterministic threshold model | Validated model using real failure, value, CO2, and risk data |
| Pick/place result | MoveIt executes approach/contact/lift/retreat trajectories, the gripper controller opens/closes, source parts use gravity, and stable proxies preserve the sorted category layout | Gripper feedback, perception confirmation, detachable joints, fully dynamic destination placement, force/contact checks, and exception handling |

## MoveIt MVP Boundaries

The current `execution_mode:=moveit` proves the robotics backend loop:

1. Load passport and decisions.
2. Select component tasks.
3. Plan UR5e-compatible named and Cartesian position targets with MoveIt.
4. Execute trajectories in Gazebo through ros2_control.
5. Drive visible Gazebo motor-part movement through the scene controller.
6. Write task progress and final report.

It does not yet perform physical disassembly, screw removal, grasp synthesis, force control, exact CAD collision checking, or live Unity-ROS streaming.
