"""MITC4 stiffness and rounding tail at 50 Decimal digits."""

from decimal import Decimal as D
from decimal import localcontext
from functools import lru_cache

import numpy as np


@lru_cache(maxsize=128)
def stiffness_parts(coordinates, young, poisson, shear_modulus, thickness):
    with localcontext() as ctx:
        ctx.prec = 50
        xyz = np.array(coordinates, dtype=object)

        def unit(v):
            return v / sum(v * v).sqrt()

        ex = unit(xyz[1])
        ez = unit(np.cross(xyz[1], xyz[3]))
        ey = np.cross(ez, ex)
        basis = np.array([ex, ey, ez])
        coords = (xyz @ basis.T)[:, :2]
        E, nu, G, t = (
            D.from_float(float(v)) for v in (young, poisson, shear_modulus, thickness)
        )
        elastic = (
            E
            / (1 - nu * nu)
            * np.array([[1, nu, 0], [nu, 1, 0], [0, 0, (1 - nu) / 2]], dtype=object)
        )

        def geometry(r, s):
            values = np.array(
                [
                    (1 + a * r) * (1 + b * s) / 4
                    for a, b in [(-1, -1), (1, -1), (1, 1), (-1, 1)]
                ],
                dtype=object,
            )
            natural = np.array(
                [
                    [
                        a * (1 + b * s) / 4
                        for a, b in [(-1, -1), (1, -1), (1, 1), (-1, 1)]
                    ],
                    [
                        b * (1 + a * r) / 4
                        for a, b in [(-1, -1), (1, -1), (1, 1), (-1, 1)]
                    ],
                ],
                dtype=object,
            )
            jac = natural @ coords
            a, b = jac[0]
            c, d = jac[1]
            det = a * d - b * c
            if det <= 0:
                raise ValueError("Degenerate or inverted quadrilateral shell")
            inverse = np.array([[d, -b], [-c, a]], dtype=object) / det
            return values, inverse @ natural, jac, inverse, det

        def strains(r, s):
            values, gradient, jac, inverse, det = geometry(r, s)
            membrane = np.full((3, 24), D(0), dtype=object)
            bend = membrane.copy()
            shear = np.full((2, 24), D(0), dtype=object)
            drill = np.full(24, D(0), dtype=object)
            for i, (dx, dy) in enumerate(gradient.T):
                membrane[0, 6 * i] = dx
                membrane[1, 6 * i + 1] = dy
                membrane[2, 6 * i : 6 * i + 2] = [dy, dx]
                bend[0, 6 * i + 4] = dx
                bend[1, 6 * i + 3] = -dy
                bend[2, 6 * i + 3 : 6 * i + 5] = [-dx, dy]
                shear[0, 6 * i + 2] = dx
                shear[0, 6 * i + 4] = values[i]
                shear[1, 6 * i + 2] = dy
                shear[1, 6 * i + 3] = -values[i]
                drill[6 * i : 6 * i + 2] = [dy / 2, -dx / 2]
                drill[6 * i + 5] = values[i]
            return membrane, bend, shear, drill, jac, inverse, det

        stiffness = np.full((24, 24), D(0), dtype=object)
        root = (D(1) / 3).sqrt()
        tying = {
            point: strains(*map(D, point))
            for point in [(0, -1), (0, 1), (-1, 0), (1, 0)]
        }
        for r, s in [(-root, -root), (root, -root), (-root, root), (root, root)]:
            membrane, bend, _, drill, _, inverse, det = strains(r, s)
            covariant = np.full((2, 24), D(0), dtype=object)
            for axis, points, weights in [
                (0, [(0, -1), (0, 1)], [(1 - s) / 2, (1 + s) / 2]),
                (1, [(-1, 0), (1, 0)], [(1 - r) / 2, (1 + r) / 2]),
            ]:
                for point, weight in zip(points, weights):
                    _, _, shear, _, jac, _, _ = tying[point]
                    covariant[axis] += weight * (jac @ shear)[axis]
            shear = inverse @ covariant
            stiffness += det * (
                t * membrane.T @ elastic @ membrane
                + t**3 / 12 * bend.T @ elastic @ bend
                + D(5) / 6 * G * t * shear.T @ shear
                + G * t / 1000 * np.outer(drill, drill)
            )
        transform = np.kron(np.eye(8, dtype=int), basis)
        stiffness = transform.T @ stiffness @ transform
        high = np.array(stiffness, dtype=float)
        low = np.array(
            [
                [v - D.from_float(float(h)) for v, h in zip(row, hr)]
                for row, hr in zip(stiffness, high)
            ],
            dtype=float,
        )
        high.setflags(write=False)
        low.setflags(write=False)
        return high, low
