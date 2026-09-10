"""Independent geometry for moving JR targets; no coupled-path claims."""
from copy import deepcopy

import pytest

from fem.nonlinear.axial_force_table import AxialForceRow, AxialForceTable, SkeletonPoints
from fem.nonlinear.hysteresis import JRStiffnessReductionModel

pytestmark = [pytest.mark.unit, pytest.mark.material_nonlinear]


def material(*, moving=False, fourth=False):
    rows = []
    for nd in (0., 1.):
        d = (.001, .002, .003, .004) if fourth else (.001, .002, .003)
        if moving:
            d = tuple(x*(1+.5*nd) for x in d)
        p = (10., 12., 13., 9.) if fourth else (10., 12., 13.)
        points = SkeletonPoints(d, tuple(x*(1-.5*nd) for x in p))
        rows.append(AxialForceRow(nd, points, points))
    return AxialForceTable(tuple(rows), beta=0.)


def experienced(table, path):
    model = JRStiffnessReductionModel(table.interpolate(0.).to_jr_params())
    state = model.create_initial_state()
    for x in path:
        p, k, info = model.get_force_and_stiffness(x, state)
        state = model.update_state(x, p, k, state, info)
    return state


def resolve(table, segment, nd):
    from fem.nonlinear.axial_force_targets import evaluate_reload_target
    return evaluate_reload_target(table, segment, nd)


@pytest.mark.parametrize('sign', [-1, 1])
def test_outer_target_metadata_keeps_experience_separate_from_current_envelope(sign):
    table = material()
    state = experienced(table, [sign*.002, -sign*.0015, sign*.001])
    segment = state.active_segment
    target = segment.target
    assert target.kind == 'skeleton'
    assert (target.side, target.experienced_curvature, target.threshold) == (sign, .002, 1)
    before = deepcopy(state)
    response = resolve(table, segment, .6)
    assert response.curvature == sign*.002
    assert response.moment == pytest.approx(sign*8.4)
    assert response.curvature_Nd_derivative == 0.
    assert response.moment_Nd_derivative == pytest.approx(-sign*6.)
    assert state == before


@pytest.mark.parametrize('sign', [-1, 1])
@pytest.mark.parametrize('nd', [.2, .6, 1.])
def test_virtual_target_moves_and_reload_derivative_includes_target_motion(sign, nd):
    table = material(moving=True)
    state = experienced(table, [sign*.002, 0.])
    segment = state.active_segment
    # Zero at sign*.0008, unexperienced opposite target is point 1.
    target_x = -sign*.001*(1+.5*nd)
    target_p = -sign*10*(1-.5*nd)
    width = target_x-sign*.0008
    k = target_p/width
    dk = (sign*5*width-target_p*(-sign*.0005))/width**2
    response = resolve(table, segment, nd)
    assert (response.curvature, response.moment, response.stiffness) == pytest.approx((target_x, target_p, k))
    assert response.stiffness_Nd_derivative == pytest.approx(dk)
    assert response.curvature_Nd_derivative == -sign*.0005


@pytest.mark.parametrize('sign', [-1, 1])
def test_internal_return_target_is_actual_point_not_the_current_skeleton(sign):
    table = material()
    state = experienced(table, [sign*.002, -sign*.0001, sign*.0012, sign*.00025])
    segment = state.active_segment
    assert segment.branch == 'inner_reloading'
    assert segment.target.kind == 'experienced'
    before = deepcopy(segment)
    for nd in (0., .3, 1.):
        response = resolve(table, segment, nd)
        assert (response.curvature, response.moment, response.stiffness) == pytest.approx(
            (segment.end_delta, segment.end_P, segment.K))
        assert response.curvature_Nd_derivative == response.moment_Nd_derivative == response.stiffness_Nd_derivative == 0.
    assert segment == before


