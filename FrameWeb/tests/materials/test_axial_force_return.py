"""Independent line/envelope intersections for direct returns at fixed Nd."""
from copy import deepcopy

import pytest

from fem.nonlinear.axial_force_history import AxialForceHistoryState, evaluate_axial_force_hold
from fem.nonlinear.axial_force_table import AxialForceRow, AxialForceTable, SkeletonPoints
from fem.nonlinear.axial_force_targets import evaluate_axial_force_curvature
from fem.nonlinear.hysteresis import JRStiffnessReductionModel


def material(force=1., length=1., fourth=False):
    d = (.001, .002, .003, .004) if fourth else (.001, .002, .003)
    p = (10., 12., 13., 9.) if fourth else (10., 12., 13.)
    rows = []
    for nd in (0., 1.):
        points = SkeletonPoints(tuple(x/length for x in d),
                                tuple(x*(1-.5*nd)*force*length for x in p))
        rows.append(AxialForceRow(nd*force, points, points))
    return AxialForceTable(tuple(rows), beta=0.)


def history(table, nd, path):
    model = JRStiffnessReductionModel(table.interpolate(nd).to_jr_params())
    state = model.create_initial_state()
    for x in path:
        p, k, info = model.get_force_and_stiffness(x, state)
        state = model.update_state(x, p, k, state, info)
    return AxialForceHistoryState.from_fixed_history(nd, state)


@pytest.mark.parametrize('sign', [-1, 1])
@pytest.mark.parametrize('x,p,k', [(.0016, 8., 10000.), (3/1750, 64/7, 1600.),
                                  (.0018, 9.28, 1600.), (.002, 9.6, 800.)])
def test_contracting_skeleton_intercepts_return_before_saved_maximum(sign, x, p, k):
    table = material()
    start = history(table, 0., [sign*.002, sign*.0015])
    held = evaluate_axial_force_hold(table, start, .4).state
    saved = deepcopy(held)
    result = evaluate_axial_force_curvature(table, held, sign*x)
    # 10000*x-8 = .8*(8+2000*x) at x=3/1750, M=64/7.
    assert (result.moment, result.bending_tangent) == pytest.approx((sign*p, k))
    assert held == saved
    assert result.state.history.delta_max_pos == held.history.delta_max_pos
    assert result.state.history.delta_max_neg == held.history.delta_max_neg
    if x >= 3/1750:
        assert result.state.history.branch == 'skeleton'
        assert result.state.history.active_segment is None


@pytest.mark.parametrize('sign', [-1, 1])
@pytest.mark.parametrize('x,p,k', [(.0018, 8., 8000.), (.002, 9.6, 8000.),
                                  (.0022, 11.2, 8000.), (41/17500, 432/35, 1000.),
                                  (.0025, 12.5, 1000.)])
def test_expanding_skeleton_keeps_actual_point_and_extends_same_return_line(sign, x, p, k):
    table = material()
    start = history(table, .4, [sign*.002, sign*.0015])
    held = evaluate_axial_force_hold(table, start, 0.).state
    saved = deepcopy(held)
    result = evaluate_axial_force_curvature(table, held, sign*x)
    # 8000*x-6.4 = 10+1000*x at x=41/17500, M=432/35.
    assert (result.moment, result.bending_tangent) == pytest.approx((sign*p, k))
    assert held == saved
    if .002 <= x < 41/17500:
        segment = result.state.history.active_segment
        assert segment.branch == 'retracing'
        assert (segment.start_delta, segment.start_P) == pytest.approx((sign*.002, sign*9.6))
        assert segment.target.kind == 'forward'


@pytest.mark.parametrize('sign', [-1, 1])
@pytest.mark.parametrize('initial_nd,held_nd,end,breaks', [
    (0., .4, .0025, (.0016, 3/1750, .0018, .002)),
    (.4, 0., .0025, (.0018, .002, .0022, 41/17500)),
])
def test_return_is_independent_of_curvature_partition(sign, initial_nd, held_nd, end, breaks):
    table = material()
    held = evaluate_axial_force_hold(table, history(table, initial_nd, [sign*.002, sign*.0015]), held_nd).state
    whole = evaluate_axial_force_curvature(table, held, sign*end)
    split = held
    for x in (*breaks, end):
        split = evaluate_axial_force_curvature(table, split, sign*x).state
    assert (split.history.current_P, split.history.current_K) == pytest.approx((whole.moment, whole.bending_tangent))
    assert split.history.active_segment is None
    assert split.history.reversal_stack == []


