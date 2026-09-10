"""High precision linear beam fallback for severely ill-conditioned frames.

Reassemble from element geometry, rather than increasing precision after the
assembled float matrix has already lost the weak stiffness. No regularization.
"""

from decimal import Decimal, localcontext
from math import cos, radians, sin

import numpy as np
from scipy.sparse import eye
from scipy.sparse.csgraph import reverse_cuthill_mckee

from .elements.loaded_bar_element import LoadedBarElement


def dec(value):
    return Decimal.from_float(float(value))


def element_matrix(element, coords, metadata=None):
    delta = [
        dec(b) - dec(a)
        for a, b in zip(coords[element.node_ids[0]], coords[element.node_ids[1]])
    ]
    length = sum(v * v for v in delta).sqrt()
    if metadata and metadata.get("member_nodes"):
        # Generated coordinates are floats. Subtracting two almost coincident
        # points can rotate a short child away from its original straight beam.
        # Reconstruct its interval on the ORIGINAL member at working precision.
        ni, nj = metadata["member_nodes"]
        parent = [dec(b) - dec(a) for a, b in zip(coords[ni], coords[nj])]
        parent_length = sum(v * v for v in parent).sqrt()
        left = (
            Decimal(0) if element.node_ids[0] == ni else dec(metadata["member_start"])
        )
        right = (
            parent_length if element.node_ids[1] == nj else dec(metadata["member_end"])
        )
        length = right - left
        delta = [v * length / parent_length for v in parent]
    x = [v / length for v in delta]
    h = (x[0] * x[0] + x[1] * x[1]).sqrt()
    if h:
        y = [-x[1] / h, x[0] / h, Decimal(0)]
        z = [-x[0] * x[2] / h, -x[1] * x[2] / h, h]
    else:
        y, z = [x[2], Decimal(0), Decimal(0)], [Decimal(0), Decimal(1), Decimal(0)]
    c, s = dec(cos(radians(element.angle))), dec(sin(radians(element.angle)))
    norm = (c * c + s * s).sqrt()
    c, s = c / norm, s / norm
    rotation = [
        x,
        [c * a + s * b for a, b in zip(y, z)],
        [-s * a + c * b for a, b in zip(y, z)],
    ]
    transform = [
        [rotation[i % 3][j % 3] if i // 3 == j // 3 else Decimal(0) for j in range(12)]
        for i in range(12)
    ]
    # Reconstruct foundation modes before rounding can erase weak stiffness.
    local = [[Decimal(0) for _ in range(12)] for _ in range(12)]
    load = [Decimal(0)] * 12
    material, p = element.material.materials[element.material_id], element.bar_param
    modes = [
        (0, [0, 6], dec(material.E) * dec(p.area), False, 1),
        (1, [1, 5, 7, 11], dec(material.E) * dec(p.Iz), True, 1),
        (2, [2, 4, 8, 10], dec(material.E) * dec(p.Iy), True, -1),
        (3, [3, 9], dec(material.G) * dec(p.J), False, 1),
    ]
    if np.any(element.foundation) or element.shear_correction:
        # Releases affect complete element blocks, so take the unreleased BVP.
        from .decimal_bvp import boundary_map
    for mode, indices, rigidity, bending, sign in modes:
        q0, q1 = map(dec, element.line_load[mode])
        if not rigidity:
            continue
        if element.foundation[mode] or bending and element.shear_correction:
            k, mode_load = boundary_map(
                length,
                rigidity,
                dec(element.foundation[mode]),
                bending,
                (q0, q1),
                shear=dec(material.G)
                * dec(p.area)
                * dec(p.kappa_y if mode == 1 else p.kappa_z)
                if bending and element.shear_correction
                else None,
            )
            values = k
        elif not bending:
            a = rigidity / length
            values = [[a, -a], [-a, a]]
            mode_load = [length * (2 * q0 + q1) / 6, length * (q0 + 2 * q1) / 6]
        else:
            phi = Decimal(0)
            if element.shear_correction:
                shear = (
                    dec(material.G)
                    * dec(p.area)
                    * dec(p.kappa_y if mode == 1 else p.kappa_z)
                )
                phi = 12 * rigidity / (shear * length * length)
            a = 12 * rigidity / (length**3 * (1 + phi))
            b = a * length / 2
            c = (4 + phi) * rigidity / (length * (1 + phi))
            d = (2 - phi) * rigidity / (length * (1 + phi))
            values = [[a, b, -a, b], [b, c, -b, d], [-a, -b, a, -b], [b, d, -b, c]]
            mode_load = [
                length * (7 * q0 + 3 * q1) / 20,
                length**2 * (3 * q0 + 2 * q1) / 60,
                length * (3 * q0 + 7 * q1) / 20,
                -(length**2) * (2 * q0 + 3 * q1) / 60,
            ]
        signs = [1, sign, 1, sign] if bending else [1, 1]
        for i, row in enumerate(indices):
            load[row] = mode_load[i] * signs[i]
            for j, col in enumerate(indices):
                local[row][col] = values[i][j] * signs[i] * signs[j]
    thermal = dec(material.E) * dec(p.area) * dec(element.temperature_strain)
    load[0] -= thermal
    load[6] += thermal
    if not any(mode[2] for mode in modes):
        load = [dec(value) for value in element._local_system()[1]]
    for released in element.releases:
        pivot = local[released][released]
        if pivot:
            column = [row[released] for row in local]
            released_load = load[released]
            for i in range(12):
                load[i] -= column[i] * released_load / pivot
                for j in range(12):
                    local[i][j] -= column[i] * column[j] / pivot
        elif load[released]:
            raise ValueError("Unbalanced load on released beam mode")
        load[released] = Decimal(0)
        for i in range(12):
            local[i][released] = local[released][i] = Decimal(0)
    temp = [
        [
            sum(
                local[i][k] * transform[k][j]
                for k in range(12)
                if local[i][k] and transform[k][j]
            )
            for j in range(12)
        ]
        for i in range(12)
    ]
    global_k = [
        [
            sum(
                transform[k][i] * temp[k][j]
                for k in range(12)
                if transform[k][i] and temp[k][j]
            )
            for j in range(12)
        ]
        for i in range(12)
    ]
    return global_k, transform, load


def sparse_solve(rows, rhs, ordering):
    """Symmetric elimination with a precision-scaled rank check."""
    n = len(rhs)
    inverse = {old: new for new, old in enumerate(ordering)}
    a = [{inverse[j]: v for j, v in rows[old].items() if v} for old in ordering]
    b = [rhs[i] for i in ordering]
    history = []
    scales = [abs(row.get(i, Decimal(0))) for i, row in enumerate(a)]
    for i in range(n):
        pivot = a[i].get(i, Decimal(0))
        if pivot <= scales[i] * Decimal("1e-50"):
            raise ValueError(
                "Singular stiffness matrix: high-precision rank deficiency"
            )
        neighbors = {j: v for j, v in a[i].items() if j > i and v}
        history.append((pivot, neighbors, b[i]))
        for j, v in neighbors.items():
            ratio = v / pivot
            b[j] -= ratio * b[i]
            for k, w in neighbors.items():
                if k < j:
                    continue
                value = a[j].get(k, Decimal(0)) - ratio * w
                a[j][k] = value
                if k != j:
                    a[k][j] = value
            a[j].pop(i, None)
        a[i].clear()
    x = [Decimal(0)] * n
    for i in range(n - 1, -1, -1):
        pivot, row, value = history[i]
        x[i] = (value - sum(v * x[j] for j, v in row.items())) / pivot
    result = [Decimal(0)] * n
    for i, old in enumerate(ordering):
        result[old] = x[i]
    return result


def solve_precise_frame(solver, mesh, boundary, elements, force, factor, basis, absent):
    if not all(isinstance(e, LoadedBarElement) for e in elements.values()):
        raise ValueError("High-precision fallback requires linear beam elements")
    with localcontext() as ctx:
        ctx.prec = 70
        n = len(force)
        rows = [{} for _ in range(n)]
        external = [Decimal(0)] * n
        for node, load in boundary.loads.items():
            base = solver._node_dof_start(node, solver.layout.stride)
            for j, value in enumerate(load.forces[: solver.layout.stride]):
                external[base + j] += dec(factor) * dec(value)
        if solver.spatial_load_contribution is not None:
            # This path reconstructs F independently; retain the compiled
            # spatial nodal load without inventing beam fixed-end corrections.
            for dof, value in enumerate(solver.spatial_load_contribution.dof_loads):
                external[dof] += dec(factor) * dec(value)
        matrices = {}
        for key, e, indices in solver.layout.elements(elements):
            k, t, f = element_matrix(e, mesh.nodes, mesh.elements[key])
            matrices[key] = (indices, k, t, f)
            for i, row in enumerate(indices):
                external[row] += dec(factor) * sum(t[j][i] * f[j] for j in range(12))
                for j, col in enumerate(indices):
                    if k[i][j]:
                        rows[row][col] = rows[row].get(col, Decimal(0)) + k[i][j]
        for item in [*boundary.distributed_loads, *boundary.pressures]:
            if item.element_id not in elements:
                continue
            element = elements[item.element_id]
            if hasattr(item, "load_type"):
                values = element.get_equivalent_nodal_loads(
                    item.load_type, item.values, item.face
                )
            else:
                values = element.get_equivalent_nodal_loads(
                    "pressure", [item.pressure], item.face
                )
            ids = solver.layout.element_dofs(item.element_id, element)
            for dof, value in zip(ids, values):
                external[dof] += dec(factor) * dec(value)
        rhs = external.copy()
        prescribed, springs = solver._get_boundary_dofs(
            boundary, n, solver.layout.stride
        )
        for dof, k in springs.items():
            rows[dof][dof] = rows[dof].get(dof, Decimal(0)) + dec(k)
        for dof, value in prescribed.items():
            value = dec(value) * dec(factor)
            for i, row in enumerate(rows):
                if i != dof:
                    rhs[i] -= row.pop(dof, Decimal(0)) * value
            rows[dof] = {dof: Decimal(1)}
            rhs[dof] = value
        if basis is None:
            basis = eye(n, format="csr")[:, np.flatnonzero(~absent)]
        entries = [
            [
                (int(j), dec(v))
                for j, v in zip(basis.getrow(i).indices, basis.getrow(i).data)
            ]
            for i in range(n)
        ]
        reduced = [{} for _ in range(basis.shape[1])]
        load = [Decimal(0)] * len(reduced)
        for i, row in enumerate(rows):
            for a, va in entries[i]:
                load[a] += va * rhs[i]
                for j, k in row.items():
                    for b, vb in entries[j]:
                        reduced[a][b] = reduced[a].get(b, Decimal(0)) + va * k * vb
        from scipy.sparse import csr_matrix

        rr, cc = zip(*[(i, j) for i, row in enumerate(reduced) for j in row])
        graph = csr_matrix(
            (np.ones(len(rr)), (rr, cc)), shape=(len(reduced), len(reduced))
        )
        ordering = reverse_cuthill_mckee(graph, symmetric_mode=True)
        x = sparse_solve(reduced, load, ordering)
        exact = [sum(v * x[j] for j, v in row) for row in entries]
        high = np.array([float(v) for v in exact])
        low = np.array([float(v - dec(h)) for v, h in zip(exact, high)])
        internal = [Decimal(0)] * n
        forces = {}
        for key, (indices, k, t, fixed) in matrices.items():
            elastic = [sum(v * exact[j] for v, j in zip(row, indices)) for row in k]
            for i, row in enumerate(indices):
                internal[row] += elastic[i]
            local = [
                float(sum(v * f for v, f in zip(row, elastic)) - dec(factor) * load)
                for row, load in zip(t, fixed)
            ]
            forces[key] = dict(i_end=np.array(local[:6]), j_end=np.array(local[6:]))
        residual = [
            internal[i] - external[i] + dec(springs.get(i, 0)) * exact[i]
            for i in range(n)
        ]
        projected = [Decimal(0)] * len(reduced)
        for i, row in enumerate(entries):
            if i not in prescribed:
                for j, v in row:
                    projected[j] += v * residual[i]
        scale = max(
            Decimal(1), *(abs(v) for v in internal), *(abs(dec(v)) for v in force)
        )
        if (
            max((abs(v) for v in projected), default=Decimal(0))
            > Decimal("1e-30") * scale
        ):
            raise ValueError("High-precision frame failed equilibrium check")
        reactions = np.array(
            [float(value - load) for value, load in zip(internal, external)]
        )
        for dof, k in springs.items():
            reactions[dof] = float(-dec(k) * exact[dof])
        return high, low, np.array([float(v) for v in internal]), forces, reactions
