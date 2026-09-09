"""Machine-readable public capability registry and analysis preflight checks."""
from __future__ import annotations

from collections import defaultdict
from copy import deepcopy
from functools import lru_cache
from importlib.resources import files
import json
from typing import Any, Mapping


_VALID_STATUSES = frozenset({"verified", "implemented", "unsupported"})


class UnsupportedCapabilityError(ValueError):
    """A model requests a capability declared unsupported by the registry."""

    error_code = "unsupported_analysis"
    error_category = "unsupported"
    http_status = 422

    def __init__(self, analysis_type: str, issues: list[dict[str, Any]]):
        self.analysis_type = analysis_type
        self.issues = tuple(deepcopy(issues))
        self.details = {
            "analysis_type": analysis_type,
            "issues": deepcopy(issues),
        }
        grouped: dict[tuple[str, str, str, str], list[int]] = defaultdict(list)
        for issue in issues:
            key = (
                issue["kind"], issue["element_type"], issue["feature"], issue["reason"]
            )
            grouped[key].append(issue["element_id"])
        details = []
        for (kind, element_type, feature, reason), element_ids in sorted(grouped.items()):
            details.append(
                f"element IDs {sorted(element_ids)} ({element_type}) "
                f"{kind} '{feature}': {reason}"
            )
        super().__init__(
            f"Unsupported capability for analysis '{analysis_type}': " + "; ".join(details)
        )


def _implementation_name(element: Any) -> str:
    cls = type(element)
    return f"{cls.__module__}.{cls.__qualname__}"


def _validate_entry_status(entry: Mapping[str, Any], location: str) -> None:
    if entry.get("status") not in _VALID_STATUSES:
        raise RuntimeError(f"Invalid capability status at {location}: {entry.get('status')!r}")


def _validate_registry(registry: dict[str, Any]) -> None:
    if registry.get("schema_version") != 1:
        raise RuntimeError("Unsupported capability registry schema_version")
    if set(registry.get("status_definitions", {})) != _VALID_STATUSES:
        raise RuntimeError("Capability registry must define every supported status")

    analysis_types = set(registry.get("analysis_types", {}))
    load_types = set(registry.get("load_types", {}))
    result_types = set(registry.get("result_types", {}))
    aliases: dict[str, str] = {}
    for canonical, capability in registry.get("elements", {}).items():
        if canonical in aliases:
            raise RuntimeError(f"Duplicate canonical element type: {canonical}")
        aliases[canonical] = canonical
        for alias in capability.get("aliases", []):
            if alias in aliases:
                raise RuntimeError(f"Duplicate element alias: {alias}")
            aliases[alias] = canonical
        if set(capability.get("analyses", {})) != analysis_types:
            raise RuntimeError(f"Incomplete analysis matrix for element {canonical}")
        if set(capability.get("loads", {})) != load_types:
            raise RuntimeError(f"Incomplete load matrix for element {canonical}")
        if set(capability.get("results", {})) != result_types:
            raise RuntimeError(f"Incomplete result matrix for element {canonical}")
        if not capability.get("implementations") or not capability.get("node_counts"):
            raise RuntimeError(f"Element {canonical} lacks implementation or node-count metadata")
        for analysis, entry in capability["analyses"].items():
            _validate_entry_status(entry, f"elements.{canonical}.analyses.{analysis}")
        _validate_entry_status(capability["mass_matrix"], f"elements.{canonical}.mass_matrix")
        for feature, entry in capability["loads"].items():
            _validate_entry_status(entry, f"elements.{canonical}.loads.{feature}")
        for feature, entry in capability["results"].items():
            _validate_entry_status(entry, f"elements.{canonical}.results.{feature}")


@lru_cache(maxsize=1)
def _registry() -> dict[str, Any]:
    resource = files("fem").joinpath("capabilities.json")
    registry = json.loads(resource.read_text(encoding="utf-8"))
    _validate_registry(registry)
    return registry


