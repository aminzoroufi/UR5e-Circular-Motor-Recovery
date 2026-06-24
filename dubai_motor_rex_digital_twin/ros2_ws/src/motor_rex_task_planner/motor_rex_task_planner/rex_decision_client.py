import json
from typing import Dict, List


def parse_component_decisions(message_data: str) -> List[Dict]:
    try:
        payload = json.loads(message_data)
    except json.JSONDecodeError:
        return []
    return payload if isinstance(payload, list) else []


def parse_component_poses(message_data: str) -> Dict[str, Dict]:
    try:
        payload = json.loads(message_data)
    except json.JSONDecodeError:
        return {}
    return payload if isinstance(payload, dict) else {}

