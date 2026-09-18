"""Project isolated legacy beam analyses to the FrameWebforJS case contract."""

from __future__ import annotations

import math
import re
from collections.abc import Mapping, Sequence
from itertools import pairwise
from typing import Any, TypedDict

from .diagnostics import InputValidationError
from .file_io import _read_json_model, result_to_jsonable
from .legacy_beam import select_case
from .model import FemModel

MAX_LEGACY_CASES = 256


class LegacyCaseResult(TypedDict):
    """One load case in the historical FrameWebforJS response shape."""

    disg: dict[str, dict[str, float]]
    reac: dict[str, dict[str, float]]
    fsec: dict[str, dict[str, dict[str, float]]]
    shell_fsec: dict[str, Any]
    size: int


_DISPLACEMENT_COMPONENTS = ("dx", "dy", "dz", "rx", "ry", "rz")
_REACTION_COMPONENTS = ("tx", "ty", "tz", "mx", "my", "mz")
_MODERN_REACTION_COMPONENTS = ("fx", "fy", "fz", "mx", "my", "mz")
_GENERATED_LABEL = re.compile(r"^(?P<member>\d+)(?P<kind>[nl])(?P<index>\d+)$")


def solve_legacy_cases(data: dict[str, Any]) -> dict[str, LegacyCaseResult]:
    """Solve every legacy beam load case independently and project atomically."""
    _validate_legacy_beam_input(data)
    cases: dict[str, LegacyCaseResult] = {}
    for original_case_id, case in data["load"].items():
        case_id = str(original_case_id)
        try:
            selected = select_case(data, case_id)
            model = FemModel()
            model.read_json_model(_read_json_model(selected))
            result = result_to_jsonable(model.run())
            cases[case_id] = _project_case(data, model, result, _case_rate(case))
        except Exception as error:
            contextual_error = _with_case_context(error, case_id)
            if contextual_error is error:
                raise
            raise contextual_error from error
    return cases


def _validate_legacy_beam_input(data: dict[str, Any]) -> None:
    if not isinstance(data, dict):
        raise InputValidationError("Legacy cases input must be a JSON object")
    if "nodes" in data or not isinstance(data.get("node"), dict) or not data["node"]:
        raise InputValidationError("Legacy cases require non-empty legacy node input")
    if not isinstance(data.get("member"), dict) or not data["member"]:
        raise InputValidationError("Legacy cases require non-empty beam member input")
    if data.get("shell") or data.get("solid"):
        raise InputValidationError("Legacy cases do not support shell or solid input")
    load_cases = data.get("load")
    if not isinstance(load_cases, dict) or not load_cases:
        raise InputValidationError("Legacy cases require at least one load case")
    if len(load_cases) > MAX_LEGACY_CASES:
        raise InputValidationError(
            f"Legacy cases support at most {MAX_LEGACY_CASES} load cases"
        )
    if any(not isinstance(case, Mapping) for case in load_cases.values()):
        raise InputValidationError("Every legacy load case must be an object")


def _with_case_context(error: Exception, case_id: str) -> Exception:
    details = getattr(error, "details", None)
    if isinstance(details, dict):
        error.details = {**details, "case_id": case_id}
        return error
    if isinstance(error, (KeyError, TypeError, ValueError)):
        return InputValidationError(
            f"Legacy load case {case_id}: {error}", case_id=case_id
        )
    error.add_note(f"Legacy load case: {case_id}")
    return error


def _case_rate(case: Mapping[str, Any]) -> float:
    try:
        rate = float(case.get("rate", 1.0))
    except (TypeError, ValueError):
        return 1.0
    return rate if math.isfinite(rate) else 1.0


def _project_case(
    data: dict[str, Any],
    model: FemModel,
    result: dict[str, Any],
    rate: float,
) -> LegacyCaseResult:
    return {
        "disg": _project_displacements(data, model, result, rate),
        "reac": _project_reactions(data, model, result, rate),
        "fsec": _project_member_forces(data, model, result, rate),
        "shell_fsec": {},
        "size": len(model.mesh.nodes),
    }


def _project_displacements(
    data: dict[str, Any],
    model: FemModel,
    result: dict[str, Any],
    rate: float,
) -> dict[str, dict[str, float]]:
    source = _mapping(result, "node_displacements")
    labels = [(int(node), str(node)) for node in sorted(data["node"], key=int)]
    generated = [
        (int(node), str(label))
        for node, label in model.node_labels.items()
        if label is not None
    ]
    labels.extend(sorted(generated, key=lambda item: _generated_label_key(item[1])))
    return {
        label: _scaled_mapping(
            _mapping_item(source, node), _DISPLACEMENT_COMPONENTS, rate
        )
        for node, label in labels
    }


def _generated_label_key(label: str) -> tuple[int, int, int, str]:
    match = _GENERATED_LABEL.fullmatch(label)
    if match is None:
        return (2, 0, 0, label)
    kind_order = 0 if match.group("kind") == "n" else 1
    return (kind_order, int(match.group("member")), int(match.group("index")), label)


def _project_reactions(
    data: dict[str, Any],
    model: FemModel,
    result: dict[str, Any],
    rate: float,
) -> dict[str, dict[str, float]]:
    auxiliary = set(getattr(model.boundary, "auxiliary_restraint_nodes", set()))
    supports = set(model.boundary.restraints) - auxiliary
    supports.update(getattr(model.boundary, "spring_supports", {}))
    supports.update(getattr(model.boundary, "nonlinear_spring_supports", {}))
    source = _mapping(result, "reaction_forces")
    reactions: dict[str, dict[str, float]] = {}
    for node in sorted(supports):
        modern = _optional_mapping_item(source, node)
        values = {
            legacy: float(modern.get(current, 0.0)) * rate
            for legacy, current in zip(
                _REACTION_COMPONENTS, _MODERN_REACTION_COMPONENTS, strict=True
            )
        }
        if data.get("dimension") == 2:
            values.update(tz=0.0, mx=0.0, my=0.0)
        reactions[str(node)] = values
    return reactions


