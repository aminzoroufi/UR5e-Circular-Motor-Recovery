FROM --platform=linux/arm64 ros:jazzy-ros-base-noble

ENV DEBIAN_FRONTEND=noninteractive
ENV ROS_DISTRO=jazzy
ENV LANG=C.UTF-8
ENV LC_ALL=C.UTF-8

SHELL ["/bin/bash", "-o", "pipefail", "-c"]

RUN apt-get update && apt-get install -y --no-install-recommends \
    bash-completion \
    build-essential \
    ca-certificates \
    cmake \
    curl \
    git \
    git-lfs \
    gnupg \
    htop \
    less \
    libegl1 \
    libgl1 \
    libgl1-mesa-dri \
    libglu1-mesa \
    libglx-mesa0 \
    libxext6 \
    libxi6 \
    libxrender1 \
    lsb-release \
    mesa-utils \
    novnc \
    nano \
    python3-argcomplete \
    python3-colcon-common-extensions \
    python3-numpy \
    python3-pip \
    python3-rosdep \
    python3-vcstool \
    python3-yaml \
    tree \
    unzip \
    vim \
    websockify \
    wget \
    x11vnc \
    xvfb \
    && rm -rf /var/lib/apt/lists/*

RUN apt-get update && apt-get install -y --no-install-recommends \
    ros-jazzy-control-toolbox \
    ros-jazzy-controller-manager \
    ros-jazzy-gz-ros2-control \
    ros-jazzy-joint-state-publisher \
    ros-jazzy-joint-state-publisher-gui \
    ros-jazzy-moveit \
    ros-jazzy-moveit-servo \
    ros-jazzy-robot-state-publisher \
    ros-jazzy-ros-gz \
    ros-jazzy-ros2-control \
    ros-jazzy-ros2-controllers \
    ros-jazzy-rmw-cyclonedds-cpp \
    ros-jazzy-rviz2 \
    ros-jazzy-tf-transformations \
    ros-jazzy-xacro \
    && rm -rf /var/lib/apt/lists/*

RUN rosdep init || true \
    && rosdep update --rosdistro "${ROS_DISTRO}"

COPY docker/ros_gz/entrypoint.sh /ros_entrypoint_project.sh
COPY docker/ros_gz/start_gazebo_novnc.sh /start_gazebo_novnc.sh
RUN chmod +x /ros_entrypoint_project.sh
RUN chmod +x /start_gazebo_novnc.sh

WORKDIR /workspace
ENTRYPOINT ["/ros_entrypoint_project.sh"]
CMD ["bash"]
