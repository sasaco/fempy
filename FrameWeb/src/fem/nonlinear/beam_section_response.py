"""Fixed-skeleton bending/shear increments for manual 7.21.

This pure local kernel implements the NonlinearBarElement bending planes. A caller
accepts a returned state only after convergence; evaluating another trial
never commits or changes any input state.
"""
from dataclasses import dataclass
from math import fsum, isfinite

import numpy as np

from ..diagnostics import InputValidationError, NumericalConditionError
from .hysteresis import HysteresisState, JRStiffnessReductionModel


@dataclass(frozen=True)
class BendingPlaneState:
    """Owned snapshot of one local bending plane, including accumulated V."""

    curvature: float
    shear_deformation: float
    moment: float
    shear_force: float
    history: HysteresisState

    @classmethod
    def initial(cls, model: JRStiffnessReductionModel) -> "BendingPlaneState":
        return cls(0., 0., 0., 0., model.create_initial_state())


@dataclass(frozen=True)
class CurvatureInterval:
    """A nonzero part of the actual directed JR path, with constant slope."""

    start: float
    end: float
    tangent: float


@dataclass(frozen=True)
class BendingPlaneResponse:
    state: BendingPlaneState
    # Rows [M, V], columns [curvature, shear_deformation].
    tangent: np.ndarray
    bending_tangent: float
    effective_inertia: float
    shear_coefficient: float
    intervals: tuple[CurvatureInterval, ...]


def _finite(name, value, *, positive=False):
    if not isinstance(value, (int, float, np.integer, np.floating)) or not isfinite(value):
        raise InputValidationError(f'{name} must be finite', field=name)
    if positive and value <= 0:
        raise InputValidationError(f'{name} must be positive', field=name)


def _shear_coefficient(bending_tangent, length, shear_rigidity):
    """Condensation with a dimensionless cancellation check, never a floor."""
    bending = 12.*(bending_tangent/length)/length
    if not isfinite(bending):
        raise NumericalConditionError('Nonfinite condensed bending coefficient',
                                      reason='nonfinite_shear_condensation')
    if shear_rigidity is None:
        return bending
    scale = max(abs(bending), shear_rigidity)
    b, g = bending/scale, shear_rigidity/scale
    denominator = b+g
    relative = abs(denominator)/(abs(b)+g)
    if relative <= 1e-12:
        raise NumericalConditionError(
            'Singular bending/shear condensation',
            reason='singular_shear_condensation',
            bending_tangent=float(bending_tangent),
            length=float(length), shear_rigidity=float(shear_rigidity),
            relative_denominator=float(relative),
        )
    # Put the smaller coefficient in the numerator. Multiplying the larger
    # one by its scaled ratio can underflow that ratio to zero even when the
    # resulting C is representable (the shear-dominated limit is C -> GA).
    if abs(bending) <= shear_rigidity:
        coefficient = bending/(1.+bending/shear_rigidity)
    else:
        coefficient = shear_rigidity/(1.+shear_rigidity/bending)
    if not isfinite(coefficient):
        raise NumericalConditionError('Nonfinite condensed shear coefficient',
                                      reason='nonfinite_shear_condensation')
    return coefficient


