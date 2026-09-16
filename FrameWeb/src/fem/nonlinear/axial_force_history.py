"""Held-curvature Nd contact kernel for manual 7.20.6.

This pure local operation follows an initially fixed unloading/retracing line
or the skeleton through an Nd path. It is a building block for the coupled
history integrator, not a general simultaneous curvature/Nd update. Moving
reloads use explicit target identities and the companion reload-hold adapter.

Within an Nd row and skeleton segment, M_b = Q(t)/W(t), where Q is quadratic
and W is positive affine. All intersections and turning points are obtained
from these polynomials; no sampling grid or endpoint force clipping is used.
"""
from dataclasses import dataclass
from math import copysign, fsum, isfinite, sqrt

from numpy.polynomial import Polynomial

from ..diagnostics import InputValidationError, NumericalConditionError, UnsupportedAnalysisError
from .axial_force_table import AxialForceTable, _number
from .hysteresis import HysteresisState, JRStiffnessReductionModel


_EPS = 2.220446049250313e-16


@dataclass(frozen=True)
class AxialForceHistoryState:
    Nd: float
    history: HysteresisState
    contact_side: int | None = None
    contact_segment: int | None = None

    @classmethod
    def from_fixed_history(cls, Nd, history):
        """Own a copy of an experienced JR state at its actual fixed Nd."""
        return cls(_number(Nd, 'Nd'), history.copy())


@dataclass(frozen=True)
class AxialForceHistoryEvent:
    kind: str
    Nd: float
    moment: float
    side: int
    segment: int


@dataclass(frozen=True)
class AxialForceHoldResponse:
    state: AxialForceHistoryState
    moment: float
    bending_tangent: float
    # Derivative of this held-curvature update with respect to terminal Nd.
    # This is NOT the derivative of a simultaneous curvature/Nd trial.
    moment_Nd_derivative: float
    events: tuple[AxialForceHistoryEvent, ...]


def _roots(polynomial):
    """Real roots of a degree <= 2 polynomial with scale-free coefficients.

    The q form avoids cancellation in the smaller quadratic root. No small
    coefficient is dropped: a small leading term can still have a root in
    the physical interval. Exactly zero polynomials need no subdivision.
    """
    coefficients = list(polynomial.trim().coef)
    scale = max(abs(value) for value in coefficients)
    if scale == 0 or len(coefficients) == 1:
        return ()
    coefficients = [value/scale for value in coefficients]
    if len(coefficients) == 2:
        return (-coefficients[0]/coefficients[1],)
    if len(coefficients) != 3:
        raise NumericalConditionError('Unexpected contact polynomial degree',
                                      reason='axial_force_contact_polynomial')
    c, b, a = coefficients
    discriminant = fsum((b*b, -4*a*c))
    if abs(discriminant) <= 8*_EPS*(b*b+abs(4*a*c)):
        # A double root otherwise becomes two spurious crossings separated
        # by sqrt(roundoff), spuriously replacing a tangent unloading line.
        return (-b/(2*a),)
    if discriminant < 0:
        return ()
    q = -.5*(b+copysign(sqrt(discriminant), b))
    if q == 0:
        return (-b/(2*a),)
    return tuple(sorted(set((q/a, c/q))))


def _enters_positive(polynomial, root):
    """Sign in the open interval immediately after an algebraic root."""
    derivative = polynomial.deriv()
    slope = float(derivative(root))
    scale = fsum(abs(coefficient*root**i) for i, coefficient in enumerate(derivative.coef))
    if abs(slope) > 8*_EPS*scale:
        return slope > 0
    return float(polynomial.deriv(2)(root)) > 0


def _affine(start, end):
    return Polynomial((start, end-start))


