"""Affine simultaneous curvature/Nd paths for skeleton and fixed-line contact.

This local kernel solves events on the actual straight path in (phi, Nd).
It is not an endpoint-Nd approximation for a nonlinear axial material, and
does not yet trace moving reloads/internal returns or produce a consistent
coupled tangent. Unsupported transitions fail before returning a candidate.
"""
from dataclasses import dataclass
from math import fsum, isfinite

from numpy.polynomial import Polynomial

from ..diagnostics import InputValidationError, UnsupportedAnalysisError, NumericalConditionError
from .axial_force_table import _number
from .axial_force_history import (AxialForceHistoryState, _EPS, _affine, _detach,
                                 _has_constant_skeleton, evaluate_axial_force_hold)
from .axial_force_reload_hold import _interval_roots
from .axial_force_targets import evaluate_axial_force_curvature
from .hysteresis import JRStiffnessReductionModel


@dataclass(frozen=True)
class AxialForcePathEvent:
    kind: str
    fraction: float
    curvature: float
    Nd: float
    moment: float
    side: int
    segment: int


@dataclass(frozen=True)
class AxialForcePathResponse:
    state: AxialForceHistoryState
    moment: float
    # Active branch partial, NOT the derivative of this endpoint update.
    bending_tangent: float
    events: tuple[AxialForcePathEvent, ...]


def _lerp(a, b, t):
    return a if t == 0 else b if t == 1 else (1-t)*a+t*b


def _right_sign(poly, t, *, at_root=False):
    """Sign in the right open neighbourhood, including multiple roots."""
    for order in range(1 if at_root else 0, poly.degree()+1):
        derivative = poly.deriv(order)
        value = float(derivative(t))
        scale = fsum(abs(c*t**i) for i, c in enumerate(derivative.coef))
        if abs(value) > 32*_EPS*scale:
            return 1 if value > 0 else -1
    return 0


def _zeros(poly, start, end, reference=None):
    roots = [r for r in _interval_roots(poly) if start <= r <= end]
    for t in (start, end):
        scale = (reference(t) if reference is not None else
                 fsum(abs(c*t**i) for i, c in enumerate(poly.coef)))
        if abs(poly(t)) <= 32*_EPS*scale:
            roots = [r for r in roots if abs(r-t) > 32*_EPS]
            roots.append(t)
    return sorted(set(roots))


def _pieces(table, x0, x1, n0, n1, side, maximum):
    """Rational skeleton pieces, with an outgoing piece at endpoint corners.

    Extend the same ray only to obtain one-sided event classification. No
    history is advanced beyond fraction 1; the extension stays in Nd range.
    The fourth point defines a slope, never a terminating breakpoint.
    """
    dn = n1-n0
    limit = table.rows[-1].Nd if dn > 0 else table.rows[0].Nd
    stop = min(2., (limit-n0)/dn)
    nodes = {0., 1., stop}
    nodes.update((row.Nd-n0)/dn for row in table.rows if 0 < (row.Nd-n0)/dn < stop)
    name = 'positive' if side > 0 else 'negative'
    for a, b in zip(sorted(nodes)[:-1], sorted(nodes)[1:]):
        na, nb = _lerp(n0, n1, a), _lerp(n0, n1, b)
        # Exact row endpoints avoid extrapolation by a floating-point ULP.
        na = min(table.rows[-1].Nd, max(table.rows[0].Nd, na))
        nb = min(table.rows[-1].Nd, max(table.rows[0].Nd, nb))
        p0, p1 = getattr(table.interpolate(na), name), getattr(table.interpolate(nb), name)
        xa, xb = _lerp(x0, x1, a), _lerp(x0, x1, b)
        splits = {0., 1.}
        for d0, d1 in zip(p0.curvatures[:3], p1.curvatures[:3]):
            for line in (_affine(side*xa-d0, side*xb-d1), _affine(maximum-d0, maximum-d1)):
                splits.update(r for r in _interval_roots(line) if 0 < r < 1)
        cuts = sorted(splits)
        for c, e in zip(cuts[:-1], cuts[1:]):
            left, right = _lerp(a, b, c), _lerp(a, b, e)
            if left == right:
                continue
            x = _affine(_lerp(xa, xb, c), _lerp(xa, xb, e))
            n = _affine(_lerp(na, nb, c), _lerp(na, nb, e))
            d = [Polynomial((0.,)), *(_affine(_lerp(u, v, c), _lerp(u, v, e))
                                     for u, v in zip(p0.curvatures, p1.curvatures))]
            p = [Polynomial((0.,)), *(_affine(_lerp(u, v, c), _lerp(u, v, e))
                                     for u, v in zip(p0.moments, p1.moments))]
            i = sum(fsum((side*x(0)-di(0), side*x(1)-di(1))) >= 0 for di in d[1:4])
            if i == 3 and len(d) == 4:
                q, w = side*p[3], Polynomial((1.,))
            else:
                w = d[i+1]-d[i]
                q = side*(p[i]*w+(p[i+1]-p[i])*(side*x-d[i]))
            yield left, right, x, n, i+1, q, w, d, p


