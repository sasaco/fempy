"""Algebraic held-curvature paths on explicitly identified moving reloads.

Nd rows, target identity switches, and both skeleton segment sets partition
the path. On each part the reload and envelope are rational polynomials;
their gap has degree at most four. Derivative-root isolation finds every
crossing, including an interior pair with equal-sign endpoint gaps.
"""
from math import fsum

from numpy.polynomial import Polynomial
from scipy.optimize import brentq

from ..diagnostics import NumericalConditionError
from .axial_force_targets import evaluate_reload_target
from .axial_force_history import (AxialForceHistoryState, AxialForceHistoryEvent,
                                 AxialForceHoldResponse, _EPS, _affine, _pieces, _roots)


def _interval_roots(polynomial):
    """All real roots on [0,1], isolated by the derivative's real roots."""
    polynomial = polynomial.trim()
    scale = max(abs(x) for x in polynomial.coef)
    if scale == 0 or polynomial.degree() == 0:
        return ()
    polynomial = polynomial/scale
    if polynomial.degree() <= 2:
        return tuple(root for root in _roots(polynomial) if 0 <= root <= 1)
    critical = [0., *_interval_roots(polynomial.deriv()), 1.]
    critical = sorted(set(critical))
    roots = []
    for x in critical:
        bound = fsum(abs(c*x**i) for i, c in enumerate(polynomial.coef))
        if abs(polynomial(x)) <= 16*_EPS*bound:
            roots.append(x)
    for a, b in zip(critical[:-1], critical[1:]):
        if a in roots or b in roots:
            continue  # A rounded double root must not spawn two crossings.
        fa, fb = polynomial(a), polynomial(b)
        if (fa < 0 < fb) or (fb < 0 < fa):
            roots.append(brentq(polynomial, a, b, xtol=5e-324, rtol=8*_EPS))
    return tuple(sorted(set(roots)))


def _positive_after(polynomial, root):
    for order in range(1, polynomial.degree()+1):
        derivative = polynomial.deriv(order)
        value = derivative(root)
        bound = fsum(abs(c*root**i) for i, c in enumerate(derivative.coef))
        if abs(value) > 16*_EPS*bound:
            return value > 0 and order % 2 == 1
    return False


def _reload_pieces(table, segment, curvature, start, end):
    side = 1 if curvature >= 0 else -1
    target = segment.target
    nodes = {start, end}
    for left, right, *_ in _pieces(table, curvature, start, end, side):
        nodes.update((left, right))
    if target.kind == 'skeleton':
        maximum = target.experienced_curvature
        for left, right, *_ in _pieces(table, target.side*maximum, start, end, target.side):
            nodes.update((left, right))
        rows = sorted(nodes, reverse=end < start)
        for left, right in zip(rows[:-1], rows[1:]):
            a = table.interpolate(left)
            b = table.interpolate(right)
            p0, p1 = ((a.positive, b.positive) if target.side > 0 else (a.negative, b.negative))
            d0, d1 = p0.curvatures[target.threshold-1], p1.curvatures[target.threshold-1]
            # Both max(experienced,d_threshold) and target/anchor order are
            # exact event partitions. No grid samples determine completeness.
            for value in (maximum, target.side*segment.start_delta):
                if d0 != d1:
                    t = (value-d0)/(d1-d0)
                    if 0 < t < 1:
                        nodes.add((1-t)*left+t*right)
    nodes = sorted(nodes, reverse=end < start)
    for left, right in zip(nodes[:-1], nodes[1:]):
        a, b = table.interpolate(left), table.interpolate(right)
        p0, p1 = ((a.positive, b.positive) if target.side > 0 else (a.negative, b.negative))
        fixed = target.kind == 'forward'
        target_distance = None
        if not fixed:
            i = target.threshold-1
            virtual = fsum((p0.curvatures[i]-target.experienced_curvature,
                            p1.curvatures[i]-target.experienced_curvature)) >= 0
            if virtual:
                x = target.side*_affine(p0.curvatures[i], p1.curvatures[i])
                q = target.side*_affine(p0.moments[i], p1.moments[i])
                w = Polynomial((1.,))
            else:
                x = Polynomial((target.side*target.experienced_curvature,))
                _, _, _, q, w = next(_pieces(table, float(x(0)), left, right, target.side))
            width = x-segment.start_delta
            target_distance = target.side*(x-curvature)
            fixed = target.side*fsum((float(width(0)), float(width(1)))) <= 0
        if fixed:
            q = Polynomial((segment.start_P+target.unloading_stiffness*(curvature-segment.start_delta),))
            w = Polynomial((1.,))
        else:
            q = segment.start_P*w*width+(q-segment.start_P*w)*(curvature-segment.start_delta)
            w = w*width
            # Make the denominator positive on the open interval.
            q, w = target.side*q, target.side*w
        _, _, index, envelope_q, envelope_w = next(_pieces(table, curvature, left, right, side))
        yield left, right, index, q, w, envelope_q, envelope_w, target_distance, fixed


