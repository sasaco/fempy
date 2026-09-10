"""Immutable Nd-dependent JR skeletons (manual 7.20).

This module defines and differentiates the table; it does not advance loading
history. Nd is compression-positive. Interpolate corresponding points before
forming slopes. Row boundaries use the interval to the right, except at the
last row, where the left derivative is returned.
"""
from bisect import bisect_right
from dataclasses import dataclass
from fractions import Fraction
from math import isfinite
from numbers import Real

from ..diagnostics import InputValidationError, NumericalConditionError
from .hysteresis import JRStiffnessReductionParams


def _number(value, field):
    if isinstance(value, bool) or not isinstance(value, Real) or not isfinite(value):
        raise InputValidationError(f'{field} must be finite', field=field)
    return float(value)


@dataclass(frozen=True)
class SkeletonPoints:
    """Positive magnitudes, or their Nd derivatives, as owned immutable tuples."""
    curvatures: tuple[float, ...]
    moments: tuple[float, ...]

    def __post_init__(self):
        object.__setattr__(self, 'curvatures', tuple(_number(v, 'curvature') for v in self.curvatures))
        object.__setattr__(self, 'moments', tuple(_number(v, 'moment') for v in self.moments))
        if len(self.curvatures) not in (3, 4) or len(self.moments) != len(self.curvatures):
            raise InputValidationError('A skeleton side requires three or four paired points')

    @property
    def slopes(self):
        d, p = (0., *self.curvatures), (0., *self.moments)
        slopes = tuple((p[i+1]-p[i])/(d[i+1]-d[i]) for i in range(len(self.curvatures)))
        return slopes if len(slopes) == 4 else (*slopes, 0.)

    def slope_derivatives(self, derivative):
        d = (0., *self.curvatures)
        dd, dp = (0., *derivative.curvatures), (0., *derivative.moments)
        slopes = self.slopes
        values = tuple(((dp[i+1]-dp[i])-slopes[i]*(dd[i+1]-dd[i]))/(d[i+1]-d[i])
                       for i in range(len(self.curvatures)))
        return values if len(values) == 4 else (*values, 0.)


@dataclass(frozen=True)
class AxialForceRow:
    Nd: float
    positive: SkeletonPoints
    negative: SkeletonPoints

    def __post_init__(self):
        object.__setattr__(self, 'Nd', _number(self.Nd, 'Nd'))
        if not all(isinstance(v, SkeletonPoints) for v in (self.positive, self.negative)):
            raise InputValidationError('Each row requires immutable positive and negative points')


@dataclass(frozen=True)
class InterpolatedSkeleton:
    Nd: float
    lower_Nd: float
    upper_Nd: float
    fraction: float
    positive: SkeletonPoints
    negative: SkeletonPoints
    positive_derivative: SkeletonPoints
    negative_derivative: SkeletonPoints
    beta: float
    K_min: float

    @property
    def positive_slope_derivatives(self):
        return self.positive.slope_derivatives(self.positive_derivative)

    @property
    def negative_slope_derivatives(self):
        return self.negative.slope_derivatives(self.negative_derivative)

    def to_jr_params(self):
        """Fresh mutable adapter for existing fixed-JR operations, never shared history."""
        fields = dict(beta=self.beta, K_min=self.K_min)
        for suffix, points in [('pos', self.positive), ('neg', self.negative)]:
            for i, (d, p) in enumerate(zip(points.curvatures, points.moments), 1):
                fields[f'delta_{i}_{suffix}'], fields[f'P_{i}_{suffix}'] = d, p
        return JRStiffnessReductionParams(**fields)


@dataclass(frozen=True)
class SkeletonResponse:
    moment: float
    bending_tangent: float
    moment_Nd_derivative: float
    side: int
    segment: int


def _validate_side(points):
    d, p = points.curvatures, points.moments
    if not 0 < d[0] < d[1] < d[2] or not 0 < p[0] <= p[1] <= p[2]:
        raise InputValidationError('Require ordered positive JR points')
    if len(d) == 4 and not (d[3] > d[2] and 0 <= p[3] < p[2]):
        raise InputValidationError('Require delta_4 > delta_3 and 0 <= P_4 < P_3')
    if not all(isfinite(k) for k in points.slopes):
        raise InputValidationError('JR slopes must be finite')


