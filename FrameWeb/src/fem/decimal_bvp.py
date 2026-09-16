"""Decimal state-transition beam BVP for the high-precision linear path.

Uses the same force/displacement state equations as LoadedBarElement. Scaling
and squaring evaluates the matrix exponential; long foundation members are
joined by static condensation to avoid exponentially ill-conditioned maps.
"""

from decimal import Decimal

ZERO = Decimal(0)
ONE = Decimal(1)


def matrix(rows, cols):
    return [[ZERO for _ in range(cols)] for _ in range(rows)]


def transpose(a):
    return list(map(list, zip(*a)))


def multiply(a, b):
    return [[sum(x * y for x, y in zip(row, col)) for col in zip(*b)] for row in a]


def solve(a, b):
    """Small dense solve with partial pivoting and multiple right hand sides."""
    a, b = [row[:] for row in a], [row[:] for row in b]
    n = len(a)
    for i in range(n):
        pivot = max(range(i, n), key=lambda row: abs(a[row][i]))
        if not a[pivot][i]:
            raise ValueError("Singular beam boundary map")
        a[i], a[pivot] = a[pivot], a[i]
        b[i], b[pivot] = b[pivot], b[i]
        for j in range(i + 1, n):
            ratio = a[j][i] / a[i][i]
            for k in range(i + 1, n):
                a[j][k] -= ratio * a[i][k]
            for k in range(len(b[0])):
                b[j][k] -= ratio * b[i][k]
    x = matrix(n, len(b[0]))
    for i in range(n - 1, -1, -1):
        for j in range(len(b[0])):
            x[i][j] = (b[i][j] - sum(a[i][k] * x[k][j] for k in range(i + 1, n))) / a[
                i
            ][i]
    return x


def exponential(a):
    n = len(a)
    norm = max(sum(abs(v) for v in row) for row in a)
    levels = 0
    while norm > Decimal(".5"):
        norm /= 2
        levels += 1
    scaled = [[v / (2**levels) for v in row] for row in a]
    result = [[ONE if i == j else ZERO for j in range(n)] for i in range(n)]
    term = [row[:] for row in result]
    for order in range(1, 300):
        term = [[v / order for v in row] for row in multiply(term, scaled)]
        result = [[x + y for x, y in zip(row, add)] for row, add in zip(result, term)]
        if max(abs(v) for row in term for v in row) < Decimal("1e-68"):
            break
    else:
        raise ValueError("Decimal matrix exponential did not converge")
    for _ in range(levels):
        result = multiply(result, result)
    return result


def boundary_map(length, rigidity, foundation, bending, q, shear=None):
    width = 2 if bending else 1
    growth = length * (foundation / rigidity).sqrt()
    if bending:
        growth = length * (foundation / rigidity).sqrt().sqrt()
        if shear:
            growth += length * (foundation / shear).sqrt()
    if growth > 4:
        # Two half intervals with the physical midpoint load; eliminate only
        # the shared displacement/rotation, preserving every foundation term.
        middle = (q[0] + q[1]) / 2
        left, fl = boundary_map(
            length / 2, rigidity, foundation, bending, (q[0], middle), shear
        )
        right, fr = boundary_map(
            length / 2, rigidity, foundation, bending, (middle, q[1]), shear
        )
        joined, loads = matrix(3 * width, 3 * width), matrix(3 * width, 1)
        for offset, k, f in [(0, left, fl), (width, right, fr)]:
            for i in range(2 * width):
                loads[offset + i][0] += f[i]
                for j in range(2 * width):
                    joined[offset + i][offset + j] += k[i][j]
        outer = list(range(width)) + list(range(2 * width, 3 * width))
        inner = list(range(width, 2 * width))
        coupling = [[joined[i][j] for j in inner] for i in outer]
        block = [[joined[i][j] for j in inner] for i in inner]
        correction = multiply(coupling, solve(block, transpose(coupling)))
        shift = multiply(coupling, solve(block, [loads[i] for i in inner]))
        return (
            [
                [joined[i][j] - correction[a][b] for b, j in enumerate(outer)]
                for a, i in enumerate(outer)
            ],
            [loads[i][0] - shift[a][0] for a, i in enumerate(outer)],
        )
    n = 2 * width
    a = matrix(n + 2, n + 2)
    if bending:
        a[0][1], a[1][2], a[2][3] = ONE, ONE / rigidity, ONE
        if shear:
            a[0][3] = -ONE / shear
        a[3][0], a[3][n] = -foundation, ONE
        displacement, force = [0, 1], [3, 2]
        signs_i, signs_j = [1, -1], [-1, 1]
    else:
        a[0][1], a[1][0], a[1][n] = ONE / rigidity, foundation, -ONE
        displacement, force = [0], [1]
        signs_i, signs_j = [-1], [1]
    a[n][n + 1] = ONE
    transition = exponential([[v * length for v in row] for row in a])
    t = [row[:n] for row in transition[:n]]
    offset = [
        row[n] * q[0] + row[n + 1] * (q[1] - q[0]) / length for row in transition[:n]
    ]
    identity = [[ONE if i == j else ZERO for j in range(n)] for i in range(n)]
    c = [identity[i] for i in displacement] + [t[i] for i in displacement]
    g = [[v * s for v in identity[i]] for i, s in zip(force, signs_i)] + [
        [v * s for v in t[i]] for i, s in zip(force, signs_j)
    ]
    k = transpose(solve(transpose(c), transpose(g)))
    h = [ZERO] * width + [offset[i] for i in displacement]
    f = [ZERO] * width + [offset[i] * s for i, s in zip(force, signs_j)]
    load = [sum(v * u for v, u in zip(row, h)) - value for row, value in zip(k, f)]
    k = [[(k[i][j] + k[j][i]) / 2 for j in range(n)] for i in range(n)]
    return k, load
