"""Remove only unsupported transverse modes of generated collinear beam points.

Original structural joints remain unknowns, so actual mechanisms are rejected.
Interpolated transverse output is explicitly labelled; it is not a solved DOF.
"""

import numpy as np
from scipy.sparse import eye

from .elements.loaded_bar_element import LoadedBarElement


def generated_point_basis(solver, mesh, boundary, elements, force, absent):
    incident = {node: [] for node in mesh.nodes}
    for key, e in elements.items():
        for node in e.node_ids:
            incident[node].append((key, e))
    prescribed, springs = solver._get_boundary_dofs(
        boundary, len(force), solver.layout.stride
    )
    transform = eye(len(force), format="lil")
    removed = set(np.flatnonzero(absent))
    records = {}
    for node, attached in incident.items():
        if len(attached) != 2 or not all(
            isinstance(e, LoadedBarElement) for _, e in attached
        ):
            continue
        props = [mesh.elements[key] for key, _ in attached]
        endpoints = props[0].get("member_nodes")
        if (
            not endpoints
            or node in endpoints
            or props[1].get("member_nodes") != endpoints
        ):
            continue
        if props[0].get("original_id") != props[1].get("original_id"):
            continue
        start = solver.layout.node_offsets[node]
        rotation = attached[0][1].transformation_matrix
        modes = []
        for mode, inertia in ((1, "Iz"), (2, "Iy")):
            if not all(
                getattr(e.bar_param, inertia) == 0 and e.foundation[mode] == 0
                for _, e in attached
            ):
                continue
            direction = rotation[mode]
            if any(
                start + i in prescribed or start + i in springs
                for i in range(3)
                if abs(direction[i]) > 1e-14
            ):
                continue
            if abs(np.dot(direction, force[start : start + 3])) > 1e-12 * max(
                1.0, np.linalg.norm(force[start : start + 3])
            ):
                raise ValueError(
                    f"Generated node {node} has an unsupported transverse load"
                )
            modes.append(mode)
        if not modes:
            continue
        transform[start : start + 3, start : start + 3] = rotation.T
        removed.update(start + mode for mode in modes)
        a, b = [mesh.nodes[n] for n in endpoints]
        ratio = np.dot(mesh.nodes[node] - a, b - a) / np.dot(b - a, b - a)
        records[node] = dict(
            directions=rotation[modes].tolist(),
            source_nodes=endpoints,
            weights=[1 - ratio, ratio],
        )
    if not records:
        return None, {}
    keep = [i for i in range(len(force)) if i not in removed]
    return transform.tocsr()[:, keep], records


def interpolate_output(solver, displacement, correction, records):
    for node, record in records.items():
        base = solver.layout.node_offsets[node]
        target = sum(
            w
            * displacement[
                solver.layout.node_offsets[n] : solver.layout.node_offsets[n] + 3
            ]
            for n, w in zip(record["source_nodes"], record["weights"])
        )
        for direction in np.asarray(record["directions"]):
            displacement[base : base + 3] += direction * np.dot(
                direction, target - displacement[base : base + 3]
            )
            correction[base : base + 3] -= direction * np.dot(
                direction, correction[base : base + 3]
            )