def _departure_parts(table, maximum, piece, side):
    """Exact rational candidate Kd and directional-gap roots.

    Arbitrary beta is algebraic here when curvature coordinates are fixed.
    With moving coordinates beta=0 is supported. Nonzero beta with moving
    coordinates requires a further nonpolynomial root-isolation contract.
    """
    _, _, x, _, _, q, w, d, p = piece
    if table.beta and any(di.trim().degree() != 0 for di in d[1:3]):
        raise UnsupportedAnalysisError('Moving curvature points with nonzero beta require general departure isolation',
                                       reason='axial_force_path_nonpolynomial_departure')
    k1 = p[1], d[1]
    k2 = p[2]-p[1], d[2]-d[1]
    candidates = [k1, (Polynomial((table.K_min,)), Polynomial((1.,)))]
    if maximum <= d[1](.5):
        base = k1
    elif maximum <= d[2](.5):
        factor = (maximum/d[1](.5))**(-table.beta)
        reduced = p[1]*factor, d[1]
        candidates.extend((reduced, k2))
        base = None
    else:
        factor = (maximum/d[2](.5))**(-table.beta)
        base = k2[0]*factor, k2[1]
        candidates.append(base)
    cuts = {0., 1.}
    for i, (u, v) in enumerate(candidates):
        for r, s in candidates[i+1:]:
            cuts.update(_interval_roots(u*s-r*v))
    cuts = sorted(cuts)
    for a, b in zip(cuts[:-1], cuts[1:]):
        mid = (a+b)/2
        selected = base if base is not None else max((reduced, k2), key=lambda pair: pair[0](mid)/pair[1](mid))
        floor = candidates[1]
        selected = max((selected, floor), key=lambda pair: pair[0](mid)/pair[1](mid))
        selected = min((selected, k1), key=lambda pair: pair[0](mid)/pair[1](mid))
        u, v = selected
        gap_rate = side*(u*x.deriv()*w*w-(q.deriv()*w-q*w.deriv())*v)
        roots = [a, *[r for r in _interval_roots(gap_rate) if a < r < b], b]
        for lo, hi in zip(roots[:-1], roots[1:]):
            yield lo, hi, gap_rate