def get_capability_registry() -> dict[str, Any]:
    """Return a copy of the versioned registry safe for callers to modify."""
    return deepcopy(_registry())


@lru_cache(maxsize=1)
def _aliases() -> dict[str, str]:
    aliases: dict[str, str] = {}
    for canonical, capability in _registry()["elements"].items():
        aliases[canonical] = canonical
        aliases.update({alias: canonical for alias in capability["aliases"]})
    return aliases


def canonical_element_type(element_type: str) -> str:
    """Resolve a public or legacy element name to its registry key."""
    try:
        return _aliases()[element_type]
    except (KeyError, TypeError) as error:
        raise ValueError(f"Unknown element type: {element_type}") from error


def get_element_capability(element_type: str) -> dict[str, Any]:
    """Return one element capability entry for a public or legacy name."""
    canonical = canonical_element_type(element_type)
    result = deepcopy(_registry()["elements"][canonical])
    result["canonical_type"] = canonical
    return result


def _unsupported_issue(
    *, element_id: int, element_type: str, kind: str, feature: str,
    entry: Mapping[str, Any], default_reason: str,
) -> dict[str, Any]:
    return {
        "element_id": element_id,
        "element_type": element_type,
        "kind": kind,
        "feature": feature,
        "reason": entry.get("reason", default_reason),
    }


def validate_analysis_capabilities(
    mesh_elements: Mapping[int, Mapping[str, Any]],
    element_instances: Mapping[int, Any],
    boundary: Any,
    analysis_type: str,
) -> None:
    """Reject registry-declared unsupported combinations before solver assembly.

    The implementation-class check is intentional: it prevents verification of a
    same-named alternate element class from being mistaken for the class actually
    constructed by the public ``FemModel`` path.
    """
    registry = _registry()
    if analysis_type not in registry["analysis_types"]:
        raise ValueError(f"Unknown analysis type: {analysis_type}")

    issues: list[dict[str, Any]] = []
    canonical_by_id: dict[int, str] = {}
    for element_id, data in mesh_elements.items():
        canonical = canonical_element_type(data.get("type"))
        canonical_by_id[element_id] = canonical
        capability = registry["elements"][canonical]
        if element_id not in element_instances:
            raise RuntimeError(f"Element {element_id} was not constructed before preflight")
        implementation = _implementation_name(element_instances[element_id])
        if implementation not in capability["implementations"]:
            raise RuntimeError(
                f"Capability registry mismatch for element {element_id} ({canonical}): "
                f"constructed {implementation}, expected one of {capability['implementations']}"
            )
        entry = capability["analyses"][analysis_type]
        if entry["status"] == "unsupported":
            issues.append(_unsupported_issue(
                element_id=element_id,
                element_type=canonical,
                kind="analysis",
                feature=analysis_type,
                entry=entry,
                default_reason=f"{analysis_type} is not implemented for {canonical}.",
            ))

    # Surface pressure otherwise reaches BaseElement.get_equivalent_nodal_loads
    # and fails only during load-vector assembly. Reject it in the same preflight.
    pressure_records = [
        (pressure.element_id, "shell_pressure")
        for pressure in getattr(boundary, "pressures", [])
    ]
    pressure_records.extend(
        (load.element_id, "shell_pressure")
        for load in getattr(boundary, "distributed_loads", [])
        if load.load_type == "pressure"
    )
    for element_id, feature in pressure_records:
        if element_id not in canonical_by_id:
            continue
        canonical = canonical_by_id[element_id]
        entry = registry["elements"][canonical]["loads"][feature]
        if entry["status"] == "unsupported":
            issues.append(_unsupported_issue(
                element_id=element_id,
                element_type=canonical,
                kind="load",
                feature=feature,
                entry=entry,
                default_reason=f"{feature} is not implemented for {canonical}.",
            ))

    if issues:
        raise UnsupportedCapabilityError(analysis_type, issues)