def _project_member_forces(
    data: dict[str, Any],
    model: FemModel,
    result: dict[str, Any],
    rate: float,
) -> dict[str, dict[str, dict[str, float]]]:
    stresses = _mapping(result, "element_stresses")
    projected: dict[str, dict[str, dict[str, float]]] = {}
    for member_id in sorted(data["member"], key=int):
        boundaries = _member_boundaries(data, member_id)
        elements = _member_elements(model, member_id)
        tolerance = 1e-8 * max(1.0, boundaries[-1])
        segments: dict[str, dict[str, float]] = {}
        for index, (start, end) in enumerate(pairwise(boundaries), 1):
            contained = [
                element
                for element in elements
                if element[0] >= start - tolerance and element[1] <= end + tolerance
            ]
            if not contained:
                raise InputValidationError(
                    f"Member {member_id} interval P{index} has no solved beam element"
                )
            first = _mapping_item(stresses, contained[0][2])
            last = _mapping_item(stresses, contained[-1][2])
            segments[f"P{index}"] = _project_segment(first, last, end - start, rate)
        projected[str(member_id)] = segments
    return projected


def _member_boundaries(data: dict[str, Any], member_id: str) -> list[float]:
    member = _mapping_item(data["member"], member_id)
    start = _node_coordinates(data, member["ni"])
    end = _node_coordinates(data, member["nj"])
    length = math.dist(start, end)
    if length <= 0:
        raise InputValidationError(f"Member {member_id} has zero length")
    points = [0.0, length]
    points.extend(_notice_boundaries(data, member_id, length))
    points.extend(_rigid_boundaries(data, member_id, length))
    tolerance = 1e-10 * max(1.0, length)
    return _coalesce(points, tolerance)


def _notice_boundaries(
    data: dict[str, Any], member_id: str, length: float
) -> list[float]:
    return [
        float(point)
        for notice in data.get("notice_points", [])
        if str(notice.get("m")) == str(member_id)
        for point in notice.get("Points", [])
        if 0 < float(point) < length
    ]


def _rigid_boundaries(
    data: dict[str, Any], member_id: str, length: float
) -> list[float]:
    points: list[float] = []
    for zone in data.get("rigid", []):
        if str(zone.get("m")) != str(member_id):
            continue
        points.extend(
            (float(zone.get("Ilength", 0)), length - float(zone.get("Jlength", 0)))
        )
    return [point for point in points if 0 < point < length]


def _coalesce(points: list[float], tolerance: float) -> list[float]:
    unique: list[float] = []
    for point in sorted(points):
        if not unique or point - unique[-1] > tolerance:
            unique.append(point)
    return unique


def _node_coordinates(data: dict[str, Any], node_id: Any) -> tuple[float, float, float]:
    node = _mapping_item(data["node"], node_id)
    return (float(node["x"]), float(node["y"]), float(node["z"]))


def _member_elements(model: FemModel, member_id: str) -> list[tuple[float, float, int]]:
    member_number = int(member_id)
    elements = [
        (
            float(properties.get("member_start", 0.0)),
            float(properties.get("member_end", 0.0)),
            int(element_id),
        )
        for element_id, properties in model.mesh.elements.items()
        if int(properties.get("original_id", element_id)) == member_number
    ]
    return sorted(elements)


def _project_segment(
    first: Mapping[str, Any],
    last: Mapping[str, Any],
    length: float,
    rate: float,
) -> dict[str, float]:
    i_end = _six_values(first.get("i_end"), "i_end")
    j_end = _six_values(last.get("j_end"), "j_end")
    return {
        "fxi": -i_end[0] * rate,
        "fyi": i_end[1] * rate,
        "fzi": i_end[2] * rate,
        "mxi": -i_end[3] * rate,
        "myi": -i_end[4] * rate,
        "mzi": i_end[5] * rate,
        "fxj": j_end[0] * rate,
        "fyj": -j_end[1] * rate,
        "fzj": -j_end[2] * rate,
        "mxj": j_end[3] * rate,
        "myj": j_end[4] * rate,
        "mzj": -j_end[5] * rate,
        "L": length,
    }


def _six_values(value: Any, label: str) -> tuple[float, ...]:
    if (
        not isinstance(value, Sequence)
        or isinstance(value, (str, bytes))
        or len(value) != 6
    ):
        raise InputValidationError(f"Beam result {label} must contain six values")
    return tuple(float(component) for component in value)


def _scaled_mapping(
    values: Mapping[str, Any], components: tuple[str, ...], rate: float
) -> dict[str, float]:
    return {
        component: float(values.get(component, 0.0)) * rate for component in components
    }


def _mapping(container: Mapping[str, Any], key: str) -> Mapping[Any, Any]:
    value = container.get(key)
    if not isinstance(value, Mapping):
        raise InputValidationError(f"Analysis result is missing {key}")
    return value


def _mapping_item(container: Mapping[Any, Any], key: Any) -> Mapping[str, Any]:
    value = container.get(key, container.get(str(key)))
    if not isinstance(value, Mapping):
        raise InputValidationError(f"Missing result entry for {key}")
    return value


def _optional_mapping_item(container: Mapping[Any, Any], key: Any) -> Mapping[str, Any]:
    value = container.get(key, container.get(str(key), {}))
    if not isinstance(value, Mapping):
        raise InputValidationError(f"Invalid result entry for {key}")
    return value
