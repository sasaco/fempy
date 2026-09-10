"""Independent exact polynomial integrals and physical-domain references.

No production geometry, interpolation, quadrature, or solver is imported.
SymPy rational integration is used for polynomial references. QUADPACK is
used on an explicitly parameterized trapezoid for non-polynomial references.
"""
from math import factorial

import numpy as np
from scipy.integrate import quad
import sympy as sp


def unit_triangle_monomial(px, py):
    return factorial(px) * factorial(py) / factorial(px + py + 2)


def rectangle_q4_loads(width, height, coefficients):
    x, y = sp.symbols('x y')
    a, b, c, d = map(sp.Rational, coefficients)
    w, h = sp.Rational(width), sp.Rational(height)
    pressure = a + b*x + c*y + d*x*y
    shapes = ((1-x/w)*(1-y/h), x/w*(1-y/h), x*y/(w*h), (1-x/w)*y/h)
    return np.array([float(sp.integrate(n * pressure, (x, 0, w), (y, 0, h))) for n in shapes])


def trapezoid_reference(*, q4):
    """x=(2+y)*u, 0<=u,y<=1, p=1+2*u+3*y+4*u*y.

    Q4 basis is explicit in (u,y). T3 basis belongs to the enclosing triangle
    (0,0),(6,0),(0,3), whose hypotenuse contains trapezoid corner (3,1.5),
    and includes the complete trapezoid (0,0),(2,0),(3,1),(0,1).
    The reference integrates in native (u,y), while production tests clip
    physical triangles and inverse-map every physical point.
    """
    def component(u, y, i):
        x = (2+y)*u
        p = 1+2*u+3*y+4*u*y
        shapes = ((1-u)*(1-y), u*(1-y), u*y, (1-u)*y) if q4 else (1-x/6-y/3, x/6, y/3)
        return shapes[i] * p * (2+y)
    return np.array([quad(lambda y: quad(lambda u: component(u, y, i), 0, 1,
                                        epsabs=1e-12, epsrel=1e-12)[0],
                          0, 1, epsabs=1e-12, epsrel=1e-12)[0]
                     for i in range(4 if q4 else 3)])


def trapezoid_diagonal_line():
    t = sp.symbols('t', nonnegative=True)
    u = 3*t / (2+t)
    shapes = ((1-u)*(1-t), u*(1-t), u*t, (1-u)*t)
    return np.array([float(sp.integrate(n*(1+2*t)*sp.sqrt(10), (t, 0, 1))) for n in shapes])


def simply_supported_uniform(length, intensity, rigidity):
    return intensity * length / 2, 5 * intensity * length**4 / (384 * rigidity)


def cantilever_uniform(length, intensity, rigidity):
    return intensity * length, intensity * length**2 / 2, intensity * length**4 / (8 * rigidity)
