"""Independent affine simultaneous paths, without alternating scalar updates."""
from copy import deepcopy
from math import nextafter, sqrt

import pytest

from fem.diagnostics import InputValidationError
from fem.nonlinear.axial_force_history import AxialForceHistoryState, evaluate_axial_force_hold
from fem.nonlinear.axial_force_table import AxialForceRow, AxialForceTable, SkeletonPoints
from fem.nonlinear.hysteresis import JRStiffnessReductionModel

pytestmark = [pytest.mark.unit, pytest.mark.material_nonlinear]


def material(rows=((0., 1.), (1., .5)), *, beta=0., fourth=False, length=1., force=1.):
    ds = (.001, .002, .003, .004) if fourth else (.001, .002, .003)
    ps = (10., 12., 13., 9.) if fourth else (10., 12., 13.)
    values = []
    for n, scale in rows:
        points = SkeletonPoints(tuple(d/length for d in ds), tuple(p*scale*force*length for p in ps))
        values.append(AxialForceRow(n*force, points, points))
    return AxialForceTable(tuple(values), beta=beta)


def experienced(table, path, Nd=0.):
    model = JRStiffnessReductionModel(table.interpolate(Nd).to_jr_params())
    history = model.create_initial_state()
    for x in path:
        m, k, info = model.get_force_and_stiffness(x, history)
        history = model.update_state(x, m, k, history, info)
    return AxialForceHistoryState.from_fixed_history(Nd, history)


def advance(table, state, x, n):
    from fem.nonlinear.axial_force_path import evaluate_axial_force_path
    return evaluate_axial_force_path(table, state, x, n)


@pytest.mark.parametrize('side', [-1, 1])
@pytest.mark.parametrize('end', [.5, .9, 1.])
def test_simultaneous_contact_has_independent_quadratic_root(side, end):
    table = material()
    state = experienced(table, [side*.002, side*.0015])
    response = advance(table, state, side*(.0015-.0001*end), end)
    root = (47-sqrt(2049))/2
    expected = 7-end if end < root else (11-.2*end)*(1-.5*end)
    assert response.moment == pytest.approx(side*expected, rel=1e-12)
    assert response.bending_tangent == pytest.approx(10000 if end < root else 2000*(1-.5*end))
    assert response.state.contact_side == (None if end < root else side)
    contacts = [e for e in response.events if e.kind == 'contact']
    if end >= root:
        assert len(contacts) == 1
        assert (contacts[0].Nd, contacts[0].curvature, contacts[0].moment) == pytest.approx(
            (root, side*(.0015-.0001*root), side*(7-root)), rel=1e-12)
        assert contacts[0].fraction == pytest.approx(root/end)
    else:
        assert contacts == []


@pytest.mark.parametrize('side', [-1, 1])
@pytest.mark.parametrize('count', [2, 8, 32])
def test_collinear_partition_including_contact_and_row_departure(side, count):
    table = material(((0., 1.), (1., .5), (2., 1.)))
    initial = experienced(table, [side*.002, side*.0015])
    direct = advance(table, initial, side*.0013, 2.)
    events = [e for e in direct.events if e.kind != 'partition']
    assert [e.kind for e in events] == ['contact', 'departure']
    assert events[1].Nd == pytest.approx(1.)
    assert direct.moment == pytest.approx(side*4.9)
    assert direct.bending_tangent == pytest.approx(5000.)
    state = initial
    stops = sorted(set([2*i/count for i in range(1, count+1)] + [(47-sqrt(2049))/2, 1.]))
    for n in stops:
        state = advance(table, state, side*(.0015-.0001*n), n).state
    assert state.history.current_P == pytest.approx(direct.moment, rel=1e-12)
    assert state.history.current_K == pytest.approx(direct.bending_tangent, rel=1e-12)
    assert state.contact_side is None
    branch = state.history.active_segment
    assert (branch.start_delta, branch.start_P, branch.K) == pytest.approx((side*.0014, side*5.4, 5000))


