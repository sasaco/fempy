"""Independent held-curvature Nd paths from manual 7.20.6.

The fixed-JR evaluator only prepares an experienced starting state. All Nd
expectations below are hand-derived, never obtained from the table evaluator.
"""
from copy import deepcopy

import numpy as np
import pytest

from fem.diagnostics import InputValidationError, UnsupportedAnalysisError
from fem.nonlinear.axial_force_table import AxialForceRow, AxialForceTable, SkeletonPoints
from fem.nonlinear.hysteresis import JRStiffnessReductionModel

pytestmark = [pytest.mark.unit, pytest.mark.material_nonlinear]


def table(*, beta=0., fourth=False, rows=(0., 1.)):
    values = []
    for nd in rows:
        points = SkeletonPoints((.001, .002, .003, .004) if fourth else (.001, .002, .003),
                                tuple(p*(1-.5*nd) for p in ((10., 12., 13., 9.) if fourth else (10., 12., 13.))))
        values.append(AxialForceRow(nd, points, points))
    return AxialForceTable(tuple(values), beta=beta)


def experienced(material, path, Nd=0.):
    from fem.nonlinear.axial_force_history import AxialForceHistoryState
    model = JRStiffnessReductionModel(material.interpolate(Nd).to_jr_params())
    state = model.create_initial_state()
    for curvature in path:
        moment, tangent, info = model.get_force_and_stiffness(curvature, state)
        state = model.update_state(curvature, moment, tangent, state, info)
    return AxialForceHistoryState.from_fixed_history(Nd, state)


def hold(material, state, Nd):
    from fem.nonlinear.axial_force_history import evaluate_axial_force_hold
    return evaluate_axial_force_hold(material, state, Nd)


@pytest.mark.parametrize('sign', [-1, 1])
@pytest.mark.parametrize('Nd,expected,branch', [
    (.5, 7., 'unloading'), (8/11, 7., 'envelope_contact'),
    (.8, 6.6, 'envelope_contact'), (1., 5.5, 'envelope_contact'),
])
def test_manual_contact_location_and_held_moment(sign, Nd, expected, branch):
    material = table()
    state = experienced(material, [sign*.002, sign*.0015])
    assert state.history.current_P == pytest.approx(sign*7.)
    response = hold(material, state, Nd)
    assert response.moment == pytest.approx(sign*expected, rel=1e-12)
    assert response.state.history.branch == branch
    if branch == 'envelope_contact':
        event = next(event for event in response.events if event.kind == 'contact')
        assert (event.Nd, event.moment, event.side) == pytest.approx((8/11, sign*7, sign))
        assert response.bending_tangent == pytest.approx(2000*(1-.5*Nd))
        assert response.moment_Nd_derivative == pytest.approx(-sign*5.5)
    else:
        assert response.bending_tangent == 10000.
        assert response.moment_Nd_derivative == 0.


@pytest.mark.parametrize('sign', [-1, 1])
def test_departure_freezes_stiffness_at_departure_not_at_final_Nd(sign):
    material = table()
    initial = experienced(material, [sign*.002, sign*.0015])
    contact = hold(material, initial, 1.).state
    response = hold(material, contact, 0.)
    assert response.moment == pytest.approx(sign*5.5)
    assert response.bending_tangent == pytest.approx(5000.)
    assert response.moment_Nd_derivative == 0.
    assert response.state.history.branch == 'unloading'
    assert response.state.contact_side is None
    assert response.events[0].kind == 'departure'
    assert response.events[0].Nd == 1.
    branch = response.state.history.active_segment
    assert (branch.start_delta, branch.start_P, branch.K) == pytest.approx((sign*.0015, sign*5.5, 5000.))
    # Continuing a frozen unload at fixed Nd must begin at the actual
    # departure point. This check exercises the existing JR branch adapter.
    model = JRStiffnessReductionModel(material.interpolate(0.).to_jr_params())
    m, k, _ = model.get_force_and_stiffness(sign*.0014, response.state.history)
    assert (m, k) == pytest.approx((sign*5., 5000.))


