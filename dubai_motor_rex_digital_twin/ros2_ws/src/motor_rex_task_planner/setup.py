from glob import glob

from setuptools import find_packages, setup


package_name = "motor_rex_task_planner"

setup(
    name=package_name,
    version="0.1.0",
    packages=find_packages(),
    data_files=[
        ("share/ament_index/resource_index/packages", [f"resource/{package_name}"]),
        (f"share/{package_name}", ["package.xml"]),
        (f"share/{package_name}/config", glob("config/*.yaml")),
        (f"share/{package_name}/launch", glob("launch/*.launch.py")),
    ],
    install_requires=["setuptools"],
    zip_safe=True,
    maintainer="Dubai Motor Re-X Team",
    maintainer_email="demo@example.com",
    description="Task planner for robotic motor recovery.",
    license="MIT",
    entry_points={
        "console_scripts": [
            "task_planner_node = motor_rex_task_planner.task_planner_node:main",
        ],
    },
)
