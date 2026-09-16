"""Exact rational MITC4 plate energy on the unit square, independent of src.

Unknowns at each corner are w, rx, ry. Physical membrane/drill fields vanish
under this transverse load. Integrate polynomials exactly, then solve the nine
free unknowns with rational elimination. No stored/production displacement use.
"""

import sympy as s


def reference():
    x, y = s.symbols("x y")
    R = s.Rational
    E, nu, t, p = s.Integer(205000000000), R(3, 10), R(1, 100), s.Integer(1000)
    G = E / (2 * (1 + nu))
    N = s.Matrix([(1 - x) * (1 - y), x * (1 - y), x * y, (1 - x) * y])
    b, shear = s.zeros(3, 12), s.zeros(2, 12)
    for i, n in enumerate(N):
        dx, dy = n.diff(x), n.diff(y)
        b[0, 3 * i + 2], b[1, 3 * i + 1] = dx, -dy
        b[2, 3 * i + 1], b[2, 3 * i + 2] = -dx, dy
        shear[0, 3 * i], shear[0, 3 * i + 2] = dx, n
        shear[1, 3 * i], shear[1, 3 * i + 1] = dy, -n
    tied = s.zeros(2, 12)
    tied[0, :] = (1 - y) * shear[0, :].subs({x: R(1, 2), y: 0}) + y * shear[0, :].subs({x: R(1, 2), y: 1})
    tied[1, :] = (1 - x) * shear[1, :].subs({x: 0, y: R(1, 2)}) + x * shear[1, :].subs({x: 1, y: R(1, 2)})
    D = E / (1 - nu**2) * s.Matrix([[1, nu, 0], [nu, 1, 0], [0, 0, (1 - nu) / 2]])

    def integral(v):
        return s.integrate(s.integrate(s.expand(v), (x, 0, 1)), (y, 0, 1))

    K = (t**3 / 12 * b.T * D * b + R(5, 6) * G * t * tied.T * tied).applyfunc(integral)
    F = s.zeros(12, 1)
    for i, n in enumerate(N):
        F[3 * i] = -p * integral(n)
    u = s.zeros(12, 1)
    u[3:, :] = K[3:, 3:].inv() * F[3:, :]
    assert K[3:, :] * u == F[3:, :]
    reactions = K * u - F
    assert list(reactions[:3]) == [1000, 500, -500]
    disg = {
        str(i + 1): dict(
            zip(("dx", "dy", "dz", "rx", "ry", "rz"), [0.0, 0.0, *map(float, u[3 * i : 3 * i + 3]), 0.0])
        )
        for i in range(4)
    }
    curvature, gamma = b * u, tied * u
    raw = {}
    vertices = [(0, 0), (1, 0), (1, 1), (0, 1)]
    for side, sign in ((1, 1), (2, -1)):
        strain = sign * t / 2 * curvature
        stress = D * strain
        tau = R(5, 6) * G * gamma
        eps = [strain[0], strain[1], s.Integer(0), strain[2], gamma[1], gamma[0]]
        sig = [stress[0], stress[1], s.Integer(0), stress[2], tau[1], tau[0]]
        energy = sum(a * c for a, c in zip(eps, sig)) / 2
        for name, vector in [("Strain", eps), ("Stress", sig)]:
            raw[f"node{name}{side}"] = [
                [float(v.subs({x: xi, y: yi})) for v in vector] for xi, yi in vertices
            ]
            raw[f"elem{name}{side}"] = [float(integral(v)) for v in vector]
        raw[f"nodeEnergy{side}"] = [float(energy.subs({x: xi, y: yi})) for xi, yi in vertices]
        raw[f"elemEnergy{side}"] = float(integral(energy))
    output = dict(
        raw_result=raw,
        strain_energy=raw["elemEnergy1"],
        stress=[
            dict(zip(("mx", "my", "mxy", "qx", "qy"), [v[0], v[1], v[3], v[4], v[5]]))
            for v in raw["nodeStress1"]
        ],
        strain=[dict(zip(("ex", "ey", "exy"), [v[0], v[1], v[3]])) for v in raw["nodeStrain1"]],
    )
    return dict(
        disg=disg,
        reac={"1": dict(tx=0.0, ty=0.0, tz=1000.0, mx=500.0, my=-500.0, mz=0.0)},
        size=4,
        fsec={},
        shell_results={"0": output},
    )
