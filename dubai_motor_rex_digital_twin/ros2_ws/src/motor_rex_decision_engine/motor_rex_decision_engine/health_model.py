import csv
from collections import Counter
from pathlib import Path
from statistics import mean
from typing import Any, Dict, Iterable, List


DECISION_RULES = (
    (75, "reuse", "reuse_bin", "pick_to_reuse_bin"),
    (50, "repair/remanufacture", "repair_bin", "pick_to_repair_station"),
    (25, "replace", "replace_bin", "pick_to_replace_bin"),
    (0, "recycle", "recycle_bin", "pick_to_recycle_bin"),
)

SENSOR_HEALTH_PENALTY_SCALE = 0.18


REQUIRED_TESTS = {
    "reuse": "verification_check",
    "repair/remanufacture": "repair_diagnostic",
    "replace": "replacement_verification",
    "recycle": "material_sorting_check",
}


def decision_from_health(health_score: float) -> tuple[str, str, str]:
    for threshold, decision, station, action in DECISION_RULES:
        if health_score >= threshold:
            return decision, station, action
    return "recycle", "recycle_bin", "pick_to_recycle_bin"


def risk_level(risk_score: float) -> str:
    if risk_score >= 75:
        return "high"
    if risk_score >= 45:
        return "medium"
    return "low"


def load_sensor_rows(path: str | Path) -> List[Dict[str, Any]]:
    sensor_path = Path(path)
    if not sensor_path.exists():
        return []

    rows: List[Dict[str, Any]] = []
    with sensor_path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        for row in reader:
            parsed = dict(row)
            for key in (
                "vibration_mm_s",
                "temperature_c",
                "current_a",
                "visual_damage_score",
                "insulation_risk",
                "alignment_error_mm",
            ):
                parsed[key] = float(parsed[key])
            rows.append(parsed)
    return rows


def _sensor_penalty(sensor_rows: Iterable[Dict[str, Any]]) -> float:
    penalties = []
    for row in sensor_rows:
        vibration_penalty = max(0.0, row["vibration_mm_s"] - 3.0) * 2.4
        temperature_penalty = max(0.0, row["temperature_c"] - 65.0) * 0.35
        visual_penalty = row["visual_damage_score"] * 10.0
        insulation_penalty = row["insulation_risk"] * 8.0
        alignment_penalty = max(0.0, row["alignment_error_mm"] - 0.15) * 18.0
        penalties.append(
            vibration_penalty
            + temperature_penalty
            + visual_penalty
            + insulation_penalty
            + alignment_penalty
        )
    return mean(penalties) if penalties else 0.0


def _adjust_health(component: Dict[str, Any], rows: List[Dict[str, Any]]) -> float:
    base = float(component.get("health_score", 0))
    penalty = _sensor_penalty(rows) * SENSOR_HEALTH_PENALTY_SCALE
    return max(0.0, min(100.0, round(base - penalty, 1)))


def _adjust_risk(component: Dict[str, Any], rows: List[Dict[str, Any]], health: float) -> float:
    base = float(component.get("risk_score", 0))
    sensor_risk = _sensor_penalty(rows) * 1.2
    health_risk = max(0.0, 70.0 - health) * 0.35
    return max(0.0, min(100.0, round(base + sensor_risk + health_risk, 1)))


def evaluate_passport(passport: Dict[str, Any], sensor_rows: List[Dict[str, Any]] | None = None) -> Dict[str, Any]:
    sensor_rows = sensor_rows or []
    rows_by_component: Dict[str, List[Dict[str, Any]]] = {}
    for row in sensor_rows:
        rows_by_component.setdefault(row["component_id"], []).append(row)

    components = []
    for component in passport.get("components", []):
        component_id = component["component_id"]
        related_rows = rows_by_component.get(component_id, [])
        health = _adjust_health(component, related_rows)
        risk = _adjust_risk(component, related_rows, health)
        decision, station, action = decision_from_health(health)
        confidence = float(component.get("confidence", 0.75))

        evaluated = {
            **component,
            "baseline_health_score": component.get("health_score"),
            "health_score": health,
            "risk_score": risk,
            "risk_level": risk_level(risk),
            "confidence": round(confidence, 2),
            "decision": decision,
            "target_station": station,
            "robot_action": action,
            "required_test": REQUIRED_TESTS[decision],
            "sensor_rows_used": len(related_rows),
        }
        components.append(evaluated)

    decisions = Counter(component["decision"] for component in components)
    total_value = round(sum(float(c.get("recovered_value_aed", 0)) for c in components), 2)
    total_co2 = round(sum(float(c.get("co2_saving_kg", 0)) for c in components), 2)
    overall_health = round(mean([float(c["health_score"]) for c in components]), 1) if components else 0.0
    average_risk = round(mean([float(c["risk_score"]) for c in components]), 1) if components else 0.0
    average_confidence = round(mean([float(c["confidence"]) for c in components]), 2) if components else 0.0

    if any(component["decision"] in {"replace", "recycle"} for component in components):
        final_status = "recoverable_with_replacement"
    elif any(component["decision"] == "repair/remanufacture" for component in components):
        final_status = "recoverable_after_remanufacture"
    else:
        final_status = "reuse_ready"

    return {
        "product_id": passport.get("product_id"),
        "source_site": passport.get("source_site"),
        "asset_type": passport.get("asset_type"),
        "rated_power_kw": passport.get("rated_power_kw"),
        "operating_hours": passport.get("operating_hours"),
        "installation_year": passport.get("installation_year"),
        "failure_symptoms": passport.get("failure_symptoms", []),
        "location_context": passport.get("location_context", []),
        "components": components,
        "summary": {
            "final_status": final_status,
            "overall_health": overall_health,
            "average_risk": average_risk,
            "average_confidence": average_confidence,
            "total_value_recovered_aed": total_value,
            "total_co2_saved_kg": total_co2,
            "decision_counts": dict(decisions),
        },
    }