def _interval_order(left, right, *, lower, upper, side):
    """Exact quadratic minima after clearing strictly positive denominators.

    Binary input floats are converted exactly to rationals for this input-only
    check. It therefore needs neither a sampling grid nor a dimensional floor,
    including when equal endpoint slopes mask a narrow interior violation.
    """
    def widths(values):
        items = [Fraction(0), *(Fraction(v) for v in values[:3])]
        return [b-a for a, b in zip(items[:-1], items[1:])]

    dl, dr = widths(left.curvatures), widths(right.curvatures)
    pl, pr = widths(left.moments), widths(right.moments)
    d = [(a, b-a) for a, b in zip(dl, dr)]
    p = [(a, b-a) for a, b in zip(pl, pr)]

    def product(a, b):
        return (a[0]*b[0], a[0]*b[1]+a[1]*b[0], a[1]*b[1])

    for i in (0, 1):
        c, b, a = [x-y for x, y in zip(product(p[i], d[i+1]), product(p[i+1], d[i]))]
        candidates = [Fraction(0), Fraction(1)]
        if a > 0 and 0 < -b < 2*a:
            candidates.append(-b/(2*a))
        for fraction in candidates:
            if c+fraction*(b+fraction*a) < 0:
                raise InputValidationError(
                    'Require K1 >= K2 >= K3 throughout every Nd interval',
                    reason='axial_force_slope_order', range=[lower, upper], side=side,
                    slope_pair=[i+1, i+2], fraction=float(fraction))


