from launch import LaunchDescription
from launch.actions import DeclareLaunchArgument, IncludeLaunchDescription, TimerAction
from launch.launch_description_sources import PythonLaunchDescriptionSource
from launch.substitutions import LaunchConfiguration, PathJoinSubstitution
from launch_ros.substitutions import FindPackageShare


def generate_launch_description():
    moveit_delay_sec = LaunchConfiguration("moveit_delay_sec")
    workflow_delay_sec = LaunchConfiguration("workflow_delay_sec")
    step_period_sec = LaunchConfiguration("step_period_sec")
    moveit_waypoint_time_step_sec = LaunchConfiguration("moveit_waypoint_time_step_sec")
    moveit_post_execute_settle_sec = LaunchConfiguration("moveit_post_execute_settle_sec")
    moveit_velocity_scale = LaunchConfiguration("moveit_velocity_scale")
    moveit_acceleration_scale = LaunchConfiguration("moveit_acceleration_scale")
    gazebo_gui = LaunchConfiguration("gazebo_gui")

    gazebo_launch = IncludeLaunchDescription(
        PythonLaunchDescriptionSource(
            [
                PathJoinSubstitution(
                    [FindPackageShare("motor_rex_gazebo"), "launch", "gazebo_recovery_cell.launch.py"]
                )
            ]
        ),
        launch_arguments={"gui": gazebo_gui}.items(),
    )

    moveit_launch = IncludeLaunchDescription(
        PythonLaunchDescriptionSource(
            [
                PathJoinSubstitution(
                    [FindPackageShare("motor_rex_moveit_config"), "launch", "moveit_planning.launch.py"]
                )
            ]
        ),
        launch_arguments={"rviz": "false", "use_sim_time": "true"}.items(),
    )

    workflow_launch = TimerAction(
        period=workflow_delay_sec,
        actions=[
            IncludeLaunchDescription(
                PythonLaunchDescriptionSource(
                    [
                        PathJoinSubstitution(
                            [
                                FindPackageShare("motor_rex_task_planner"),
                                "launch",
                                "rex_workflow.launch.py",
                            ]
                        )
                    ]
                ),
                launch_arguments={
                    "execution_mode": "moveit",
                    "step_period_sec": step_period_sec,
                    "moveit_waypoint_time_step_sec": moveit_waypoint_time_step_sec,
                    "moveit_post_execute_settle_sec": moveit_post_execute_settle_sec,
                    "moveit_velocity_scale": moveit_velocity_scale,
                    "moveit_acceleration_scale": moveit_acceleration_scale,
                }.items(),
            )
        ],
    )

    return LaunchDescription(
        [
            DeclareLaunchArgument(
                "moveit_delay_sec",
                default_value="7.0",
                description="Delay before starting MoveIt so Gazebo clock/controllers are available.",
            ),
            DeclareLaunchArgument(
                "workflow_delay_sec",
                default_value="16.0",
                description="Delay before starting the decision/perception/task workflow.",
            ),
            DeclareLaunchArgument(
                "step_period_sec",
                default_value="0.5",
                description="Seconds between task-planner workflow ticks.",
            ),
            DeclareLaunchArgument(
                "moveit_waypoint_time_step_sec",
                default_value="0.25",
                description="Seconds added between retimed MoveIt trajectory waypoints.",
            ),
            DeclareLaunchArgument(
                "moveit_post_execute_settle_sec",
                default_value="0.35",
                description="Short pause after each MoveIt execution so current-state monitoring catches up.",
            ),
            DeclareLaunchArgument(
                "moveit_velocity_scale",
                default_value="0.75",
                description="MoveIt trajectory velocity scaling for the simulated UR5e.",
            ),
            DeclareLaunchArgument(
                "moveit_acceleration_scale",
                default_value="0.75",
                description="MoveIt trajectory acceleration scaling for the simulated UR5e.",
            ),
            DeclareLaunchArgument(
                "gazebo_gui",
                default_value="false",
                description="Start Gazebo GUI client when display forwarding is available.",
            ),
            gazebo_launch,
            TimerAction(period=moveit_delay_sec, actions=[moveit_launch]),
            workflow_launch,
        ]
    )
