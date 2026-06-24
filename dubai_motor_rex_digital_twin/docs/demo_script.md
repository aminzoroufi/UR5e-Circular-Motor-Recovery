# Demo Script

1. Introduce the problem: Dubai cooling infrastructure depends on many motors in hotels, malls, towers, district cooling plants, chillers, pumps, AHUs, and cooling towers.
2. Explain the Re-X goal: recover value before default recycling by deciding reuse, repair/remanufacture, replace, or recycle at component level.
3. Show `data/sample_motor_passport.json` and point out motor ID `DXB-HVAC-MOTOR-001`, 7.5 kW rating, 18,500 operating hours, and Dubai hotel chiller room context.
4. Run the standalone decision engine and show `data/processed_motor_results.json`.
5. Open the Streamlit dashboard and show Overview, Component Health, Re-X Decisions, and Digital Twin Readiness pages.
6. Launch the full MVP with `ros2 launch motor_rex_task_planner rex_full_mvp.launch.py`.
7. Show the Gazebo recovery cell: one official UR5e, one Robotiq 2F-85 style gripper, one large source table, four category tables, and separated dynamic motor parts.
8. Explain that MoveIt plans approach/contact trajectories from each component pose to its category table and executes them through ros2_control.
9. Point out the MVP pick/place behavior: the gripper opens/closes, source parts settle under gravity, a collision-free visual proxy follows `tool0`, and deterministic offsets keep every sorted part visible without overlap.
10. Echo `/motor_rex/task_progress` and `/motor_rex/current_robot_action`, or open `data/task_progress_latest.json`, to show the workflow advancing through component sorting.
11. Open Unity and show the operator-facing digital twin: real UR5e/Robotiq visual assets, double-sided URP materials, separable gravity-enabled motor parts, Re-X colors, and task timeline controls.
12. State the placeholder boundaries clearly: no real screw removal, no perfect contact physics, no live Unity-ROS bridge yet, no physical robot driver yet, and the generated runtime motor OBJ meshes are replaceable by STEP-derived CAD parts.
13. Close with the engineering claim: this is simulation-based today, but the architecture is ready for physical UR5e/Robotiq calibration, calibrated perception, real sensors, and OPC UA/MQTT plant connections.
