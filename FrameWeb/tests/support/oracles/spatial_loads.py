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


def rectangle_t3_loads(width, height, coefficients):
    """Analytic integrals on the two triangles separated by y=h*x/w."""
    x, y = sp.symbols('x y')
    a, b, c, d = map(sp.Rational, coefficients)
    w, h = sp.Rational(width), sp.Rational(height)
    p = a + b*x + c*y + d*x*y
    lower = (1-x/w, x/w-y/h, y/h, sp.S.Zero)
    upper = (1-y/h, sp.S.Zero, x/w, y/h-x/w)
    return np.array([float(sp.integrate(n*p, (y, 0, h*x/w), (x, 0, w))
                           + sp.integrate(m*p, (y, h*x/w, h), (x, 0, w)))
                     for n, m in zip(lower, upper)])


def rectangle_distribution(width, height, coefficients):
    """Resultant and global-origin moment from distribution alone."""
    x, y = sp.symbols('x y')
    a, b, c, d = map(sp.Rational, coefficients)
    p = a + b*x + c*y + d*x*y
    values = [float(sp.integrate(f*p, (x, 0, width), (y, 0, height))) for f in (1, y, -x)]
    return np.array([0., 0., values[0]]), np.array([values[1], values[2], 0.])


def square_strip_loads(*, shell, area):
    """Independent integration of the legacy builder's clipped line/band."""
    x, y = sp.symbols('x y')
    q4 = ((1-x/2)*(1-y/2), x/2*(1-y/2), x*y/4, (1-x/2)*y/2)
    lower = q4 if shell else (1-x/2, x/2-y/2, y/2, sp.S.Zero)
    upper = q4 if shell else (1-y/2, sp.S.Zero, x/2, y/2-x/2)
    result = []
    for n, m in zip(lower, upper):
        if area:
            p = 5*x + 20*y
            value = sp.integrate(sp.integrate(m*p, (x, 0, y))
                                 + sp.integrate(n*p, (x, y, 2)), (y, sp.Rational(1, 2), sp.Rational(3, 2)))
        else:
            p = 10 + 5*x
            value = sp.integrate((m*p).subs(y, sp.Rational(1, 2)), (x, 0, sp.Rational(1, 2)))
            value += sp.integrate((n*p).subs(y, sp.Rational(1, 2)), (x, sp.Rational(1, 2), 2))
        result.append(float(value))
    return np.array(result)


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


def uniform_cylindrical_plate(length, intensity, young, thickness, *, simply_supported, mindlin):
    """nu=0 plate strip: bending D=Et^3/12, shear stiffness (5/6)Gt.

    From equilibrium V'=q and integration w_s'=V/((5/6)Gt).
    This reference imports no production element or solver.
    """
    rigidity = young*thickness**3/12
    bending = (simply_supported_uniform(length, intensity, rigidity)[-1]
               if simply_supported else cantilever_uniform(length, intensity, rigidity)[-1])
    shear = intensity*length**2/((8 if simply_supported else 2)*(5/6)*(young/2)*thickness)
    return bending + (shear if mindlin else 0.)