@dataclass(frozen=True)
class AxialForceTable:
    rows: tuple[AxialForceRow, ...]
    beta: float = 0.4
    K_min: float | None = None

    def __post_init__(self):
        rows = tuple(self.rows)
        object.__setattr__(self, 'rows', rows)
        if len(rows) < 2 or not all(isinstance(row, AxialForceRow) for row in rows):
            raise InputValidationError('An Nd table requires at least two AxialForceRows')
        beta = _number(self.beta, 'beta')
        if beta < 0:
            raise InputValidationError('beta must be nonnegative', field='beta')
        object.__setattr__(self, 'beta', beta)
        if any(a.Nd >= b.Nd for a, b in zip(rows[:-1], rows[1:])):
            raise InputValidationError('Nd rows must be strictly increasing')
        if not rows[0].Nd <= 0 <= rows[-1].Nd:
            raise InputValidationError('Nd table must include zero in its range')
        for row in rows:
            for name in ('positive', 'negative'):
                points = getattr(row, name)
                _validate_side(points)
                if len(points.curvatures) != len(getattr(rows[0], name).curvatures):
                    raise InputValidationError('Fourth-point presence must agree across rows', side=name)
        for left, right in zip(rows[:-1], rows[1:]):
            if not isfinite(right.Nd-left.Nd):
                raise InputValidationError('Nd interval width must be finite')
            for side in ('positive', 'negative'):
                _interval_order(getattr(left, side), getattr(right, side),
                                lower=left.Nd, upper=right.Nd, side=side)
        upper = min(points.slopes[0] for row in rows for points in (row.positive, row.negative))
        floor = .01*upper if self.K_min is None else _number(self.K_min, 'K_min')
        if not 0 < floor <= upper:
            raise InputValidationError('Require 0 < K_min <= minimum K1 over all rows and sides')
        object.__setattr__(self, 'K_min', floor)

    @classmethod
    def from_dict(cls, definition):
        if not isinstance(definition, dict):
            raise InputValidationError('Nd law must be an object')
        allowed = {'type', 'symmetric', 'beta', 'K_min', 'axial_force_points'}
        if set(definition)-allowed:
            raise InputValidationError('Unknown Nd law fields', fields=sorted(set(definition)-allowed))
        if definition.get('type', 'jr_stiffness_reduction') != 'jr_stiffness_reduction':
            raise InputValidationError('Unsupported Nd hysteresis type')
        symmetric = definition.get('symmetric', True)
        if not isinstance(symmetric, bool):
            raise InputValidationError('symmetric must be boolean')
        items = definition.get('axial_force_points')
        if not isinstance(items, (list, tuple)):
            raise InputValidationError('axial_force_points must be an array')
        rows = []
        for index, item in enumerate(items):
            if not isinstance(item, dict):
                raise InputValidationError('Nd row must be an object', row=index)
            allowed = {'Nd'} | {f'{name}_{i}{suffix}' for name in ('delta', 'P')
                                for i in (1, 2, 3, 4) for suffix in ('', '_neg')}
            if set(item)-allowed or (symmetric and any(k.endswith('_neg') for k in item)):
                raise InputValidationError('Unknown or conflicting Nd row fields', row=index)

            def points(suffix):
                if (f'delta_4{suffix}' in item) != (f'P_4{suffix}' in item):
                    raise InputValidationError('Fourth-point curvature and moment must be paired', row=index)
                count = 4 if f'delta_4{suffix}' in item else 3
                return SkeletonPoints(tuple(item[f'delta_{i}{suffix}'] for i in range(1, count+1)),
                                      tuple(item[f'P_{i}{suffix}'] for i in range(1, count+1)))
            try:
                positive = points('')
                rows.append(AxialForceRow(item['Nd'], positive, positive if symmetric else points('_neg')))
            except KeyError as error:
                raise InputValidationError('Missing Nd row field', row=index, field=error.args[0]) from error
        return cls(tuple(rows), beta=definition.get('beta', .4), K_min=definition.get('K_min'))

    def to_dict(self):
        symmetric = all(row.positive == row.negative for row in self.rows)
        result = dict(type='jr_stiffness_reduction', symmetric=symmetric,
                      beta=self.beta, K_min=self.K_min, axial_force_points=[])
        for row in self.rows:
            item = dict(Nd=row.Nd)
            for suffix, points in [('', row.positive), *([] if symmetric else [('_neg', row.negative)])]:
                for i, (d, p) in enumerate(zip(points.curvatures, points.moments), 1):
                    item[f'delta_{i}{suffix}'], item[f'P_{i}{suffix}'] = d, p
            result['axial_force_points'].append(item)
        return result

    def interpolate(self, Nd):
        Nd = _number(Nd, 'Nd')
        lower, upper = self.rows[0].Nd, self.rows[-1].Nd
        if not lower <= Nd <= upper:
            raise InputValidationError('Trial Nd is outside the skeleton table',
                                       reason='axial_force_out_of_range', Nd=Nd, range=[lower, upper])
        index = min(bisect_right([row.Nd for row in self.rows], Nd)-1, len(self.rows)-2)
        left, right = self.rows[index:index+2]
        width = right.Nd-left.Nd
        fraction = (Nd-left.Nd)/width

        def interpolate_side(a, b):
            values, derivatives = [], []
            for name in ('curvatures', 'moments'):
                va, vb = getattr(a, name), getattr(b, name)
                values.append(tuple(x if x == y or Nd == left.Nd else y if Nd == right.Nd else
                                    (1-fraction)*x+fraction*y for x, y in zip(va, vb)))
                derivatives.append(tuple((y-x)/width for x, y in zip(va, vb)))
            return SkeletonPoints(*values), SkeletonPoints(*derivatives)

        positive, dp = interpolate_side(left.positive, right.positive)
        negative, dn = interpolate_side(left.negative, right.negative)
        return InterpolatedSkeleton(Nd, left.Nd, right.Nd, fraction, positive, negative, dp, dn,
                                    self.beta, self.K_min)

    def evaluate_skeleton(self, curvature, Nd, *, side=None):
        curvature = _number(curvature, 'curvature')
        if side is None:
            side = 1 if curvature >= 0 else -1
        if isinstance(side, bool) or side not in (-1, 1) or side*curvature < 0:
            raise InputValidationError('Skeleton side must match curvature')
        curve = self.interpolate(Nd)
        points = curve.positive if side > 0 else curve.negative
        derivative = curve.positive_derivative if side > 0 else curve.negative_derivative
        magnitude = side*curvature
        index = bisect_right(points.curvatures[:3], magnitude)
        slope = points.slopes[index]
        d, p = (0., 0.) if index == 0 else (points.curvatures[index-1], points.moments[index-1])
        dd, dp = (0., 0.) if index == 0 else (derivative.curvatures[index-1], derivative.moments[index-1])
        moment = side*(p+slope*(magnitude-d))
        sensitivity = side*(dp-slope*dd+points.slope_derivatives(derivative)[index]*(magnitude-d))
        if not all(isfinite(v) for v in (moment, slope, sensitivity)):
            raise NumericalConditionError('Nonfinite Nd skeleton response', reason='nonfinite_section_response')
        return SkeletonResponse(moment, slope, sensitivity, side, index+1)
