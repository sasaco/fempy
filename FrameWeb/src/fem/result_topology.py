"""Deterministic public topology for canonical analysis results.

Solver node and element identifiers are intentionally kept out of the wire
shape.  The helpers in this module rebuild the private mapping from geometry
when a solved case is projected.
"""

from __future__ import annotations

from collections.abc import Mapping, Sequence
import math
from typing import Any

import numpy as np

from .elements import ShellElement


_BAR_TYPES = {
    "bar",
    "beam",
    "nonlinear_bar",
    "BarElement",
    "BeamElement",
    "TrussElement",
}
_SHELL_TYPES = {"shell", "TriElement1", "QuadElement1", "ShellElement"}
_SOLID_TYPES = {
    "tetra",
    "tet",
    "wedge",
    "hexa",
    "hex",
    "tetra2",
    "wedge2",
    "hexa2",
    "TetraElement1",
    "WedgeElement1",
    "HexaElement1",
    "TetraElement2",
    "WedgeElement2",
    "HexaElement2",
    "TetraElement",
    "WedgeElement",
    "HexaElement",
}
_SOLID_PUBLIC_TYPES = {
    "tetra": "tetra4",
    "tet": "tetra4",
    "TetraElement1": "tetra4",
    "TetraElement": "tetra4",
    "wedge": "wedge6",
    "WedgeElement1": "wedge6",
    "WedgeElement": "wedge6",
    "hexa": "hexa8",
    "hex": "hexa8",
    "HexaElement1": "hexa8",
    "HexaElement": "hexa8",
    "tetra2": "tetra10",
    "TetraElement2": "tetra10",
    "wedge2": "wedge15",
    "WedgeElement2": "wedge15",
    "hexa2": "hexa20",
    "HexaElement2": "hexa20",
}


def build_result_topology(source_data: Mapping[str, Any], model: Any) -> dict[str, Any]:
    """Build the contract topology from original public identity and a solved model.

    Legacy preprocessing already inserts the union of every case's member-load
    boundaries into each isolated mesh.  Consequently the member stations read
    here are request-wide even though ``model`` represents only one case.
    """
    source_nodes = _source_nodes(source_data)
    topology_nodes: list[dict[str, Any]] = []
    solver_to_public: dict[int, str] = {}
    used_public_ids: set[str] = set()

    for source_id, value in source_nodes.items():
        public_id = str(source_id)
        if not public_id or public_id in used_public_ids:
            raise ValueError(f"Duplicate or empty public node ID: {public_id!r}")
        solver_id = _solver_node_id(model, source_id)
        coordinates = _coordinates(value, model.mesh.nodes[solver_id])
        topology_nodes.append(
            {
                "node_id": public_id,
                "coordinates": _vector(coordinates),
                "source_node_id": public_id,
                "generated": False,
            }
        )
        solver_to_public[solver_id] = public_id
        used_public_ids.add(public_id)

    members: list[dict[str, Any]] = []
    for member_id, node_i, node_j in _member_definitions(source_data, model):
        member_elements = _member_elements(model, member_id)
        if not member_elements:
            raise ValueError(f"Public member {member_id} has no solved beam elements")
        node_i_solver = _solver_node_id(model, node_i)
        node_j_solver = _solver_node_id(model, node_j)
        start = np.asarray(model.mesh.nodes[node_i_solver], dtype=float)
        end = np.asarray(model.mesh.nodes[node_j_solver], dtype=float)
        length = float(np.linalg.norm(end - start))
        if not math.isfinite(length) or length <= 0:
            raise ValueError(f"Public member {member_id} has invalid length")
        tolerance = 1e-10 * max(1.0, length)
        station_nodes = _member_station_nodes(
            model, member_elements, start, end, length, tolerance
        )
        positions = _coalesce([0.0, length, *station_nodes], tolerance)
        positions[0], positions[-1] = 0.0, length
        stations = [
            {"station_id": f"S{index}", "position": float(position)}
            for index, position in enumerate(positions)
        ]
        for index, position in enumerate(positions[1:-1], 1):
            solver_id = station_nodes[_nearest_key(station_nodes, position)]
            if solver_id in solver_to_public:
                continue
            public_id = f"generated:{member_id}:S{index}"
            if public_id in used_public_ids:
                raise ValueError(f"Generated node ID collides with public input: {public_id}")
            topology_nodes.append(
                {
                    "node_id": public_id,
                    "coordinates": _vector(model.mesh.nodes[solver_id]),
                    "source_node_id": None,
                    "generated": True,
                }
            )
            solver_to_public[solver_id] = public_id
            used_public_ids.add(public_id)

        first_element = model.elements[member_elements[0]]
        basis = np.asarray(first_element.transformation_matrix, dtype=float)
        members.append(
            {
                "member_id": str(member_id),
                "node_i": str(node_i),
                "node_j": str(node_j),
                "local_frame": _coordinate_frame(start, basis),
                "stations": stations,
            }
        )

    unmapped = set(model.mesh.nodes) - set(solver_to_public)
    if unmapped:
        raise ValueError(
            "Solved topology contains nodes without public identity: "
            + ", ".join(map(str, sorted(unmapped)))
        )

    shell_elements = []
    for public_id, solver_id, source_type in _element_definitions(
        source_data, model, "shell"
    ):
        element = model.elements[solver_id]
        if not isinstance(element, ShellElement):
            raise ValueError(f"Public shell {public_id} did not resolve to a shell element")
        _, basis = element._local_frame()
        origin = model.mesh.nodes[element.node_ids[0]]
        shell_elements.append(
            {
                "element_id": public_id,
                "element_type": "triangle3" if len(element.node_ids) == 3 else "quadrilateral4",
                "node_ids": [solver_to_public[node] for node in element.node_ids],
                "local_frame": _coordinate_frame(origin, basis),
                "result_locations": [
                    {"location_id": "element_average", "kind": "element_average"}
                ],
            }
        )

    solid_elements = []
    for public_id, solver_id, source_type in _element_definitions(
        source_data, model, "solid"
    ):
        element = model.elements[solver_id]
        public_type = _solid_public_type(source_type, model.mesh.elements[solver_id], element)
        points = _solid_result_points(element)
        solid_elements.append(
            {
                "element_id": public_id,
                "element_type": public_type,
                "node_ids": [solver_to_public[node] for node in element.node_ids],
                "coordinate_frame": "global",
                "result_locations": [
                    {
                        "location_id": f"GP{index}",
                        "natural_coordinates": {
                            "xi": float(point[0]),
                            "eta": float(point[1]),
                            "zeta": float(point[2]),
                        },
                    }
                    for index, point in enumerate(points)
                ],
            }
        )

    return {
        "nodes": topology_nodes,
        "members": members,
        "shell_elements": shell_elements,
        "solid_elements": solid_elements,
    }