@pytest.mark.parametrize('side', [-1, 1])
@pytest.mark.parametrize('beta', [0., .4, 2.])
def test_departure_uses_actual_Nd_regional_reduction(side, beta):
    table = material(beta=beta)
    initial = experienced(table, [side*.004, side*.0035])
    contact = evaluate_axial_force_hold(table, initial, 1.).state
    response = advance(table, contact, side*.0034, 0.)
    kd = max(table.K_min, 1000*2**(-beta))
    assert response.moment == pytest.approx(side*(6.5-kd*.0001))
    assert response.bending_tangent == pytest.approx(kd)
    assert response.events[0].kind == 'departure'
    assert response.events[0].Nd == 1.
    assert response.state.history.P_max_pos == initial.history.P_max_pos
    assert response.state.history.P_max_neg == initial.history.P_max_neg


@pytest.mark.parametrize('side', [-1, 1])
def test_outward_contact_becomes_skeleton(side):
    table = material()
    initial = experienced(table, [side*.002, side*.0015])
    contact = evaluate_axial_force_hold(table, initial, 1.).state
    response = advance(table, contact, side*.0017, 0.)
    assert (response.moment, response.bending_tangent) == pytest.approx((side*11.4, 2000.))
    assert response.state.contact_side is None
    assert response.state.history.branch == 'skeleton'


@pytest.mark.parametrize('side', [-1, 1])
def test_skeleton_crosses_moving_breakpoint(side):
    rows = []
    for n in (0., .3, 1.):
        points = SkeletonPoints(tuple(d*(1+n) for d in (.001, .002, .003)), (10., 12., 13.))
        rows.append(AxialForceRow(n, points, points))
    table = AxialForceTable(tuple(rows), beta=0.)
    state = experienced(table, [side*.0015])
    response = advance(table, state, side*.0017, 1.)
    assert (response.moment, response.bending_tangent) == pytest.approx((side*8.5, 5000.))
    assert any(e.Nd == pytest.approx(.625) for e in response.events if e.kind == 'partition')


@pytest.mark.parametrize('side', [-1, 1])
def test_negative_K4_and_moment_sign_do_not_change_contact_side(side):
    table = material(((0., 1.), (1., 3.)), fourth=True)
    state = experienced(table, [side*.0075, side*.007])
    response = advance(table, state, side*.0069, 1.)
    assert response.moment == pytest.approx(-side*7.8)
    assert response.bending_tangent == pytest.approx(-12000.)
    assert response.state.contact_side == side


@pytest.mark.parametrize('length,force', [(1., 1.), (1000., .001), (.001, 1000.)])
def test_consistent_unit_conversion(length, force):
    table = material(length=length, force=force)
    state = experienced(table, [.002/length, .0015/length])
    response = advance(table, state, .0014/length, force)
    assert response.moment/(length*force) == pytest.approx(5.4, rel=1e-12)
    assert response.bending_tangent/(force*length**2) == pytest.approx(1000., rel=1e-12)


def test_trial_order_failure_and_nested_graph_ownership():
    table = material()
    state = experienced(table, [.002, .0015])
    before = deepcopy(state)
    direct = advance(table, state, .0014, 1.)
    for x, n in [(.00145, .5), (.00148, .2), (.00142, .8)]:
        advance(table, state, x, n)
    with pytest.raises(InputValidationError):
        advance(table, state, .0014, 1.01)
    assert state == before
    assert advance(table, state, .0014, 1.) == direct
    candidate = advance(table, state, .00145, .5).state
    candidate.history.active_segment.next_segment.start_P = 1234.
    assert state == before


@pytest.mark.parametrize('value', [True, None, '1', float('nan'), float('inf')])
def test_invalid_endpoints(value):
    table = material()
    state = experienced(table, [.002, .0015])
    with pytest.raises(InputValidationError):
        advance(table, state, value, .5)
    with pytest.raises(InputValidationError):
        advance(table, state, .0014, value)


@pytest.mark.parametrize('side', [-1, 1])
@pytest.mark.parametrize('count', [1, 4, 16])
def test_simultaneous_moving_reload_refreshes_target_and_is_partition_invariant(side, count):
    table = material()
    initial = experienced(table, [side*.002, -side*.0001])
    before = deepcopy(initial)
    start, end = -side*.0001, -side*.0002
    state = initial
    for i in range(1, count+1):
        fraction = i/count
        response = advance(table, state, start+fraction*(end-start), .4*fraction)
        state = response.state
    assert (response.moment, response.bending_tangent) == pytest.approx(
        (-side*40/9, 40000/9), rel=1e-12)
    assert state.history.branch == 'reloading'
    assert (state.history.active_segment.end_delta, state.history.active_segment.end_P) == pytest.approx(
        (-side*.001, -side*8.))
    assert initial == before