@pytest.mark.parametrize('sign', [-1, 1])
def test_internal_return_reanchors_resumed_outer_line_continuously(sign):
    from fem.nonlinear.axial_force_targets import resume_reload_target
    table = material()
    state = experienced(table, [sign*.002, -sign*.0001, sign*.0012, sign*.00025])
    returning = state.active_segment
    suspended = returning.next_segment
    before = deepcopy(state)
    x, p = returning.end_delta, returning.end_P
    resumed = resume_reload_target(table, suspended, .5, x, p)
    assert (resumed.start_delta, resumed.start_P) == (x, p)
    target = resolve(table, resumed, .5)
    assert resumed.end_P == pytest.approx(target.moment)
    assert resumed.start_P+resumed.K*(resumed.end_delta-x) == pytest.approx(target.moment)
    assert resumed.K == pytest.approx((target.moment-p)/(target.curvature-x))
    assert (resumed.start_delta, resumed.start_P, resumed.end_delta, resumed.end_P, resumed.K) == pytest.approx(
        (-sign*.0001, -sign*5., -sign*.001, -sign*7.5, 25000/9))
    assert state == before
    assert resumed is not suspended


@pytest.mark.parametrize('sign', [-1, 1])
@pytest.mark.parametrize('nd', [.2, .6, .8])
def test_moving_target_derivatives_match_independent_endpoint_differences(sign, nd):
    table = material(moving=True)
    state = experienced(table, [sign*.002, 0.])
    response = resolve(table, state.active_segment, nd)
    for h in (1e-4, 1e-5, 1e-6):
        left, right = (resolve(table, state.active_segment, nd+s*h) for s in (-1, 1))
        for value, derivative in [('curvature', 'curvature_Nd_derivative'), ('moment', 'moment_Nd_derivative'),
                                  ('stiffness', 'stiffness_Nd_derivative')]:
            difference = (getattr(right, value)-getattr(left, value))/(2*h)
            assert difference == pytest.approx(getattr(response, derivative), rel=1e-8, abs=1e-10)


@pytest.mark.parametrize('sign', [-1, 1])
@pytest.mark.parametrize('nd', [.2, .6, 1.])
def test_hold_updates_moving_reload_instead_of_freezing_old_target(sign, nd):
    from fem.nonlinear.axial_force_history import AxialForceHistoryState, evaluate_axial_force_hold
    table = material(moving=True)
    history = experienced(table, [sign*.002, 0.])
    initial = AxialForceHistoryState.from_fixed_history(0., history)
    before = deepcopy(initial)
    response = evaluate_axial_force_hold(table, initial, nd)
    # M(0,Nd) = -sign*8*(1-.5Nd)/(1.8+.5Nd).
    expected = -sign*8*(1-.5*nd)/(1.8+.5*nd)
    derivative = sign*11.2/(1.8+.5*nd)**2
    assert response.moment == pytest.approx(expected, rel=1e-12)
    assert response.moment_Nd_derivative == pytest.approx(derivative, rel=1e-12)
    assert response.bending_tangent == pytest.approx(-expected/(sign*.0008), rel=1e-12)
    assert initial == before
    assert response.state.history.active_segment.end_delta == pytest.approx(-sign*.001*(1+.5*nd))
    state = initial
    for n in (nd/4, nd/2, 3*nd/4, nd):
        state = evaluate_axial_force_hold(table, state, n).state
    assert (state.history.current_P, state.history.current_K) == pytest.approx((response.moment, response.bending_tangent))


@pytest.mark.parametrize('sign', [-1, 1])
def test_internal_actual_return_hold_keeps_target_and_suspended_graph(sign):
    from fem.nonlinear.axial_force_history import AxialForceHistoryState, evaluate_axial_force_hold
    table = material()
    history = experienced(table, [sign*.002, -sign*.0001, sign*.0012, sign*.00025])
    initial = AxialForceHistoryState.from_fixed_history(0., history)
    response = evaluate_axial_force_hold(table, initial, .3)
    assert response.moment == pytest.approx(-sign*2.5)
    assert response.bending_tangent == pytest.approx(50000/7)
    assert response.moment_Nd_derivative == 0.
    assert response.state.history.active_segment == history.active_segment


