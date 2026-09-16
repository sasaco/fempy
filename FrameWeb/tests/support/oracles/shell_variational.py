"""Independent 65-digit shell energy and surface tensors.

DKT rotations are derived from cubic Hermite edge slopes and quadratic
triangle interpolation. Quad shear is interpolated from four edge-midpoint
covariant strains. No product element, solver or output routine is imported.
"""

import mpmath as mp

from tests.support.oracles.plane_frame_series import number
from tests.support.oracles.space_frame_series import solve_sparse


def cross(a, b):
    return mp.matrix(
        [
            a[1] * b[2] - a[2] * b[1],
            a[2] * b[0] - a[0] * b[2],
            a[0] * b[1] - a[1] * b[0],
        ]
    )


def unit(v):
    return v / mp.sqrt(mp.fsum(x * x for x in v))


def operator(points, material, thickness):
    n = len(points)
    size = 6 * n
    ex = unit(points[1] - points[0])
    ez = unit(cross(ex, points[-1] - points[0]))
    ey = cross(ez, ex)
    basis = mp.matrix([list(ex), list(ey), list(ez)])
    coords = [basis * (point - points[0]) for point in points]
    assert all(abs(point[2]) < mp.mpf("1e-40") for point in coords)
    E, nu, G = map(number, [material["E"], material.get("nu", 0.3), material["G"]])
    t = number(thickness)
    elastic = (
        E / (1 - nu**2) * mp.matrix([[1, nu, 0], [nu, 1, 0], [0, 0, (1 - nu) / 2]])
    )
    transform = mp.matrix(size, size)
    for base in range(0, size, 3):
        for i in range(3):
            for j in range(3):
                transform[base + i, base + j] = basis[i, j]

    def shape(r, s):
        if n == 3:
            return [1 - r - s, r, s], mp.matrix([[-1, 1, 0], [-1, 0, 1]])
        values = []
        derivative = mp.matrix(2, 4)
        for i, (a, b) in enumerate([(-1, -1), (1, -1), (1, 1), (-1, 1)]):
            values.append((1 + a * r) * (1 + b * s) / 4)
            derivative[0, i] = a * (1 + b * s) / 4
            derivative[1, i] = b * (1 + a * r) / 4
        return values, derivative

    def geometry(r, s):
        values, natural = shape(r, s)
        jac = natural * mp.matrix([[p[0], p[1]] for p in coords])
        det = mp.det(jac)
        assert det > 0
        return values, jac**-1 * natural, jac, det

    if n == 3:
        vertex_gradients = []
        for i in range(3):
            g = mp.matrix(2, size)
            g[0, 6 * i + 4] = -1
            g[1, 6 * i + 3] = 1
            vertex_gradients.append(g)
        slope_nodes = vertex_gradients.copy()
        for i, j in [(0, 1), (1, 2), (2, 0)]:
            edge = coords[j] - coords[i]
            length = mp.sqrt(edge[0] ** 2 + edge[1] ** 2)
            tangent = mp.matrix([edge[0] / length, edge[1] / length])
            normal = mp.matrix([-tangent[1], tangent[0]])
            summed = vertex_gradients[i] + vertex_gradients[j]
            tangential = -mp.mpf(1) / 4 * tangent.T * summed
            tangential[0, 6 * j + 2] += mp.mpf(3) / (2 * length)
            tangential[0, 6 * i + 2] -= mp.mpf(3) / (2 * length)
            midpoint = tangent * tangential + normal * (normal.T * summed / 2)
            slope_nodes.append(midpoint)

    def raw_shear(r, s):
        values, gradient, jac, det = geometry(r, s)
        matrix = mp.matrix(2, size)
        for i in range(n):
            matrix[0, 6 * i + 2] = gradient[0, i]
            matrix[0, 6 * i + 4] = values[i]
            matrix[1, 6 * i + 2] = gradient[1, i]
            matrix[1, 6 * i + 3] = -values[i]
        return matrix, jac

    def fields(r, s):
        values, gradient, jac, det = geometry(r, s)
        membrane, bend, shear = (
            mp.matrix(3, size),
            mp.matrix(3, size),
            mp.matrix(2, size),
        )
        drill = mp.matrix(1, size)
        for i in range(n):
            dx, dy = gradient[0, i], gradient[1, i]
            membrane[0, 6 * i] = dx
            membrane[1, 6 * i + 1] = dy
            membrane[2, 6 * i] = dy
            membrane[2, 6 * i + 1] = dx
            drill[0, 6 * i] = dy / 2
            drill[0, 6 * i + 1] = -dx / 2
            drill[0, 6 * i + 5] = values[i]
            if n == 4:
                bend[0, 6 * i + 4] = dx
                bend[1, 6 * i + 3] = -dy
                bend[2, 6 * i + 4] = dy
                bend[2, 6 * i + 3] = -dx
        if n == 3:
            derivatives = [
                [(4 * values[i] - 1) * gradient[j, i] for j in range(2)]
                for i in range(3)
            ]
            derivatives += [
                [
                    4 * (values[i] * gradient[k, j] + values[j] * gradient[k, i])
                    for k in range(2)
                ]
                for i, j in [(0, 1), (1, 2), (2, 0)]
            ]
            dx, dy = mp.matrix(2, size), mp.matrix(2, size)
            for g, deriv in zip(slope_nodes, derivatives):
                dx += deriv[0] * g
                dy += deriv[1] * g
            for i in range(size):
                bend[0, i] = -dx[0, i]
                bend[1, i] = -dy[1, i]
                bend[2, i] = -dy[0, i] - dx[1, i]
        else:
            covariant = mp.matrix(2, size)
            for axis, positions, weights in [
                (0, [(0, -1), (0, 1)], [(1 - s) / 2, (1 + s) / 2]),
                (1, [(-1, 0), (1, 0)], [(1 - r) / 2, (1 + r) / 2]),
            ]:
                for point, weight in zip(positions, weights):
                    direct, j = raw_shear(*map(mp.mpf, point))
                    row = j * direct
                    for col in range(size):
                        covariant[axis, col] += weight * row[axis, col]
            shear = jac**-1 * covariant
        return membrane, bend, shear, drill, det

    if n == 3:
        quadrature = [
            (mp.mpf(1) / 6, mp.mpf(1) / 6, mp.mpf(1) / 6),
            (mp.mpf(2) / 3, mp.mpf(1) / 6, mp.mpf(1) / 6),
            (mp.mpf(1) / 6, mp.mpf(2) / 3, mp.mpf(1) / 6),
        ]
        vertices = [(0, 0), (1, 0), (0, 1)]
    else:
        v = 1 / mp.sqrt(3)
        quadrature = [
            (r, s, mp.mpf(1)) for r, s in [(-v, -v), (v, -v), (-v, v), (v, v)]
        ]
        vertices = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
    stiffness = mp.matrix(size, size)
    for r, s, weight in quadrature:
        membrane, bend, shear, drill, det = fields(r, s)
        stiffness += (
            det
            * weight
            * (
                t * membrane.T * elastic * membrane
                + t**3 / 12 * bend.T * elastic * bend
                + mp.mpf(5) / 6 * G * t * shear.T * shear
                + G * t / 1000 * drill.T * drill
            )
        )

    def response(global_u):
        u = transform * mp.matrix(global_u)

        def surface(r, s, sign):
            membrane, bend, shear, _, _ = fields(r, s)
            strain = (membrane + sign * t / 2 * bend) * u
            stress = elastic * strain
            gamma = shear * u
            tau = mp.mpf(5) / 6 * G * gamma
            eps = mp.matrix(
                [
                    [strain[0], strain[2] / 2, gamma[0] / 2],
                    [strain[2] / 2, strain[1], gamma[1] / 2],
                    [gamma[0] / 2, gamma[1] / 2, 0],
                ]
            )
            sigma = mp.matrix(
                [
                    [stress[0], stress[2], tau[0]],
                    [stress[2], stress[1], tau[1]],
                    [tau[0], tau[1], 0],
                ]
            )
            energy = (
                mp.fsum(eps[i, j] * sigma[i, j] for i in range(3) for j in range(3)) / 2
            )
            eps = basis.T * eps * basis
            sigma = basis.T * sigma * basis
            indices = [(0, 0), (1, 1), (2, 2), (0, 1), (1, 2), (2, 0)]
            return (
                [eps[i, j] * (1 if i == j else 2) for i, j in indices],
                [sigma[i, j] for i, j in indices],
                energy,
            )

        raw = {}
        for side, sign in [(1, 1), (2, -1)]:
            nodal = [surface(mp.mpf(r), mp.mpf(s), sign) for r, s in vertices]
            interior = [surface(r, s, sign) for r, s, _ in quadrature]
            for component, name in enumerate(["Strain", "Stress", "Energy"]):
                raw["node" + name + str(side)] = [
                    [float(v) for v in row[component]]
                    if component < 2
                    else float(row[component])
                    for row in nodal
                ]
                raw["elem" + name + str(side)] = (
                    [
                        float(
                            mp.fsum(row[component][j] for row in interior)
                            / len(interior)
                        )
                        for j in range(6)
                    ]
                    if component < 2
                    else float(
                        mp.fsum(row[component] for row in interior) / len(interior)
                    )
                )
        return dict(
            raw_result=raw,
            strain_energy=raw["elemEnergy1"],
            stress=[
                dict(
                    zip(("mx", "my", "mxy", "qx", "qy"), [v[0], v[1], v[3], v[4], v[5]])
                )
                for v in raw["nodeStress1"]
            ],
            strain=[
                dict(zip(("ex", "ey", "exy"), [v[0], v[1], v[3]]))
                for v in raw["nodeStrain1"]
            ],
        )

    return transform.T * stiffness * transform, response