@pytest.mark.parametrize('sign', [-1, 1])
def test_partitions_and_trial_order_do_not_change_physical_path(sign):
    material = table(rows=(0., .3, .8, 1.))
    initial = experienced(material, [sign*.002, sign*.0015])
    saved = deepcopy(initial)
    direct = hold(material, initial, 1.)
    state = initial
    for nd in [.2, .3, .6, 8/11, .8, .93, 1.]:
        state = hold(material, state, nd).state
    assert state.history.current_P == pytest.approx(direct.moment)
    assert state.contact_side == direct.state.contact_side == sign
    for nd in [.91, .1, 1., .4, .8]:
        hold(material, initial, nd)
    repeated = hold(material, initial, 1.)
    assert repeated == direct
    assert initial == saved
    # Rollback means discarding a candidate; subsequent trials start from
    # the same owned committed state, including nested segment graphs.
    direct.state.history.delta_max_pos = 999.
    assert initial == saved
    assert repeated.state.history.delta_max_pos != 999.
    for nd in [.9, .8, .3, 0.]:
        state = hold(material, state, nd).state
    assert (state.history.current_P, state.history.current_K) == pytest.approx((sign*5.5, 5000.))


@pytest.mark.parametrize('sign', [-1, 1])
@pytest.mark.parametrize('curvature', [.0005, .0015, .0025, .004, .007])
def test_skeleton_hold_tracks_Nd_even_after_negative_K4_force_zero(sign, curvature):
    material = table(fourth=True)
    state = experienced(material, [sign*curvature])
    before = deepcopy(state)
    moment = 10000*curvature if curvature < .001 else (10+2000*(curvature-.001) if curvature < .002 else
             (12+1000*(curvature-.002) if curvature < .003 else 13-4000*(curvature-.003)))
    slope = 10000 if curvature < .001 else 2000 if curvature < .002 else 1000 if curvature < .003 else -4000
    response = hold(material, state, 1.)
    assert (response.moment, response.bending_tangent, response.moment_Nd_derivative) == pytest.approx(
        (sign*moment*.5, slope*.5, -sign*moment*.5))
    assert state == before


@pytest.mark.parametrize('sign', [-1, 1])
def test_contact_side_survives_negative_fourth_slope_and_force_sign_change(sign):
    # At phi=.007 the positive-side skeleton is -3. Unload from .0075:
    # M=-5+2000*(-.0005)=-6. Expanding the negative K4 magnitude from
    # 1 to 3 moves M_b=-3-6Nd through the fixed unload at Nd=.5.
    points0 = SkeletonPoints((.001, .002, .003, .004), (10., 12., 13., 9.))
    points1 = SkeletonPoints((.001, .002, .003, .004), (30., 36., 39., 27.))
    material = AxialForceTable((AxialForceRow(0., points0, points0), AxialForceRow(1., points1, points1)), beta=0.)
    state = experienced(material, [sign*.0075, sign*.007])
    response = hold(material, state, 1.)
    assert response.moment == pytest.approx(-sign*9.)
    assert response.state.contact_side == sign
    assert next(e.Nd for e in response.events if e.kind == 'contact') == pytest.approx(.5)
    assert response.bending_tangent == pytest.approx(-12000.)


def test_range_failure_and_noop_never_mutate_or_alias_committed_state():
    material = table()
    initial = experienced(material, [.002, .0015])
    saved = deepcopy(initial)
    with pytest.raises(InputValidationError) as failure:
        hold(material, initial, 1.01)
    assert failure.value.details['reason'] == 'axial_force_out_of_range'
    assert initial == saved
    response = hold(material, initial, 0.)
    assert response.state == initial
    assert response.state is not initial
    assert response.state.history.active_segment is not initial.history.active_segment


@pytest.mark.parametrize('Nd', [.4, .9])
def test_analytic_Nd_derivative_against_multiple_independent_differences(Nd):
    material = table()
    initial = experienced(material, [.002, .0015])
    response = hold(material, initial, Nd)
    for h in [1e-4, 1e-5, 1e-6]:
        difference = (hold(material, initial, Nd+h).moment-hold(material, initial, Nd-h).moment)/(2*h)
        assert difference == pytest.approx(response.moment_Nd_derivative, rel=1e-8, abs=1e-9)


def test_identical_rows_preserve_fixed_history_including_inner_return_graph():
    points = SkeletonPoints((.001, .002, .003), (10., 12., 13.))
    material = AxialForceTable((AxialForceRow(-1., points, points), AxialForceRow(1., points, points)), beta=0.)
    for path in [[.002, .0015], [.004, -.002, .002], [.004, -.002, .002, .001], [.004, -.002, .002, .001, .0015]]:
        initial = experienced(material, path)
        response = hold(material, initial, .7)
        assert response.state.history == initial.history
        assert response.moment_Nd_derivative == 0.


