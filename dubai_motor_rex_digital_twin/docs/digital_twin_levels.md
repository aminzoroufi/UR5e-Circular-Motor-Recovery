# Digital Twin Levels

## Digital Model

A digital model is an offline representation. It does not automatically receive real-world updates or send commands back to a physical asset.

In this project, the generated separable motor meshes, robot geometry, and Unity scene scaffold are digital model elements.

## Digital Shadow

A digital shadow receives data from the real world, but the flow is mostly one-way. Real sensor readings, maintenance logs, and inspection data update the digital representation.

This project simulates that layer with mock vibration, temperature, current, visual damage, insulation risk, and alignment data.

## Digital Twin

A full digital twin has live bidirectional connection: physical asset state updates the virtual model, and validated decisions or commands can influence the physical process.

For this project, full digital twin status would require a physical robot, calibrated sensors, real-time data streams, safety validation, and controlled execution back to the cell.

## Current Project Status

Current status: simulation-based digital twin demonstrator / digital-twin-ready prototype.

It has the software architecture and data contracts needed for a real digital twin, but the robot execution, perception, sensor inputs, and task results are still simulated or mock.

## Required For Full Industrial Twin

- Official robot driver for UR5e/UR10e or xArm.
- Real gripper/tool changer integration.
- Calibrated robot/world/camera frames.
- Depth camera and component recognition.
- Live vibration, thermal, current, insulation, and alignment data.
- Industrial data integration through MQTT, OPC UA, database, or maintenance system APIs.
- Safety zones, collision validation, and operator approval gates.
- Commissioned task execution with real fixtures and end-of-arm tooling.
