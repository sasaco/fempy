"""Independent planar Bernoulli frame reference using scalar power series.

Solve EA*u''-kx*u=-qx and EI*v''''+ky*v=qy from the original member
endpoints. Interior loading/observation points do not become global unknowns.
No product imports, saved response values, matrix exponential or float solve.
"""

import math
from functools import lru_cache

import mpmath as mp


def number(value):
    return mp.mpf(float(value))


@lru_cache(maxsize=1024)
def series_map(length, rigidity, spring, bending):
    order = 4 if bending else 2
    matrix = mp.matrix(order, order)
    loads = mp.matrix(order, 2)
    for basis in range(order + 2):
        coefficients = [mp.mpf(0)] * 100
        if basis < order:
            coefficients[basis] = 1 / mp.factorial(basis)
        for n in range(100 - order):
            forcing = mp.mpf(
                int(basis == order and n == 0 or basis == order + 1 and n == 1)
            )
            sign = 1 if bending else -1
            coefficients[n + order] = (
                sign * (forcing - spring * coefficients[n]) / rigidity
            )
            for j in range(n + 1, n + order + 1):
                coefficients[n + order] /= j
        for derivative in range(order):
            terms = [
                coefficients[n]
                * mp.factorial(n)
                / mp.factorial(n - derivative)
                * length ** (n - derivative)
                for n in range(derivative, 100)
            ]
            value = mp.fsum(terms)
            assert mp.fsum(abs(term) for term in terms[-8:]) <= mp.mpf("1e-55") * max(
                1, abs(value)
            ), "Series tail is not resolved"
            if basis < order:
                matrix[derivative, basis] = value
            else:
                loads[derivative, basis - order] = value
    return matrix, loads