def solve_reference(data, case_id="1"):
    assert not data.get("member") and not data.get("solid")
    with mp.workdps(65):
        case = data["load"][case_id]
        materials = data["element"][str(case.get("element", 1))]
        supports = data["fix_node"][str(case.get("fix_node", 1))]
        assert not case.get("load_member") and not case.get("load_shell")
        nodes = list(data["node"])
        index = {node: 6 * i for i, node in enumerate(nodes)}
        coords = {
            node: mp.matrix([number(data["node"][node][key]) for key in "xyz"])
            for node in nodes
        }
        rows = [{} for _ in range(6 * len(nodes))]
        F = mp.matrix(len(rows), 1)
        operators = []
        for shell in data["shell"].values():
            ids = [str(node) for node in shell["nodes"]]
            material = materials[str(shell["e"])]
            k, response = operator(
                [coords[node] for node in ids],
                material,
                material.get("thickness", material["A"]),
            )
            dofs = [index[node] + j for node in ids for j in range(6)]
            for i, row in enumerate(dofs):
                for j, col in enumerate(dofs):
                    if k[i, j]:
                        rows[row][col] = rows[row].get(col, mp.mpf(0)) + k[i, j]
            operators.append((dofs, response))
        for row in case.get("load_node", []):
            for j, key in enumerate(["tx", "ty", "tz", "rx", "ry", "rz"]):
                F[index[str(row["n"])] + j] += number(row.get(key, 0))
        fixed = set()
        for row in supports:
            for j, key in enumerate(["tx", "ty", "tz", "rx", "ry", "rz"]):
                value = row.get(key, 0)
                assert value in (0, 1)
                if value:
                    fixed.add(index[str(row["n"])] + j)
        u = solve_sparse(rows, F, fixed)
        residual = [
            mp.fsum(value * u[j] for j, value in row.items()) - F[i]
            for i, row in enumerate(rows)
        ]
        displacement = {
            node: dict(
                zip(
                    ("dx", "dy", "dz", "rx", "ry", "rz"),
                    map(float, u[index[node] : index[node] + 6, 0]),
                )
            )
            for node in nodes
        }
        reactions = {
            str(row["n"]): dict(
                zip(
                    ("tx", "ty", "tz", "mx", "my", "mz"),
                    [
                        float(residual[index[str(row["n"])] + j])
                        if index[str(row["n"])] + j in fixed
                        else 0.0
                        for j in range(6)
                    ],
                )
            )
            for row in supports
        }
        shells = {
            str(i): response([u[dof] for dof in dofs])
            for i, (dofs, response) in enumerate(operators)
        }
        return dict(
            disg=displacement,
            reac=reactions,
            fsec={},
            size=len(nodes),
            shell_results=shells,
        )
