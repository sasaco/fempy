"""Condense material interfaces around independent scalar ODE solutions.

Only actual material changes become internal unknowns. Loading and observation
points remain analytic positions within each constant-property interval.
"""

import mpmath as mp


def piecewise_mode(
    length, rigidity, spring, bending, distributed, points, thermal, spans, moments=()
):
    from tests.support.oracles.plane_frame_series import member_mode

    width = 2 if bending else 1
    count = width * (len(spans) + 1)
    matrix, load = mp.matrix(count, count), mp.matrix(count, 1)
    operators = []
    for index, (a, b, r, eps) in enumerate(spans):
        local_distributed = []
        for left, right, q0, q1 in distributed:
            start, end = max(left, a), min(right, b)
            if start < end:
                gradient = (q1 - q0) / (right - left)
                local_distributed.append(
                    (
                        start - a,
                        end - a,
                        q0 + gradient * (start - left),
                        q0 + gradient * (end - left),
                    )
                )
        local_points = [(x - a, value) for x, value in points if a < x < b]
        local_moments = [(x - a, value) for x, value in moments if a < x < b]
        k, f, state = member_mode(
            b - a,
            r,
            spring,
            bending,
            local_distributed,
            local_points,
            eps,
            local_moments,
        )
        indices = list(range(width * index, width * (index + 2)))
        for i, row in enumerate(indices):
            load[row] += f[i]
            for j, col in enumerate(indices):
                matrix[row, col] += k[i, j]
        operators.append((a, b, r, eps, indices, state))
    for x, value in points:
        for i, (a, _, _, _) in enumerate(spans[1:], 1):
            if x == a:
                load[width * i] += value
    for x, value in moments:
        assert bending
        for i, (a, _, _, _) in enumerate(spans[1:], 1):
            if x == a:
                load[width * i + 1] += value
    outer = list(range(width)) + list(range(count - width, count))
    inner = list(range(width, count - width))
    block = mp.matrix([[matrix[i, j] for j in inner] for i in inner])
    relation = mp.matrix(count, 2 * width)
    offset = mp.matrix(count, 1)
    for j, dof in enumerate(outer):
        relation[dof, j] = 1
        result = mp.lu_solve(block, mp.matrix([-matrix[i, dof] for i in inner]))
        for row, value in zip(inner, result):
            relation[row, j] = value
    result = mp.lu_solve(block, mp.matrix([load[i] for i in inner]))
    for row, value in zip(inner, result):
        offset[row] = value
    full_k = matrix * relation
    full_f = load - matrix * offset
    k = mp.matrix([[full_k[i, j] for j in range(2 * width)] for i in outer])
    f = mp.matrix([full_f[i] for i in outer])

    def state(displacements, position, side="right"):
        values = relation * mp.matrix(displacements) + offset
        choices = [item for item in operators if item[0] <= position <= item[1]]
        a, b, r, eps, indices, evaluate = choices[-1 if side == "right" else 0]
        result = evaluate([values[i] for i in indices], position - a, side)
        # Preserve u/rotation; express force derivatives against the member's
        # nominal rigidity so the caller can expose a uniform result schema.
        if bending:
            result[2] *= r / rigidity
            result[3] *= r / rigidity
        else:
            result[1] = (result[1] - eps) * r / rigidity + thermal
        return result

    return k, f, state
