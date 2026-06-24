#!/usr/bin/env bash
set -euo pipefail

export DISPLAY="${DISPLAY:-:99}"
export VNC_RESOLUTION="${VNC_RESOLUTION:-1600x900x24}"
export LIBGL_ALWAYS_SOFTWARE="${LIBGL_ALWAYS_SOFTWARE:-1}"
export GALLIUM_DRIVER="${GALLIUM_DRIVER:-llvmpipe}"
export QT_X11_NO_MITSHM=1
export XDG_RUNTIME_DIR="${XDG_RUNTIME_DIR:-/tmp/runtime-root}"
export AMENT_TRACE_SETUP_FILES="${AMENT_TRACE_SETUP_FILES:-}"
export GZ_SIM_RESOURCE_PATH="/workspace/dubai_motor_rex_digital_twin/ros2_ws/src:/workspace/dubai_motor_rex_digital_twin/ros2_ws/install/robotiq_description/share:/workspace/dubai_motor_rex_digital_twin/ros2_ws/install/ur_description/share:${GZ_SIM_RESOURCE_PATH:-}"
export IGN_GAZEBO_RESOURCE_PATH="${GZ_SIM_RESOURCE_PATH}:${IGN_GAZEBO_RESOURCE_PATH:-}"

mkdir -p "${XDG_RUNTIME_DIR}"
chmod 700 "${XDG_RUNTIME_DIR}"
rm -f "/tmp/.X${DISPLAY#:}-lock" "/tmp/.X11-unix/X${DISPLAY#:}" || true

# Gazebo persists an old camera and 1000px window in the Docker home volume.
# Normalize it on every launch so the one-arm sorting cell fills the noVNC view.
gz_gui_config="/root/.gz/sim/8/gui.config"
if [ -f "${gz_gui_config}" ]; then
  sed -i '0,/<width>/{s#<width>[^<]*</width>#<width>1600</width>#}' "${gz_gui_config}"
  sed -i '0,/<height>/{s#<height>[^<]*</height>#<height>900</height>#}' "${gz_gui_config}"
  sed -i '0,/<camera_pose>/{s#<camera_pose>[^<]*</camera_pose>#<camera_pose>2.15 -2.45 1.55 0 0.50 2.30</camera_pose>#}' "${gz_gui_config}"
fi

echo "Starting virtual Ubuntu display on ${DISPLAY} (${VNC_RESOLUTION})"
Xvfb "${DISPLAY}" -screen 0 "${VNC_RESOLUTION}" -ac +extension GLX +render -noreset &
xvfb_pid=$!

sleep 1
if command -v fluxbox >/dev/null 2>&1; then
  fluxbox >/tmp/fluxbox.log 2>&1 &
fi

x11vnc -display "${DISPLAY}" -forever -shared -rfbport 5900 -nopw -quiet >/tmp/x11vnc.log 2>&1 &
x11vnc_pid=$!

websockify --web=/usr/share/novnc 0.0.0.0:6080 localhost:5900 >/tmp/novnc.log 2>&1 &
novnc_pid=$!

cleanup() {
  kill "${novnc_pid}" "${x11vnc_pid}" "${xvfb_pid}" 2>/dev/null || true
}
trap cleanup EXIT

echo "noVNC URL: http://localhost:6080/vnc.html?autoconnect=true&resize=scale"
echo "Checking software OpenGL:"
glxinfo -B || true

set +u
source /opt/ros/jazzy/setup.bash
set -u
cd /workspace/dubai_motor_rex_digital_twin/ros2_ws
if [ -f install/setup.bash ]; then
  set +u
  source install/setup.bash
  set -u
else
  echo "ROS workspace is not built yet. Building now..."
  colcon build --symlink-install
  set +u
  source install/setup.bash
  set -u
fi

if [ "$#" -gt 0 ]; then
  exec "$@"
fi

exec ros2 launch motor_rex_task_planner rex_full_mvp.launch.py \
  gazebo_gui:=true \
  step_period_sec:=0.25 \
  moveit_waypoint_time_step_sec:=0.18 \
  moveit_post_execute_settle_sec:=0.35
