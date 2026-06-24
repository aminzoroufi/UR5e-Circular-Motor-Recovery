from launch import LaunchDescription
from launch.actions import (
    AppendEnvironmentVariable,
    DeclareLaunchArgument,
    IncludeLaunchDescription,
    TimerAction,
)
from launch.launch_description_sources import PythonLaunchDescriptionSource
from launch.substitutions import Command, FindExecutable, LaunchConfiguration, PathJoinSubstitution, PythonExpression
from launch_ros.actions import Node
from launch_ros.substitutions import FindPackageShare


def generate_launch_description():
    gui = LaunchConfiguration("gui")
    world = PathJoinSubstitution(
        [
            FindPackageShare("motor_rex_gazebo"),
            "worlds",
            "motor_rex_recovery_cell.sdf",
        ]
    )
    robot_description_content = Command(
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
            " ",
            "force_abs_paths:=true",
        ]
    )
    robot_description = {"robot_description": robot_description_content}

    gz_sim = IncludeLaunchDescription(
        PythonLaunchDescriptionSource(
            [
                PathJoinSubstitution(
                    [FindPackageShare("ros_gz_sim"), "launch", "gz_sim.launch.py"]
                )
            ]
        ),
        launch_arguments={
            "gz_args": [
                PythonExpression(["'-r -v 2 ' if '", gui, "' == 'true' else '-r -s -v 2 '"]),
                world,
            ]
        }.items(),
    )

    robot_state_publisher = Node(
        package="robot_state_publisher",
        executable="robot_state_publisher",
        output="screen",
        parameters=[robot_description, {"use_sim_time": True}],
    )

    clock_bridge = Node(
        package="ros_gz_bridge",
        executable="parameter_bridge",
        arguments=["/clock@rosgraph_msgs/msg/Clock[gz.msgs.Clock"],
        output="screen",
    )

    scene_service_bridge = Node(
        package="ros_gz_bridge",
        executable="parameter_bridge",
        arguments=[
            "/world/motor_rex_recovery_cell/create@ros_gz_interfaces/srv/SpawnEntity",
            "/world/motor_rex_recovery_cell/set_pose@ros_gz_interfaces/srv/SetEntityPose",
            "/world/motor_rex_recovery_cell/remove@ros_gz_interfaces/srv/DeleteEntity",
        ],
        output="screen",
    )

    scene_controller = Node(
        package="motor_rex_gazebo",
        executable="gazebo_scene_controller.py",
        output="screen",
        parameters=[{"use_sim_time": True}],
    )

    unity_udp_bridge = Node(
        package="motor_rex_gazebo",
        executable="unity_udp_bridge.py",
        output="screen",
        parameters=[
            {
                "unity_host": "host.docker.internal",
                "unity_port": 15000,
                "max_joint_rate_hz": 30.0,
            }
        ],
    )

    spawn_cell = Node(
        package="ros_gz_sim",
        executable="create",
        arguments=[
            "-topic",
            "robot_description",
            "-name",
            "motor_rex_cell",
            "-allow_renaming",
            "true",
        ],
        output="screen",
    )

    joint_state_broadcaster = Node(
        package="controller_manager",
        executable="spawner",
        arguments=[
            "joint_state_broadcaster",
            "--controller-manager",
            "/controller_manager",
            "--controller-manager-timeout",
            "30",
        ],
        output="screen",
    )

    arm_controller = Node(
        package="controller_manager",
        executable="spawner",
        arguments=[
            "arm_controller",
            "--controller-manager",
            "/controller_manager",
            "--controller-manager-timeout",
            "30",
        ],
        output="screen",
    )

    gripper_controller = Node(
        package="controller_manager",
        executable="spawner",
        arguments=[
            "gripper_controller",
            "--controller-manager",
            "/controller_manager",
            "--controller-manager-timeout",
            "30",
        ],
        output="screen",
    )

    return LaunchDescription(
        [
            AppendEnvironmentVariable(
                "GZ_SIM_RESOURCE_PATH",
                PathJoinSubstitution(
                    [FindPackageShare("robotiq_description"), ".."]
                ),
            ),
            DeclareLaunchArgument(
                "gui",
                default_value="false",
                description="Start Gazebo GUI client when display forwarding is available.",
            ),
            gz_sim,
            clock_bridge,
            scene_service_bridge,
            robot_state_publisher,
            spawn_cell,
            TimerAction(
                period=4.0,
                actions=[
                    joint_state_broadcaster,
                    arm_controller,
                    gripper_controller,
                ],
            ),
            TimerAction(period=5.0, actions=[scene_controller, unity_udp_bridge]),
        ]
    )