@pytest.mark.parametrize('sign', [-1, 1])
def test_reversal_during_extension_retraces_through_actual_point(sign):
    table = material()
    held = evaluate_axial_force_hold(table, history(table, .4, [sign*.002, sign*.0015]), 0.).state
    extended = evaluate_axial_force_curvature(table, held, sign*.0022).state
    for x, p in ((.0021, 10.4), (.002, 9.6), (.0018, 8.)):
        result = evaluate_axial_force_curvature(table, extended, sign*x)
        assert (result.moment, result.bending_tangent) == pytest.approx((sign*p, 8000.))


@pytest.mark.parametrize('sign', [-1, 1])
@pytest.mark.parametrize('extended', [False, True])
def test_Nd_target_arrival_on_direct_return_enters_skeleton(sign, extended):
    table = material()
    held = evaluate_axial_force_hold(table, history(table, .4, [sign*.002, sign*.0015]), 0.).state
    # Return line M=8000*x-6.4. At x=.0019 M=8.8; at .0022 M=11.2.
    x, p, contact_nd = (.0022, 11.2, 10/61) if extended else (.0019, 8.8, 30/59)
    returning = evaluate_axial_force_curvature(table, held, sign*x).state
    saved = deepcopy(returning)
    result = evaluate_axial_force_hold(table, returning, .6)
    assert returning == saved
    assert result.state.history.branch == 'skeleton'
    assert next(e.Nd for e in result.events if e.kind == 'target') == pytest.approx(contact_nd)
    restored = evaluate_axial_force_hold(table, result.state, 0.)
    assert restored.moment == pytest.approx(sign*(11.8 if not extended else 12.2))


@pytest.mark.parametrize('sign', [-1, 1])
@pytest.mark.parametrize('force,length', [(1., 1.), (1e-6, 1e3), (1e6, 1e-3)])
def test_return_scale_and_trial_order(sign, force, length):
    table = material(force, length)
    held = evaluate_axial_force_hold(table, history(table, .4*force,
        [sign*.002/length, sign*.0015/length]), 0.).state
    before = deepcopy(held)
    for x, p, k in ((.0025, 12.5, 1000.), (.0021, 10.4, 8000.), (.0018, 8., 8000.)):
        result = evaluate_axial_force_curvature(table, held, sign*x/length)
        assert result.moment/(force*length) == pytest.approx(sign*p, rel=1e-12)
        assert result.bending_tangent/(force*length**2) == pytest.approx(k, rel=1e-12)
    assert held == before


@pytest.mark.parametrize('sign', [-1, 1])
@pytest.mark.parametrize('initial_nd,held_nd,x,p,k', [
    (.4, 0., .0076, -6.24, 1600.),
    (.4, 0., .00775, -6., -4000.),
    (.4, 0., .0085, -9., -4000.),
    (0., .4, .008, -7., 2000.),
    (0., .4, .0081, -6.8, 2000.),
    (0., .4, 43/5200, -84/13, -3200.),
    (0., .4, .0085, -7.2, -3200.),
])
def test_negative_K4_direct_return_after_force_zero(sign, initial_nd, held_nd, x, p, k):
    table = material(fourth=True)
    held = evaluate_axial_force_hold(table, history(table, initial_nd, [sign*.008, sign*.0075]), held_nd).state
    saved = deepcopy(held)
    result = evaluate_axial_force_curvature(table, held, sign*x)
    assert (result.moment, result.bending_tangent) == pytest.approx((sign*p, k), rel=1e-12)
    assert held == saved


@pytest.mark.parametrize('sign', [-1, 1])
def test_extension_hold_refreshes_forward_target_without_moving_actual_anchor(sign):
    table = material()
    held = evaluate_axial_force_hold(table, history(table, .4, [sign*.002, sign*.0015]), 0.).state
    returned = evaluate_axial_force_curvature(table, held, sign*.0021).state
    saved = deepcopy(returned)
    result = evaluate_axial_force_hold(table, returned, .1)
    segment = result.state.history.active_segment
    # 8000*x-6.4 = .95*(10+1000*x), target x=15.9/7050.
    assert (segment.start_delta, segment.start_P, segment.end_delta) == pytest.approx(
        (sign*.002, sign*9.6, sign*15.9/7050))
    assert (result.moment, result.bending_tangent, result.moment_Nd_derivative) == pytest.approx(
        (sign*10.4, 8000., 0.))
    assert returned == saved
    result.state.history.active_segment.reverse_segment.start_P = 999.
    assert returned == saved