@pytest.mark.parametrize('side', [-1, 1])
@pytest.mark.parametrize('count', [1, 4, 16])
def test_simultaneous_zero_event_enters_moving_reload_without_a_force_jump(side, count):
    table = material()
    initial = experienced(table, [side*.002, side*.0015])
    before = deepcopy(initial)
    start, end = side*.0015, side*.0005
    state, events = initial, []
    for i in range(1, count+1):
        fraction = i/count
        response = advance(table, state, start+fraction*(end-start), .1*fraction)
        state = response.state
        events.extend(response.events)
    zeros = [event for event in events if event.kind == 'zero']
    assert len(zeros) == 1
    assert (zeros[0].curvature, zeros[0].Nd, zeros[0].moment) == pytest.approx(
        (side*.0008, .07, 0.), abs=2e-15)
    assert (response.moment, response.bending_tangent) == pytest.approx(
        (-side*19/12, 47500/9), rel=1e-12)
    assert state.history.branch == 'reloading'
    assert initial == before


def rational_material(beta=0.):
    rows = []
    for n in (0., 1.5):
        points = SkeletonPoints((.001, .003-.001*n, .004), (10-2*n, 12-n, 13.))
        rows.append(AxialForceRow(n, points, points))
    return AxialForceTable(tuple(rows), beta=beta)


def moving_power_contact(side=1, *, reverse_Nd=False):
    first = SkeletonPoints((.001, .0025, .004), (10., 13., 15.))
    last = SkeletonPoints((.0007, .00185, .0041), (8., 12., 15.7))
    if reverse_Nd:
        first, last = last, first
    table = AxialForceTable((AxialForceRow(0., first, first),
                             AxialForceRow(2., last, last)), beta=.7)
    Nd, curvature, maximum = ((1.85, .0012, .0015) if reverse_Nd else
                              (.15, .0012, .0015))
    model = JRStiffnessReductionModel(table.interpolate(Nd).to_jr_params())
    history = model.create_initial_state()
    moment, tangent, info = model.get_force_and_stiffness(side*maximum, history)
    history = model.update_state(side*maximum, moment, tangent, history, info)
    envelope = table.evaluate_skeleton(side*curvature, Nd, side=side)
    history.current_delta = history.previous_delta = side*curvature
    history.current_P = history.previous_P = envelope.moment
    history.current_K = envelope.bending_tangent
    history.active_segment = None
    history.reversal_stack.clear()
    history.reversal_paths.clear()
    history.branch = 'envelope_contact'
    history.loading_direction = side
    return table, AxialForceHistoryState(Nd, history, side, envelope.segment)


@pytest.mark.parametrize('side', [-1, 1])
@pytest.mark.parametrize('count', [1, 8, 32])
def test_two_interior_intersections_and_directional_departure(side, count):
    table = rational_material()
    initial = experienced(table, [side*(13.7/9000), side*.0015])
    # On the actual path x=.0015-.0001n, M_b=-(n-5)(19n-42)/(10(n-2)).
    # The old line 10.3-n intersects at (7 +/- sqrt(13))/9. Departure
    # solves 2n^3+n^2-28n+24=0, NOT dM_b/dn=0 as on a curvature hold.
    contact = (7-sqrt(13))/9
    departure = .950834163075112811963359648927874364142631252
    mb = -(departure-5)*(19*departure-42)/(10*(departure-2))
    kd = 10000-2000*departure
    expected = mb-kd*.0001*(1.3-departure)
    direct = advance(table, initial, side*.00137, 1.3)
    transitions = [e for e in direct.events if e.kind != 'partition']
    assert [e.kind for e in transitions] == ['contact', 'departure']
    assert [e.Nd for e in transitions] == pytest.approx([contact, departure], rel=1e-12)
    assert direct.moment == pytest.approx(side*expected, rel=1e-12)
    assert direct.bending_tangent == pytest.approx(kd, rel=1e-12)
    state = initial
    for n in sorted(set([1.3*i/count for i in range(1, count+1)] + [contact, departure])):
        state = advance(table, state, side*(.0015-.0001*n), n).state
    assert state.history.current_P == pytest.approx(direct.moment, rel=1e-12)
    assert state.history.current_K == pytest.approx(direct.bending_tangent, rel=1e-12)


