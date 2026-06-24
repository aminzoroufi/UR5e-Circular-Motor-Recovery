# Dubai Cooling Motor Robotic Re-X Digital Twin

Simulation-based, hardware-ready demonstrator for robotic recovery of end-of-life electric motors from Dubai cooling infrastructure. The prototype combines ROS 2, Gazebo, MoveIt, a Digital Product Passport, a Python Re-X decision engine, mock perception/sensor streams, a Streamlit operator dashboard, and a Unity visual digital twin scaffold.

This is not just an animation. It is a practical architecture that can become a real industrial digital twin by replacing mock data and simulated execution with physical robot drivers, calibrated perception, real sensors, and live plant data.

## Why Dubai

Dubai depends heavily on HVAC, district cooling, pumps, chillers, cooling towers, AHUs, hotels, malls, towers, and industrial cooling systems. Failed motors often still contain reusable housings, rotors, covers, shafts, terminal boxes, and recoverable copper/steel value. This prototype demonstrates a robotic Re-X workflow: reuse, repair/remanufacture, replace, or recycle each part before defaulting to low-value scrap.

## Architecture

- ROS 2 Jazzy workspace with description, Gazebo, MoveIt, decision, perception mock, and task planner packages.
- Gazebo Sim recovery cell with one official UR5e, a Robotiq 2F-85 style gripper, one large motor-parts table, four correctly sized category tables, and gravity-enabled separable source-part models.
- MoveIt configuration with `arm`/`gripper` groups, named poses for sorting/recovery stations, and an MVP execution path through MoveIt planning plus trajectory execution.
- Digital Product Passport in JSON plus mock sensor CSV for health/risk evaluation.
- Streamlit dashboard for operator analytics and decision review.
- Unity scaffold for a visual digital twin with separable/selectable motor components and Re-X color coding.

## Folder Structure

```text
dubai_motor_rex_digital_twin/
  data/
  docs/
  ros2_ws/src/
    motor_rex_description/
    motor_rex_gazebo/
    motor_rex_moveit_config/
    motor_rex_decision_engine/
    motor_rex_perception_mock/
    motor_rex_task_planner/
  streamlit_dashboard/
  tools/
  unity_digital_twin/
```

## Run With Docker

From `/Users/megaking/Codex_Projects/Electric_Motor`:

```bash
docker compose run --rm ros-gz bash
cd dubai_motor_rex_digital_twin/ros2_ws
colcon build --symlink-install
source install/setup.bash
```

The Docker image contains ROS 2 Jazzy, Gazebo Harmonic, MoveIt 2, ros2_control, ros_gz, xacro, and common ROS development tools.

## Run The Decision Engine

Outside ROS:

```bash
cd /workspace/dubai_motor_rex_digital_twin
python3 tools/run_decision_engine.py --print-summary
```

Inside ROS:

```bash
ros2 run motor_rex_decision_engine decision_engine_node
```

Outputs:

- `/motor_rex/passport`
- `/motor_rex/component_decisions`
- `/motor_rex/summary`
- `data/processed_motor_results.json`

## Launch Gazebo

```bash
cd /workspace/dubai_motor_rex_digital_twin/ros2_ws
source install/setup.bash
ros2 launch motor_rex_gazebo gazebo_recovery_cell.launch.py
```

This starts the industrial recovery cell server, spawns the robot/motor URDF into Gazebo, and loads the controllers. The default is headless for Docker. Run a Gazebo GUI separately only when your host display is configured for container GUI apps.

To request the Gazebo 3D GUI after macOS display forwarding is configured:

```bash
ros2 launch motor_rex_gazebo gazebo_recovery_cell.launch.py gui:=true
```

On macOS with Docker Desktop this usually requires XQuartz:

```bash
open -a XQuartz
xhost + 127.0.0.1
DISPLAY=host.docker.internal:0 docker compose run --rm ros-gz bash
```

Then build/source the workspace and launch with `gui:=true`. If the GUI is unreliable on macOS, keep Gazebo headless and use RViz or Unity for visualization.

## Launch MoveIt

```bash
cd /workspace/dubai_motor_rex_digital_twin/ros2_ws
source install/setup.bash
ros2 launch motor_rex_moveit_config moveit_planning.launch.py
```

The MVP MoveIt config includes an `arm` planning group, a `gripper` group, named poses, joint limits, OMPL settings, and controller mappings. RViz is disabled by default for headless Docker. Use `rviz:=true` when a desktop display is available.

