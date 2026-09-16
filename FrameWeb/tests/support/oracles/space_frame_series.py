"""Independent spatial frame assembled from scalar ODE boundary solutions.

Original endpoints are the global unknowns. A float sparse factorization is
only a preconditioner: residuals and accepted solutions retain 65-digit scalar
series operators, and every unconstrained equation must close below 1e-35.
"""

import math

import mpmath as mp
import numpy as np
from scipy.sparse import coo_matrix, diags
from scipy.sparse.linalg import splu

from tests.support.oracles.plane_frame_series import member_mode, number


def check_rigid_supports(data, supports, foundations):
    constraints = []
    origin = np.array(
        [next(iter(data["node"].values()))[key] for key in "xyz"], dtype=float
    )

    def motion(node):
        x, y, z = (
            np.array([data["node"][str(node)][key] for key in "xyz"], dtype=float)
            - origin
        )
        return np.block(
            [
                [np.eye(3), np.array([[0, z, -y], [-z, 0, x], [y, -x, 0]])],
                [np.zeros((3, 3)), np.eye(3)],
            ]
        )

    for row in supports:
        modes = motion(row["n"])
        constraints.extend(
            modes[j]
            for j, key in enumerate(["tx", "ty", "tz", "rx", "ry", "rz"])
            if row.get(key, 0)
        )
    for row in foundations:
        member = data["member"][str(row["m"])]
        a, b = [
            np.array(
                [data["node"][str(member[end])][key] for key in "xyz"], dtype=float
            )
            for end in ["ni", "nj"]
        ]
        ex = (b - a) / np.linalg.norm(b - a)
        horizontal = np.linalg.norm(ex[:2])
        if horizontal:
            ey = np.array([-ex[1], ex[0], 0]) / horizontal
            ez = np.cross(ex, ey)
        else:
            ey, ez = np.array([ex[2], 0, 0]), np.array([0, 1, 0])
        angle = math.radians(member.get("cg") or 0)
        c, s = math.cos(angle), math.sin(angle)
        axes = [ex, c * ey + s * ez, -s * ey + c * ez]
        for mode, key in enumerate(["tx", "ty", "tz", "tr"]):
            if not row.get(key, 0):
                continue
            for end in ["ni", "nj"]:
                m = motion(member[end])
                constraints.append(axes[mode] @ m[:3] if mode < 3 else ex @ m[3:])
    assert constraints, "Unsupported global rigid motion"
    matrix = np.array(constraints)
    matrix /= np.maximum(np.linalg.norm(matrix, axis=0), 1)
    assert np.linalg.matrix_rank(matrix, tol=1e-12) == 6, (
        "Unsupported global rigid motion"
    )


def solve_sparse(rows, force, fixed):
    n = len(force)
    absent = {
        i
        for i, row in enumerate(rows)
        if not any(row.values()) and not force[i] and i % 6 >= 3
    }
    free = [i for i in range(n) if i not in fixed and i not in absent]
    lookup = {dof: i for i, dof in enumerate(free)}
    rr, cc, vv = [], [], []
    for i in free:
        for j, value in rows[i].items():
            if j in lookup and value:
                rr.append(lookup[i])
                cc.append(lookup[j])
                vv.append(float(value))
    k = coo_matrix((vv, (rr, cc)), shape=(len(free), len(free))).tocsc()
    assert np.all(k.diagonal() > 0), "Unsupported free translation"
    scale = 1 / np.sqrt(k.diagonal())
    lu = splu((diags(scale) @ k @ diags(scale)).tocsc())
    u = [mp.mpf(0)] * n
    for iteration in range(40):
        residual = [
            force[i] - mp.fsum(value * u[j] for j, value in rows[i].items())
            for i in free
        ]
        if max(map(abs, residual), default=0) < mp.mpf("1e-35"):
            return mp.matrix(u)
        correction = scale * lu.solve(scale * np.array([float(v) for v in residual]))
        assert np.isfinite(correction).all()
        for dof, value in zip(free, correction):
            u[dof] += number(value)
    raise AssertionError("Independent spatial refinement did not converge")


