#!/usr/bin/env python3
"""Run the Motor Re-X decision engine outside ROS 2."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parents[1]
DECISION_PACKAGE = PROJECT_ROOT / "ros2_ws" / "src" / "motor_rex_decision_engine"
if str(DECISION_PACKAGE) not in sys.path:
    sys.path.insert(0, str(DECISION_PACKAGE))

from motor_rex_decision_engine.health_model import evaluate_passport, load_sensor_rows
from motor_rex_decision_engine.passport_loader import load_passport, save_results


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Evaluate a motor passport and export Re-X decisions.")
    parser.add_argument(
        "--passport",
        default=str(PROJECT_ROOT / "data" / "sample_motor_passport.json"),
        help="Path to Digital Product Passport JSON.",
    )
    parser.add_argument(
        "--sensors",
        default=str(PROJECT_ROOT / "data" / "mock_sensor_stream.csv"),
        help="Path to mock or real sensor CSV.",
    )
    parser.add_argument(
        "--output",
        default=str(PROJECT_ROOT / "data" / "processed_motor_results.json"),
        help="Path for processed Re-X result JSON.",
    )
    parser.add_argument("--print-summary", action="store_true", help="Print summary JSON to stdout.")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    passport = load_passport(args.passport)
    sensor_rows = load_sensor_rows(args.sensors)
    results = evaluate_passport(passport, sensor_rows)
    save_results(args.output, results)

    if args.print_summary:
        print(json.dumps(results["summary"], indent=2))
    else:
        print(f"Processed {results['product_id']} -> {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