RViz is often the easiest way to inspect the robot model and planned motion:

```bash
ros2 launch motor_rex_moveit_config moveit_planning.launch.py rviz:=true use_sim_time:=true
```

## Run The Robot Workflow

```bash
cd /workspace/dubai_motor_rex_digital_twin/ros2_ws
source install/setup.bash
ros2 launch motor_rex_task_planner rex_workflow.launch.py
```

The default workflow uses task-level simulated robot actions. For the end-to-end MVP with Gazebo, MoveIt planning, ros2_control trajectory execution, decision loading, task progress, and final reporting, run:

```bash
cd /workspace/dubai_motor_rex_digital_twin/ros2_ws
source install/setup.bash
ros2 launch motor_rex_task_planner rex_full_mvp.launch.py
```

This launches Gazebo first, starts MoveIt after the controllers are available, then runs the 17-step Re-X workflow in `execution_mode:=moveit`. It writes live progress to `data/task_progress_latest.json` for Streamlit/Unity/dashboard consumers. The simulator stays alive after the workflow finishes; stop it with Ctrl+C when the demo is done.

The Gazebo MVP now also starts `gazebo_scene_controller.py`. This node spawns dynamic OBJ models named `rex_part_*` for the separable motor components, opens/closes the gripper controller during pick/place, removes each source model at grasp, and uses a collision-free `rex_held_*` proxy during carry. Deterministic per-component station offsets preserve a visible, non-overlapping sorted layout. Fully dynamic destination placement remains a documented placeholder for future contact-rich simulation.

With display forwarding available, add `gazebo_gui:=true`:

```bash
ros2 launch motor_rex_task_planner rex_full_mvp.launch.py gazebo_gui:=true
```

This shows the 3D cell while the 17-step workflow runs.

## Run Streamlit

On the host, use a dedicated virtual environment so Streamlit/Pandas/NumPy do not conflict with Anaconda base packages:

```bash
cd /Users/megaking/Codex_Projects/Electric_Motor/dubai_motor_rex_digital_twin
python3 -m venv .venv_streamlit
source .venv_streamlit/bin/activate
python -m pip install --upgrade pip
python -m pip install -r streamlit_dashboard/requirements.txt
python -m streamlit run streamlit_dashboard/app.py
```

Then open `http://localhost:8501`. If port 8501 is already in use, add `--server.port 8502`.

## Open Unity

Open `/Users/megaking/Codex_Projects/Electric_Motor/dubai_motor_rex_digital_twin/unity_digital_twin` in Unity 2022.3 LTS or compatible. The current Unity layer is a visual digital twin scaffold with official UR5e/Robotiq mesh assets, detailed separable motor OBJ parts, selectable components, Re-X colors, and `Assets/Data/sample_motor_passport.json` for local TextAsset loading. Live Unity-ROS bridging is intentionally deferred until the ROS/Gazebo/MoveIt backend is stable.

## What Is Real, Simulated, And Mock

- Real engineering structure: ROS packages, Docker ROS/Gazebo/MoveIt environment, decision rules, data schemas, topic architecture, MoveIt named-pose execution path, Gazebo/MoveIt launch structure, Streamlit dashboard, Unity script architecture.
- Target robot: Universal Robots UR5e. The ROS/Gazebo side uses the official UR5e description and UR-style joint/controller interfaces; Unity uses staged UR5e visual meshes with a primitive fallback only if imports fail.
- Target gripper: Robotiq 2F-85 style parallel gripper. The ROS/Gazebo side uses the staged Robotiq description/controller path; Unity uses staged Robotiq visual meshes with a primitive fallback only if imports fail.
- Simulated: workcell, component poses, MoveIt trajectory execution, gripper open/close, gravity-enabled source parts, stabilized category-table assignment, and final task status.
- Mock: camera detections, sensor stream, health data, motor passport values, and component source poses.
- Future real replacements: physical UR5e driver/calibration, physical Robotiq driver feedback, calibrated depth camera, vibration/thermal/current sensors, OPC UA/MQTT plant data, and STEP-derived motor meshes replacing the generated runtime OBJ visuals.

See `docs/placeholders.md` for the full placeholder ledger and replacement contract.

## Sim-To-Real Path

Connect the official UR5e description to a calibrated physical UR5e driver, calibrate robot/world/camera frames, replace mock component poses with perception output, replace sensor CSV with live data, validate safety zones, then test one guided sorting task before expanding to full disassembly/reassembly. The task manager already supports a MoveIt execution mode for named-pose MVP trajectories.