@pytest.mark.parametrize('side', [-1, 1])
def test_interior_tangency_preserves_old_Kd(side):
    table = rational_material()
    intercept = 8.1+1.2*sqrt(3)
    initial = experienced(table, [side*((24-intercept)/9000), side*.0015])
    response = advance(table, initial, side*.00137, 1.3)
    assert response.moment == pytest.approx(side*(intercept-1.3), rel=1e-12)
    assert response.bending_tangent == 10000.
    assert response.state.contact_side is None
    assert not any(e.kind in ('contact', 'departure') for e in response.events)


@pytest.mark.parametrize('side', [-1, 1])
def test_simultaneous_direct_return_reaches_shrinking_skeleton(side):
    table = material()
    state = experienced(table, [side*.002, side*.0015])
    response = advance(table, state, side*.0018, .4)
    assert (response.moment, response.bending_tangent) == pytest.approx((side*9.28, 1600.))
    assert [e.kind for e in response.events if e.kind != 'partition'] == ['target']
    assert response.state.contact_side is None
    assert response.state.history.branch == 'skeleton'


@pytest.mark.parametrize('side', [-1, 1])
def test_row_corner_touch_does_not_create_contact(side):
    # The affine trial line and envelope meet at n=1, then separate inside.
    # Phi=.0015-.0001n, line=7-n; envelope at n=1 is 10.8*(5/9)=6.
    table = material(((0., 1.), (1., 5/9), (2., 1.)))
    state = experienced(table, [side*.002, side*.0015])
    direct = advance(table, state, side*.0013, 2.)
    corner = advance(table, state, side*.0014, 1.)
    split = advance(table, corner.state, side*.0013, 2.)
    for response in (direct, split):
        assert (response.moment, response.bending_tangent) == pytest.approx((side*5., 10000.))
        assert response.state.contact_side is None
    assert not any(e.kind == 'contact' for e in direct.events)
    assert corner.state.contact_side is None


@pytest.mark.parametrize('phi,n', [(.0015, .7), (.0014, 0.), (.0015, 0.)])
def test_scalar_legs_keep_existing_adapter_contract(phi, n):
    from fem.nonlinear.axial_force_targets import evaluate_axial_force_curvature
    table = material()
    state = experienced(table, [.002, .0015])
    expected = (evaluate_axial_force_hold(table, state, n) if phi == .0015 else
                evaluate_axial_force_curvature(table, state, phi))
    actual = advance(table, state, phi, n)
    assert (actual.moment, actual.bending_tangent, actual.state) == (
        expected.moment, expected.bending_tangent, expected.state)


@pytest.mark.parametrize('end', [-.001, .0, .0025])
def test_invariant_table_retains_complete_JR_return_graph(end):
    from fem.nonlinear.axial_force_targets import evaluate_axial_force_curvature
    table = material(((0., 1.), (1., 1.)), beta=.4)
    state = experienced(table, [.004, -.002, .001, .0])
    expected = evaluate_axial_force_curvature(table, state, end)
    actual = advance(table, state, end, 1.)
    assert actual.state.history == expected.state.history
    assert actual.state.Nd == 1.


def test_nonzero_beta_moving_point_departure_isolated_without_sampling():
    table = rational_material(beta=.4)
    state = experienced(table, [.0016, .0015])
    contact = evaluate_axial_force_hold(table, state, .8).state
    assert contact.contact_side == 1
    before = deepcopy(contact)
    response = advance(table, contact, .00145, 1.3)
    departures = [event for event in response.events if event.kind == 'departure']
    assert len(departures) == 1
    assert departures[0].Nd == pytest.approx(.8827356237458797, rel=1e-12)
    assert departures[0].fraction == pytest.approx((departures[0].Nd-.8)/.5)
    assert response.moment == pytest.approx(9.218558568354527, rel=1e-12)
    assert response.bending_tangent == pytest.approx(6823.241726163142, rel=1e-12)
    assert response.state.contact_side is None
    assert contact == before
    # Skeleton following needs no candidate-departure root, so remains valid.
    skeleton = experienced(table, [.0015])
    assert advance(table, skeleton, .0017, 1.).moment == pytest.approx(8+3*.7)