def map_solver_nodes_to_public(
    model: Any, topology: Mapping[str, Any]
) -> dict[int, str]:
    """Reconstruct the private solved-node to public-node mapping."""
    nodes = topology.get("nodes")
    if not isinstance(nodes, Sequence):
        raise ValueError("Result topology is missing nodes")
    mapping: dict[int, str] = {}
    remaining = set(model.mesh.nodes)
    generated_rows = []
    for row in nodes:
        source_id = row.get("source_node_id")
        if source_id is None:
            generated_rows.append(row)
            continue
        solver_id = _solver_node_id(model, source_id)
        if not _same_coordinates(model.mesh.nodes[solver_id], row["coordinates"]):
            raise ValueError(f"Public node {source_id} coordinates changed between cases")
        mapping[solver_id] = str(row["node_id"])
        remaining.discard(solver_id)
    for row in generated_rows:
        matches = [
            node
            for node in remaining
            if _same_coordinates(model.mesh.nodes[node], row["coordinates"])
        ]
        if len(matches) != 1:
            raise ValueError(
                f"Generated public node {row.get('node_id')} maps to {len(matches)} solved nodes"
            )
        solver_id = matches[0]
        mapping[solver_id] = str(row["node_id"])
        remaining.remove(solver_id)
    if remaining or len(mapping) != len(nodes):
        raise ValueError("Solved nodes do not exactly match the canonical topology")
    return mapping


def public_support_node_ids(model: Any, topology: Mapping[str, Any]) -> list[str]:
    """Return user-defined support nodes in public topology order."""
    solver_to_public = map_solver_nodes_to_public(model, topology)
    auxiliary = set(getattr(model.boundary, "auxiliary_restraint_nodes", set()))
    supports = {
        node
        for node, restraint in model.boundary.restraints.items()
        if node not in auxiliary and any(restraint.dof_restraints)
    }
    supports.update(getattr(model.boundary, "spring_supports", {}))
    supports.update(getattr(model.boundary, "nonlinear_spring_supports", {}))
    public = {solver_to_public[node] for node in supports}
    return [row["node_id"] for row in topology["nodes"] if row["node_id"] in public]