def _pieces(table, curvature, start, end, side):
    """Split Nd rows and moving curvature breakpoints before root finding."""
    direction = 1 if end > start else -1
    nodes = [start, *(row.Nd for row in table.rows if min(start, end) < row.Nd < max(start, end)), end]
    nodes.sort(reverse=direction < 0)
    side_name = 'positive' if side > 0 else 'negative'
    magnitude = side*curvature
    for left, right in zip(nodes[:-1], nodes[1:]):
        p0 = getattr(table.interpolate(left), side_name)
        p1 = getattr(table.interpolate(right), side_name)
        fractions = [0., 1.]
        for d0, d1 in zip(p0.curvatures[:3], p1.curvatures[:3]):
            if d1 != d0:
                root = (magnitude-d0)/(d1-d0)
                if 0 < root < 1:
                    fractions.append(root)
        fractions = sorted(set(fractions))
        for a, b in zip(fractions[:-1], fractions[1:]):
            n0 = left if a == 0 else (1-a)*left+a*right
            n1 = right if b == 1 else (1-b)*left+b*right
            if n0 == n1:
                continue
            q0 = getattr(table.interpolate(n0), side_name)
            q1 = getattr(table.interpolate(n1), side_name)
            # Compare differences in normalized coordinates. An Nd midpoint
            # rounded to a row/segment endpoint must not select another branch.
            index = sum(fsum((magnitude-d0, magnitude-d1)) >= 0
                        for d0, d1 in zip(q0.curvatures[:3], q1.curvatures[:3]))
            d = [_affine(0., 0.), *(_affine(x, y) for x, y in zip(q0.curvatures, q1.curvatures))]
            p = [_affine(0., 0.), *(_affine(x, y) for x, y in zip(q0.moments, q1.moments))]
            if index == 3 and len(q0.curvatures) == 3:
                numerator, denominator = side*p[3], Polynomial((1.,))
            else:
                denominator = d[index+1]-d[index]
                numerator = side*(p[index]*denominator+(p[index+1]-p[index])*(magnitude-d[index]))
            yield n0, n1, index+1, numerator, denominator


def _detach(table, history, Nd, side):
    """Use the existing JR reversal rule at the actual departure point."""
    model = JRStiffnessReductionModel(table.interpolate(Nd).to_jr_params())
    history.active_segment = None
    history.reversal_stack.clear()
    history.reversal_paths.clear()
    model._reverse(history, -side)
    history.current_K = history.active_segment.K
    history.branch = history.active_segment.branch
    history.loading_direction = -side
    history.crossed_zero = False
    history.delta_max_inner = 0.


def _has_constant_skeleton(table, start, end):
    """Exact fixed-law limit, including suspended JR return graphs."""
    curves = [table.interpolate(start), table.interpolate(end)]
    curves.extend(table.interpolate(row.Nd) for row in table.rows if min(start, end) < row.Nd < max(start, end))
    return all((curve.positive, curve.negative) == (curves[0].positive, curves[0].negative) for curve in curves[1:])


