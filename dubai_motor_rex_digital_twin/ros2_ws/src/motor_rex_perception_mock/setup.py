from setuptools import find_packages, setup


package_name = "motor_rex_perception_mock"

setup(
    name=package_name,
    version="0.1.0",
    packages=find_packages(),
    data_files=[
        ("share/ament_index/resource_index/packages", [f"resource/{package_name}"]),
        (f"share/{package_name}", ["package.xml"]),
    ],
    install_requires=["setuptools"],
    zip_safe=True,
    maintainer="Dubai Motor Re-X Team",
    maintainer_email="demo@example.com",
    description="Mock perception publishers for motor recovery.",
    license="MIT",
    entry_points={
        "console_scripts": [
            "mock_camera_node = motor_rex_perception_mock.mock_camera_node:main",
            "mock_sensor_node = motor_rex_perception_mock.mock_sensor_node:main",
            "component_pose_publisher = motor_rex_perception_mock.component_pose_publisher:main",
        ],
    },
)

