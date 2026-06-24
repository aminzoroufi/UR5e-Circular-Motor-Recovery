# Unity Digital Twin Scaffold

Open this folder as a Unity project in Unity 2021.3 LTS or 2022.3 LTS.

The scripts under `Assets/Scripts` provide:

- separable motor components
- gravity-enabled separable motor parts
- JSON Digital Product Passport loading
- Re-X decision coloring
- selectable component details
- simulated robot/task progress playback

`Assets/Data/sample_motor_passport.json` is included for local Digital Product Passport loading. The Unity side is the high-quality visual/operator layer for the MVP; live Unity-ROS streaming is deferred, so use JSON/task-log polling or scripted playback first.

The MVP scene can be assembled from Unity primitives using the hierarchy documented in `../docs/architecture.md` and the placeholder ledger in `../docs/placeholders.md`.