@pytest.mark.parametrize('sign', [-1, 1])
def test_return_tangent_matches_frozen_history_difference(sign):
    table = material()
    held = evaluate_axial_force_hold(table, history(table, .4, [sign*.002, sign*.0015]), 0.).state
    for x in (.0018, .0021, .0025):
        response = evaluate_axial_force_curvature(table, held, sign*x)
        for h in (1e-7, 1e-8, 1e-9):
            plus = evaluate_axial_force_curvature(table, held, sign*x+h).moment
            minus = evaluate_axial_force_curvature(table, held, sign*x-h).moment
            assert (plus-minus)/(2*h) == pytest.approx(response.bending_tangent, rel=1e-8)


@pytest.mark.parametrize('sign', [-1, 1])
def test_reversal_after_interception_uses_actual_Nd_and_preserved_maximum(sign):
    table = material()
    held = evaluate_axial_force_hold(table, history(table, 0., [sign*.002, sign*.0015]), .4).state
    intercepted = evaluate_axial_force_curvature(table, held, sign*.0018).state
    reverse = evaluate_axial_force_curvature(table, intercepted, sign*.0017)
    assert (reverse.moment, reverse.bending_tangent) == pytest.approx((sign*8.48, 8000.))
    assert (reverse.state.history.active_segment.start_delta,
            reverse.state.history.active_segment.start_P) == pytest.approx((sign*.0018, sign*9.28))


@pytest.mark.parametrize('sign', [-1, 1])
def test_invalid_Nd_trial_after_extension_does_not_change_return_graph(sign):
    from fem.diagnostics import InputValidationError
    table = material()
    held = evaluate_axial_force_hold(table, history(table, .4, [sign*.002, sign*.0015]), 0.).state
    returned = evaluate_axial_force_curvature(table, held, sign*.0021).state
    saved = deepcopy(returned)
    with pytest.raises(InputValidationError):
        evaluate_axial_force_hold(table, returned, 1.1)
    assert returned == saved
    result = evaluate_axial_force_curvature(table, returned, sign*.0025)
    assert (result.moment, result.bending_tangent) == pytest.approx((sign*12.5, 1000.))


@pytest.mark.parametrize('sign', [-1, 1])
def test_Nd_row_touch_at_extension_anchor_preserves_zero_length_forward_target(sign):
    full = SkeletonPoints((.001, .002, .003), (10., 12., 13.))
    lower = SkeletonPoints((.001, .002, .003), (8., 9.6, 10.4))
    table = AxialForceTable((AxialForceRow(0., full, full), AxialForceRow(1., lower, lower),
                            AxialForceRow(2., full, full)), beta=0.)
    held = evaluate_axial_force_hold(table, history(table, 1., [sign*.002, sign*.0015]), 0.).state
    returned = evaluate_axial_force_curvature(table, held, sign*.002).state
    # At Nd=1 the envelope touches the actual anchor then expands again.
    # The fixed line stays valid; its forward target is momentarily its origin.
    whole = evaluate_axial_force_hold(table, returned, 2.)
    touching = evaluate_axial_force_hold(table, returned, 1.)
    assert not any(e.kind in ('contact', 'departure', 'target') for e in touching.events)
    assert (touching.moment, touching.bending_tangent) == pytest.approx((sign*9.6, 8000.))
    assert touching.state.history.active_segment.end_delta == pytest.approx(sign*.002)
    split = evaluate_axial_force_hold(table, touching.state, 2.)
    assert (split.moment, split.bending_tangent) == pytest.approx((whole.moment, whole.bending_tangent))
    outward = evaluate_axial_force_curvature(table, touching.state, sign*.0021)
    assert (outward.moment, outward.bending_tangent) == pytest.approx((sign*9.68, 800.))


@pytest.mark.parametrize('sign', [-1, 1])
def test_missing_internal_continuation_is_not_silently_projected_onto_skeleton(sign):
    from fem.diagnostics import UnsupportedAnalysisError
    table = material()
    state = history(table, 0., [sign*.002, -sign*.0001, sign*.0012, sign*.00025])
    assert state.history.active_segment.target.kind == 'experienced'
    # An incomplete imported return graph cannot turn its off-envelope
    # actual point into a skeleton target with a force discontinuity.
    state.history.active_segment.next_segment = None
    held = evaluate_axial_force_hold(table, state, .5).state
    saved = deepcopy(held)
    with pytest.raises(UnsupportedAnalysisError) as failure:
        evaluate_axial_force_curvature(table, held, -sign*.0001)
    assert failure.value.details['reason'] == 'axial_force_return_to_moved_skeleton'
    assert held == saved