@pytest.mark.parametrize('side', [-1, 1])
@pytest.mark.parametrize('count', [1, 2, 4, 8, 16])
def test_moving_power_departure_is_partition_invariant(side, count):
    table, state = moving_power_contact(side)
    start_x, start_n = side*.0012, .15
    end_x, end_n = side*.00108, 1.5
    events = []
    for index in range(1, count+1):
        fraction = index/count
        response = advance(table, state,
                           start_x+fraction*(end_x-start_x),
                           start_n+fraction*(end_n-start_n))
        state = response.state
        events.extend(response.events)
    departures = [event for event in events if event.kind == 'departure']
    assert len(departures) == 1
    assert departures[0].Nd == pytest.approx(1.06434854006352, rel=1e-12)
    assert state.history.current_P == pytest.approx(side*9.40962688491097, rel=1e-12)
    assert state.history.current_K == pytest.approx(7088.01505831542, rel=1e-12)
    assert state.contact_side is None


def test_moving_power_departure_with_decreasing_Nd():
    table, state = moving_power_contact(reverse_Nd=True)
    response = advance(table, state, .00108, .5)
    departure = next(event for event in response.events if event.kind == 'departure')
    assert departure.Nd == pytest.approx(.93565145993648, rel=1e-12)
    assert response.moment == pytest.approx(9.40962688491097, rel=1e-12)
    assert response.bending_tangent == pytest.approx(7088.01505831542, rel=1e-12)


def test_simultaneous_path_differs_from_two_distinct_loading_legs():
    table = material(((0., 1.), (1., .5), (2., 1.)))
    initial = experienced(table, [.002, .0015])
    simultaneous = advance(table, initial, .0013, 2.)
    held = evaluate_axial_force_hold(table, initial, 2.)
    sequential = advance(table, held.state, .0013, 2.)
    assert simultaneous.moment == pytest.approx(4.9)
    assert sequential.moment == pytest.approx(4.5)


@pytest.mark.parametrize('side', [-1, 1])
def test_global_unloading_floor_is_preserved_at_departure(side):
    table = material(beta=10.)
    initial = experienced(table, [side*.004, side*.0035])
    contact = evaluate_axial_force_hold(table, initial, 1.).state
    response = advance(table, contact, side*.0034, 0.)
    assert response.bending_tangent == table.K_min == 50.
    assert response.moment == pytest.approx(side*6.495)


@pytest.mark.parametrize('contact_side', [False, True, 0, 2])
def test_invalid_contact_metadata_is_rejected(contact_side):
    table = material()
    initial = experienced(table, [.002, .0015])
    contact = evaluate_axial_force_hold(table, initial, 1.).state
    invalid = AxialForceHistoryState(contact.Nd, contact.history, contact_side, contact.contact_segment)
    before = deepcopy(invalid)
    with pytest.raises(InputValidationError):
        advance(table, invalid, .0014, 0.)
    assert invalid == before


def test_virgin_simultaneous_path_updates_actual_experience():
    table = material(beta=.4)
    initial = experienced(table, [])
    response = advance(table, initial, .0006, .5)
    assert response.moment == pytest.approx(4.5)
    assert response.bending_tangent == 7500.
    assert response.state.history.branch == 'initial'
    assert response.state.history.delta_max_pos == .0006
    assert response.state.history.P_max_pos == pytest.approx(4.5)
    assert initial.history.delta_max_pos == 0.


@pytest.mark.parametrize('side', [-1, 1])
@pytest.mark.parametrize('count', [1, 2, 5, 16])
def test_curvature_side_crossing_preserves_the_same_affine_path(side, count):
    table = material()
    initial = experienced(table, [side*.002, side*.0015])
    before = deepcopy(initial)
    start, end = side*.0015, -side*.001
    state, events = initial, []
    for i in range(1, count+1):
        fraction = i/count
        response = advance(table, state, start+fraction*(end-start), .5*fraction)
        state = response.state
        events.extend(response.events)
    assert (response.moment, response.bending_tangent) == pytest.approx((-side*7.5, 1500.))
    assert state.history.branch == 'skeleton'
    assert [event.kind for event in events if event.kind != 'partition'] == ['zero', 'target']
    assert state.history.previous_delta == pytest.approx(start+(count-1)/count*(end-start))
    assert initial == before


