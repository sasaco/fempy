"""Physical line/triangle quadrature with componentwise global error budgets.

The callback supplies the entire integrand (e.g. shape times load plus force
and moment audit components). Weights already contain physical ds or dA;
callers must not multiply by a shell Jacobian again.
"""
from dataclasses import dataclass
from functools import lru_cache
from numbers import Integral

import numpy as np
from numpy.polynomial.legendre import leggauss

from .geometry import cross, points_array


@lru_cache(maxsize=32)
def _gauss(order):
    x, w = leggauss(order)
    return (x + 1) / 2, w / 2


def _order(order):
    if isinstance(order, bool) or not isinstance(order, Integral) or order < 1:
        raise ValueError('quadrature order must be a positive integer')


def line_rule(start, end, order=2):
    _order(order)
    a, b = points_array([start, end])
    length = np.linalg.norm(b - a)
    if not np.isfinite(length) or length <= 0:
        raise ValueError('quadrature: degenerate line')
    x, w = _gauss(order)
    return a + x[:, None] * (b - a), w * length


def triangle_rule(vertices, order=4):
    """Duffy tensor Gauss, exact through total polynomial degree 2*order-2."""
    _order(order)
    p = points_array(vertices)
    if len(p) != 3:
        raise ValueError('quadrature: expected a triangle')
    a, b, c = p
    jacobian = abs(cross(b - a, c - a))
    if not np.isfinite(jacobian) or jacobian <= 0:
        raise ValueError('quadrature: degenerate triangle')
    x, w = _gauss(order)
    u, v = np.meshgrid(x, x, indexing='ij')
    weights = w[:, None] * w[None, :] * (1 - u) * jacobian
    points = a + u[..., None] * (b - a) + ((1-u)*v)[..., None] * (c - a)
    return points.reshape((-1, 2)), weights.ravel()


@dataclass(frozen=True)
class IntegralResult:
    value: np.ndarray
    estimated_error: np.ndarray
    absolute_integral: np.ndarray
    evaluations: int
    subdivisions: int


def _evaluate(function, rule, label):
    points, weights = rule
    values = np.asarray([function(point) for point in points], dtype=float)
    if not np.all(np.isfinite(values)):
        raise ValueError(f'{label}: non-finite quadrature integrand')
    result = np.tensordot(weights, values, axes=(0, 0))
    magnitude = np.tensordot(weights, np.abs(values), axes=(0, 0))
    if not np.all(np.isfinite(result)) or not np.all(np.isfinite(magnitude)):
        raise ValueError(f'{label}: quadrature overflow')
    return result, magnitude


def _children(domain, kind):
    if kind == 'line':
        a, b = domain
        mid = (a + b) / 2
        return ((a, mid), (mid, b))
    a, b, c = domain
    ab, bc, ca = (a+b)/2, (b+c)/2, (c+a)/2
    return ((a, ab, ca), (ab, b, bc), (ca, bc, c), (ab, bc, ca))


def _integrate(function, domains, kind, *, degree, atol, rtol, max_depth, label):
    if not isinstance(max_depth, Integral) or isinstance(max_depth, bool) or max_depth < 0:
        raise ValueError(f'{label}: max_depth must be a nonnegative integer')
    absolute, relative = np.asarray(atol, dtype=float), np.asarray(rtol, dtype=float)
    if (not np.all(np.isfinite(absolute)) or not np.all(np.isfinite(relative))
            or np.any(absolute < 0) or np.any(relative < 0)
            or np.any((absolute == 0) & (relative == 0))):
        raise ValueError(f'{label}: quadrature tolerances must be finite, nonnegative and nonzero')
    if degree is not None and (isinstance(degree, bool) or not isinstance(degree, Integral) or degree < 0):
        raise ValueError(f'{label}: polynomial degree must be a nonnegative integer')
    domains = [points_array(d) for d in domains]
    if not domains:
        raise ValueError(f'{label}: empty integration domain')
    rule = (lambda d, n: line_rule(*d, n)) if kind == 'line' else triangle_rule
    if degree is not None:
        order = max(1, (degree + (2 if kind == 'line' else 3)) // 2)
        results = [_evaluate(function, rule(d, order), label) for d in domains]
        value = np.sum([r[0] for r in results], axis=0)
        magnitude = np.sum([r[1] for r in results], axis=0)
        if not np.all(np.isfinite(value)) or not np.all(np.isfinite(magnitude)):
            raise ValueError(f'{label}: summed quadrature overflow')
        return IntegralResult(value, np.zeros_like(value), magnitude,
                              len(domains) * order**(1 if kind == 'line' else 2), 0)
    evaluations, subdivisions = 0, 0

    def estimate(domain, depth):
        nonlocal evaluations
        low, _ = _evaluate(function, rule(domain, 4), label)
        high, magnitude = _evaluate(function, rule(domain, 8), label)
        evaluations += 12 if kind == 'line' else 80
        return (domain, depth, high, np.abs(high - low), magnitude)

    leaves = [estimate(d, 0) for d in domains]
    while True:
        value = np.sum([leaf[2] for leaf in leaves], axis=0)
        error = np.sum([leaf[3] for leaf in leaves], axis=0)
        magnitude = np.sum([leaf[4] for leaf in leaves], axis=0)
        if not all(np.all(np.isfinite(v)) for v in (value, error, magnitude)):
            raise ValueError(f'{label}: summed quadrature overflow')
        # The error is summed across ALL pieces before acceptance. Each
        # component has its own units. Integral(abs(f)) prevents cancellation
        # of positive and negative loads from collapsing the relative scale.
        budget = absolute + relative * magnitude
        if budget.shape != np.shape(value) and budget.shape != ():
            raise ValueError(f'{label}: tolerance shape differs from integrand')
        if np.all(error <= budget):
            return IntegralResult(value, error, magnitude, evaluations, subdivisions)
        candidates = [i for i, leaf in enumerate(leaves) if leaf[1] < max_depth]
        if not candidates:
            raise ValueError(f'{label}: quadrature did not converge at subdivision limit {max_depth}')
        tiny = np.finfo(float).tiny
        index = max(candidates, key=lambda i: float(np.max(leaves[i][3] / np.maximum(budget, tiny))))
        domain, depth, _, _, _ = leaves.pop(index)
        leaves.extend(estimate(child, depth + 1) for child in _children(domain, kind))
        subdivisions += 1


def integrate_lines(function, segments, *, degree=None, atol=1e-10, rtol=1e-9,
                    max_depth=10, label='line load'):
    """Integrate all segments against one shared componentwise error budget."""
    return _integrate(function, segments, 'line', degree=degree, atol=atol, rtol=rtol,
                      max_depth=max_depth, label=label)


def integrate_triangles(function, triangles, *, degree=None, atol=1e-10, rtol=1e-9,
                        max_depth=8, label='area load'):
    """Integrate all triangles against one shared componentwise error budget."""
    return _integrate(function, triangles, 'triangle', degree=degree, atol=atol, rtol=rtol,
                      max_depth=max_depth, label=label)
