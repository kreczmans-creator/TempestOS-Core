# TempestOS Material Selection — reference semantics v1.3.0
# This module is intentionally dependency-light and is a specification aid.
from __future__ import annotations

from typing import Any, Iterable

def _value(obs: dict[str, Any], field: str):
    value = obs.get("properties", {}).get(field)
    if isinstance(value, dict):
        return value.get("value"), value.get("unit")
    return None, None

def satisfies(value, op, target):
    if op == "eq": return value == target
    if op == "neq": return value != target
    if op == "lt": return value < target
    if op == "lte": return value <= target
    if op == "gt": return value > target
    if op == "gte": return value >= target
    if op == "in": return value in target
    if op == "not_in": return value not in target
    if op == "between": return target[0] <= value <= target[1]
    if op == "exists": return value is not None
    raise ValueError(f"Unsupported operator: {op}")

def hard_filter(candidate: dict[str, Any], constraints: dict[str, Any]) -> tuple[bool, list[str], list[str]]:
    matched, gaps = [], []
    for field, rule in constraints.items():
        if field == "environment":
            gaps.append("environment")
            continue
        if field == "service_temperature":
            gaps.append("service_temperature")
            continue
        value, unit = _value(candidate, field)
        if value is None:
            gaps.append(field)
            continue
        op, target = next(iter(rule.items()))
        if not satisfies(value, op, target):
            return False, matched, gaps
        matched.append(field)
    return len(gaps) == 0, matched, gaps

def select(candidates: Iterable[dict[str, Any]], query: dict[str, Any]) -> dict[str, Any]:
    excluded_ids = set(query.get("exclusions", []))
    eligible, excluded, gaps = [], [], []
    for c in candidates:
        cid = c.get("id")
        if cid in excluded_ids:
            excluded.append(cid)
            continue
        ok, matched, missing = hard_filter(c, query.get("constraints", {}))
        if missing:
            gaps.extend(missing)
        if ok:
            eligible.append({
                "material_id": cid,
                "designation": c.get("designation"),
                "eligibility": "screening_eligible",
                "matched_constraints": matched,
                "warnings": ["candidate_reference data only"]
            })
    if gaps:
        return {
            "status": "evidence_required",
            "candidates": eligible,
            "excluded": excluded,
            "evidence_gaps": sorted(set(gaps))
        }
    return {
        "status": "screening_match" if eligible else "no_match",
        "candidates": sorted(eligible, key=lambda x: x["material_id"]),
        "excluded": excluded,
        "evidence_gaps": []
    }