@pytest.mark.parametrize('side', [-1, 1])
@pytest.mark.parametrize('count', [1, 4, 16])
def test_internal_actual_return_reanchors_the_moved_outer_reload(side, count):
    table = material()
    initial = experienced(table, [side*.002, -side*.0001, side*.0012, side*.00025])
    before = deepcopy(initial)
    start, end = side*.00025, -side*.0005
    state, events = initial, []
    for i in range(1, count+1):
        fraction = i/count
        response = advance(table, state, start+fraction*(end-start), .5*fraction)
        state = response.state
        events.extend(response.events)
    returns = [event for event in events if event.kind == 'return']
    assert len(returns) == 1
    assert (returns[0].curvature, returns[0].Nd, returns[0].moment) == pytest.approx(
        (-side*.0001, 7/30, -side*5.), rel=1e-12)
    assert (response.moment, response.bending_tangent) == pytest.approx(
        (-side*55/9, 25000/9), rel=1e-12)
    resumed = state.history.active_segment
    assert resumed.branch == 'reloading'
    assert (resumed.start_delta, resumed.start_P, resumed.end_delta, resumed.end_P) == pytest.approx(
        (-side*.0001, -side*5., -side*.001, -side*7.5))
    assert state.history.reversal_stack == []
    assert initial == before


@pytest.mark.parametrize('side', [-1, 1])
@pytest.mark.parametrize('count', [1, 4, 13])
def test_moving_curvature_target_is_reached_before_its_anchor(side, count):
    rows = []
    for nd in (0., 1.):
        points = SkeletonPoints(tuple(d*(1+.5*nd) for d in (.001, .002, .003)),
                                tuple(p*(1-.5*nd) for p in (10., 12., 13.)))
        rows.append(AxialForceRow(nd, points, points))
    table = AxialForceTable(tuple(rows), beta=0.)
    initial = experienced(table, [side*.002, 0.])
    before = deepcopy(initial)
    start, end = 0., -side*.0018
    state, events = initial, []
    for i in range(1, count+1):
        fraction = i/count
        response = advance(table, state, start+fraction*(end-start), fraction)
        state = response.state
        events.extend(response.events)
    targets = [event for event in events if event.kind == 'target']
    assert len(targets) == 1
    assert (targets[0].fraction if count == 1 else targets[0].Nd,
            targets[0].curvature, targets[0].Nd, targets[0].moment) == pytest.approx(
        (10/13, -side*18/13000, 10/13, -side*80/13), rel=1e-12)
    assert (response.moment, response.bending_tangent) == pytest.approx((-side*5.2, 2000/3))
    assert state.history.branch == 'skeleton'
    assert initial == before


@pytest.mark.parametrize('side', [-1, 1])
@pytest.mark.parametrize('count', [1, 4, 16])
def test_moving_reload_contact_and_later_departure_are_partition_invariant(side, count):
    from fem.nonlinear.hysteresis.base_hysteresis import HysteresisSegment, HysteresisState, JRReloadTarget

    rows = []
    for nd in (0., 1.4):
        points = SkeletonPoints((.001, .003-.001*nd, .004),
                                (10-2*nd, 12-nd, 12.5))
        rows.append(AxialForceRow(nd, points, points))
    table = AxialForceTable(tuple(rows), beta=0.)
    segment = HysteresisSegment(-side*.0085, 0., side*.004, side*12.5, 1000., 'reloading',
                                target=JRReloadTarget('skeleton', side, .004, 1, 2000.))
    history = HysteresisState(current_delta=side*.0015, current_P=side*10., current_K=1000.,
                              delta_max_pos=.004, delta_max_neg=.004, active_segment=segment,
                              branch='reloading', loading_direction=side)
    initial = AxialForceHistoryState.from_fixed_history(0., history)
    before = deepcopy(initial)
    state, events = initial, []
    for i in range(1, count+1):
        fraction = i/count
        response = advance(table, state, side*(.0015+.0001*fraction), 1.4*fraction)
        state = response.state
        events.extend(response.events)
    transitions = [event for event in events if event.kind in ('contact', 'departure')]
    assert [event.kind for event in transitions] == ['contact', 'departure']
    assert [event.Nd for event in transitions] == pytest.approx(
        [.369142613930081, .935287591386653], rel=2e-12)
    assert (response.moment, response.bending_tangent) == pytest.approx(
        (side*9.78355464663828, 2756.88304901930), rel=2e-12)
    assert state.history.branch == 'unloading'
    assert state.contact_side is None
    assert initial == before