@pytest.mark.parametrize('sign', [-1, 1])
@pytest.mark.parametrize('force_scale,length_scale', [(1., 1.), (1e-6, 1e3), (1e6, 1e-3)])
def test_two_intersections_choose_first_then_depart_at_interior_turn(sign, force_scale, length_scale):
    # At held phi=.0015, M_b=9.5-2Nd+2/(2-Nd). The line M=10 has
    # two intersections (7 +/- sqrt(17))/8. The physical path contacts
    # at the first, reaches M=9.5 at Nd=1 and then leaves at Kd=8000.
    # Both endpoints have M_b=10.5; endpoint clipping misses the whole loop.
    def points(nd):
        return SkeletonPoints(tuple(d/length_scale for d in (.001, .003-.001*nd, .004)),
                              tuple(m*force_scale*length_scale for m in (10-2*nd, 12-nd, 13)))
    scale_nd = force_scale
    material = AxialForceTable(tuple(AxialForceRow(n*scale_nd, points(n), points(n)) for n in (0., 1.5)), beta=0.)
    initial = experienced(material, [sign*(14/9000)/length_scale, sign*.0015/length_scale])
    before = deepcopy(initial)
    response = hold(material, initial, 1.5*scale_nd)
    scale_m = force_scale*length_scale
    events = [e for e in response.events if e.kind in ('contact', 'departure')]
    assert [e.kind for e in events] == ['contact', 'departure']
    assert [e.Nd/scale_nd for e in events] == pytest.approx([(7-np.sqrt(17))/8, 1.], rel=1e-12)
    assert [e.moment/scale_m for e in events] == pytest.approx([sign*10., sign*9.5], rel=1e-12)
    assert response.moment/scale_m == pytest.approx(sign*9.5, rel=1e-12)
    assert response.bending_tangent/(force_scale*length_scale**2) == pytest.approx(8000., rel=1e-12)
    assert response.moment_Nd_derivative == 0.
    assert initial == before
    state = initial
    for n in [.1, .4, .7, 1., 1.2, 1.5]:
        state = hold(material, state, n*scale_nd).state
    assert state.history.current_P == pytest.approx(response.moment, rel=1e-12)
    assert state.history.current_K == pytest.approx(response.bending_tangent, rel=1e-12)


@pytest.mark.parametrize('sign', [-1, 1])
def test_moving_skeleton_breakpoint_is_a_partition_not_a_force_jump(sign):
    # All curvatures double; moments stay fixed. Held |phi|=.0015
    # crosses the moving first point at Nd=.5. M_b is 8+3/(1+Nd)
    # before that and 15/(1+Nd) afterward.
    rows = []
    for n in (0., .3, 1.):
        points = SkeletonPoints(tuple(d*(1+n) for d in (.001, .002, .003)), (10., 12., 13.))
        rows.append(AxialForceRow(n, points, points))
    material = AxialForceTable(tuple(rows), beta=0.)
    initial = experienced(material, [sign*.0015])
    response = hold(material, initial, 1.)
    assert response.moment == pytest.approx(sign*7.5)
    assert response.bending_tangent == pytest.approx(5000.)
    assert response.moment_Nd_derivative == pytest.approx(-sign*3.75)
    partitions = [e.Nd for e in response.events if e.kind == 'partition']
    assert partitions == pytest.approx([.3, .5])
    state = hold(material, initial, .5).state
    assert state.history.current_P == pytest.approx(sign*10.)
    assert hold(material, state, 1.).moment == pytest.approx(response.moment)


@pytest.mark.parametrize('beta', [.4, 2.])
def test_departure_uses_regional_JR_reduction_and_fixed_global_floor(beta):
    material = table(beta=beta)
    # Beyond point 2, Kd=K2*(phi_max/phi2)^(-beta), including at departure.
    initial = experienced(material, [.004, .0035])
    contact = hold(material, initial, 1.).state
    assert contact.contact_side == 1
    response = hold(material, contact, 0.)
    expected = max(material.K_min, 1000*2**(-beta))
    assert response.moment == pytest.approx(6.5)
    assert response.bending_tangent == pytest.approx(expected)
    assert response.state.history.delta_max_pos == .004
    assert response.state.history.P_max_pos == 13.  # Actual experience, not moving target M.