def solve_reference(data, case_id):
    assert data["dimension"] == 3
    assert not data.get("solid")
    with mp.workdps(65):
        case = data["load"][case_id]
        materials = data["element"][str(case.get("element", 1))]
        supports = data.get("fix_node", {}).get(str(case.get("fix_node", 1)), [])
        joints = data.get("joint", {}).get(str(case.get("joint", 1)), [])
        foundations = data.get("fix_member", {}).get(str(case.get("fix_member", 1)), [])
        check_rigid_supports(data, supports, foundations)
        nodes = list(data["node"])
        coords = {
            n: mp.matrix([number(data["node"][n][k]) for k in "xyz"]) for n in nodes
        }
        index = {n: 6 * i for i, n in enumerate(nodes)}
        rows = [{} for _ in range(6 * len(nodes))]
        F = mp.matrix(len(rows), 1)
        members = {}
        notices = {
            str(row["m"]): [number(x) for x in row["Points"]]
            for row in data.get("notice_points", [])
        }
        for mid, member in data["member"].items():
            assert not member.get("shear_correction")
            ni, nj = str(member["ni"]), str(member["nj"])
            delta = coords[nj] - coords[ni]
            length = mp.sqrt(mp.fsum(x * x for x in delta))
            ex = delta / length
            horizontal = mp.sqrt(ex[0] ** 2 + ex[1] ** 2)
            if horizontal:
                ey = mp.matrix([-ex[1] / horizontal, ex[0] / horizontal, 0])
                ez = mp.matrix(
                    [
                        -ex[0] * ex[2] / horizontal,
                        -ex[1] * ex[2] / horizontal,
                        horizontal,
                    ]
                )
            else:
                ey, ez = mp.matrix([ex[2], 0, 0]), mp.matrix([0, 1, 0])
            angle = number(member.get("cg") or 0) * mp.pi / 180
            c, s = mp.cos(angle), mp.sin(angle)
            basis = mp.matrix([list(ex), list(c * ey + s * ez), list(-s * ey + c * ez)])
            transform = mp.matrix(12, 12)
            for base in range(0, 12, 3):
                for i in range(3):
                    for j in range(3):
                        transform[base + i, base + j] = basis[i, j]
            material = materials[str(member["e"])]
            zones = [row for row in data.get("rigid", []) if str(row["m"]) == mid]
            interfaces = sorted(
                {
                    mp.mpf(0),
                    length,
                    *[
                        x
                        for row in zones
                        for x in (
                            number(row.get("Ilength", 0)),
                            length - number(row.get("Jlength", 0)),
                        )
                        if 0 < x < length
                    ],
                }
            )
            if zones:
                notices[mid] = sorted(set(notices.get(mid, [])) | set(interfaces[1:-1]))
            rigidities = [
                number(material[a]) * number(material[b])
                for a, b in [("E", "A"), ("E", "Iz"), ("E", "Iy"), ("G", "J")]
            ]
            phantom = not any(rigidities)
            springs = [
                mp.fsum(
                    abs(number(row.get(k, 0)))
                    for row in foundations
                    if str(row["m"]) == mid
                )
                for k in ["tx", "ty", "tz", "tr"]
            ]
            distributed = [[] for _ in range(4)]
            points = [[] for _ in range(4)]
            moments = [[] for _ in range(4)]
            endpoint = mp.matrix(12, 1)
            thermal = mp.mpf(0)
            active = set()

            def vector(direction, value):
                v = mp.matrix(4, 1)
                if direction == "r":
                    v[3] = value
                elif direction in ("x", "y", "z"):
                    v["xyz".index(direction)] = value
                elif direction in ("gx", "gy", "gz"):
                    for i in range(3):
                        v[i] = basis[i, "xyz".index(direction[1])] * value
                else:
                    raise AssertionError("Unsupported spatial load direction")
                return v

            for row in case.get("load_member", []):
                if str(row["m"]) != mid:
                    continue
                mark = row.get("mark", 0)
                if mark == 0:
                    continue
                if mark == 9:
                    thermal += number(material.get("Xp", 0)) * number(row.get("P1", 0))
                    continue
                assert mark in (1, 2, 11)
                if mark == 2:
                    a = number(row.get("L1") or 0)
                    tail = number(row.get("L2") or 0)
                    b = a - tail if tail < 0 else length - tail
                    if abs(a - b) < mp.mpf("1e-10") * max(1, length):
                        continue
                    assert 0 <= a < b <= length
                    q0, q1 = (
                        vector(row["direction"], number(row.get("P1", 0))),
                        vector(row["direction"], number(row.get("P2", 0))),
                    )
                    if phantom:
                        for mode in range(4):
                            total = (b - a) * (q0[mode] + q1[mode]) / 2
                            first = (
                                a * total + (b - a) ** 2 * (q0[mode] + 2 * q1[mode]) / 6
                            )
                            endpoint[mode] += total - first / length
                            endpoint[6 + mode] += first / length
                    else:
                        for mode in range(4):
                            if q0[mode] or q1[mode]:
                                distributed[mode].append((a, b, q0[mode], q1[mode]))
                        active.update((a, b))
                else:
                    for suffix in ("1", "2"):
                        value = number(row.get("P" + suffix) or 0)
                        if not value:
                            continue
                        p = number(row.get("L" + suffix) or 0)
                        assert 0 <= p <= length
                        v = vector(row["direction"], value)
                        assert not v[3], "Point torque uses mark=11, direction=x"
                        offset = 0 if mark == 1 else 3
                        if phantom:
                            for axis in range(3):
                                endpoint[offset + axis] += (1 - p / length) * v[axis]
                                endpoint[6 + offset + axis] += p / length * v[axis]
                        elif min(abs(p), abs(length - p)) <= mp.mpf("1e-10") * max(
                            1, length
                        ):
                            end = 0 if p < length / 2 else 6
                            for axis in range(3):
                                endpoint[end + offset + axis] += v[axis]
                        else:
                            active.add(p)
                            if mark == 1:
                                for axis in range(3):
                                    if v[axis]:
                                        points[axis].append((p, v[axis]))
                            else:
                                if v[0]:
                                    points[3].append((p, v[0]))
                                if v[2]:
                                    moments[1].append((p, v[2]))
                                if v[1]:
                                    moments[2].append((p, -v[1]))
            if not phantom:
                locations = sorted({mp.mpf(0), length, *active, *notices.get(mid, [])})
                unique = []
                for value in locations:
                    if not unique or value - unique[-1] > mp.mpf("1e-10") * max(
                        1, length
                    ):
                        unique.append(value)
                unique[-1] = length
                snap = lambda value: min(unique, key=lambda x: abs(x - value))
                active = {snap(x) for x in active}
                notices[mid] = list(
                    dict.fromkeys(snap(x) for x in notices.get(mid, []))
                )
                distributed = [
                    [(snap(a), snap(b), q0, q1) for a, b, q0, q1 in rows]
                    for rows in distributed
                ]
                points = [[(snap(x), value) for x, value in rows] for rows in points]
                moments = [[(snap(x), value) for x, value in rows] for rows in moments]
            local = mp.matrix(12, 12)
            load = mp.matrix(12, 1)
            operators = []
            if not phantom:
                for mode, axes, signs in [
                    (0, [0, 6], [1, 1]),
                    (1, [1, 5, 7, 11], [1, 1, 1, 1]),
                    (2, [2, 4, 8, 10], [1, -1, 1, -1]),
                    (3, [3, 9], [1, 1]),
                ]:
                    if (
                        not rigidities[mode]
                        and not springs[mode]
                        and not distributed[mode]
                        and not points[mode]
                        and not moments[mode]
                    ):
                        operators.append(None)
                        continue
                    op = member_mode(
                        length,
                        rigidities[mode],
                        springs[mode],
                        mode in (1, 2),
                        distributed[mode],
                        points[mode],
                        thermal if mode == 0 else 0,
                        moments[mode],
                    )
                    if len(interfaces) > 2:
                        from tests.support.oracles.piecewise_series import (
                            piecewise_mode,
                        )

                        spans = []
                        for a, b in zip(interfaces[:-1], interfaces[1:]):
                            section = material
                            for zone in zones:
                                if b <= number(
                                    zone.get("Ilength", 0)
                                ) or a >= length - number(zone.get("Jlength", 0)):
                                    section = materials[str(zone["e"])]
                            young, section_key = [
                                ("E", "A"),
                                ("E", "Iz"),
                                ("E", "Iy"),
                                ("G", "J"),
                            ][mode]
                            r = number(section[young]) * number(section[section_key])
                            strain = (
                                mp.fsum(
                                    number(section.get("Xp", 0))
                                    * number(row.get("P1", 0))
                                    for row in case.get("load_member", [])
                                    if str(row["m"]) == mid and row.get("mark") == 9
                                )
                                if mode == 0
                                else mp.mpf(0)
                            )
                            spans.append((a, b, r, strain))
                        op = piecewise_mode(
                            length,
                            rigidities[mode],
                            springs[mode],
                            mode in (1, 2),
                            distributed[mode],
                            points[mode],
                            thermal if mode == 0 else 0,
                            spans,
                            moments[mode],
                        )
                    operators.append((op, axes, signs))
                    k, f, _ = op
                    for i, row in enumerate(axes):
                        load[row] += f[i] * signs[i]
                        for j, col in enumerate(axes):
                            local[row, col] = k[i, j] * signs[i] * signs[j]
            releases = sorted(
                {
                    axis
                    for row in joints
                    if str(row["m"]) == mid
                    for axis, key in zip(
                        (3, 4, 5, 9, 10, 11), ("xi", "yi", "zi", "xj", "yj", "zj")
                    )
                    if row.get(key, 1) == 0
                }
            )
            recovery = []
            for released in releases:
                pivot = local[released, released]
                if abs(pivot) > mp.mpf("1e-50"):
                    column = local[:, released]
                    recovery.append((released, pivot, list(column), load[released]))
                    load -= column * (load[released] / pivot)
                    local -= column * column.T / pivot
                else:
                    assert abs(load[released]) < mp.mpf("1e-40"), (
                        "Unsupported released load"
                    )
                load[released] = 0
                for i in range(12):
                    local[released, i] = local[i, released] = 0
            ids = [index[n] + j for n in (ni, nj) for j in range(6)]
            global_k = transform.T * local * transform
            global_f = transform.T * (load + endpoint)
            for i, row in enumerate(ids):
                F[row] += global_f[i]
                for j, col in enumerate(ids):
                    if global_k[i, j]:
                        rows[row][col] = rows[row].get(col, mp.mpf(0)) + global_k[i, j]
            members[mid] = (
                ni,
                nj,
                length,
                transform,
                ids,
                rigidities,
                thermal,
                operators,
                active,
                phantom,
                endpoint,
                recovery,
            )
        shell_operators = []
        if data.get("shell"):
            from tests.support.oracles.shell_variational import operator

            for shell in data["shell"].values():
                labels = list(map(str, shell["nodes"]))
                material = materials[str(shell["e"])]
                k, response = operator(
                    [coords[n] for n in labels],
                    material,
                    material.get("thickness", material["A"]),
                )
                dofs = [index[n] + j for n in labels for j in range(6)]
                for i, row in enumerate(dofs):
                    for j, col in enumerate(dofs):
                        if k[i, j]:
                            rows[row][col] = rows[row].get(col, mp.mpf(0)) + k[i, j]
                shell_operators.append((dofs, response))
        for row in case.get("load_node", []):
            for j, key in enumerate(("tx", "ty", "tz", "rx", "ry", "rz")):
                F[index[str(row["n"])] + j] += number(row.get(key, 0))
        fixed = set()
        springs = {}
        for row in supports:
            for j, key in enumerate(("tx", "ty", "tz", "rx", "ry", "rz")):
                value = row.get(key, 0)
                dof = index[str(row["n"])] + j
                if value == 1:
                    fixed.add(dof)
                elif value:
                    springs[dof] = abs(number(value))
                    rows[dof][dof] = rows[dof].get(dof, 0) + springs[dof]
        u = solve_sparse(rows, F, fixed)
        residual = [
            mp.fsum(value * u[j] for j, value in row.items()) - F[i]
            for i, row in enumerate(rows)
        ]
        displacement = {
            n: dict(
                zip(
                    ("dx", "dy", "dz", "rx", "ry", "rz"),
                    map(float, u[index[n] : index[n] + 6, 0]),
                )
            )
            for n in nodes
        }
        reactions = {}
        for row in supports:
            n = str(row["n"])
            base = index[n]
            reactions[n] = dict(
                zip(
                    ("tx", "ty", "tz", "mx", "my", "mz"),
                    [
                        float(
                            residual[base + j]
                            if base + j in fixed
                            else -springs.get(base + j, 0) * u[base + j]
                        )
                        for j in range(6)
                    ],
                )
            )
        sections = {}
        all_coords = [tuple(map(float, coords[n])) for n in nodes]
        for mid, (
            ni,
            nj,
            length,
            transform,
            ids,
            rigidities,
            thermal,
            operators,
            active,
            phantom,
            endpoint,
            recovery,
        ) in members.items():
            local = transform * mp.matrix([u[i] for i in ids])
            for released, pivot, column, value in reversed(recovery):
                local[released] = (
                    value
                    - mp.fsum(column[j] * local[j] for j in range(12) if j != released)
                ) / pivot

            def at(x, side="right"):
                displacement = mp.matrix(6, 1)
                force = mp.matrix(6, 1)
                for mode, operator in enumerate(operators):
                    if operator is None:
                        continue
                    op, axes, signs = operator
                    v = op[2](
                        [local[axis] * sign for axis, sign in zip(axes, signs)], x, side
                    )
                    r = rigidities[mode]
                    if mode == 0:
                        displacement[0] = v[0]
                        force[0] = r * (v[1] - thermal)
                    elif mode == 3:
                        displacement[3] = v[0]
                        force[3] = r * v[1]
                    else:
                        axis, rotation, sign = (1, 5, 1) if mode == 1 else (2, 4, -1)
                        displacement[axis] = v[0]
                        displacement[rotation] = sign * v[1]
                        force[axis] = r * v[3]
                        force[rotation] = -r * v[2]
                return displacement, force

            observed = sorted(set(p for p in notices.get(mid, []) if 0 < p < length))
            cuts = [mp.mpf(0), *observed, length]
            segments = {}
            if phantom:
                assert not observed
            for i, (a, b) in enumerate(zip(cuts[:-1], cuts[1:]), 1):
                if phantom:
                    signs = [1, -1, -1, 1, 1, -1]
                    first = [endpoint[j] * signs[j] for j in range(6)]
                    last = [-endpoint[6 + j] * signs[j] for j in range(6)]
                else:
                    first, last = at(a)[1], at(b, "left")[1]
                segment = {"L": float(b - a)}
                for suffix, values in [("i", first), ("j", last)]:
                    segment.update(
                        {
                            key + suffix: float(value)
                            for key, value in zip(
                                ("fx", "fy", "fz", "mx", "my", "mz"), values
                            )
                        }
                    )
                segments["P" + str(i)] = segment
            sections[mid] = segments
            count = 0
            for p in sorted(set(observed) | {p for p in active if 0 < p < length}):
                if p in observed:
                    label = mid + "n" + str(observed.index(p) + 1)
                else:
                    count += 1
                    label = mid + "l" + str(count)
                v = transform[:6, :6].T * at(p)[0]
                displacement[label] = dict(
                    zip(("dx", "dy", "dz", "rx", "ry", "rz"), map(float, v))
                )
            if phantom:
                continue
            total_positions = set(observed)
            for lc in data["load"].values():
                for row in lc.get("load_member", []):
                    if str(row["m"]) != mid:
                        continue
                    a = number(row.get("L1") or 0)
                    b = number(row.get("L2") or 0)
                    if row["mark"] == 2:
                        total_positions.update((a, a - b if b < 0 else length - b))
                    elif row["mark"] in (1, 11):
                        total_positions.update((a, b))
            for p in total_positions:
                if 0 < p < length:
                    xyz = tuple(
                        map(float, coords[ni] + p / length * (coords[nj] - coords[ni]))
                    )
                    if not any(
                        math.dist(xyz, old) < 1e-10 * max(1, float(length))
                        for old in all_coords
                    ):
                        all_coords.append(xyz)
        return dict(
            disg=displacement,
            reac=reactions,
            fsec=sections,
            size=len(all_coords),
            shell_results={
                str(i): response([u[dof] for dof in dofs])
                for i, (dofs, response) in enumerate(shell_operators)
            },
        )