@pytest.mark.parametrize('sign', [-1, 1])
def test_forward_intersection_after_nominal_target_overtakes_anchor(sign):
    from fem.nonlinear.hysteresis.base_hysteresis import HysteresisSegment, JRReloadTarget
    table = material(fourth=True)
    # The nominal first point .001 is behind anchor .004. Frozen Kd=2000
    # intersects K4: 2000(x-.004)=(25-4000x)*(1-.5Nd).
    segment = HysteresisSegment(sign*.004, 0., sign*.0055, sign*3., 2000., 'reloading',
                               target=JRReloadTarget('skeleton', sign, 0., 1, 2000.))
    for nd in (0., .4, 1.):
        f = 1-.5*nd
        x = (8+25*f)/(2000+4000*f)
        dx = (-12.5*(2000+4000*f)+2000*(8+25*f))/(2000+4000*f)**2
        response = resolve(table, segment, nd)
        assert response.kind == 'forward'
        assert (response.curvature, response.moment, response.stiffness) == pytest.approx(
            (sign*x, sign*2000*(x-.004), 2000.))
        assert (response.curvature_Nd_derivative, response.moment_Nd_derivative) == pytest.approx((sign*dx, sign*2000*dx))
        assert response.stiffness_Nd_derivative == 0.


@pytest.mark.parametrize('sign', [-1, 1])
@pytest.mark.parametrize('curvature,expected,contact', [(.005, -6., False), (.007, -9., True)])
def test_reload_hold_uses_curvature_side_even_beyond_negative_K4_force_zero(sign, curvature, expected, contact):
    from fem.nonlinear.axial_force_history import AxialForceHistoryState, evaluate_axial_force_hold
    # The opposite target stays fixed. A contact with positive-side K4
    # cannot be inferred from abs(M), even though M is negative.
    positive0 = SkeletonPoints((.001, .002, .003, .004), (10., 12., 13., 9.))
    positive1 = SkeletonPoints((.001, .002, .003, .004), (30., 36., 39., 27.))
    negative = positive0
    rows = tuple(AxialForceRow(nd, p if sign > 0 else negative, negative if sign > 0 else p)
                 for nd, p in ((0., positive0), (1., positive1)))
    table = AxialForceTable(rows, beta=0.)
    # A local reload at x has M=-6 exactly: zero=2*x+.002, target=(-.002,-12).
    # At x=.007 the positive K4 envelope -3-6Nd catches it at Nd=.5.
    from fem.nonlinear.hysteresis.base_hysteresis import HysteresisSegment, HysteresisState, JRReloadTarget
    zero = 2*curvature+.002
    k = 12/(zero+.002)
    segment = HysteresisSegment(sign*zero, 0., -sign*.002, -sign*12., k, 'reloading',
                               target=JRReloadTarget('skeleton', -sign, .002, 1, 2000.))
    x = sign*curvature
    m = segment.K*(x-segment.start_delta)
    history = HysteresisState(current_delta=x, current_P=m, current_K=segment.K,
                              delta_max_pos=zero if sign > 0 else .002,
                              delta_max_neg=.002 if sign > 0 else zero,
                              branch='reloading', loading_direction=-sign, active_segment=segment)
    response = evaluate_axial_force_hold(table, AxialForceHistoryState.from_fixed_history(0., history), 1.)
    assert response.moment == pytest.approx(sign*expected)
    assert response.state.contact_side == (sign if contact else None)
    if contact:
        assert next(e.Nd for e in response.events if e.kind == 'contact') == pytest.approx(.5)
        assert response.state.history.active_segment is None
        assert response.state.history.reversal_paths == []


