"""Pure JR target geometry and continuous resumption of suspended paths.

These are local adapters, not a simultaneous curvature/Nd path integrator.
Target derivatives hold the experienced history and line anchor fixed.
"""
from copy import deepcopy
from dataclasses import dataclass
from math import isfinite
from typing import TYPE_CHECKING

from ..diagnostics import InputValidationError, NumericalConditionError, UnsupportedAnalysisError
from .axial_force_table import AxialForceTable, _number
from .hysteresis.base_hysteresis import HysteresisSegment
from .hysteresis import JRStiffnessReductionModel

if TYPE_CHECKING:
    from .axial_force_history import AxialForceHistoryState


@dataclass(frozen=True)
class ReloadTargetResponse:
    curvature: float
    moment: float
    stiffness: float
    curvature_Nd_derivative: float
    moment_Nd_derivative: float
    stiffness_Nd_derivative: float
    kind: str


def _forward_target(table, segment, Nd):
    """First forward intersection of the frozen-Kd line with the envelope.

    Includes the unbounded fourth segment, even beyond its force zero.
    The root sensitivity follows (Kd-B) dphi/dNd = partial M_b/partial Nd.
    """
    target = segment.target
    side, kd = target.side, target.unloading_stiffness
    if not isfinite(kd) or kd <= 0:
        raise InputValidationError('A forward target requires the frozen positive unloading stiffness')
    curve = table.interpolate(Nd)
    points = curve.positive if side > 0 else curve.negative
    derivative = curve.positive_derivative if side > 0 else curve.negative_derivative
    start_x, start_p = segment.start_delta, segment.start_P
    d = (0., *points.curvatures[:3], float('inf'))
    p = (0., *points.moments[:3])
    for i, slope in enumerate(points.slopes):
        intercept = side*(p[i]-slope*d[i])
        if kd == slope:
            continue
        x = (kd*start_x-start_p+intercept)/(kd-slope)
        if d[i] <= side*x <= d[i+1] and side*(x-start_x) > 0:
            # At a breakpoint use the interval just selected for the root.
            # Sensitivities there are one-sided; callers must split events.
            dd, dp = (0., 0.) if i == 0 else (derivative.curvatures[i-1], derivative.moments[i-1])
            partial = side*(dp-slope*dd+points.slope_derivatives(derivative)[i]*(side*x-d[i]))
            dx = partial/(kd-slope)
            return x, start_p+kd*(x-start_x), kd, dx, kd*dx, 0.
    raise NumericalConditionError('No forward skeleton intersection for the held unloading line',
                                  reason='axial_force_no_forward_target', Nd=Nd, side=side,
                                  origin_curvature=start_x, origin_moment=start_p)


def evaluate_reload_target(table: AxialForceTable, segment: HysteresisSegment,
                           Nd: float) -> ReloadTargetResponse:
    """Evaluate a target identity without changing actual experienced points.

    A virgin threshold (point 1 or 2) is chosen at reversal and kept with the
    path. Its coordinates move with Nd; crossing a source-side yield point
    during an Nd hold must not silently switch the target identity.
    """
    Nd = _number(Nd, 'Nd')
    curve = table.interpolate(Nd)
    target = segment.target
    if target is None or target.kind not in ('experienced', 'skeleton', 'forward'):
        raise InputValidationError('Reload segment requires an explicit target identity',
                                   reason='axial_force_target_metadata')
    if isinstance(target.side, bool) or target.side not in (-1, 1):
        raise InputValidationError('Target side must be +1 or -1')
    for name in ('start_delta', 'start_P', 'end_delta', 'end_P', 'K'):
        _number(getattr(segment, name), name)
    side = target.side
    kind = target.kind
    if kind == 'experienced':
        values = segment.end_delta, segment.end_P, segment.K, 0., 0., 0.
    else:
        if kind == 'skeleton':
            if isinstance(target.threshold, bool) or target.threshold not in (1, 2):
                raise InputValidationError('Virgin target threshold must be point 1 or 2')
            experienced = _number(target.experienced_curvature, 'experienced_curvature')
            if experienced < 0:
                raise InputValidationError('Experienced target curvature must be nonnegative')
            points = curve.positive if side > 0 else curve.negative
            derivative = curve.positive_derivative if side > 0 else curve.negative_derivative
            i = target.threshold-1
            magnitude = max(experienced, points.curvatures[i])
            # Equality uses the moving-point side of max; it is an event,
            # not a location for a central-difference tangent comparison.
            dx = side*derivative.curvatures[i] if points.curvatures[i] >= experienced else 0.
            x = side*magnitude
            envelope = table.evaluate_skeleton(x, Nd, side=side)
            p = envelope.moment
            dp = envelope.moment_Nd_derivative+envelope.bending_tangent*dx
            width = x-segment.start_delta
            if side*width > 0:
                k = (p-segment.start_P)/width
                dk = (dp-k*dx)/width
                values = x, p, k, dx, dp, dk
            else:
                kind = 'forward'
        if kind == 'forward':
            values = _forward_target(table, segment, Nd)
    if not all(isfinite(value) for value in values):
        raise NumericalConditionError('Nonfinite moving target geometry',
                                      reason='nonfinite_axial_force_target', Nd=Nd, side=side)
    return ReloadTargetResponse(*values, kind)


