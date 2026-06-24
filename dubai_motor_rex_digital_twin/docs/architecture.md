# Architecture

## System Layers

The prototype is split into five practical layers:

1. Data and decision layer: `data/` plus `motor_rex_decision_engine`.
2. ROS 2 control and workflow layer: `motor_rex_task_planner`, `motor_rex_perception_mock`, and ROS topics.
3. Simulation layer: `motor_rex_description`, `motor_rex_gazebo`, `ros2_control`, and Gazebo Sim.
4. Motion planning layer: `motor_rex_moveit_config` and MoveIt 2.
5. Operator visualization layer: Streamlit analytics plus Unity visual digital twin scaffold.

## ROS 2 Control Layer

`motor_rex_description` defines the official UR5e model integration, a Robotiq 2F-85 style parallel gripper integration, and the workcell in Xacro. It includes a `ros2_control` block using `gz_ros2_control/GazeboSimSystem` so the same controller structure can later be replaced with a physical hardware interface.

Controllers:

- `joint_state_broadcaster`
- `arm_controller`
- `gripper_controller`

## Gazebo Simulation Layer

`motor_rex_gazebo` provides a focused one-arm recovery cell: one large source table, four category tables, one UR5e, one Robotiq-style gripper, and 11 separable dynamic motor parts.

Gazebo spawns each detailed OBJ as a separate dynamic source-table model with gravity, inertial data, friction, and lightweight primitive collision geometry. At grasp, the dynamic source model is removed and a collision-free visual proxy follows `tool0`; after release, that proxy remains at a deterministic non-overlapping category-table pose. STEP-derived meshes can replace the visuals without changing component IDs or task-planner contracts.

## MoveIt Planning Layer

`motor_rex_moveit_config` defines:

- `arm` planning group from `robot_base_link` to `tool0`
- `gripper` group for finger joints
- Named poses for home, inspection, pickup, and category placement
- OMPL planning settings
- Controller mapping for trajectory execution

The task planner has two execution modes:

- `simulated`: advances task-level pick/place results without moving the robot.
- `moveit`: plans each named pose through MoveIt, retimes the trajectory for ros2_control, executes it through `/execute_trajectory`, and then records the same task-progress contract used by Streamlit and Unity.

For MVP reliability, the planner uses component world positions and category target positions with approach/contact offsets. The scene controller attaches a part only after the gripper reaches the contact phase.

## Python Decision Engine

`motor_rex_decision_engine` loads the Digital Product Passport and mock sensor CSV, adjusts health/risk scores, applies the Re-X rules, writes `processed_motor_results.json`, and publishes ROS summaries.

Decision rules:

- Health >= 75: reuse
- Health >= 50 and < 75: repair/remanufacture
- Health >= 25 and < 50: replace
- Health < 25: recycle

## Streamlit Dashboard

The Streamlit app reuses the same decision engine code and displays:

- Motor overview
- Component health/risk
- Decision counts
- Robot task plan
- Digital twin readiness checklist

## Unity Visual Digital Twin Layer

Unity is the operator-facing visual layer. It is currently a scaffold with UR5e/Robotiq visual assets, detailed separable motor OBJ meshes, and scripts for:

- Loading local JSON passport data
- Selecting motor parts
- Separable motor-part view
- Applying Re-X color coding
- Simulated task playback
- UI hooks for passport, component, and progress panels

Live ROS bridge integration is optional in this MVP and should be added after the ROS workflow stabilizes. The current bridge point is `data/task_progress_latest.json` plus the ROS topics documented in `ros_topics_services_actions.md`.

## Replacement Contracts

- Robot contract: keep the MoveIt planning group name `arm`, UR-style joint names, station named-pose names, and `/execute_trajectory` or controller trajectory interface stable when connecting a physical UR5e driver.
- Gripper contract: keep a `gripper` planning/control group and open/close command abstraction when connecting a physical Robotiq 2F-85 driver.
- Motor contract: keep component IDs `housing`, `front_cover`, `rear_cover`, `rotor`, `stator`, `shaft`, `bearing_front`, `bearing_rear`, `fan`, `terminal_box`, and `bolts` stable when replacing generated runtime meshes with STEP-derived CAD parts.
- UI/data contract: keep `processed_motor_results.json`, `task_progress_latest.json`, and `/motor_rex/*` JSON schemas stable for Streamlit and Unity.

## Data Flow

```text
Digital Product Passport JSON
        +
Mock sensor CSV
        |
        v
Decision engine
        |
        +--> processed_motor_results.json
        +--> /motor_rex/passport
        +--> /motor_rex/component_decisions
        +--> /motor_rex/summary
                 |
Mock perception --> task planner --> MoveIt/Gazebo execution + task progress/final report
                 |
                 v
Gazebo/MoveIt simulation and Streamlit/Unity visualization
```