@pytest.mark.parametrize('sign', [-1, 1])
def test_moving_virgin_target_reaches_held_curvature_before_overtaking_origin(sign):
    from fem.nonlinear.axial_force_history import AxialForceHistoryState, evaluate_axial_force_hold
    fixed = SkeletonPoints((.001, .002, .003), (10., 12., 13.))
    moving = SkeletonPoints((.0004, .0008, .0012), (10., 12., 13.))
    table = AxialForceTable((AxialForceRow(0., fixed, fixed),
                            AxialForceRow(1., moving if sign > 0 else fixed, fixed if sign > 0 else moving)), beta=0.)
    initial = AxialForceHistoryState.from_fixed_history(0., experienced(table, [-sign*.002, sign*.0007]))
    result = evaluate_axial_force_hold(table, initial, 1.)
    # Target phi=.001*(1-.6Nd) meets held phi=.0007 at Nd=.5, M=10.
    # Afterwards the second skeleton branch gives 10+5000*(.0007-.0004).
    assert result.moment == pytest.approx(sign*11.5)
    assert result.state.history.branch == 'skeleton'
    event = next(event for event in result.events if event.kind == 'target')
    assert (event.Nd, event.moment) == pytest.approx((.5, sign*10.))
    state = initial
    for n in (.2, .49, .5, .51, .8, 1.):
        state = evaluate_axial_force_hold(table, state, n).state
    assert state.history.current_P == pytest.approx(result.moment)


@pytest.mark.parametrize('sign', [-1, 1])
def test_zero_Nd_increment_on_moving_reload_preserves_state_but_not_zero_derivative(sign):
    from fem.nonlinear.axial_force_history import AxialForceHistoryState, evaluate_axial_force_hold
    table = material(moving=True)
    initial = AxialForceHistoryState.from_fixed_history(0., experienced(table, [sign*.002, 0.]))
    response = evaluate_axial_force_hold(table, initial, 0.)
    assert response.state == initial
    assert response.state.history is not initial.history
    assert response.moment_Nd_derivative == pytest.approx(sign*11.2/1.8**2)


@pytest.mark.parametrize('roots', [(.1, .2, .6, .9), (.2, .2, .7, .9), (.05, .3, .7)])
def test_contact_polynomial_isolation_finds_all_crossings_and_touches(roots):
    from numpy.polynomial import Polynomial
    from fem.nonlinear.axial_force_reload_hold import _interval_roots
    polynomial = Polynomial.fromroots(roots)
    for scale in (1e-18, 1., 1e18):
        actual = _interval_roots(scale*polynomial)
        assert actual == pytest.approx(sorted(set(roots)), abs=2e-12)


@pytest.mark.parametrize('sign', [-1, 1])
@pytest.mark.parametrize('force_scale,length_scale', [(1., 1.), (1e-6, 1e3), (1e6, 1e-3)])
def test_reload_two_intersections_contact_departure_and_unit_conversion(sign, force_scale, length_scale):
    from math import sqrt
    from fem.nonlinear.axial_force_history import AxialForceHistoryState, evaluate_axial_force_hold
    from fem.nonlinear.hysteresis.base_hysteresis import HysteresisSegment, HysteresisState, JRReloadTarget
    sf, sl = force_scale, length_scale
    sm, sk = sf*sl, sf*sl**2
    rows = []
    for nd in (0., 1.4):
        points = SkeletonPoints(tuple(x/sl for x in (.001, .003-.001*nd, .004)),
                                tuple(p*sm for p in (10-2*nd, 12-nd, 12.5)))
        rows.append(AxialForceRow(nd*sf, points, points))
    table = AxialForceTable(tuple(rows), beta=0.)
    # Reload from zero=-.0085 to target (.004,12.5) has K=1000 and
    # M(.0015)=10. Moving local envelope 9.5-2Nd+2/(2-Nd) catches it
    # at (7-sqrt(17))/8, then departs at Nd=1, M=9.5, Kd=3000.
    segment = HysteresisSegment(-sign*.0085/sl, 0., sign*.004/sl, sign*12.5*sm, 1000*sk,
                               'reloading', target=JRReloadTarget('skeleton', sign, .004/sl, 1, 2000*sk))
    history = HysteresisState(current_delta=sign*.0015/sl, current_P=sign*10*sm, current_K=1000*sk,
                              delta_max_pos=.004/sl, delta_max_neg=.004/sl, active_segment=segment,
                              branch='reloading', loading_direction=sign)
    initial = AxialForceHistoryState.from_fixed_history(0., history)
    saved = deepcopy(initial)
    response = evaluate_axial_force_hold(table, initial, 1.4*sf)
    assert response.moment/sm == pytest.approx(sign*9.5, rel=1e-12)
    assert response.bending_tangent/sk == pytest.approx(3000., rel=1e-12)
    events = [e for e in response.events if e.kind in ('contact', 'departure')]
    assert [e.Nd/sf for e in events] == pytest.approx([(7-sqrt(17))/8, 1.], rel=1e-12)
    for count in (8, 16, 32):
        state = initial
        for step in range(1, count+1):
            state = evaluate_axial_force_hold(table, state, 1.4*sf*step/count).state
        assert (state.history.current_P/sm, state.history.current_K/sk) == pytest.approx((sign*9.5, 3000.), rel=1e-12)
    assert initial == saved


