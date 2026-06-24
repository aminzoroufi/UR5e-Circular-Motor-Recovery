# Sim-To-Real Plan

1. Keep the official Universal Robots UR5e ROS 2 description as the simulated model contract.
2. Add and calibrate the official Universal Robots ROS 2 driver for a physical UR5e.
3. Replace the simulated `gz_ros2_control` system with the real hardware interface and controller configuration.
4. Select and integrate a Robotiq 2F-85 gripper model and driver, or a verified equivalent tool changer/gripper.
5. Build physical fixtures for the motor, covers, shaft, bearings, fan, terminal box, and bolts.
6. Calibrate robot, world, tool, table, fixture, and camera frames.
7. Replace mock component poses with real camera/depth perception.
8. Replace mock vibration, thermal, current, insulation, and alignment values with real sensors.
9. Add safety zones, speed limits, collision geometry, E-stop behavior, and operator confirmation steps.
10. Keep the component IDs and station named poses stable while replacing generated runtime motor meshes and primitive collision links with CAD-derived links.
11. Test one component sorting task, for example bearing movement to replace bin.
12. Test guided disassembly with human approval between steps.
13. Test replacement bearing insertion and reassembly with conservative speed and force limits.
14. Connect industrial protocols such as MQTT or OPC UA for plant/maintenance data.
15. Log real execution results back into the Digital Product Passport.
16. Iterate from replayed simulation to supervised live robot execution.

The safest first physical milestone is one component pick-and-place from a known pose, not full autonomous motor disassembly.