def _fixed_intervals(model, state, target):
    """Adapt the existing JR branch graph without reproducing reversal rules.

    _reverse is the JR-owned operation also used by get_force_and_stiffness;
    it runs only on our private copy. Traversing next_segment affects neither
    that graph nor its suspended return paths. The final candidate is obtained
    separately through the normal JR evaluator, which owns stack restoration.
    """
    start = state.current_delta
    if target == start:
        return ()
    direction = 1 if target > start else -1
    path_state = state.copy()
    elastic = state.is_elastic(model.params.delta_1_pos, model.params.delta_1_neg)
    if not (elastic and state.active_segment is None):
        if state.loading_direction and state.loading_direction != direction:
            model._reverse(path_state, direction)

    result = []
    segment = path_state.active_segment
    visited = set()
    while segment is not None and direction*(target-start) > 0:
        if id(segment) in visited:
            raise InputValidationError('Cyclic JR continuation path')
        visited.add(id(segment))
        end = min(target, segment.end_delta) if direction > 0 else max(target, segment.end_delta)
        if direction*(end-start) > 0:
            result.append(CurvatureInterval(start, end, segment.K))
            start = end
        segment = segment.next_segment

    if direction*(target-start) > 0:
        boundaries = [0.]
        for side, sign in [('pos', 1.), ('neg', -1.)]:
            boundaries.extend(sign*getattr(model.params, f'delta_{i}_{side}') for i in (1, 2, 3))
        boundaries = sorted(
            (x for x in boundaries if direction*(x-start) > 0 and direction*(target-x) > 0),
            reverse=direction < 0,
        )
        for end in [*boundaries, target]:
            # Breakpoints partition the magnitude axis into [lo, hi) intervals.
            # A floating midpoint can round to hi in a one-ULP interval and
            # select the following branch. lo gives the correct incoming slope.
            magnitude = min(abs(start), abs(end))
            side = 1 if max(start, end) > 0 else -1
            _, tangent = model.get_skeleton_force(side*magnitude, side)
            result.append(CurvatureInterval(start, end, tangent))
            start = end
    return tuple(result)


def evaluate_bending_plane(
    model: JRStiffnessReductionModel,
    committed: BendingPlaneState,
    curvature: float,
    shear_deformation: float,
    *,
    length: float,
    young_modulus: float,
    shear_rigidity: float | None = None,
) -> BendingPlaneResponse:
    """Integrate dV=C ds along a straight [curvature, s] trial from committed.

    shear_rigidity is G*k*A. None selects the no-shear-deformation limit.
    The analytic tangent differentiates interval lengths as well as the shear
    increment. At exact events it uses the JR endpoint branch convention. At
    zero curvature increment it uses the stored branch; reversal can give a
    different one-sided response, so a central derivative is not promised.
    """
    for name, value in [('curvature', curvature), ('shear_deformation', shear_deformation),
                        ('committed.curvature', committed.curvature),
                        ('committed.shear_deformation', committed.shear_deformation),
                        ('committed.moment', committed.moment),
                        ('committed.shear_force', committed.shear_force)]:
        _finite(name, value)
    _finite('length', length, positive=True)
    _finite('young_modulus', young_modulus, positive=True)
    if shear_rigidity is not None:
        _finite('shear_rigidity', shear_rigidity, positive=True)
    if (committed.history.current_delta != committed.curvature or
            committed.history.current_P != committed.moment):
        raise InputValidationError('Committed moment/curvature must match JR history')

    moment, bending_tangent, info = model.get_force_and_stiffness(curvature, committed.history)
    history = model.update_state(curvature, moment, bending_tangent, committed.history, info)
    intervals = _fixed_intervals(model, committed.history, curvature)
    terminal_c = _shear_coefficient(bending_tangent, length, shear_rigidity)
    delta_curvature = curvature-committed.curvature
    delta_shear = shear_deformation-committed.shear_deformation
    if intervals:
        weighted = [
            (_shear_coefficient(part.tangent, length, shear_rigidity),
             abs(part.end-part.start)/abs(delta_curvature))
            for part in intervals
        ]
        mean_c = fsum(c*weight for c, weight in weighted)
        # Summing differences avoids subtracting near-equal averages on a
        # constant branch (whose curvature coupling is identically zero).
        coupling = delta_shear/delta_curvature * fsum(
            (terminal_c-c)*weight for c, weight in weighted
        )
    else:
        mean_c, coupling = terminal_c, 0.
    shear_force = committed.shear_force+mean_c*delta_shear
    inertia = bending_tangent/young_modulus
    tangent = np.array([[bending_tangent, 0.], [coupling, mean_c]])
    if not all(isfinite(x) for x in [shear_force, inertia]) or not np.all(np.isfinite(tangent)):
        raise NumericalConditionError('Nonfinite bending-plane response',
                                      reason='nonfinite_section_response')
    candidate = BendingPlaneState(float(curvature), float(shear_deformation), float(moment),
                                 float(shear_force), history)
    return BendingPlaneResponse(candidate, tangent, float(bending_tangent), float(inertia),
                                float(terminal_c), intervals)