def member_mode(
    length, rigidity, spring, bending, distributed, points, temperature=0, moments=()
):
    order = 4 if bending else 2
    if not rigidity:
        assert bending and not spring and not distributed and not points and not moments

        def interpolated_state(displacements, position, side="right"):
            return mp.matrix(
                [
                    displacements[0]
                    + (displacements[2] - displacements[0]) * position / length,
                    0,
                    0,
                    0,
                ]
            )

        return mp.matrix(order, order), mp.matrix(order, 1), interpolated_state
    growth = (
        length * (spring / rigidity) ** (mp.mpf(1) / order) if spring else mp.mpf(0)
    )
    if growth > 4:
        from tests.support.oracles.piecewise_series import piecewise_mode

        count = int(mp.ceil(growth / 4))
        spans = [
            (length * i / count, length * (i + 1) / count, rigidity, temperature)
            for i in range(count)
        ]
        return piecewise_mode(
            length,
            rigidity,
            spring,
            bending,
            distributed,
            points,
            temperature,
            spans,
            moments,
        )
    cuts = sorted(
        {
            mp.mpf(0),
            length,
            *[p for a, b, _, _ in distributed for p in (a, b)],
            *[p for p, _ in points],
            *[p for p, _ in moments],
        }
    )
    intervals = []
    total = mp.eye(order)
    offset = mp.matrix(order, 1)
    for a, b in zip(cuts[:-1], cuts[1:]):
        # Coincident point loads are applied before the interval to their right.
        jump = mp.fsum(value for p, value in points if p == a)
        offset[order - 1] += (1 if bending else -1) * jump / rigidity
        if moments:
            assert bending
            offset[order - 2] -= (
                mp.fsum(value for p, value in moments if p == a) / rigidity
            )
        q0 = mp.mpf(0)
        slope = mp.mpf(0)
        for left, right, first, last in distributed:
            if left <= a and b <= right:
                gradient = (last - first) / (right - left)
                q0 += first + gradient * (a - left)
                slope += gradient
        transition, forcing = series_map(b - a, rigidity, spring, bending)
        intervals.append((a, b, total.copy(), offset.copy(), q0, slope))
        offset = transition * offset + forcing * mp.matrix([q0, slope])
        total = transition * total
    near = list(range(order // 2))
    unknown = list(range(order // 2, order))
    transfer = mp.matrix([[total[i, j] for j in unknown] for i in near])
    boundary = mp.matrix(order // 2, order)
    for i, row in enumerate(near):
        for j in near:
            boundary[i, j] = -total[row, j]
        boundary[i, order // 2 + i] = 1
    initial = mp.matrix(order, order)
    for i in near:
        initial[i, i] = 1
    for j in range(order):
        solved = mp.lu_solve(transfer, boundary[:, j])
        for i, row in enumerate(unknown):
            initial[row, j] = solved[i]
    initial_offset = mp.matrix(order, 1)
    solved = mp.lu_solve(transfer, mp.matrix([-offset[i] for i in near]))
    for i, row in enumerate(unknown):
        initial_offset[row] = solved[i]

    def actions(start, end):
        if bending:
            return mp.matrix(
                [
                    rigidity * start[3],
                    -rigidity * start[2],
                    -rigidity * end[3],
                    rigidity * end[2],
                ]
            )
        return mp.matrix(
            [-rigidity * (start[1] - temperature), rigidity * (end[1] - temperature)]
        )

    constant = actions(initial_offset, total * initial_offset + offset)
    stiffness = mp.matrix(order, order)
    for col in range(order):
        force = (
            actions(
                initial[:, col] + initial_offset,
                total * (initial[:, col] + initial_offset) + offset,
            )
            - constant
        )
        for row in range(order):
            stiffness[row, col] = force[row]

    def state(displacements, position, side="right"):
        start = initial * mp.matrix(displacements) + initial_offset
        options = [entry for entry in intervals if entry[0] <= position <= entry[1]]
        a, b, transition, forcing, q0, slope = options[-1 if side == "right" else 0]
        local = transition * start + forcing
        advance, load = series_map(position - a, rigidity, spring, bending)
        return advance * local + load * mp.matrix([q0, slope])

    return stiffness, -constant, state


def solve_reference(data, case_id):
    assert data["dimension"] == 2 and not data.get("joint")
    assert not data.get("shell") and not data.get("solid")
    assert all(not node.get("z", 0) for node in data["node"].values())
    with mp.workdps(65):
        case = data["load"][case_id]
        materials = data["element"][str(case.get("element", 1))]
        supports = data["fix_node"][str(case.get("fix_node", 1))]
        foundations = data.get("fix_member", {}).get(str(case.get("fix_member", 1)), [])
        nodes = list(data["node"])
        index = {n: 3 * i for i, n in enumerate(nodes)}
        coords = {
            n: [number(v[k]) for k in ("x", "y")] for n, v in data["node"].items()
        }
        size = 3 * len(nodes)
        K = mp.matrix(size, size)
        F = mp.matrix(size, 1)
        members = {}
        notices = {
            str(row["m"]): [number(x) for x in row["Points"]]
            for row in data.get("notice_points", [])
        }
        for mid, member in data["member"].items():
            assert not member.get("cg") and not member.get("shear_correction")
            ni, nj = str(member["ni"]), str(member["nj"])
            a, b = coords[ni], coords[nj]
            length = mp.sqrt(mp.fsum((v - u) ** 2 for u, v in zip(a, b)))
            cosine, sine = [(v - u) / length for u, v in zip(a, b)]
            R = mp.matrix([[cosine, sine, 0], [-sine, cosine, 0], [0, 0, 1]])
            T = mp.matrix(6, 6)
            for base in (0, 3):
                for i in range(3):
                    for j in range(3):
                        T[base + i, base + j] = R[i, j]
            mat = materials[str(member["e"])]
            ea = number(mat["E"]) * number(mat["A"])
            ei = number(mat["E"]) * number(mat["Iz"])
            assert ea > 0 and ei >= 0
            zones = [row for row in data.get("rigid", []) if str(row["m"]) == mid]
            interfaces = sorted(
                {
                    mp.mpf(0),
                    length,
                    *[
                        x
                        for zone in zones
                        for x in (
                            number(zone.get("Ilength", 0)),
                            length - number(zone.get("Jlength", 0)),
                        )
                        if 0 < x < length
                    ],
                }
            )
            if zones:
                notices[mid] = sorted(set(notices.get(mid, [])) | set(interfaces[1:-1]))
            springs = [
                mp.fsum(
                    number(row.get(k, 0)) for row in foundations if str(row["m"]) == mid
                )
                for k in ("tx", "ty")
            ]
            distributed = [[], []]
            points = [[], []]
            endpoint_load = mp.matrix(6, 1)
            thermal = mp.mpf(0)
            active = set()
            for row in case.get("load_member", []):
                if str(row["m"]) != mid:
                    continue
                mark = int(row.get("mark", 0))
                if mark == 0:
                    continue
                if mark == 9:
                    thermal += number(mat.get("Xp", 0)) * number(row.get("P1", 0))
                    continue
                assert row["direction"] in ("x", "y") and mark in (1, 2)
                axis = ("x", "y").index(row["direction"])
                if mark == 2:
                    left = number(row.get("L1") or 0)
                    tail = number(row.get("L2") or 0)
                    right = left - tail if tail < 0 else length - tail
                    if abs(left - right) <= mp.mpf("1e-10") * max(1, length):
                        continue
                    assert 0 <= left < right <= length
                    distributed[axis].append(
                        (
                            left,
                            right,
                            number(row.get("P1", 0)),
                            number(row.get("P2", 0)),
                        )
                    )
                    active.update((left, right))
                else:
                    for suffix in ("1", "2"):
                        value = number(row.get("P" + suffix) or 0)
                        p = number(row.get("L" + suffix) or 0)
                        if value:
                            tolerance = mp.mpf("1e-10") * max(1, length)
                            assert -tolerance <= p <= length + tolerance
                            if abs(p) <= tolerance:
                                endpoint_load[axis] += value
                            elif abs(p - length) <= tolerance:
                                endpoint_load[3 + axis] += value
                            else:
                                points[axis].append((p, value))
                                active.add(p)
            positions = sorted({mp.mpf(0), length, *notices.get(mid, []), *active})
            unique = []
            for position in positions:
                if not unique or position - unique[-1] > mp.mpf("1e-10") * max(
                    1, length
                ):
                    unique.append(position)
            unique[-1] = length
            snap = lambda value: min(unique, key=lambda position: abs(position - value))
            distributed = [
                [(snap(a), snap(b), q0, q1) for a, b, q0, q1 in loads]
                for loads in distributed
            ]
            points = [[(snap(p), value) for p, value in loads] for loads in points]
            active = {snap(p) for p in active}
            notices[mid] = list(dict.fromkeys(snap(p) for p in notices.get(mid, [])))
            ax = member_mode(
                length, ea, springs[0], False, distributed[0], points[0], thermal
            )
            bend = member_mode(length, ei, springs[1], True, distributed[1], points[1])
            if len(interfaces) > 2:
                from tests.support.oracles.piecewise_series import piecewise_mode

                axial_spans, bending_spans = [], []
                for a, b in zip(interfaces[:-1], interfaces[1:]):
                    section = mat
                    for zone in zones:
                        if b <= number(zone.get("Ilength", 0)) or a >= length - number(
                            zone.get("Jlength", 0)
                        ):
                            section = materials[str(zone["e"])]
                    r_ax = number(section["E"]) * number(section["A"])
                    r_bend = number(section["E"]) * number(section["Iz"])
                    strain = mp.fsum(
                        number(section.get("Xp", 0)) * number(row.get("P1", 0))
                        for row in case.get("load_member", [])
                        if str(row["m"]) == mid and row.get("mark") == 9
                    )
                    axial_spans.append((a, b, r_ax, strain))
                    bending_spans.append((a, b, r_bend, mp.mpf(0)))
                ax = piecewise_mode(
                    length,
                    ea,
                    springs[0],
                    False,
                    distributed[0],
                    points[0],
                    thermal,
                    axial_spans,
                )
                bend = piecewise_mode(
                    length,
                    ei,
                    springs[1],
                    True,
                    distributed[1],
                    points[1],
                    mp.mpf(0),
                    bending_spans,
                )
            local = mp.matrix(6, 6)
            load = mp.matrix(6, 1)
            for values, axes in ((ax, [0, 3]), (bend, [1, 2, 4, 5])):
                k, f, _ = values
                for i, row in enumerate(axes):
                    load[row] = f[i]
                    for j, col in enumerate(axes):
                        local[row, col] = k[i, j]
            global_k = T.T * local * T
            global_f = T.T * (load + endpoint_load)
            ids = [index[n] + j for n in (ni, nj) for j in range(3)]
            for i, row in enumerate(ids):
                F[row] += global_f[i]
                for j, col in enumerate(ids):
                    K[row, col] += global_k[i, j]
            members[mid] = (ni, nj, length, T, ids, ea, ei, thermal, ax, bend, active)
        for row in case.get("load_node", []):
            assert not any(row.get(key, 0) for key in ("tz", "rx", "ry"))
            for j, key in enumerate(("tx", "ty", "rz")):
                F[index[str(row["n"])] + j] += number(row.get(key, 0))
        fixed = {}
        springs = {}
        for row in supports:
            for j, key in enumerate(("tx", "ty", "rz")):
                value = row.get(key, 0)
                dof = index[str(row["n"])] + j
                if value == 1:
                    fixed[dof] = mp.mpf(0)
                elif value:
                    springs[dof] = abs(number(value))
                    K[dof, dof] += abs(number(value))
        absent = {
            i
            for i in range(size)
            if i % 3 == 2 and not F[i] and not any(K[i, j] for j in range(size))
        }
        free = [i for i in range(size) if i not in fixed and i not in absent]
        u = mp.matrix(size, 1)
        solved = mp.lu_solve(
            mp.matrix([[K[i, j] for j in free] for i in free]),
            mp.matrix([F[i] for i in free]),
        )
        for i, value in zip(free, solved):
            u[i] = value
        residual = K * u - F
        assert max(abs(residual[i]) for i in free) < mp.mpf("1e-40")

        def nodal(values):
            return dict(
                zip(
                    ("dx", "dy", "dz", "rx", "ry", "rz"),
                    map(float, [values[0], values[1], 0, 0, 0, values[2]]),
                )
            )

        displacement = {n: nodal(u[index[n] : index[n] + 3, 0]) for n in nodes}
        reactions = {}
        for row in supports:
            n = str(row["n"])
            base = index[n]
            values = [
                residual[base + j]
                if base + j in fixed
                else -springs.get(base + j, 0) * u[base + j]
                for j in range(3)
            ]
            reactions[n] = dict(
                zip(
                    ("tx", "ty", "tz", "mx", "my", "mz"),
                    map(float, [values[0], values[1], 0, 0, 0, values[2]]),
                )
            )
        sections = {}
        all_coordinates = [tuple(map(float, coords[n])) for n in nodes]
        for mid, (
            ni,
            nj,
            length,
            T,
            ids,
            ea,
            ei,
            thermal,
            ax,
            bend,
            active,
        ) in members.items():
            local = T * mp.matrix([u[i] for i in ids])
            axial = [local[0], local[3]]
            bending = [local[i] for i in (1, 2, 4, 5)]
            observed = sorted(set(p for p in notices.get(mid, []) if 0 < p < length))
            section_points = [mp.mpf(0), *observed, length]
            segments = {}
            for i, (a, b) in enumerate(zip(section_points[:-1], section_points[1:]), 1):
                lefta, righta = ax[2](axial, a), ax[2](axial, b, "left")
                leftb, rightb = bend[2](bending, a), bend[2](bending, b, "left")
                segment = dict(L=float(b - a))
                for suffix, aa, bb in [("i", lefta, leftb), ("j", righta, rightb)]:
                    segment.update(
                        {
                            key + suffix: float(value)
                            for key, value in zip(
                                ("fx", "fy", "fz", "mx", "my", "mz"),
                                [
                                    ea * (aa[1] - thermal),
                                    ei * bb[3],
                                    0,
                                    0,
                                    0,
                                    -ei * bb[2],
                                ],
                            )
                        }
                    )
                segments["P" + str(i)] = segment
            sections[mid] = segments
            visible = sorted(set(observed) | {p for p in active if 0 < p < length})
            count = 0
            for p in visible:
                if p in observed:
                    label = mid + "n" + str(observed.index(p) + 1)
                else:
                    count += 1
                    label = mid + "l" + str(count)
                va = ax[2](axial, p)
                vb = bend[2](bending, p)
                global_values = T[:3, :3].T * mp.matrix([va[0], vb[0], vb[1]])
                displacement[label] = nodal(global_values)
            total_positions = set(observed)
            for load_case in data["load"].values():
                for row in load_case.get("load_member", []):
                    if str(row["m"]) != mid:
                        continue
                    l1 = number(row.get("L1") or 0)
                    l2 = number(row.get("L2") or 0)
                    if row["mark"] == 2:
                        total_positions.update((l1, l1 - l2 if l2 < 0 else length - l2))
                    elif row["mark"] in (1, 11):
                        total_positions.update((l1, l2))
            for p in total_positions:
                if not 0 < p < length:
                    continue
                xy = tuple(
                    float(a + (b - a) * p / length)
                    for a, b in zip(coords[ni], coords[nj])
                )
                if not any(
                    math.dist(xy, old) <= 1e-10 * max(1, float(length))
                    for old in all_coordinates
                ):
                    all_coordinates.append(xy)
        if len(all_coordinates) > len(nodes):
            reactions["0"] = dict(tx=0.0, ty=0.0, tz=0.0, mx=0.0, my=0.0, mz=0.0)
        return dict(
            disg=displacement,
            reac=reactions,
            fsec=sections,
            size=len(all_coordinates),
            shell_results={},
        )