def test_recontact_after_departure_and_row_boundary_turn():
    rows = []
    for n, scale in [(0., 1.), (1., .5), (2., 1.), (3., .25)]:
        points = SkeletonPoints((.001, .002, .003), tuple(p*scale for p in (10., 12., 13.)))
        rows.append(AxialForceRow(n, points, points))
    material = AxialForceTable(tuple(rows), beta=0.)
    initial = experienced(material, [.002, .0015])
    response = hold(material, initial, 3.)
    events = [e for e in response.events if e.kind != 'partition']
    assert [e.kind for e in events] == ['contact', 'departure', 'contact']
    assert [e.Nd for e in events] == pytest.approx([8/11, 1., 8/3])
    assert response.moment == pytest.approx(2.75)
    state = initial
    for n in [.4, 1., 1.4, 2., 2.9, 3.]:
        state = hold(material, state, n).state
    assert state.history.current_P == pytest.approx(response.moment)
    assert state.contact_side == 1


def test_noop_on_skeleton_reports_active_partial_Nd_derivative():
    material = table()
    initial = experienced(material, [.0015], Nd=.4)
    response = hold(material, initial, .4)
    assert response.state == initial
    assert response.moment_Nd_derivative == pytest.approx(-5.5)


def test_moving_reload_start_is_explicitly_unsupported_not_silently_frozen():
    material = table()
    initial = experienced(material, [.002, -.0001])
    saved = deepcopy(initial)
    assert initial.history.branch == 'reloading'
    with pytest.raises(UnsupportedAnalysisError) as failure:
        hold(material, initial, .4)
    assert failure.value.details['reason'] == 'axial_force_hold_moving_target'
    assert initial == saved


def test_tangential_touch_does_not_replace_a_fixed_unloading_branch():
    def points(nd):
        return SkeletonPoints((.001, .003-.001*nd, .004), (10-2*nd, 12-nd, 13.))
    material = AxialForceTable(tuple(AxialForceRow(n, points(n), points(n)) for n in (0., 1.5)), beta=0.)
    # The held M=9.5 line only touches the minimum of M_b at Nd=1.
    # It never goes outside; no new unloading line may replace Kd=10000.
    initial = experienced(material, [14.5/9000, .0015])
    response = hold(material, initial, 1.5)
    assert response.moment == pytest.approx(9.5)
    assert response.bending_tangent == 10000.
    assert response.state.contact_side is None
    assert not any(event.kind in ('contact', 'departure') for event in response.events)


@pytest.mark.parametrize('Nd', [.2, .5, .8, 1.2, 1.4])
def test_rational_contact_response_and_Nd_sensitivity(Nd):
    def points(nd):
        return SkeletonPoints((.001, .003-.001*nd, .004), (10-2*nd, 12-nd, 13.))
    material = AxialForceTable(tuple(AxialForceRow(n, points(n), points(n)) for n in (0., 1.5)), beta=0.)
    initial = experienced(material, [14/9000, .0015])
    response = hold(material, initial, Nd)
    first = (7-np.sqrt(17))/8
    moment = 10. if Nd < first else 9.5-2*Nd+2/(2-Nd) if Nd < 1. else 9.5
    derivative = -2+2/(2-Nd)**2 if first < Nd < 1. else 0.
    assert response.moment == pytest.approx(moment, rel=1e-12)
    assert response.moment_Nd_derivative == pytest.approx(derivative, rel=1e-12, abs=1e-12)
    for h in [1e-4, 1e-5, 1e-6]:
        difference = (hold(material, initial, Nd+h).moment-hold(material, initial, Nd-h).moment)/(2*h)
        assert difference == pytest.approx(derivative, rel=2e-7, abs=1e-8)


@pytest.mark.parametrize('Nd', [None, True, '1', np.nan, np.inf, -np.inf])
def test_invalid_Nd_queries_preserve_state(Nd):
    material = table()
    initial = experienced(material, [.002, .0015])
    before = deepcopy(initial)
    with pytest.raises(InputValidationError):
        hold(material, initial, Nd)
    assert initial == before


@pytest.mark.parametrize('sign', [-1, 1])
def test_contact_adapter_continues_outward_and_unloads_inward_at_fixed_Nd(sign):
    material = table()
    initial = experienced(material, [sign*.002, sign*.0015])
    contact = hold(material, initial, 1.).state
    model = JRStiffnessReductionModel(material.interpolate(1.).to_jr_params())
    # The fixed-curvature contact state has a known skeleton side. A later
    # outward curvature increment must follow that skeleton, not reverse
    # the pre-contact unloading direction a second time.
    moment, stiffness, _ = model.get_force_and_stiffness(sign*.002, contact.history)
    assert (moment, stiffness) == pytest.approx((sign*6., 500.))
    moment, stiffness, _ = model.get_force_and_stiffness(sign*.0014, contact.history)
    assert (moment, stiffness) == pytest.approx((sign*5., 5000.))
