"""JR stiffness reduction, manual 7.11; phase-3 conventions in the TDD plan.

The scalar deformation is section strain, curvature or twist per length.
Evaluation traces a monotone trial from the committed point, without mutation.
"""
from dataclasses import dataclass, fields
from math import isfinite, isclose
from typing import Optional

from .base_hysteresis import BaseHysteresis, HysteresisState, HysteresisSegment


@dataclass
class JRStiffnessReductionParams:
    """Nonnegative magnitudes on both sides; final (fourth) slope is zero.

    This RC model accepts decreasing nonnegative skeleton slopes. K_min is a
    floor on unloading stiffness only, bounded by both initial stiffnesses.
    """
    delta_1_pos: float
    delta_2_pos: float
    delta_3_pos: float
    P_1_pos: float
    P_2_pos: float
    P_3_pos: float
    delta_1_neg: float
    delta_2_neg: float
    delta_3_neg: float
    P_1_neg: float
    P_2_neg: float
    P_3_neg: float
    beta: float
    K_min: Optional[float] = None

    def __post_init__(self):
        for item in fields(self):
            value = getattr(self, item.name)
            if item.name == 'K_min' and value is None:
                continue
            if not isinstance(value, (int, float)) or not isfinite(value):
                raise ValueError(f'{item.name} must be finite')
        for side in ('pos', 'neg'):
            d1, d2, d3 = (getattr(self, f'delta_{i}_{side}') for i in (1, 2, 3))
            p1, p2, p3 = (getattr(self, f'P_{i}_{side}') for i in (1, 2, 3))
            if not 0 < d1 < d2 < d3:
                raise ValueError(f'{side}: require 0 < delta_1 < delta_2 < delta_3')
            if not 0 < p1 <= p2 <= p3:
                raise ValueError(f'{side}: require 0 < P_1 <= P_2 <= P_3')
            k1, k2, k3 = p1/d1, (p2-p1)/(d2-d1), (p3-p2)/(d3-d2)
            if not all(isfinite(k) for k in (k1, k2, k3)) or not 0 <= k3 <= k2 <= k1:
                raise ValueError(f'{side}: require finite K1 >= K2 >= K3 >= 0')
        if self.beta < 0:
            raise ValueError('beta must be nonnegative')
        upper = min(self.K_1_pos, self.K_1_neg)
        if self.K_min is None:
            self.K_min = upper * .01
        if not 0 < self.K_min <= upper:
            raise ValueError('require 0 < K_min <= min(K1_pos, K1_neg)')

    @classmethod
    def symmetric(cls, delta_1, delta_2, delta_3, P_1, P_2, P_3, beta, K_min=None):
        return cls(delta_1, delta_2, delta_3, P_1, P_2, P_3,
                   delta_1, delta_2, delta_3, P_1, P_2, P_3, beta, K_min)

    @property
    def K_1_pos(self):
        return self.P_1_pos / self.delta_1_pos

    @property
    def K_1_neg(self):
        return self.P_1_neg / self.delta_1_neg

    @property
    def K_2_pos(self):
        return (self.P_2_pos-self.P_1_pos)/(self.delta_2_pos-self.delta_1_pos)

    @property
    def K_2_neg(self):
        return (self.P_2_neg-self.P_1_neg)/(self.delta_2_neg-self.delta_1_neg)

    @property
    def K_3_pos(self):
        return (self.P_3_pos-self.P_2_pos)/(self.delta_3_pos-self.delta_2_pos)

    @property
    def K_3_neg(self):
        return (self.P_3_neg-self.P_2_neg)/(self.delta_3_neg-self.delta_2_neg)