def resume_reload_target(table, suspended, Nd, curvature, moment):
    """Reconnect a moving continuation through the actual internal return.

    The old zero-force anchor cannot generally satisfy both the saved return
    moment and a moved skeleton target. The return point becomes the new
    anchor. Actual return targets and their continuation graph stay intact.
    This operation is to be called at a resolved return event only.
    """
    curvature = _number(curvature, 'curvature')
    moment = _number(moment, 'moment')
    candidate = deepcopy(suspended)
    response = evaluate_reload_target(table, candidate, Nd)
    if candidate.target.kind == 'experienced':
        return candidate
    # Preserve the fixed-law representation exactly whenever the old line
    # and target still apply. In particular invariant Nd tables return JR.
    if ((response.curvature, response.moment) == (candidate.end_delta, candidate.end_P)
            and moment == candidate.start_P+candidate.K*(curvature-candidate.start_delta)):
        return candidate
    candidate.start_delta, candidate.start_P = curvature, moment
    candidate.unloading_origin = None
    response = evaluate_reload_target(table, candidate, Nd)
    candidate.end_delta, candidate.end_P, candidate.K = response.curvature, response.moment, response.stiffness
    return candidate


@dataclass(frozen=True)
class AxialForceCurvatureResponse:
    state: 'AxialForceHistoryState'
    moment: float
    bending_tangent: float


class _FixedNdTargetModel(JRStiffnessReductionModel):
    """Reuse JR reversals and stack ownership; resolve targets at entry."""

    def __init__(self, table, Nd):
        super().__init__(table.interpolate(Nd).to_jr_params())
        self.table, self.Nd = table, Nd

    def _trace(self, state, delta, direction):
        while state.active_segment is not None:
            segment = state.active_segment
            if segment.branch in ('reloading', 'inner_reloading'):
                response = evaluate_reload_target(self.table, segment, self.Nd)
                segment.end_delta, segment.end_P, segment.K = response.curvature, response.moment, response.stiffness
            if not self._reached(delta, segment.end_delta, direction):
                state.branch = segment.branch
                state.crossed_zero = segment.branch in ('reloading', 'inner_reloading')
                return segment.start_P+segment.K*(delta-segment.start_delta), segment.K
            if segment.restore_depth is not None:
                del state.reversal_stack[segment.restore_depth:]
                del state.reversal_paths[segment.restore_depth:]
            continuation = segment.next_segment
            actual_return = segment.branch == 'retracing' or (
                segment.target is not None and segment.target.kind == 'experienced')
            if actual_return and continuation is not None and continuation.target is not None:
                continuation = resume_reload_target(self.table, continuation, self.Nd,
                                                    segment.end_delta, segment.end_P)
            elif actual_return and continuation is None:
                p, _ = self.get_skeleton_force(segment.end_delta, 1 if segment.end_delta >= 0 else -1)
                if abs(p-segment.end_P) > 16*2.220446049250313e-16*(abs(p)+abs(segment.end_P)):
                    raise UnsupportedAnalysisError('Actual return point no longer lies on the moved skeleton',
                                                   reason='axial_force_return_to_moved_skeleton', Nd=self.Nd,
                                                   curvature=segment.end_delta, moment=segment.end_P,
                                                   skeleton_moment=p)
            state.active_segment = continuation
        return super()._trace(state, delta, direction)


def evaluate_axial_force_curvature(table, committed, curvature):
    """Trace curvature at the already committed Nd, with owned JR history.

    This operation and an Nd hold are distinct physical loading legs. Calling
    them successively is NOT a simultaneous curvature/Nd update. Restoration
    directly to a displaced skeleton (with no reload continuation) remains
    unsupported; it is diagnosed rather than introducing a force jump.
    """
    from .axial_force_history import AxialForceHistoryState
    curvature = _number(curvature, 'curvature')
    model = _FixedNdTargetModel(table, committed.Nd)
    if curvature == committed.history.current_delta:
        state = AxialForceHistoryState(committed.Nd, committed.history.copy(),
                                       committed.contact_side, committed.contact_segment)
        return AxialForceCurvatureResponse(state, state.history.current_P, state.history.current_K)
    moment, tangent, info = model.get_force_and_stiffness(curvature, committed.history)
    history = model.update_state(curvature, moment, tangent, committed.history, info)
    return AxialForceCurvatureResponse(AxialForceHistoryState(committed.Nd, history), moment, tangent)
