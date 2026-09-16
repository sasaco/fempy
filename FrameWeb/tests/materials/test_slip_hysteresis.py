"""Independent piecewise-linear references for manual 7.19."""
from dataclasses import FrozenInstanceError

import numpy as np
import pytest

from fem.nonlinear.hysteresis.slip import SlipSpringParams, SlipSpringState, evaluate_slip

pytestmark = pytest.mark.unit

PATH = [.005, .030, .024, .018, .010, .025, 0., -.005,
        -.030, -.024, -.018, .010, .024, .040]
FORCES = [5., 12., 6., 0., 0., 7., 0., -5., -12., -6., 0., 0., 6., 13.]


@pytest.mark.parametrize('scale', [1., .001, 1000.])
def test_hand_calculated_history_and_unit_conversion(scale):
    params = SlipSpringParams(1000/scale, 100/scale, .01*scale)
    state = SlipSpringState.initial(params)
    for delta, force in zip(PATH, FORCES):
        state = evaluate_slip(params, state, delta*scale)
        assert state.force == pytest.approx(force, abs=1e-12)
    assert state.zero_pos == pytest.approx(.027*scale)
    assert state.zero_neg == pytest.approx(-.018*scale)
    assert state.delta_max_pos == pytest.approx(.04*scale)
    assert state.delta_max_neg == pytest.approx(-.03*scale)
    assert state.force_at_max_neg == -12.
    assert state.yielded_pos and state.yielded_neg


def test_trial_is_pure_repeatable_and_preserves_unexperienced_side():
    p = SlipSpringParams(1000, 100, .01)
    initial = SlipSpringState.initial(p)
    oversized = evaluate_slip(p, initial, 10.)
    accepted = evaluate_slip(p, initial, .03)
    assert initial.delta_max_pos == initial.delta_max_neg == 0.
    assert accepted.force == 12.
    assert accepted.delta_max_neg == accepted.force_at_max_neg == 0.
    assert not accepted.yielded_neg
    assert evaluate_slip(p, accepted, accepted.deformation) == accepted
    assert evaluate_slip(p, initial, .03) == accepted
    assert oversized.delta_max_pos == 10.
    with pytest.raises(FrozenInstanceError):
        accepted.force = 99.


@pytest.mark.parametrize('sign', [-1, 1])
def test_endpoints_choose_outgoing_tangent_and_zero_step_retains_state(sign):
    p = SlipSpringParams(1000, 100, .01)
    state = SlipSpringState.initial(p)
    state = evaluate_slip(p, state, sign*.01)
    assert state.tangent == 100.
    state = evaluate_slip(p, state, sign*.03)
    maximum = state
    state = evaluate_slip(p, state, sign*.018)
    assert state.force == 0.
    assert state.tangent == 0.
    assert evaluate_slip(p, state, state.deformation) == state
    state = evaluate_slip(p, state, sign*.01)
    state = evaluate_slip(p, state, sign*.018)
    assert state.tangent == 1000.
    state = evaluate_slip(p, state, sign*.03)
    assert state.tangent == 100.
    # A predictor can ask for the reverse one-sided tangent without committing.
    reverse = evaluate_slip(p, maximum, maximum.deformation, direction_hint=-sign)
    assert reverse.tangent == 1000.
    assert maximum.tangent == 100.


@pytest.mark.parametrize('delta, tangent', [(.006, 0.), (.022, 1000.),
                                         (.04, 100.), (-.005, 1000.), (-.04, 100.)])
def test_tangent_is_force_derivative_inside_each_branch(delta, tangent):
    p = SlipSpringParams(1000, 100, .01)
    state = evaluate_slip(p, SlipSpringState.initial(p), .03)
    response = evaluate_slip(p, state, delta)
    h = 1e-7
    derivative = (evaluate_slip(p, state, delta+h).force -
                  evaluate_slip(p, state, delta-h).force)/(2*h)
    assert response.tangent == tangent
    assert derivative == pytest.approx(tangent, abs=1e-7)


@pytest.mark.parametrize('K2', [0., 100., 1000.])
def test_subdivision_and_reversals_preserve_extrema(K2):
    p = SlipSpringParams(1000, K2, .01)
    direct = divided = SlipSpringState.initial(p)
    for target in [.005, -.004, .03, .028, .029, .01, .012, -.03, 0., .03, .05]:
        for value in np.linspace(divided.deformation, target, 18)[1:]:
            divided = evaluate_slip(p, divided, value)
        direct = evaluate_slip(p, direct, target)
        assert divided == direct
        if K2 == 1000.:
            assert direct.force == pytest.approx(1000*target)
            assert direct.tangent == 1000.
            assert direct.zero_pos == direct.zero_neg == 0.
        elif K2 == 0.:
            assert abs(direct.force) <= 10.


def test_first_excursion_work_and_internal_closed_path():
    p = SlipSpringParams(1000, 100, .01)
    state = SlipSpringState.initial(p)

    def work(points):
        nonlocal state
        total = 0.
        for delta in points:
            new = evaluate_slip(p, state, delta)
            total += .5*(state.force+new.force)*(delta-state.deformation)
            state = new
        return total

    # Endpoints of every affine interval: trapezoids are exact integrals.
    assert work([.01, .03, .018, 0., -.01, -.03, -.018, 0.]) == pytest.approx(.396)
    assert work([.018, .027, .018, -.018, -.027, -.018, 0.]) == pytest.approx(0., abs=1e-15)


@pytest.mark.parametrize('values', [(0, 0, .01), (-1, 0, .01), (1000, -1, .01),
                                  (1000, 1001, .01), (1000, 1, 0),
                                  (True, 0, 1), ('1000', 100, .01),
                                  (1000, float('nan'), .01), (float('inf'), 0, .01)])
def test_invalid_parameters_are_rejected(values):
    with pytest.raises(ValueError):
        SlipSpringParams(*values)


@pytest.mark.parametrize('value', [float('nan'), float('inf'), True, '.01'])
def test_invalid_trial_is_rejected(value):
    p = SlipSpringParams(1000, 100, .01)
    with pytest.raises(ValueError):
        evaluate_slip(p, SlipSpringState.initial(p), value)