class JRStiffnessReductionModel(BaseHysteresis):
    """Affine branches, exact zero/target events, and suspended return paths.

    previous_* are diagnostic only. None active_segment denotes the skeleton;
    a segment endpoint restores its continuation and (if set) stack depth.
    """

    def __init__(self, params: JRStiffnessReductionParams):
        self.params = params

    @staticmethod
    def _side(direction):
        if direction not in (-1, 1):
            raise ValueError('direction must be +1 or -1')
        return 'pos' if direction == 1 else 'neg'

    def _value(self, name, direction):
        return getattr(self.params, f'{name}_{self._side(direction)}')

    def create_initial_state(self):
        return HysteresisState(current_K=self.params.K_1_pos)

    def get_skeleton_force(self, delta, direction):
        if not isfinite(delta):
            raise ValueError('delta must be finite')
        self._side(direction)
        d = abs(delta)
        start_d = start_p = 0.
        for i in (1, 2, 3):
            end_d = self._value(f'delta_{i}', direction)
            end_p = self._value(f'P_{i}', direction)
            if d < end_d:
                k = (end_p-start_p)/(end_d-start_d)
                return direction*(start_p+k*(d-start_d)), k
            start_d, start_p = end_d, end_p
        return direction*start_p, 0.

    def get_reduced_stiffness(self, state, direction):
        side = self._side(direction)
        d = getattr(state, f'delta_max_{side}')
        d1, d2 = (self._value(f'delta_{i}', direction) for i in (1, 2))
        k1, k2 = (self._value(f'K_{i}', direction) for i in (1, 2))
        if d <= d1:
            return k1
        if d <= d2:
            kd = max(k2, k1*(d/d1)**(-self.params.beta))
        else:
            # The secant lower bound in 7.11.1 does NOT apply to 7.11.2/3.
            kd = k2*(d/d2)**(-self.params.beta)
        return min(k1, max(self.params.K_min, kd))

    def get_target_point(self, direction, state):
        """Outer target in the explicit travel direction (not sign of delta)."""
        side = self._side(direction)
        source = self._side(-direction)
        source_max = getattr(state, f'delta_max_{source}')
        threshold = 2 if source_max > self._value('delta_2', -direction) else 1
        d = max(getattr(state, f'delta_max_{side}'), self._value(f'delta_{threshold}', direction))
        return direction*d, self.get_skeleton_force(direction*d, direction)[0]

    @staticmethod
    def _reached(delta, end, direction):
        # Only roundoff at an event is snapped, never a finite hold increment.
        return direction*(delta-end) >= 0 or isclose(delta, end, rel_tol=2e-14, abs_tol=0.)

    def _forward_intersection(self, zero, kd, direction):
        """Extend to the first forward envelope intersection if zero overshoots.

        Solve kd*(delta-zero)=a+k*delta on each forward skeleton interval.
        The constant final capacity and kd>0 guarantee a finite intersection.
        """
        start = start_p = 0.
        for i in (1, 2, 3, 4):
            end = self._value(f'delta_{i}', direction) if i < 4 else float('inf')
            end_p = self._value(f'P_{i}', direction) if i < 4 else start_p
            k = (end_p-start_p)/(end-start) if i < 4 else 0.
            intercept = direction*(start_p-k*start)
            if kd != k:
                x = (kd*zero+intercept)/(kd-k)
                if start <= direction*x <= end and direction*(x-zero) > 0:
                    if not isfinite(x):
                        raise ValueError('JR target overflow')
                    return x, direction*(start_p+k*(direction*x-start))
            start, start_p = end, end_p
        raise ValueError('No forward JR skeleton intersection')

    def _reverse(self, s, direction):
        old = s.active_segment
        x, p = s.current_delta, s.current_P
        s.reversal_delta, s.reversal_P = x, p
        s.crossed_zero = False
        if (old is not None and old.unloading_origin is not None
                and isclose(x, old.start_delta, rel_tol=2e-14, abs_tol=0.)):
            origin_x, origin_p, kd = old.unloading_origin
            s.active_segment = HysteresisSegment(
                x, p, origin_x, origin_p, kd, 'retracing',
                next_segment=old.reverse_segment, restore_depth=old.origin_depth,
                reverse_segment=old, origin_depth=len(s.reversal_stack))
            return
        if old is not None and old.branch in ('unloading', 'inner_unloading', 'retracing'):
            # Before zero: retrace this same line, then restore the old path.
            s.active_segment = HysteresisSegment(
                x, p, old.start_delta, old.start_P, old.K, 'retracing',
                next_segment=old.reverse_segment, restore_depth=old.origin_depth,
                reverse_segment=old, origin_depth=len(s.reversal_stack))
            return

        depth = len(s.reversal_stack)
        if old is not None:
            s.reversal_stack.append((x, p))
            s.reversal_paths.append(old)

        source_direction = 1 if p > 0 else -1 if p < 0 else -direction
        kd = self.get_reduced_stiffness(s, source_direction)
        zero = x-p/kd
        if len(s.reversal_stack) >= 2:
            target_x, target_p = s.reversal_stack[-2]
            continuation = s.reversal_paths[-2]
            restore_depth = len(s.reversal_stack)-2
        else:
            target_x, target_p = self.get_target_point(direction, s)
            continuation, restore_depth = None, 0
        if direction*(target_x-zero) <= 0:
            # No forward, positive-slope connection to that target is possible.
            target_x, target_p = self._forward_intersection(zero, kd, direction)
            continuation, restore_depth = None, 0
        reload_k = target_p/(target_x-zero)
        if not all(isfinite(v) for v in (zero, target_x, reload_k)) or reload_k <= 0:
            raise ValueError('Invalid JR reloading geometry')
        inner = old is not None
        reload = HysteresisSegment(
            zero, 0., target_x, target_p, reload_k,
            'inner_reloading' if inner else 'reloading',
            next_segment=continuation, restore_depth=restore_depth,
            reverse_segment=old, origin_depth=depth, unloading_origin=(x, p, kd))
        s.active_segment = HysteresisSegment(
            x, p, zero, 0., kd, 'inner_unloading' if inner else 'unloading',
            next_segment=reload, reverse_segment=old, origin_depth=depth)

    def _trace(self, s, delta, direction):
        while s.active_segment is not None:
            segment = s.active_segment
            if not self._reached(delta, segment.end_delta, direction):
                s.branch = segment.branch
                s.crossed_zero = segment.branch in ('reloading', 'inner_reloading')
                return segment.start_P+segment.K*(delta-segment.start_delta), segment.K
            if segment.restore_depth is not None:
                del s.reversal_stack[segment.restore_depth:]
                del s.reversal_paths[segment.restore_depth:]
            s.active_segment = segment.next_segment
        s.branch = 'skeleton'
        s.crossed_zero = False
        s.reversal_stack.clear()
        s.reversal_paths.clear()
        return self.get_skeleton_force(delta, direction if delta == 0 else (1 if delta > 0 else -1))

    def get_force_and_stiffness(self, delta, state):
        if not isfinite(delta):
            raise ValueError('delta must be finite')
        if delta == state.current_delta:
            return state.current_P, state.current_K, None
        s = state.copy()
        direction = 1 if delta > s.current_delta else -1
        elastic = s.is_elastic(self.params.delta_1_pos, self.params.delta_1_neg)
        if elastic and s.active_segment is None:
            p, k = self.get_skeleton_force(delta, direction if delta == 0 else (1 if delta > 0 else -1))
            s.branch = 'initial' if abs(delta) < self._value('delta_1', 1 if delta >= 0 else -1) else 'skeleton'
        else:
            if s.loading_direction and direction != s.loading_direction:
                self._reverse(s, direction)
            p, k = self._trace(s, delta, direction)
        if not all(isfinite(v) for v in (p, k)):
            raise ValueError('Nonfinite JR response')
        s.previous_delta, s.previous_P = state.current_delta, state.current_P
        s.current_delta, s.current_P, s.current_K = delta, p, k
        s.loading_direction = direction
        # All experienced extrema, including points inside the envelope. Active
        # branches retain their fixed targets/slopes until the next reversal.
        if delta > s.delta_max_pos:
            s.delta_max_pos, s.P_max_pos = delta, abs(p)
        if -delta > s.delta_max_neg:
            s.delta_max_neg, s.P_max_neg = -delta, abs(p)
        s.delta_max_inner = abs(s.reversal_stack[-1][0]) if s.reversal_stack else 0.
        return p, k, {'branch': s.branch, 'candidate': s}

    def update_state(self, delta, P, K, state, branch_info):
        """Return the evaluated candidate; only the element's commit accepts it."""
        if not all(isfinite(v) for v in (delta, P, K)):
            raise ValueError('JR state values must be finite')
        if branch_info is None:
            if (delta, P, K) != (state.current_delta, state.current_P, state.current_K):
                raise ValueError('Missing JR candidate for changed response')
            return state.copy()
        candidate = branch_info['candidate']
        if (delta, P, K) != (candidate.current_delta, candidate.current_P, candidate.current_K):
            raise ValueError('JR response does not match evaluated candidate')
        return candidate.copy()