def evaluate_reload_hold(table, committed, Nd):
    """First contact discards the old graph and delegates sustained contact."""
    history = committed.history.copy()
    segment = history.active_segment
    curvature = history.current_delta
    side = 1 if curvature >= 0 else -1
    events = []
    evaluate_reload_target(table, segment, committed.Nd)  # Validate metadata before path construction.
    if Nd != committed.Nd:
        pieces = list(_reload_pieces(table, segment, curvature, committed.Nd, Nd))
        for piece_index, (left, right, index, q, w, eq, ew, distance, forward) in enumerate(pieces):
            gap = side*(q*ew-eq*w)
            candidates = list(_interval_roots(gap))
            for endpoint in (0., 1.):
                bound = abs(q(endpoint)*ew(endpoint))+abs(eq(endpoint)*w(endpoint))
                if abs(gap(endpoint)) <= 16*_EPS*bound:
                    candidates.append(endpoint)
            roots = [root for root in candidates if _positive_after(gap, root) and w(root) > 0]
            if any(abs(root-1) <= 32*_EPS for root in roots):
                following = pieces[piece_index+1] if piece_index+1 < len(pieces) else None
                if following is None:
                    limit = table.rows[-1].Nd if Nd > committed.Nd else table.rows[0].Nd
                    if right != limit:
                        following = next(_reload_pieces(table, segment, curvature, right, limit))
                if following is not None:
                    nq, nw, neq, new = following[3:7]
                    if not _positive_after(side*(nq*new-neq*nw), 0.):
                        # At a row corner the old piece's extrapolation can
                        # go outside even though the actual next piece does
                        # not. A touch must not replace the reload stiffness.
                        roots = [root for root in roots if abs(root-1) > 32*_EPS]
            target_roots = []
            if distance is not None and float(distance.deriv()(0)) < 0:
                target_roots = list(_interval_roots(distance))
                for t in (0., 1.):
                    if abs(distance(t)) <= 16*_EPS*(abs(curvature)+abs(segment.start_delta)):
                        target_roots.append(t)
            is_target = bool(target_roots) and (not roots or min(target_roots) <= min(roots)+16*_EPS)
            if is_target:
                roots = target_roots
            elif forward and segment.target.side == side and roots:
                # A forward-intersection target is the endpoint of this
                # reload; reaching it is not unloading-envelope contact.
                is_target = True
            if roots:
                root = min(roots)
                nd = left if root == 0 else right if root == 1 else (1-root)*left+root*right
                response = table.evaluate_skeleton(curvature, nd, side=side)
                history.current_P, history.current_K = response.moment, response.bending_tangent
                history.branch, history.loading_direction = ('skeleton' if is_target else 'envelope_contact'), side
                history.active_segment = None
                history.reversal_stack.clear()
                history.reversal_paths.clear()
                history.delta_max_inner, history.crossed_zero = 0., False
                state = AxialForceHistoryState(nd, history, None if is_target else side,
                                              None if is_target else response.segment)
                events.append(AxialForceHistoryEvent('target' if is_target else 'contact', nd, response.moment, side, index))
                from .axial_force_history import evaluate_axial_force_hold
                result = evaluate_axial_force_hold(table, state, Nd)
                result.state.history.previous_delta = committed.history.current_delta
                result.state.history.previous_P = committed.history.current_P
                return AxialForceHoldResponse(result.state, result.moment, result.bending_tangent,
                                              result.moment_Nd_derivative, (*events, *result.events))
            # A denominator collision without prior contact needs an explicit
            # event law. Never evaluate through a pole or silently freeze M.
            if min(w(0), w(1)) <= 0 and curvature != segment.start_delta:
                raise NumericalConditionError('Moving target reaches its anchor before contact',
                                              reason='axial_force_target_collision', Nd_interval=[left, right])
            if right != Nd:
                moment = float(q(1)/w(1)) if w(1) != 0 else segment.start_P
                events.append(AxialForceHistoryEvent('partition', right, moment, side, index))
    response = evaluate_reload_target(table, segment, Nd)
    moment = segment.start_P+response.stiffness*(curvature-segment.start_delta)
    derivative = response.stiffness_Nd_derivative*(curvature-segment.start_delta)
    if Nd == committed.Nd:
        return AxialForceHoldResponse(AxialForceHistoryState(Nd, history), history.current_P,
                                      history.current_K, derivative, ())
    segment.end_delta, segment.end_P, segment.K = response.curvature, response.moment, response.stiffness
    history.previous_delta, history.previous_P = history.current_delta, history.current_P
    history.current_P, history.current_K = moment, response.stiffness
    state = AxialForceHistoryState(Nd, history)
    return AxialForceHoldResponse(state, moment, response.stiffness, derivative, tuple(events))
