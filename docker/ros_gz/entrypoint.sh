#!/usr/bin/env bash
set -e

source "/opt/ros/${ROS_DISTRO}/setup.bash"

if [ -f /workspace/ros_ws/install/setup.bash ]; then
  source /workspace/ros_ws/install/setup.bash
elif [ -f /workspace/install/setup.bash ]; then
  source /workspace/install/setup.bash
fi

exec "$@"

