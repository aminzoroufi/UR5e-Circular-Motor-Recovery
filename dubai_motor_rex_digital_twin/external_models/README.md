# External Model Assets

This folder stages real model sources used by the Dubai Motor Re-X digital twin.

| Asset | Source | Local source folder | Active use |
| --- | --- | --- | --- |
| Universal Robots UR5e | https://github.com/UniversalRobots/Universal_Robots_ROS2_Description | `sources/Universal_Robots_ROS2_Description` | ROS 2 / Gazebo / MoveIt robot description and Unity mesh source |
| Universal Robots Gazebo simulation | https://github.com/UniversalRobots/Universal_Robots_ROS2_Gazebo_Simulation | `sources/Universal_Robots_ROS2_Gazebo_Simulation` | Reference launch/simulation package for later deeper Gazebo integration |
| Robotiq 2F-85 ROS 2 description | https://github.com/PickNikRobotics/ros2_robotiq_gripper | `sources/ros2_robotiq_gripper` | ROS 2 / Gazebo / MoveIt gripper description and Unity mesh source |
| Robotiq ROS-Industrial description | https://github.com/ros-industrial/robotiq | `sources/robotiq_ros_industrial` | Backup/reference Robotiq meshes and ROS 1 URDFs |
| OSE axial-flux motor CAD | https://gitlab.com/mi_shell/ose_freecad_models | `sources/ose_freecad_models` | Source CAD staged for later FreeCAD-to-mesh conversion |

## Active ROS Packages

The UR5e and Robotiq description packages are copied into:

- `ros2_ws/src/ur_description`
- `ros2_ws/src/robotiq_description`

`motor_rex_description/urdf/motor_rex_cell.urdf.xacro` now includes these packages directly.

## Active Unity Assets

Unity-readable mesh sources are copied into:

- `unity_digital_twin/Assets/Models/Robots/UR5e`
- `unity_digital_twin/Assets/Models/Grippers/Robotiq2F85`
- `unity_digital_twin/Assets/Models/Motors/OSE_AF_Motor_v1909/source_cad`

The Unity scene builder tries to instantiate the real UR5e and Robotiq meshes first, then falls back to the old primitive robot only if Unity cannot import the model files.

## License Notes

- `ur_description` source code is BSD-3-Clause. UR mesh/graphical documentation should be treated as Universal Robots graphical documentation; keep source/license notices with the asset.
- `ros2_robotiq_gripper` is BSD 3-Clause.
- `robotiq` ROS-Industrial packages are BSD/Apache depending on package.
- `ose_freecad_models` is staged as an open-source motor CAD candidate, but final project usage should confirm the license attribution needed for the exact CAD files before public distribution.

## Motor Caveat

The downloaded OSE motor is separable CAD source, not a finished IEC cooling motor assembly. It helps with rotor/stator/bearing/fan-style parts, but it does not fully replace the MVP primitive motor yet. The MVP component IDs must stay:

`housing`, `front_cover`, `rear_cover`, `rotor`, `stator`, `shaft`, `bearing_front`, `bearing_rear`, `fan`, `terminal_box`, `bolts`.