@pytest.mark.parametrize('sign', [-1, 1])
@pytest.mark.parametrize('nd', [.15, .5, .85])
def test_reload_hold_Nd_tangent_multiple_difference_sizes_and_discarded_trials(sign, nd):
    from fem.nonlinear.axial_force_history import AxialForceHistoryState, evaluate_axial_force_hold
    table = material(moving=True)
    initial = AxialForceHistoryState.from_fixed_history(0., experienced(table, [sign*.002, 0.]))
    saved = deepcopy(initial)
    response = evaluate_axial_force_hold(table, initial, nd)
    for h in (1e-4, 1e-5, 1e-6):
        left, right = (evaluate_axial_force_hold(table, initial, nd+s*h).moment for s in (-1, 1))
        assert (right-left)/(2*h) == pytest.approx(sign*11.2/(1.8+.5*nd)**2, rel=1e-8)
    for trial in (.9, .05, .4, .7):
        evaluate_axial_force_hold(table, initial, trial)
    assert evaluate_axial_force_hold(table, initial, nd) == response
    response.state.history.active_segment.target = None
    assert initial == saved


@pytest.mark.parametrize('bad_nd', [-.01, 1.01, float('nan'), True])
def test_target_failure_preserves_suspended_graph(bad_nd):
    from fem.diagnostics import InputValidationError
    table = material()
    state = experienced(table, [.002, -.0001, .0012, .00025])
    saved = deepcopy(state)
    with pytest.raises(InputValidationError):
        resolve(table, state.active_segment, bad_nd)
    assert state == saved


@pytest.mark.parametrize('sign', [-1, 1])
@pytest.mark.parametrize('curvature,expected,stiffness', [(-.0001, -5., 25000/9), (-.0005, -55/9, 25000/9),
                                                       (-.001, -7.5, 1500.), (-.0015, -8.25, 1500.)])
def test_fixed_Nd_curvature_operation_restores_actual_point_then_moved_outer_target(sign, curvature, expected, stiffness):
    from fem.nonlinear.axial_force_history import AxialForceHistoryState, evaluate_axial_force_hold
    from fem.nonlinear.axial_force_targets import evaluate_axial_force_curvature
    table = material()
    initial = AxialForceHistoryState.from_fixed_history(0., experienced(table, [sign*.002, -sign*.0001, sign*.0012, sign*.00025]))
    held = evaluate_axial_force_hold(table, initial, .5).state
    saved = deepcopy(held)
    result = evaluate_axial_force_curvature(table, held, sign*curvature)
    assert (result.moment, result.bending_tangent) == pytest.approx((sign*expected, stiffness), rel=1e-12)
    assert result.state.Nd == .5
    assert result.state.history.reversal_stack == []
    assert held == saved
    split = evaluate_axial_force_curvature(table, held, -sign*.0001).state
    split = evaluate_axial_force_curvature(table, split, sign*curvature)
    assert split.moment == pytest.approx(result.moment)