@pytest.mark.parametrize('side', [-1, 1])
@pytest.mark.parametrize('count', [1, 4, 17])
def test_forward_target_arrival_on_a_simultaneous_path(side, count):
    from fem.nonlinear.hysteresis.base_hysteresis import HysteresisSegment, HysteresisState, JRReloadTarget

    table = material(fourth=True)
    segment = HysteresisSegment(side*.004, 0., side*.0055, side*3., 2000., 'reloading',
                                target=JRReloadTarget('forward', side, unloading_stiffness=2000.))
    history = HysteresisState(current_delta=side*.0052, current_P=side*2.4, current_K=2000.,
                              delta_max_pos=.006, delta_max_neg=.006, active_segment=segment,
                              branch='reloading', loading_direction=side)
    initial = AxialForceHistoryState.from_fixed_history(0., history)
    before = deepcopy(initial)
    state, events = initial, []
    for i in range(1, count+1):
        fraction = i/count
        response = advance(table, state, side*(.0052+.0002*fraction), fraction)
        state = response.state
        events.extend(response.events)
    targets = [event for event in events if event.kind == 'target']
    root = (33-sqrt(801))/8
    assert len(targets) == 1
    assert (targets[0].Nd, abs(targets[0].curvature), abs(targets[0].moment)) == pytest.approx(
        (root, .0052+.0002*root, 2.4+.4*root), rel=1e-12)
    assert (response.moment, response.bending_tangent) == pytest.approx((side*1.7, -2000.))
    assert state.history.branch == 'skeleton'
    assert initial == before


@pytest.mark.parametrize('count', [1, 2, 8])
def test_direct_return_accepts_the_first_crossing_from_either_gap_direction(count):
    from fem.nonlinear.hysteresis.base_hysteresis import HysteresisSegment, HysteresisState

    first = SkeletonPoints((.001, .002, .003), (10., 12., 13.))
    last = SkeletonPoints((.001, .002, .003), (8., 10., 12.))
    table = AxialForceTable((AxialForceRow(0., first, first), AxialForceRow(1., last, last)), beta=0.)
    segment = HysteresisSegment(-.0005, .5, .002, 3., 1000., 'retracing')
    history = HysteresisState(current_delta=-.0005, current_P=.5, current_K=1000.,
                              delta_max_pos=.002, delta_max_neg=.001, active_segment=segment,
                              branch='retracing', loading_direction=1)
    initial = AxialForceHistoryState.from_fixed_history(0., history)
    state, events = initial, []
    for i in range(1, count+1):
        fraction = i/count
        response = advance(table, state, -.0005+.002*fraction, fraction)
        state = response.state
        events.extend(response.events)
    targets = [event for event in events if event.kind == 'target']
    assert len(targets) == 1
    assert (targets[0].Nd, targets[0].curvature, targets[0].moment) == pytest.approx(
        (.309661044767712, .000119322089535423, 1.11932208953542), rel=1e-12)
    assert (response.moment, response.bending_tangent) == pytest.approx((9., 2000.))
    assert state.history.branch == 'skeleton'


@pytest.mark.parametrize('side', [-1, 1])
def test_invalid_moving_target_metadata_is_rejected_before_path_mutation(side):
    from fem.nonlinear.hysteresis.base_hysteresis import JRReloadTarget

    table = material()
    initial = experienced(table, [side*.002, -side*.0001])
    initial.history.active_segment.target = JRReloadTarget('skeleton', -side, 0., 3, 10000.)
    before = deepcopy(initial)
    with pytest.raises(InputValidationError):
        advance(table, initial, -side*.0002, .4)
    assert initial == before


@pytest.mark.parametrize('side', [-1, 1])
def test_one_ulp_curvature_increment_across_multiple_Nd_rows(side):
    table = material(((0., 1.), (.3, .85), (.8, .6), (1., .5)))
    initial = experienced(table, [side*.002, side*.0015])
    x = nextafter(side*.0015, 0.)
    response = advance(table, initial, x, 1.)
    assert response.state.history.current_delta == x
    assert response.moment == pytest.approx(side*5.5, rel=1e-12)
    assert response.bending_tangent == pytest.approx(1000., rel=1e-12)
    assert response.state.contact_side == side