def public_element_id_map(
    model: Any, topology: Mapping[str, Any], kind: str
) -> dict[str, int]:
    """Map canonical shell/solid IDs to the current case's solver elements."""
    field = {"shell": "shell_elements", "solid": "solid_elements"}[kind]
    candidates = _model_elements(model, kind)
    result: dict[str, int] = {}
    for public in topology[field]:
        public_id = str(public["element_id"])
        exact = [
            solver_id
            for solver_id in candidates
            if str(_stored_public_element_id(model.mesh.elements[solver_id], solver_id, kind))
            == public_id
        ]
        if len(exact) != 1:
            raise ValueError(
                f"Canonical {kind} element {public_id} maps to {len(exact)} solved elements"
            )
        result[public_id] = exact[0]
    if set(result.values()) != set(candidates):
        raise ValueError(f"Solved {kind} elements do not exactly match canonical topology")
    return result


def member_element_ids(model: Any, member_id: str) -> list[int]:
    """Return one public member's solved segments in station order."""
    return _member_elements(model, member_id)


def _source_nodes(source_data: Mapping[str, Any]) -> Mapping[Any, Any]:
    nodes = source_data.get("node", source_data.get("nodes"))
    if not isinstance(nodes, Mapping) or not nodes:
        raise ValueError("Canonical topology requires non-empty source nodes")
    return nodes


def _solver_node_id(model: Any, public_id: Any) -> int:
    try:
        candidate = int(public_id)
    except (TypeError, ValueError) as error:
        raise ValueError(f"Public node ID is not representable by the current input: {public_id}") from error
    if candidate not in model.mesh.nodes:
        raise ValueError(f"Public node {public_id} is missing from the solved mesh")
    return candidate


def _coordinates(value: Any, fallback: Any) -> np.ndarray:
    if isinstance(value, Mapping):
        coordinates = [value[name] for name in ("x", "y", "z")]
    elif isinstance(value, Sequence) and not isinstance(value, (str, bytes)):
        coordinates = value
    else:
        coordinates = fallback
    array = np.asarray(coordinates, dtype=float)
    if array.shape != (3,) or not np.isfinite(array).all():
        raise ValueError("Public node coordinates must contain three finite numbers")
    return array


def _vector(value: Any) -> dict[str, float]:
    array = np.asarray(value, dtype=float)
    if array.shape != (3,) or not np.isfinite(array).all():
        raise ValueError("Coordinate vector must contain three finite numbers")
    return {name: float(component) for name, component in zip(("x", "y", "z"), array, strict=True)}


def _coordinate_frame(origin: Any, basis: Any) -> dict[str, Any]:
    basis = np.asarray(basis, dtype=float)
    if basis.shape != (3, 3) or not np.isfinite(basis).all():
        raise ValueError("Local frame basis must be a finite 3x3 matrix")
    gram = basis @ basis.T
    if not np.allclose(gram, np.eye(3), rtol=0.0, atol=1e-10):
        raise ValueError("Local frame axes must be orthonormal")
    if np.linalg.det(basis) <= 0:
        raise ValueError("Local frame must be right-handed")
    return {
        "origin": _vector(origin),
        "x_axis": _vector(basis[0]),
        "y_axis": _vector(basis[1]),
        "z_axis": _vector(basis[2]),
    }


def _member_definitions(source_data: Mapping[str, Any], model: Any):
    legacy = source_data.get("member")
    if isinstance(legacy, Mapping):
        for member_id, member in legacy.items():
            yield str(member_id), str(member["ni"]), str(member["nj"])
        return
    elements = source_data.get("elements")
    if isinstance(elements, Mapping):
        for element_id, properties in elements.items():
            if isinstance(properties, Mapping) and properties.get("type") in _BAR_TYPES:
                yield str(element_id), str(properties["nodes"][0]), str(properties["nodes"][1])
        return
    seen: set[str] = set()
    for solver_id, properties in model.mesh.elements.items():
        if properties.get("type") in _BAR_TYPES:
            member_id = str(properties.get("original_id", solver_id))
            if member_id in seen:
                continue
            seen.add(member_id)
            nodes = properties.get("member_nodes", properties["nodes"])
            yield member_id, str(nodes[0]), str(nodes[1])