@pytest.mark.parametrize('sign', [-1, 1])
def test_fixed_Nd_curvature_operation_refreshes_reload_suspended_under_unloading(sign):
    from fem.nonlinear.axial_force_history import AxialForceHistoryState, evaluate_axial_force_hold
    from fem.nonlinear.axial_force_targets import evaluate_axial_force_curvature
    table = material()
    initial = AxialForceHistoryState.from_fixed_history(0., experienced(table, [sign*.002, sign*.0015]))
    # Nd .4 does not contact the held unload M=7. Its zero stays .0008,
    # but its as-yet unvisited opposite target is now (-.001,-8).
    held = evaluate_axial_force_hold(table, initial, .4).state
    result = evaluate_axial_force_curvature(table, held, -sign*.0001)
    assert result.moment == pytest.approx(-sign*4.)
    assert result.bending_tangent == pytest.approx(40000/9)


@pytest.mark.parametrize('sign', [-1, 1])
def test_invariant_table_fixed_Nd_curvature_operation_reduces_to_JR_nested_paths(sign):
    from fem.nonlinear.axial_force_history import AxialForceHistoryState
    from fem.nonlinear.axial_force_targets import evaluate_axial_force_curvature
    points = SkeletonPoints((.001, .002, .003), (10., 12., 13.))
    table = AxialForceTable((AxialForceRow(0., points, points), AxialForceRow(1., points, points)), beta=0.)
    model = JRStiffnessReductionModel(table.interpolate(.7).to_jr_params())
    fixed = model.create_initial_state()
    state = AxialForceHistoryState.from_fixed_history(.7, fixed)
    for x in (.002, -.0001, .0012, .00025, .00085, .00025, -.0001, -.001, -.002, .001):
        p, k, info = model.get_force_and_stiffness(sign*x, fixed)
        fixed = model.update_state(sign*x, p, k, fixed, info)
        response = evaluate_axial_force_curvature(table, state, sign*x)
        state = response.state
        assert (response.moment, response.bending_tangent) == pytest.approx((p, k), rel=1e-12)
        assert state.history.reversal_stack == fixed.reversal_stack


@pytest.mark.parametrize('sign', [-1, 1])
def test_resumed_actual_anchor_survives_another_Nd_hold(sign):
    from fem.nonlinear.axial_force_history import AxialForceHistoryState, evaluate_axial_force_hold
    from fem.nonlinear.axial_force_targets import evaluate_axial_force_curvature
    table = material()
    initial = AxialForceHistoryState.from_fixed_history(0., experienced(table, [sign*.002, -sign*.0001, sign*.0012, sign*.00025]))
    held = evaluate_axial_force_hold(table, initial, .5).state
    resumed = evaluate_axial_force_curvature(table, held, -sign*.0005).state
    saved = deepcopy(resumed)
    result = evaluate_axial_force_hold(table, resumed, .75)
    # New anchor=(-.0001,-5), moved target=(-.001,-6.25).
    assert (result.moment, result.moment_Nd_derivative) == pytest.approx((-sign*50/9, sign*20/9))
    assert (result.state.history.active_segment.start_delta, result.state.history.active_segment.start_P) == (
        -sign*.0001, -sign*5.)
    assert resumed == saved


@pytest.mark.parametrize('sign', [-1, 1])
def test_return_to_displaced_skeleton_is_diagnosed_without_mutation(sign):
    from fem.diagnostics import UnsupportedAnalysisError
    from fem.nonlinear.axial_force_history import AxialForceHistoryState, evaluate_axial_force_hold
    from fem.nonlinear.axial_force_targets import evaluate_axial_force_curvature
    table = material()
    initial = AxialForceHistoryState.from_fixed_history(0., experienced(table, [sign*.002, sign*.0015]))
    held = evaluate_axial_force_hold(table, initial, .4).state
    saved = deepcopy(held)
    with pytest.raises(UnsupportedAnalysisError) as failure:
        evaluate_axial_force_curvature(table, held, sign*.002)
    assert failure.value.details['reason'] == 'axial_force_return_to_moved_skeleton'
    assert held == saved


