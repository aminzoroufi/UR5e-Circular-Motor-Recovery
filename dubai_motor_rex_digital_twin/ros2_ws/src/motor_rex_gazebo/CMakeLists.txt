cmake_minimum_required(VERSION 3.8)
project(motor_rex_gazebo)

find_package(ament_cmake REQUIRED)

install(DIRECTORY launch models worlds
  DESTINATION share/${PROJECT_NAME}
)

install(PROGRAMS
  scripts/gazebo_scene_controller.py
  scripts/unity_udp_bridge.py
  DESTINATION lib/${PROJECT_NAME}
)

ament_package()
