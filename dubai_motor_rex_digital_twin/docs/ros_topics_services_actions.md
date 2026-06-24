# ROS Topics, Services, And Actions

## Published Topics

| Topic | Type | Publisher | Purpose |
| --- | --- | --- | --- |
| `/motor_rex/passport` | `std_msgs/String` JSON | `decision_engine_node` | Motor-level passport summary |
| `/motor_rex/component_decisions` | `std_msgs/String` JSON | `decision_engine_node` | Component Re-X decisions |
| `/motor_rex/summary` | `std_msgs/String` JSON | `decision_engine_node` | Overall status, health, risk, value, CO2 |
| `/motor_rex/detected_components` | `std_msgs/String` JSON | `mock_camera_node` | Mock detected component names |
| `/motor_rex/component_poses` | `std_msgs/String` JSON | `component_pose_publisher` | Known component poses for MVP |
| `/motor_rex/mock_sensor_data` | `std_msgs/String` JSON | `mock_sensor_node` | Mock vibration/thermal/current/visual/insulation/alignment rows |
| `/motor_rex/task_progress` | `std_msgs/String` JSON | `task_planner_node` | Current workflow step |
| `/motor_rex/current_robot_action` | `std_msgs/String` JSON | `task_planner_node` | Current system, MoveIt, or simulated pick/place action |
| `/motor_rex/final_report` | `std_msgs/String` JSON | `task_planner_node` | Completed workflow report |
| `/joint_states` | `sensor_msgs/JointState` | `joint_state_broadcaster` | Robot and gripper joint state |
| `/gripper_controller/joint_trajectory` | `trajectory_msgs/JointTrajectory` | `task_planner_node` | Placeholder gripper open/close commands during pick/place |

## Subscribed Topics

| Node | Topic | Purpose |
| --- | --- | --- |
| `task_planner_node` | `/motor_rex/component_decisions` | Build component task sequence |
| `task_planner_node` | `/motor_rex/component_poses` | Attach source poses to tasks |
| `gazebo_scene_controller` | `/motor_rex/current_robot_action` | Move visual motor-part models to carry/bin/station poses |

## Services

| Service | Provider | Purpose |
| --- | --- | --- |
| `/controller_manager/*` | `controller_manager` | Load, list, configure, and switch controllers |
| `/robot_state_publisher/*` | `robot_state_publisher` | Standard robot description and TF support |
| `/world/motor_rex_recovery_cell/create` | `ros_gz_bridge` to Gazebo | Spawn lightweight movable `rex_part_*` visual motor models |
| `/world/motor_rex_recovery_cell/set_pose` | `ros_gz_bridge` to Gazebo | Move a collision-free selected-part proxy with `tool0`, then place it at a deterministic category-table pose |
| `/world/motor_rex_recovery_cell/remove` | `ros_gz_bridge` to Gazebo | Remove the gravity-enabled source model after the gripper reaches the grasp pose |

No project-specific custom ROS services are required for the MVP; Gazebo service bridges are used for visual model spawn and pose updates.

## Actions

| Action | Status | Purpose |
| --- | --- | --- |
| `/move_action` (`moveit_msgs/action/MoveGroup`) | Used in `execution_mode:=moveit` | Plan to configured named poses |
| `/execute_trajectory` (`moveit_msgs/action/ExecuteTrajectory`) | Used in `execution_mode:=moveit` | Execute planned arm trajectories through MoveIt controller integration |
| `FollowJointTrajectory` for `arm_controller` | Controller-ready | MoveIt or custom action clients can execute arm trajectories |
| `FollowJointTrajectory` for `gripper_controller` | Controller-ready | Open/close gripper fingers |

## Runtime Files

| File | Writer | Purpose |
| --- | --- | --- |
| `data/processed_motor_results.json` | `decision_engine_node` or standalone tool | Latest health/Re-X decision output |
| `data/task_progress_latest.json` | `task_planner_node` | Latest workflow status for Streamlit and Unity-side polling |

The task planner supports both `execution_mode:=simulated` and `execution_mode:=moveit`. In MoveIt mode, `RobotActionManager` still owns the component-to-station decision payload while `MoveItNamedPoseExecutor` performs the named-pose robot motion. `gazebo_scene_controller` consumes the same payload to keep the Gazebo visual twin synchronized with task progress.