def evaluate_axial_force_hold(table: AxialForceTable, committed: AxialForceHistoryState,
                              Nd: float) -> AxialForceHoldResponse:
    """Advance Nd monotonically at exactly the committed curvature.

    Supported starts include fixed return lines and explicitly identified
    moving reload targets. An invariant table preserves all JR branches.
    This operation does not trace curvature or resume an internal loop.

    Contact supersedes the intercepted return path. Departure creates a new
    unload with the departure Nd and experienced extrema, then holds its Kd.
    Only moment_Nd_derivative is an algorithmic endpoint sensitivity here;
    bending_tangent is the active branch partial used to form effective I.
    """
    Nd = _number(Nd, 'Nd')
    table.interpolate(committed.Nd)
    table.interpolate(Nd)  # Validate before copying or evaluating any events.
    history = committed.history.copy()
    for name in ('current_delta', 'current_P', 'current_K', 'delta_max_pos', 'delta_max_neg'):
        _number(getattr(history, name), name)
    curvature = history.current_delta
    side = committed.contact_side or (1 if curvature >= 0 else -1)
    if isinstance(side, bool) or side not in (-1, 1) or side*curvature < 0:
        raise InputValidationError('Contact side must match the curvature side')
    if (committed.contact_side is not None) != (history.branch == 'envelope_contact'):
        raise InputValidationError('Contact metadata must agree with the history branch')
    contact = committed.contact_side is not None
    invariant = _has_constant_skeleton(table, committed.Nd, Nd)
    segment = history.active_segment
    reload = segment is not None and history.branch in ('reloading', 'inner_reloading')
    forward_return = (segment is not None and history.branch == 'retracing'
                      and segment.target is not None and segment.target.kind == 'forward')
    if (reload or forward_return) and segment.target is not None and segment.target.kind in ('skeleton', 'forward'):
        # The zero-length response still has a nonzero Nd sensitivity.
        if Nd == committed.Nd or not invariant:
            from .axial_force_reload_hold import evaluate_reload_hold
            return evaluate_reload_hold(table, committed, Nd)
    if Nd == committed.Nd or invariant:
        derivative = 0.
        if Nd == committed.Nd and (contact or history.active_segment is None):
            derivative = table.evaluate_skeleton(curvature, Nd, side=side).moment_Nd_derivative
        state = AxialForceHistoryState(Nd, history, committed.contact_side, committed.contact_segment)
        return AxialForceHoldResponse(state, history.current_P, history.current_K, derivative, ())
    fixed_return = reload and segment.target is not None and segment.target.kind == 'experienced'
    if segment is not None and history.branch not in ('unloading', 'inner_unloading', 'retracing') and not fixed_return:
        raise UnsupportedAnalysisError('Moving reload targets require the coupled history integrator',
                                       reason='axial_force_hold_moving_target', branch=history.branch)
    skeleton = history.active_segment is None and not contact
    direct_return = (segment is not None and segment.branch == 'retracing'
                     and segment.next_segment is None and history.loading_direction == side)
    events = []
    current_moment = history.current_P

    def event(kind, position, segment):
        events.append(AxialForceHistoryEvent(kind, float(position), float(current_moment), side, segment))

    for left, right, segment, numerator, denominator in _pieces(table, curvature, committed.Nd, Nd, side):
        def position(t):
            return left if t == 0 else right if t == 1 else (1-t)*left+t*right

        def envelope(t):
            return float(numerator(t)/denominator(t))

        derivative_numerator = numerator.deriv()*denominator-numerator*denominator.deriv()
        turns = [0., *(root for root in _roots(derivative_numerator) if 0 < root < 1), 1.]
        turns = sorted(set(turns))
        for a, b in zip(turns[:-1], turns[1:]):
            # Sign of motion into the open interval. The denominator squared
            # is positive, and t increases even for decreasing physical Nd.
            motion = side*float(derivative_numerator((a+b)/2))
            if skeleton:
                current_moment = envelope(b)
                continue
            if contact and motion > 0:
                current_moment = envelope(a)
                history.current_P = current_moment
                _detach(table, history, position(a), side)
                contact = False
                event('departure', position(a), segment)
            if contact:
                current_moment = envelope(b)
                continue
            if motion >= 0:
                continue  # Expanding envelope cannot catch a fixed line.
            gap = side*(current_moment*denominator-numerator)
            candidates = [root for root in _roots(gap) if a <= root <= b]
            # Endpoints are handled with a force-relative roundoff bound;
            # no finite Nd increment or physical force is rounded away.
            for endpoint in (a, b):
                scale = abs(current_moment*denominator(endpoint))+abs(numerator(endpoint))
                if abs(gap(endpoint)) <= 8*_EPS*scale:
                    candidates.append(endpoint)
            candidates = [root for root in candidates if _enters_positive(gap, root)]
            if any(abs(root-1) <= 32*_EPS for root in candidates):
                limit = table.rows[-1].Nd if Nd > committed.Nd else table.rows[0].Nd
                if right != limit:
                    _, _, _, next_q, next_w = next(_pieces(table, curvature, right, limit, side))
                    if not _enters_positive(side*(current_moment*next_w-next_q), 0.):
                        # The outgoing row can turn back inside at this
                        # corner. Do not create a contact/departure pair just
                        # from extrapolating the incoming row beyond its end.
                        candidates = [root for root in candidates if abs(root-1) > 32*_EPS]
            if candidates:
                root = min(candidates)
                current_moment = envelope(root)
                event('target' if direct_return else 'contact', position(root), segment)
                # A direct outward return has reached its skeleton target.
                # It must stay on the skeleton when Nd later reverses, just
                # like arrival at an explicitly moving reload target.
                skeleton, contact = direct_return, not direct_return
                history.active_segment = None
                history.reversal_stack.clear()
                history.reversal_paths.clear()
                history.delta_max_inner = 0.
                history.crossed_zero = False
                current_moment = envelope(b)
        if right != Nd:
            event('partition', right, segment)

    response = table.evaluate_skeleton(curvature, Nd, side=side)
    if skeleton or contact:
        current_moment = response.moment
        history.current_K = response.bending_tangent
        history.branch = 'envelope_contact' if contact else (
            'initial' if response.segment == 1 and history.branch == 'initial' else 'skeleton')
        if contact:
            # The next outward curvature increment continues the skeleton;
            # an inward increment starts a new JR unload at this actual Nd.
            history.loading_direction = side
    history.previous_delta, history.previous_P = committed.history.current_delta, committed.history.current_P
    history.current_P = current_moment
    if not all(isfinite(value) for value in (current_moment, history.current_K)):
        raise NumericalConditionError('Nonfinite Nd history response', reason='nonfinite_section_response')
    state = AxialForceHistoryState(Nd, history, side if contact else None, response.segment if contact else None)
    return AxialForceHoldResponse(state, float(current_moment), float(history.current_K),
                                 response.moment_Nd_derivative if skeleton or contact else 0., tuple(events))
