import json
from pathlib import Path
from typing import Any, Dict


def load_passport(path: str | Path) -> Dict[str, Any]:
    passport_path = Path(path)
    if not passport_path.exists():
        raise FileNotFoundError(f"Passport file not found: {passport_path}")
    with passport_path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def save_results(path: str | Path, results: Dict[str, Any]) -> None:
    output_path = Path(path)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with output_path.open("w", encoding="utf-8") as handle:
        json.dump(results, handle, indent=2)
        handle.write("\n")

