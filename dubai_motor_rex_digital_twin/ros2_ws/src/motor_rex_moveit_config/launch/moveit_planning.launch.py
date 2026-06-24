import os
import yaml

from ament_index_python.packages import get_package_share_directory
from launch import LaunchDescription
from launch.actions import DeclareLaunchArgument
from launch.conditions import IfCondition
from launch.substitutions import Command, FindExecutable, LaunchConfiguration, PathJoinSubstitution
from launch_ros.actions import Node
from launch_ros.substitutions import FindPackageShare


def load_yaml(package_name, relative_path):
    package_path = get_package_share_directory(package_name)
    absolute_path = os.path.join(package_path, relative_path)
    with open(absolute_path, "r", encoding="utf-8") as handle:
        return yaml.safe_load(handle)


def load_text(package_name, relative_path):
    package_path = get_package_share_directory(package_name)
    absolute_path = os.path.join(package_path, relative_path)
    with open(absolute_path, "r", encoding="utf-8") as handle:
        return handle.read()


def generate_launch_description():
    use_rviz = LaunchConfiguration("rviz")
    use_sim_time = LaunchConfiguration("use_sim_time")
    robot_description = {
        "robot_description": Command(
            [
                FindExecutable(name="xacro"),
                " ",
                PathJoinSubstitution(
                    [
                        FindPackageShare("motor_rex_description"),
                        "urdf",
                        "motor_rex_cell.urdf.xacro",
                    ]
                ),
            ]
        )
    }

    robot_description_semantic = {
        "robot_description_semantic": load_text(
            "motor_rex_moveit_config", "config/motor_rex_cell.srdf"
        )
    }
    robot_description_kinematics = {
        "robot_description_kinematics": load_yaml(
            "motor_rex_moveit_config", "config/kinematics.yaml"
        )
    }
    joint_limits = load_yaml("motor_rex_moveit_config", "config/joint_limits.yaml")
    ompl = load_yaml("motor_rex_moveit_config", "config/ompl_planning.yaml")
    controllers = load_yaml("motor_rex_moveit_config", "config/moveit_controllers.yaml")

    move_group = Node(
        package="moveit_ros_move_group",
        executable="move_group",
        output="screen",
        arguments=[
            "--ros-args",
            "--log-level",
            "move_group.moveit.moveit.ros.occupancy_map_monitor:=fatal",
        ],
        parameters=[
            robot_description,
            robot_description_semantic,
            robot_description_kinematics,
            joint_limits,
            ompl,
            controllers,
            {"publish_robot_description_semantic": True},
            {"allow_trajectory_execution": True},
            {"trajectory_execution.allowed_start_tolerance": 10.0},
            {"use_sim_time": use_sim_time},
        ],
    )

    rviz = Node(
        package="rviz2",
        executable="rviz2",
        output="log",
        condition=IfCondition(use_rviz),
        parameters=[
            robot_description,
            robot_description_semantic,
            robot_description_kinematics,
            joint_limits,
            {"use_sim_time": use_sim_time},
        ],
    )

    return LaunchDescription(
        [
            DeclareLaunchArgument(
                "rviz",
                default_value="false",
                description="Launch RViz when a desktop display is available.",
            ),
            DeclareLaunchArgument(
                "use_sim_time",
                default_value="false",
                description="Use Gazebo/ROS simulation clock.",
            ),
            move_group,
            rviz,
        ]
    )
