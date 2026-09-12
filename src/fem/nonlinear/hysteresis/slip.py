"""Symmetric grounded slip spring (theory manual 7.19).

Every evaluation follows a monotone path from an immutable committed state.
Positive force is an internal restoring force; the support reaction is -force.
"""
from dataclasses import dataclass, replace
from math import isfinite, copysign, ulp
from numbers import Real


def _finite_number(value, name):
    if isinstance(value, bool) or not isinstance(value, Real) or not isfinite(value):
        raise ValueError(f'{name} must be a finite number (not bool)')
    return float(value)


@dataclass(frozen=True)
class SlipSpringParams:
    K1: float
    K2: float
    delta_1: float

    def __post_init__(self):
        for name in ('K1', 'K2', 'delta_1'):
            object.__setattr__(self, name, _finite_number(getattr(self, name), name))
        if self.K1 <= 0 or self.delta_1 <= 0 or not 0 <= self.K2 <= self.K1:
            raise ValueError('Slip spring requires K1 > 0, delta_1 > 0, 0 <= K2 <= K1')
        if not isfinite(self.K1*self.delta_1):
            raise ValueError('Slip spring K1*delta_1 must be finite')

    def to_dict(self):
        return dict(type='slip', K1=self.K1, K2=self.K2, delta_1=self.delta_1)


def parse_slip_spring(value, *, location='slip spring'):
    """One strict definition parser for legacy JSON, normalized JSON and Python."""
    if isinstance(value, SlipSpringParams):
        return value
    if not isinstance(value, dict):
        raise ValueError(f'{location}: expected a slip spring object')
    if set(value) != {'type', 'K1', 'K2', 'delta_1'} or value.get('type') != 'slip':
        raise ValueError(f'{location}: requires exactly type="slip", K1, K2, delta_1')
    try:
        return SlipSpringParams(value['K1'], value['K2'], value['delta_1'])
    except ValueError as error:
        raise ValueError(f'{location}: {error}') from error


@dataclass(frozen=True)
class SlipSpringState:
    deformation: float = 0.
    force: float = 0.
    tangent: float = 0.
    branch: str = 'elastic'
    direction: int = 0
    delta_max_pos: float = 0.
    delta_max_neg: float = 0.
    force_at_max_pos: float = 0.
    force_at_max_neg: float = 0.
    zero_pos: float = 0.
    zero_neg: float = 0.
    yielded_pos: bool = False
    yielded_neg: bool = False

    @classmethod
    def initial(cls, params):
        return cls(tangent=params.K1)


def _at(a, b):
    # Only rounding-sized event snapping, without a unit-dependent dead band.
    return abs(a-b) <= 4*max(ulp(a), ulp(b))


def evaluate_slip(params, committed, deformation, *, direction_hint=0):
    """Return a candidate state without advancing history.

    At zero increment the accepted state is retained. An optional predictor
    direction selects a one-sided event tangent, without changing its force.
    """
    delta = _finite_number(deformation, 'Slip spring deformation')
    increment = delta-committed.deformation
    if increment == 0 and not direction_hint:
        return committed
    direction = 1 if (increment if increment else direction_hint) > 0 else -1

    def backbone(value):
        magnitude = abs(value)
        return (params.K1*value if magnitude <= params.delta_1 else
                copysign(params.K1*params.delta_1 + params.K2*(magnitude-params.delta_1), value))

    maximum = max(committed.delta_max_pos, delta)
    minimum = min(committed.delta_max_neg, delta)
    positive = max(params.delta_1, committed.delta_max_pos)
    negative = min(-params.delta_1, committed.delta_max_neg)
    zp, zn = committed.zero_pos, committed.zero_neg

    # Event order matters when both zero positions are zero (virgin / elastic).
    if ((delta > positive and not _at(delta, positive))
            or (_at(delta, positive) and direction > 0)):
        force, tangent, branch = backbone(delta), params.K2, 'skeleton_pos'
    elif ((delta < negative and not _at(delta, negative))
            or (_at(delta, negative) and direction < 0)):
        force, tangent, branch = backbone(delta), params.K2, 'skeleton_neg'
    elif (delta > zp and not _at(delta, zp)) or (_at(delta, zp) and direction > 0):
        force = 0. if _at(delta, zp) else params.K1*(delta-zp)
        tangent = params.K1
        branch = ('reload_pos' if direction > 0 else 'unload_pos') if committed.yielded_pos else 'elastic'
    elif (delta < zn and not _at(delta, zn)) or (_at(delta, zn) and direction < 0):
        force = 0. if _at(delta, zn) else params.K1*(delta-zn)
        tangent = params.K1
        branch = ('reload_neg' if direction < 0 else 'unload_neg') if committed.yielded_neg else 'elastic'
    else:
        force, tangent, branch = 0., 0., 'slip'
    if params.K2 == params.K1:
        force, tangent, branch = params.K1*delta, params.K1, 'elastic'
    fmax, fmin = backbone(maximum), backbone(minimum)
    # K2=K1 must degenerate exactly to a linear spring without roundoff gaps.
    zero_pos = (1-params.K2/params.K1)*max(0., maximum-params.delta_1)
    zero_neg = (1-params.K2/params.K1)*min(0., minimum+params.delta_1)
    if not all(isfinite(v) for v in (force, fmax, fmin, zero_pos, zero_neg)):
        raise ValueError('Non-finite slip spring response')
    return replace(committed, deformation=delta, force=force, tangent=tangent,
                   branch=branch, direction=direction,
                   delta_max_pos=maximum, delta_max_neg=minimum,
                   force_at_max_pos=fmax, force_at_max_neg=fmin,
                   zero_pos=zero_pos, zero_neg=zero_neg,
                   yielded_pos=maximum > params.delta_1,
                   yielded_neg=minimum < -params.delta_1)
