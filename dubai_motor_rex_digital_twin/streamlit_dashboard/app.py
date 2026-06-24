from __future__ import annotations

import json
import sys
from pathlib import Path

import pandas as pd
import streamlit as st


PROJECT_ROOT = Path(__file__).resolve().parents[1]
DECISION_PACKAGE = PROJECT_ROOT / "ros2_ws" / "src" / "motor_rex_decision_engine"
if str(DECISION_PACKAGE) not in sys.path:
    sys.path.insert(0, str(DECISION_PACKAGE))

from motor_rex_decision_engine.health_model import evaluate_passport, load_sensor_rows
from motor_rex_decision_engine.passport_loader import load_passport, save_results


PASSPORT_PATH = PROJECT_ROOT / "data" / "sample_motor_passport.json"
SENSOR_PATH = PROJECT_ROOT / "data" / "mock_sensor_stream.csv"
RESULTS_PATH = PROJECT_ROOT / "data" / "processed_motor_results.json"
TASK_LOG_PATH = PROJECT_ROOT / "data" / "task_progress_latest.json"


DECISION_COLORS = {
    "reuse": "#16a34a",
    "repair/remanufacture": "#ca8a04",
    "replace": "#dc2626",
    "recycle": "#2563eb",
}


@st.cache_data(show_spinner=False)
def load_results() -> dict:
    passport = load_passport(PASSPORT_PATH)
    sensors = load_sensor_rows(SENSOR_PATH)
    results = evaluate_passport(passport, sensors)
    save_results(RESULTS_PATH, results)
    return results


def component_frame(results: dict) -> pd.DataFrame:
    return pd.DataFrame(results["components"])


def sensor_frame() -> pd.DataFrame:
    return pd.read_csv(SENSOR_PATH)


def decision_badge(decision: str) -> str:
    color = DECISION_COLORS.get(decision, "#64748b")
    return f"<span style='color:white;background:{color};padding:0.2rem 0.55rem;border-radius:0.35rem'>{decision}</span>"


st.set_page_config(
    page_title="Dubai Motor Re-X Digital Twin",
    page_icon="DXB",
    layout="wide",
)

results = load_results()
components = component_frame(results)
sensors = sensor_frame()
summary = results["summary"]

st.title("Dubai Cooling Motor Robotic Re-X Digital Twin")
st.caption("Simulation-based digital twin demonstrator using synthetic Dubai cooling-infrastructure data.")

page = st.sidebar.radio(
    "Dashboard",
    [
        "Overview",
        "Component Health",
        "Re-X Decisions",
        "Robot Task Progress",
        "Digital Twin Readiness",
    ],
)

if page == "Overview":
    c1, c2, c3, c4 = st.columns(4)
    c1.metric("Motor ID", results["product_id"])
    c2.metric("Overall Health", f"{summary['overall_health']}%")
    c3.metric("Recovered Value", f"AED {summary['total_value_recovered_aed']:,.0f}")
    c4.metric("CO2 Saved", f"{summary['total_co2_saved_kg']:,.1f} kg")

    st.subheader("Asset Context")
    left, right = st.columns([1, 1])
    with left:
        st.write(
            {
                "source_site": results["source_site"],
                "asset_type": results["asset_type"],
                "rated_power_kw": results["rated_power_kw"],
                "operating_hours": results["operating_hours"],
                "installation_year": results["installation_year"],
                "final_status": summary["final_status"],
            }
        )
    with right:
        st.write("Failure symptoms")
        st.write(results["failure_symptoms"])
        st.write("Location context")
        st.write(results["location_context"])

elif page == "Component Health":
    st.subheader("Component Health")
    st.dataframe(
        components[
            [
                "component_id",
                "name",
                "material",
                "health_score",
                "risk_score",
                "risk_level",
                "confidence",
                "damage_type",
            ]
        ],
        use_container_width=True,
    )
    left, right = st.columns(2)
    with left:
        st.bar_chart(components.set_index("component_id")["health_score"])
    with right:
        st.bar_chart(components.set_index("component_id")["risk_score"])

    st.subheader("Mock Sensor Stream")
    st.dataframe(sensors, use_container_width=True)

elif page == "Re-X Decisions":
    st.subheader("Re-X Decision Distribution")
    decision_counts = pd.Series(summary["decision_counts"], name="count").sort_index()
    st.bar_chart(decision_counts)

    table = components[
        [
            "component_id",
            "name",
            "health_score",
            "risk_level",
            "decision",
            "target_station",
            "robot_action",
            "required_test",
            "co2_saving_kg",
            "recovered_value_aed",
        ]
    ].copy()
    st.dataframe(table, use_container_width=True)

    st.markdown("### Color Code")
    cols = st.columns(4)
    for col, decision in zip(cols, ["reuse", "repair/remanufacture", "replace", "recycle"]):
        col.markdown(decision_badge(decision), unsafe_allow_html=True)

elif page == "Robot Task Progress":
    st.subheader("MVP Workflow")
    if TASK_LOG_PATH.exists():
        task_log = json.loads(TASK_LOG_PATH.read_text(encoding="utf-8"))
        st.write(
            {
                "workflow_status": task_log.get("workflow_status"),
                "execution_mode": task_log.get("execution_mode"),
                "target_robot": task_log.get("target_robot"),
                "target_gripper": task_log.get("target_gripper"),
            }
        )
        progress_df = pd.DataFrame(task_log.get("tasks", []))
        st.progress(
            min(
                1.0,
                float(task_log.get("current_step", 0))
                / max(1.0, float(task_log.get("total_steps", 1))),
            )
        )
        st.dataframe(progress_df, use_container_width=True)
    else:
        ordered = [
            "Load motor passport",
            "Scan motor",
            "Evaluate health",
            "Generate Re-X decisions",
            "Move robot to home",
        ]
        for _, row in components.iterrows():
            ordered.append(f"Pick {row['component_id']} -> {row['target_station']} ({row['decision']})")
        ordered.extend(
            [
                "Replace damaged bearings with new placeholder parts",
                "Reassemble motor in simulated steps",
                "Run final test",
                "Publish final summary",
            ]
        )
        progress_df = pd.DataFrame(
            [{"step": i + 1, "description": task, "status": "planned"} for i, task in enumerate(ordered)]
        )
        st.dataframe(progress_df, use_container_width=True)
        st.info("Run `ros2 launch motor_rex_task_planner rex_full_mvp.launch.py` to publish task progress.")

elif page == "Digital Twin Readiness":
    st.subheader("What is simulated")
    st.write(
        [
            "Robot cell geometry and motor component poses",
            "Digital Product Passport",
            "Re-X decisions and task sequence",
            "Mock perception and sensor stream",
            "Gazebo-ready URDF/Xacro structure",
        ]
    )
    st.subheader("What is mock")
    st.write(
        [
            "Camera detections",
            "Component poses",
            "Vibration, thermal, current, visual damage, insulation, and alignment data",
            "Robot execution status",
        ]
    )
    st.subheader("Needed for real deployment")
    st.write(
        [
            "Physical UR5e/UR10e or xArm robot and official ROS 2 driver",
            "Real gripper or tool changer",
            "Hand-eye calibration and robot/world/camera transforms",
            "Depth camera and trained component segmentation",
            "Real vibration, thermal, current, and insulation sensors",
            "Safety zones, risk assessment, and industrial validation",
            "OPC UA/MQTT connection to maintenance and plant systems",
        ]
    )
