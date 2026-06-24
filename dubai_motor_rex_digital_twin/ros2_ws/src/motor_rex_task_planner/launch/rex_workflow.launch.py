from launch import LaunchDescription
from launch.actions import DeclareLaunchArgument
from launch.substitutions import LaunchConfiguration
from launch_ros.actions import Node


def generate_launch_description():
    execution_mode = LaunchConfiguration("execution_mode")
    step_period_sec = LaunchConfiguration("step_period_sec")
    moveit_waypoint_time_step_sec = LaunchConfiguration("moveit_waypoint_time_step_sec")
    moveit_post_execute_settle_sec = LaunchConfiguration("moveit_post_execute_settle_sec")
    moveit_velocity_scale = LaunchConfiguration("moveit_velocity_scale")
    moveit_acceleration_scale = LaunchConfiguration("moveit_acceleration_scale")

    return LaunchDescription(
        [
            DeclareLaunchArgument(
                "execution_mode",
                default_value="simulated",
                description="Use 'simulated' for task-only playback or 'moveit' for MoveGroup execution.",
            ),
            DeclareLaunchArgument(
                "step_period_sec",
                default_value="1.0",
                description="Seconds between workflow ticks.",
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
            Node(
                package="motor_rex_decision_engine",
                executable="decision_engine_node",
                name="decision_engine_node",
                output="screen",
            ),
            Node(
                package="motor_rex_perception_mock",
                executable="mock_camera_node",
                name="mock_camera_node",
                output="screen",
            ),
            Node(
                package="motor_rex_perception_mock",
                executable="mock_sensor_node",
                name="mock_sensor_node",
                output="screen",
            ),
            Node(
                package="motor_rex_perception_mock",
                executable="component_pose_publisher",
                name="component_pose_publisher",
                output="screen",
            ),
            Node(
                package="motor_rex_task_planner",
                executable="task_planner_node",
                name="task_planner_node",
                output="screen",
                parameters=[
                    {
                        "execution_mode": execution_mode,
                        "step_period_sec": step_period_sec,
                        "moveit_waypoint_time_step_sec": moveit_waypoint_time_step_sec,
                        "moveit_post_execute_settle_sec": moveit_post_execute_settle_sec,
                        "moveit_velocity_scale": moveit_velocity_scale,
                        "moveit_acceleration_scale": moveit_acceleration_scale,
                    }
                ],
            ),
        ]
    )