def evaluate_axial_force_path(table, committed, curvature, Nd):
    """Follow an affine (curvature, Nd) leg from owned committed history.

    Simultaneous motion supports a single curvature side, the skeleton and
    fixed lines up to an unresolved return/reload endpoint. Contact uses the
    directional gap with candidate JR Kd. Only its creation/departure freezes
    an unloading line; contact does not reconstruct one at every partition.
    Scalar legs retain the existing exact adapters and invariant tables JR.
    """
    curvature, Nd = _number(curvature, 'curvature'), _number(Nd, 'Nd')
    table.interpolate(committed.Nd)
    table.interpolate(Nd)
    history = committed.history.copy()
    for name in ('current_delta', 'current_P', 'current_K', 'delta_max_pos', 'delta_max_neg'):
        _number(getattr(history, name), name)
    x0, n0 = history.current_delta, committed.Nd
    contact = committed.contact_side is not None
    if contact and (isinstance(committed.contact_side, bool) or committed.contact_side not in (-1, 1)):
        raise InputValidationError('Contact side must be +1 or -1')
    side = committed.contact_side or (1 if x0 > 0 or x0 == 0 and curvature >= 0 else -1)
    if (isinstance(side, bool) or side not in (-1, 1) or side*x0 < 0
            or contact != (history.branch == 'envelope_contact')):
        raise InputValidationError('Contact metadata must agree with the curvature and history branch')
    if curvature == x0:
        response = evaluate_axial_force_hold(table, committed, Nd)
        events = tuple(AxialForcePathEvent(e.kind, (e.Nd-n0)/(Nd-n0), curvature,
                                          e.Nd, e.moment, e.side, e.segment) for e in response.events)
        return AxialForcePathResponse(response.state, response.moment, response.bending_tangent, events)
    if Nd == n0 or _has_constant_skeleton(table, n0, Nd):
        response = evaluate_axial_force_curvature(table, committed, curvature)
        state = AxialForceHistoryState(Nd, response.state.history, response.state.contact_side, response.state.contact_segment)
        return AxialForcePathResponse(state, response.moment, response.bending_tangent, ())
    if side*curvature <= 0:
        raise UnsupportedAnalysisError('Simultaneous curvature-side crossing requires the general return integrator',
                                       reason='axial_force_path_side_crossing')
    direction = 1 if curvature > x0 else -1
    model = JRStiffnessReductionModel(table.interpolate(n0).to_jr_params())
    if contact and direction == side:
        contact = False
        history.branch = 'skeleton'
    elif not contact and history.loading_direction and direction != history.loading_direction:
        if not (history.active_segment is None and history.is_elastic(model.params.delta_1_pos, model.params.delta_1_neg)):
            model._reverse(history, direction)
            history.branch = history.active_segment.branch
            history.current_K = history.active_segment.K
    maximum = history.delta_max_pos if side > 0 else history.delta_max_neg
    pieces = list(_pieces(table, x0, curvature, n0, Nd, side, maximum))
    events = []

    def point(x, moment, tangent):
        history.current_delta, history.current_P, history.current_K = float(x), float(moment), float(tangent)

    def event(kind, fraction, x, n, moment, index):
        events.append(AxialForcePathEvent(kind, float(fraction), float(x), float(n), float(moment), side, index))

    def departure_at_start(piece):
        return next((poly for a, b, poly in _departure_parts(table, maximum, piece, side) if a == 0), None)

    for number, piece in enumerate(pieces):
        left, right, x, n, index, q, w, _, _ = piece
        if left >= 1:
            break
        following = pieces[number+1] if number+1 < len(pieces) else None
        cursor = 0.
        transitions = 0
        while cursor < 1:
            transitions += 1
            if transitions > 16:
                raise NumericalConditionError('Repeated zero-length path events', reason='axial_force_path_event_cycle')
            if contact:
                departure = None
                for a, b, rate in _departure_parts(table, maximum, piece, side):
                    start = max(cursor, a)
                    if start < b and _right_sign(rate, start) < 0:
                        departure = start
                        break
                if departure is None and following is not None:
                    rate = departure_at_start(following)
                    if rate is not None and _right_sign(rate, 0.) < 0:
                        departure = 1.
                if departure is None:
                    break
                cursor = departure
                moment = float(q(cursor)/w(cursor))
                point(x(cursor), moment, history.current_K)
                _detach(table, history, float(n(cursor)), side)
                contact = False
                event('departure', _lerp(left, right, cursor), x(cursor), n(cursor), moment, index)
                if cursor == 1:
                    break
            segment = history.active_segment
            if segment is None:
                break  # Ordinary skeleton loading does not detach under Nd expansion.
            if segment.branch not in ('unloading', 'inner_unloading', 'retracing'):
                raise UnsupportedAnalysisError('Simultaneous moving reload is not implemented',
                                               reason='axial_force_path_moving_target', branch=segment.branch)
            line = segment.start_P+segment.K*(x-segment.start_delta)
            gap = side*(line*w-q)
            roots = _zeros(gap, cursor, 1., lambda t: abs(line(t)*w(t))+abs(q(t)))
            crossing = None
            for root in roots:
                if root == 1 and following is not None:
                    nx, nq, nw = following[2], following[5], following[6]
                    outgoing = side*((segment.start_P+segment.K*(nx-segment.start_delta))*nw-nq)
                    enters = _right_sign(outgoing, 0., at_root=True) > 0
                else:
                    enters = _right_sign(gap, root, at_root=True) > 0
                if enters:
                    crossing = root
                    break
            dx = float(x.deriv()(0))
            # An Nd partition can have equal rounded curvature endpoints
            # even when the complete trial has a finite one-ULP increment.
            end = ((segment.end_delta-x(0))/dx if dx else
                   0. if segment.end_delta == x(0) else float('inf'))
            if cursor <= end <= 1 and (crossing is None or end < crossing-32*_EPS):
                raise UnsupportedAnalysisError('Simultaneous path reached an unresolved return/reload event',
                                               reason='axial_force_path_return_event', curvature=segment.end_delta,
                                               Nd=float(n(end)), branch=segment.branch)
            if crossing is None:
                break
            cursor = crossing
            direct_return = (segment.branch == 'retracing' and segment.next_segment is None
                             and direction == side)
            contact = not direct_return
            history.active_segment = None
            history.reversal_stack.clear()
            history.reversal_paths.clear()
            history.crossed_zero = False
            history.delta_max_inner = 0.
            event('target' if direct_return else 'contact', _lerp(left, right, cursor), x(cursor), n(cursor),
                  q(cursor)/w(cursor), index)
        envelope = table.evaluate_skeleton(float(x(1)), float(n(1)), side=side)
        if history.active_segment is None:
            point(x(1), envelope.moment, envelope.bending_tangent)
            history.branch = ('envelope_contact' if contact else
                              'initial' if history.branch == 'initial' and envelope.segment == 1 else 'skeleton')
        else:
            segment = history.active_segment
            point(x(1), segment.start_P+segment.K*(x(1)-segment.start_delta), segment.K)
            history.branch = segment.branch
        if right < 1:
            event('partition', right, x(1), n(1), history.current_P, index)
    history.previous_delta, history.previous_P = x0, committed.history.current_P
    history.current_delta = curvature
    history.loading_direction = side if contact else direction
    if curvature > history.delta_max_pos:
        history.delta_max_pos, history.P_max_pos = curvature, abs(history.current_P)
    if -curvature > history.delta_max_neg:
        history.delta_max_neg, history.P_max_neg = -curvature, abs(history.current_P)
    history.delta_max_inner = abs(history.reversal_stack[-1][0]) if history.reversal_stack else 0.
    if not all(isfinite(v) for v in (history.current_P, history.current_K)):
        raise NumericalConditionError('Nonfinite simultaneous history response', reason='nonfinite_section_response')
    index = table.evaluate_skeleton(curvature, Nd, side=side).segment
    state = AxialForceHistoryState(Nd, history, side if contact else None, index if contact else None)
    return AxialForcePathResponse(state, history.current_P, history.current_K, tuple(events))
