from setuptools import find_packages, setup


package_name = "motor_rex_decision_engine"

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
    description="Digital Product Passport and Re-X decision engine.",
    license="MIT",
    entry_points={
        "console_scripts": [
            "decision_engine_node = motor_rex_decision_engine.decision_engine_node:main",
        ],
    },
)