def _member_elements(model: Any, member_id: str) -> list[int]:
    result = []
    for solver_id, properties in model.mesh.elements.items():
        if properties.get("type") not in _BAR_TYPES:
            continue
        public_id = str(properties.get("original_id", solver_id))
        if public_id == str(member_id):
            result.append(solver_id)
    return sorted(
        result,
        key=lambda solver_id: (
            float(model.mesh.elements[solver_id].get("member_start", 0.0)),
            float(model.mesh.elements[solver_id].get("member_end", math.inf)),
            solver_id,
        ),
    )


def _member_station_nodes(model, element_ids, start, end, length, tolerance):
    axis = (end - start) / length
    station_nodes: dict[float, int] = {}
    for element_id in element_ids:
        properties = model.mesh.elements[element_id]
        for node in properties["nodes"]:
            coordinate = np.asarray(model.mesh.nodes[node], dtype=float)
            position = float((coordinate - start) @ axis)
            transverse = np.linalg.norm(coordinate - (start + position * axis))
            if transverse > tolerance or position < -tolerance or position > length + tolerance:
                raise ValueError(f"Solved member segment {element_id} is not on its public member axis")
            position = min(length, max(0.0, position))
            existing = next((key for key in station_nodes if abs(key - position) <= tolerance), None)
            if existing is None:
                station_nodes[position] = node
            elif station_nodes[existing] != node:
                raise ValueError(f"Member station {position} resolves to multiple solved nodes")
    return station_nodes


def _nearest_key(values: Mapping[float, Any], target: float) -> float:
    return min(values, key=lambda value: abs(value - target))


def _coalesce(values, tolerance):
    result = []
    for value in sorted(map(float, values)):
        if not result or value - result[-1] > tolerance:
            result.append(value)
    return result


def _element_definitions(source_data, model, kind):
    legacy_name = "shell" if kind == "shell" else "solid"
    legacy = source_data.get(legacy_name)
    if isinstance(legacy, Mapping):
        for public_id, source in legacy.items():
            matches = [
                solver_id
                for solver_id in _model_elements(model, kind)
                if str(_stored_public_element_id(model.mesh.elements[solver_id], solver_id, kind))
                == str(public_id)
            ]
            if len(matches) != 1:
                raise ValueError(f"Public {kind} {public_id} maps to {len(matches)} solved elements")
            yield str(public_id), matches[0], source.get("type")
        return
    source_elements = source_data.get("elements")
    if isinstance(source_elements, Mapping):
        allowed = _SHELL_TYPES if kind == "shell" else _SOLID_TYPES
        for public_id, source in source_elements.items():
            if not isinstance(source, Mapping) or source.get("type") not in allowed:
                continue
            solver_id = int(public_id)
            if solver_id not in model.elements:
                raise ValueError(f"Public {kind} {public_id} is missing from solved elements")
            yield str(public_id), solver_id, source.get("type")
        return
    for solver_id in _model_elements(model, kind):
        properties = model.mesh.elements[solver_id]
        public_id = _stored_public_element_id(properties, solver_id, kind)
        yield str(public_id), solver_id, properties.get("type")


def _model_elements(model, kind):
    if kind == "shell":
        return [solver_id for solver_id, element in model.elements.items() if isinstance(element, ShellElement)]
    return [
        solver_id
        for solver_id, properties in model.mesh.elements.items()
        if properties.get("type") in _SOLID_TYPES
    ]


def _stored_public_element_id(properties, solver_id, kind):
    return properties.get("shell_id" if kind == "shell" else "solid_id", solver_id)


def _solid_public_type(source_type, properties, element):
    candidates = [source_type, properties.get("type"), getattr(element, "kind", None), element.get_name()]
    for candidate in candidates:
        if candidate in _SOLID_PUBLIC_TYPES:
            return _SOLID_PUBLIC_TYPES[candidate]
    raise ValueError(f"Unsupported canonical solid element type: {candidates[0]}")


def _solid_result_points(element):
    if len(element.node_ids) == 4:
        return np.asarray([[0.25, 0.25, 0.25]], dtype=float)
    points, _ = element.get_gauss_points()
    points = np.asarray(points, dtype=float)
    if points.ndim != 2 or points.shape[1] != 3 or not np.isfinite(points).all():
        raise ValueError("Solid result locations must be finite natural coordinates")
    return points


def _same_coordinates(actual, declared):
    expected = np.array([declared[name] for name in ("x", "y", "z")], dtype=float)
    actual = np.asarray(actual, dtype=float)
    scale = max(1.0, float(np.max(np.abs(np.r_[actual, expected]))))
    return np.linalg.norm(actual - expected) <= 1e-10 * scale