@pytest.mark.parametrize('sign', [-1, 1])
def test_virgin_threshold_identity_does_not_jump_when_source_yield_point_moves(sign):
    fixed = SkeletonPoints((.001, .002, .003), (10., 12., 13.))
    moved = SkeletonPoints((.003, .006, .009), (10., 12., 13.))
    table = AxialForceTable((AxialForceRow(0., fixed, fixed),
                            AxialForceRow(1., moved if sign > 0 else fixed, fixed if sign > 0 else moved)), beta=0.)
    state = experienced(table, [sign*.005, -sign*.0018])
    assert state.active_segment.target.threshold == 2
    # Source phi_max=.005 crosses the moving delta2 at Nd=.75. The target
    # remains the selected negative point2, not point1 or a forward fallback.
    for nd in (.7, .75, .8, 1.):
        result = resolve(table, state.active_segment, nd)
        assert (result.curvature, result.moment, result.stiffness) == pytest.approx((-sign*.002, -sign*12., 24000.))
        assert result.kind == 'skeleton'


@pytest.mark.parametrize('sign', [-1, 1])
def test_forward_target_arrival_ends_reload_before_Nd_reversal(sign):
    from fem.nonlinear.axial_force_history import AxialForceHistoryState, evaluate_axial_force_hold
    from fem.nonlinear.hysteresis.base_hysteresis import HysteresisSegment, HysteresisState, JRReloadTarget
    table = material(fourth=True)
    segment = HysteresisSegment(sign*.004, 0., sign*.0055, sign*3., 2000., 'reloading',
                               target=JRReloadTarget('forward', sign, unloading_stiffness=2000.))
    history = HysteresisState(current_delta=sign*.0052, current_P=sign*2.4, current_K=2000.,
                              delta_max_pos=.006, delta_max_neg=.006, active_segment=segment,
                              branch='reloading', loading_direction=sign)
    result = evaluate_axial_force_hold(table, AxialForceHistoryState.from_fixed_history(0., history), 1.)
    assert result.moment == pytest.approx(sign*2.1)
    assert result.state.history.branch == 'skeleton'
    assert next(e.Nd for e in result.events if e.kind == 'target') == pytest.approx(6/7)
    reverse = evaluate_axial_force_hold(table, result.state, 0.)
    assert reverse.moment == pytest.approx(sign*4.2)


@pytest.mark.parametrize('sign', [-1, 1])
@pytest.mark.parametrize('branch', ['reloading', 'unloading'])
def test_reload_touch_at_Nd_row_corner_keeps_original_branch(sign, branch):
    from fem.nonlinear.axial_force_history import AxialForceHistoryState, evaluate_axial_force_hold
    from fem.nonlinear.hysteresis.base_hysteresis import HysteresisSegment, HysteresisState, JRReloadTarget
    rows = []
    for nd, p in ((0., (10., 12., 12.)), (1., (9., 11., 12.)), (2., (10., 12., 12.))):
        points = SkeletonPoints((.001, .002, .003), p)
        rows.append(AxialForceRow(nd, points, points))
    table = AxialForceTable(tuple(rows), beta=0.)
    # M_b(.0015) is 11 -> 10 -> 11. The reload M=10 only touches
    # the row corner. There is no outside excursion and no new Kd.
    segment = HysteresisSegment(-sign*.011, 0., sign*.004, sign*12., 800., 'reloading',
                               target=JRReloadTarget('skeleton', sign, .004, 1, 2000.))
    history = HysteresisState(current_delta=sign*.0015, current_P=sign*10., current_K=800.,
                              delta_max_pos=.004, delta_max_neg=.004, active_segment=segment,
                              branch='reloading', loading_direction=sign)
    if branch == 'unloading':
        history.branch, history.loading_direction = branch, -sign
        history.active_segment = HysteresisSegment(sign*.002, sign*10.4, -sign*.011, 0., 800., branch)
    result = evaluate_axial_force_hold(table, AxialForceHistoryState.from_fixed_history(0., history), 2.)
    assert (result.moment, result.bending_tangent) == pytest.approx((sign*10., 800.))
    assert not any(e.kind in ('contact', 'departure') for e in result.events)
    first = evaluate_axial_force_hold(table, AxialForceHistoryState.from_fixed_history(0., history), 1.)
    split = evaluate_axial_force_hold(table, first.state, 2.)
    assert (split.moment, split.bending_tangent) == pytest.approx((sign*10., 800.))
