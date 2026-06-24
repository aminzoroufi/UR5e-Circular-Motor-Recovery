# Data Schema

## Digital Product Passport

File: `data/sample_motor_passport.json`

Motor-level fields:

| Field | Meaning |
| --- | --- |
| `product_id` | Unique motor ID |
| `source_site` | Source location or facility |
| `asset_type` | Motor use case |
| `rated_power_kw` | Rated power |
| `operating_hours` | Runtime before recovery |
| `installation_year` | Installation year |
| `failure_symptoms` | Reported failure symptoms |
| `location_context` | Dubai cooling context tags |
| `components` | Component-level passport entries |

Component-level fields:

| Field | Meaning |
| --- | --- |
| `component_id` | Stable machine-readable ID |
| `part_id` | Traceable part ID |
| `name` | Human-readable part name |
| `material` | Dominant material or material family |
| `health_score` | 0-100 health estimate |
| `risk_score` | 0-100 risk estimate |
| `confidence` | 0-1 confidence estimate |
| `damage_type` | Known or suspected damage |
| `test_required` | Baseline inspection/test |
| `co2_saving_kg` | Estimated CO2 saving if recovered |
| `recovered_value_aed` | Estimated recovered value in AED |
| `decision` | Initial decision, recalculated by engine |
| `robot_action` | Initial robot action hint |
| `target_station` | Initial station hint |

## Mock Sensor Stream

File: `data/mock_sensor_stream.csv`

Columns:

| Column | Meaning |
| --- | --- |
| `timestamp` | ISO timestamp |
| `motor_id` | Motor/product ID |
| `component_id` | Component receiving the reading |
| `vibration_mm_s` | Vibration severity |
| `temperature_c` | Temperature |
| `current_a` | Current draw |
| `visual_damage_score` | 0-1 visual damage estimate |
| `insulation_risk` | 0-1 insulation risk estimate |
| `alignment_error_mm` | Alignment/runout proxy |

## Processed Results

File: `data/processed_motor_results.json`

The decision engine exports:

- Motor metadata copied from the passport.
- Evaluated component list with adjusted health, risk level, decision, target station, robot action, required test, and sensor row count.
- Summary object with final status, overall health, average risk, average confidence, total recovered value, total CO2 saved, and decision counts.

## Decision Rules

| Health Score | Decision | Target |
| --- | --- | --- |
| `>= 75` | reuse | `reuse_bin` |
| `>= 50 and < 75` | repair/remanufacture | `repair_bin` |
| `>= 25 and < 50` | replace | `replace_bin` |
| `< 25` | recycle | `recycle_bin` |

