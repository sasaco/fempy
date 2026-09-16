"""Independent local references for the updated-inertia formulation.

These tests exercise the constitutive kernel before its public beam integration.
Expected forces and slopes are hand-derived; finite differences are secondary.
"""
from copy import deepcopy
from fractions import Fraction

import numpy as np
import pytest

from fem.nonlinear.hysteresis import JRStiffnessReductionModel, JRStiffnessReductionParams


pytestmark = [pytest.mark.unit, pytest.mark.material_nonlinear]


def law(*, negative=False, beta=0.0):
    return JRStiffnessReductionModel(JRStiffnessReductionParams.symmetric(
        .001, .003, .005, .1, .12, .13, beta=beta,
        **({"delta_4": .007, "P_4": .11} if negative else {}),
    ))


def kernel():
    # Import inside the tests so the first Red reports every missing behavior.
    from fem.nonlinear.beam_section_response import BendingPlaneState, evaluate_bending_plane
    return BendingPlaneState, evaluate_bending_plane


def response(model, curvature, shear, state=None, **geometry):
    state_type, evaluate = kernel()
    if state is None:
        state = state_type.initial(model)
    return evaluate(model, state, curvature, shear,
                    length=geometry.pop("length", 1.0), young_modulus=2000., **geometry)


@pytest.mark.parametrize("sign", [-1., 1.])
def test_crossed_breakpoint_force_and_full_tangent_have_independent_values(sign):
    r = response(law(), sign*.002, sign*.001)
    assert r.state.moment == pytest.approx(sign*.11, rel=1e-12)
    assert r.state.shear_force == pytest.approx(sign*.66, rel=1e-12)
    assert r.bending_tangent == pytest.approx(10., rel=1e-12)
    assert r.effective_inertia == pytest.approx(.005)
    assert r.shear_coefficient == pytest.approx(120., rel=1e-12)
    np.testing.assert_allclose(r.tangent, [[10., 0.], [-270., 660.]], rtol=1e-12, atol=1e-12)


def test_same_straight_path_split_at_breakpoint_preserves_force():
    model = law()
    whole = response(model, .002, .001)
    first = response(model, .001, .0005)
    split = response(model, .002, .001, first.state)
    assert split.state.moment == pytest.approx(.11, rel=1e-12)
    assert split.state.shear_force == pytest.approx(.66, rel=1e-12)
    for name in ["current_delta", "current_P", "current_K", "delta_max_pos", "P_max_pos"]:
        assert getattr(split.state.history, name) == getattr(whole.state.history, name)


def test_timoshenko_coefficient_is_integrated_on_each_branch():
    r = response(law(), .002, .001, shear_rigidity=1200.)
    # C1=600, C2=1200/11. Each branch covers half the straight path.
    average = (600.+1200./11.)/2.
    assert r.state.shear_force == pytest.approx(average*.001, rel=1e-12)
    np.testing.assert_allclose(r.tangent, [
        [10., 0.], [.5*(1200./11.-average), average],
    ], rtol=1e-12, atol=1e-12)


@pytest.mark.parametrize("length", [.5, 1., 3.])
@pytest.mark.parametrize("ga", [None, 1200.])
def test_constant_branch_cantilever_increment_compliance(length, ga):
    model = law()
    state = response(model, .0015, .0003, length=length, shear_rigidity=ga).state
    r = response(model, .002, .0007, state, length=length, shear_rigidity=ga)
    # Tip DOFs [v, theta], generalized modes [curvature, s].
    b = np.array([[0., 1./length], [1./length, -.5]])
    stiffness = length*b.T @ r.tangent @ b
    dv, rotation = np.linalg.solve(stiffness, [1., 0.])
    expected = length**3/(3.*10.) + (length/ga if ga is not None else 0.)
    assert dv == pytest.approx(expected, rel=1e-12)
    assert rotation == pytest.approx(length**2/(2.*10.), rel=1e-12)


def test_reversal_updates_future_increments_without_recomputing_past_shear():
    model = law()
    committed = response(model, .002, .001).state
    held_shear = response(model, .0018, .001, committed)
    assert held_shear.state.moment == pytest.approx(.09, rel=1e-12)
    assert held_shear.state.shear_force == pytest.approx(.66, rel=1e-12)
    assert held_shear.bending_tangent == 100.
    assert held_shear.state.history.branch == "unloading"
    moved = response(model, .0018, .0012, committed)
    assert moved.state.shear_force == pytest.approx(.90, rel=1e-12)


def test_unloading_zero_and_reload_are_integrated_as_separate_intervals():
    model = law()
    committed = response(model, .002, .001).state
    # Unload from (.002,.11) with Kd=100: zero at .0009.
    # Target (-.001,-.1) gives reload K=1000/19.
    r = response(model, 0., 0., committed)
    average = (1200.*.0011 + (12000./19.)*.0009)/.002
    assert r.state.moment == pytest.approx(-.9/19., rel=1e-12)
    assert r.state.shear_force == pytest.approx(.66-.001*average, rel=1e-12)
    assert r.bending_tangent == pytest.approx(1000./19., rel=1e-12)
    assert r.state.history.branch == "reloading"


@pytest.mark.parametrize("ga", [None, 1200.])
def test_finite_difference_of_force_after_zero_crossing(ga):
    model = law(beta=.4)
    state = response(model, .002, .001, shear_rigidity=ga).state
    target = np.array([-.0004, .0002])
    before = deepcopy(state)
    r = response(model, *target, state, shear_rigidity=ga)
    for h in [1e-7, 1e-8, 1e-9]:
        columns = []
        for direction in np.eye(2):
            plus = response(model, *(target+h*direction), state, shear_rigidity=ga).state
            minus = response(model, *(target-h*direction), state, shear_rigidity=ga).state
            columns.append((np.array([plus.moment, plus.shear_force])-
                            np.array([minus.moment, minus.shear_force]))/(2*h))
        np.testing.assert_allclose(r.tangent, np.column_stack(columns), rtol=1e-6, atol=1e-7)
    assert state == before


def test_trial_order_and_returned_history_do_not_modify_committed_state():
    model = law()
    committed = response(model, .002, .001).state
    before = deepcopy(committed)
    first = response(model, .0018, .0012, committed)
    response(model, -.004, -.001, committed)
    repeated = response(model, .0018, .0012, committed)
    assert repeated.state == first.state
    np.testing.assert_array_equal(repeated.tangent, first.tangent)
    first.state.history.current_delta = 999.
    assert committed == before
    assert repeated.state.history.current_delta == .0018


def test_zero_curvature_increment_uses_committed_branch_and_keeps_moment():
    model = law()
    committed = response(model, .002, .001).state
    r = response(model, .002, .002, committed)
    assert r.state.moment == committed.moment
    assert r.state.shear_force == pytest.approx(.78, rel=1e-12)
    np.testing.assert_allclose(r.tangent, [[10., 0.], [0., 120.]], atol=1e-12)


def test_plateau_is_zero_not_regularized_and_control_rank_depends_on_constraint():
    model = law()
    committed = response(model, .006, .001).state
    r = response(model, .007, .002, committed)
    assert r.state.moment == .13
    assert r.state.shear_force == committed.shear_force
    assert r.effective_inertia == r.shear_coefficient == 0.
    np.testing.assert_array_equal(r.tangent, np.zeros((2, 2)))
    b = np.array([[0., 1.], [1., -.5]])
    k = b.T @ r.tangent @ b
    augmented = np.block([[k, -np.array([[0.], [1.]])],
                          [np.array([[0., 1.]]), np.zeros((1, 1))]])
    assert np.linalg.matrix_rank(augmented) == 2
    # With v restrained the remaining [theta, lambda] system is regular.
    assert np.linalg.matrix_rank(augmented[np.ix_([1, 2], [1, 2])]) == 2


def test_negative_fourth_slope_and_force_sign_change_are_not_clamped():
    model = law(negative=True)
    committed = response(model, .006, .001).state
    r = response(model, .020, .002, committed)
    assert r.state.moment == pytest.approx(-.02, rel=1e-12)
    assert r.bending_tangent == pytest.approx(-10.)
    assert r.effective_inertia == pytest.approx(-.005)
    assert r.state.shear_force == pytest.approx(committed.shear_force-.12, rel=1e-12)
    reversed_response = response(model, .019, .003, r.state)
    assert reversed_response.bending_tangent > 0
    assert reversed_response.state.moment < r.state.moment


@pytest.mark.parametrize("scale", [1e-6, 1., 1e6])
def test_local_condensation_singularity_has_scaled_diagnostic_and_no_state_mutation(scale):
    from fem.diagnostics import NumericalConditionError
    model = JRStiffnessReductionModel(JRStiffnessReductionParams.symmetric(
        .001, .003, .005, .1*scale, .12*scale, .13*scale, beta=0.,
        delta_4=.007, P_4=.11*scale,
    ))
    state_type, evaluate = kernel()
    state = state_type.initial(model)
    before = deepcopy(state)
    with pytest.raises(NumericalConditionError) as error:
        evaluate(model, state, .006, .001, length=1., young_modulus=2000.*scale,
                 shear_rigidity=120.*scale)
    assert error.value.details["reason"] == "singular_shear_condensation"
    assert error.value.details["relative_denominator"] <= 1e-12
    assert state == before


@pytest.mark.parametrize("argument,value", [
    ("length", 0.), ("length", float("inf")),
    ("young_modulus", -1.), ("shear_rigidity", 0.),
    ("curvature", float("nan")), ("shear_deformation", float("inf")),
])
def test_invalid_local_inputs_are_rejected_before_evaluation(argument, value):
    state_type, evaluate = kernel()
    model = law()
    state = state_type.initial(model)
    arguments = dict(curvature=.002, shear_deformation=.001, length=1., young_modulus=2000.)
    arguments[argument] = value
    with pytest.raises(ValueError):
        evaluate(model, state, **arguments)


def test_asymmetric_negative_skeleton_uses_its_own_events_and_rigidities():
    model = JRStiffnessReductionModel(JRStiffnessReductionParams(
        .001, .003, .005, .1, .12, .13,
        .0015, .003, .006, .12, .135, .15, beta=0.,
    ))
    r = response(model, -.002, -.001)
    assert r.state.moment == pytest.approx(-.125, rel=1e-12)
    assert r.state.shear_force == pytest.approx(-.75, rel=1e-12)
    np.testing.assert_allclose(r.tangent, [[10., 0.], [-315., 750.]], rtol=1e-12, atol=1e-12)


@pytest.mark.parametrize("ga", [None, 1200.])
def test_nested_loop_restores_the_suspended_branch_during_the_integral(ga):
    # Independent rational geometry: x=curvature/.001, m=M/.1.
    outer_slope = Fraction(10, 19)
    a_x, a_m = Fraction(-2, 5), Fraction(-13, 19)
    zero_a = a_x-a_m
    positive_slope = Fraction(11, 10)/(2-zero_a)
    b_x = Fraction(7, 10)
    b_m = positive_slope*(b_x-zero_a)
    zero_b = b_x-b_m
    inner_slope = a_m/(a_x-zero_b)
    model = law()
    state = None
    for curvature, shear in [(.002, .001), (-.0004, -.0002), (.0007, .0003)]:
        state = response(model, curvature, shear, state, shear_rigidity=ga).state
    assert state.moment == pytest.approx(float(b_m)/10., rel=1e-12)
    r = response(model, -.0008, -.0005, state, shear_rigidity=ga)

    def c(slope):
        rigidity = float(slope)*100.
        return 12.*rigidity if ga is None else 12.*rigidity*ga/(12.*rigidity+ga)

    average = (c(1)*(b_x-zero_b) + c(inner_slope)*(zero_b-a_x)
               + c(outer_slope)*(a_x+Fraction(4, 5)))/Fraction(3, 2)
    assert r.state.moment == pytest.approx(-17./190., rel=1e-12)
    assert r.state.shear_force-state.shear_force == pytest.approx(-.0008*float(average), rel=1e-12)
    assert r.state.history.branch == "reloading"
    assert r.state.history.reversal_stack == []


@pytest.mark.parametrize("sign", [-1., 1.])
def test_one_ulp_interval_before_breakpoint_keeps_the_incoming_slope(sign):
    model = law()
    before = float(np.nextafter(sign*.001, 0.))
    state = response(model, before, 0.).state
    r = response(model, sign*.001, .001, state)
    # Its midpoint may round onto the endpoint; the actual interval is K1.
    assert r.state.shear_force == pytest.approx(1.2, rel=1e-12)
    assert r.tangent[1, 1] == pytest.approx(1200., rel=1e-12)


@pytest.mark.parametrize("ga", [120.*(1.+1e-13), 120.*(1.-1e-13)])
def test_near_singularity_on_either_side_is_rejected(ga):
    from fem.diagnostics import NumericalConditionError
    with pytest.raises(NumericalConditionError):
        response(law(negative=True), .006, .001, shear_rigidity=ga)


def test_finite_negative_branch_away_from_pole_remains_usable():
    model = law(negative=True)
    state = response(model, .006, .001, shear_rigidity=1200.).state
    r = response(model, .008, .002, state, shear_rigidity=1200.)
    assert r.state.shear_force-state.shear_force == pytest.approx(-2./15., rel=1e-12)
    assert r.effective_inertia == pytest.approx(-.005, rel=1e-12)


def test_mismatched_history_is_rejected_without_mutation():
    from dataclasses import replace
    state = response(law(), .002, .001).state
    inconsistent = replace(state, moment=.2)
    before = deepcopy(inconsistent)
    with pytest.raises(ValueError, match="match JR history"):
        response(law(), .003, .002, inconsistent)
    assert inconsistent == before


@pytest.mark.parametrize("length_scale,force_scale", [(1e-3, 1.), (1e3, 1e-3), (1., 1e6)])
@pytest.mark.parametrize("ga", [None, 1200.])
def test_consistent_unit_change_preserves_physical_force_inertia_and_tangent(length_scale, force_scale, ga):
    state_type, evaluate = kernel()
    model = JRStiffnessReductionModel(JRStiffnessReductionParams.symmetric(
        .001/length_scale, .003/length_scale, .005/length_scale,
        .1*force_scale*length_scale, .12*force_scale*length_scale,
        .13*force_scale*length_scale, beta=0.,
    ))
    transformed = evaluate(
        model, state_type.initial(model), .002/length_scale, .001,
        length=length_scale, young_modulus=2000.*force_scale/length_scale**2,
        shear_rigidity=None if ga is None else ga*force_scale,
    )
    # Independent physical values in the original units.
    average = 660. if ga is None else (600.+1200./11.)/2.
    coupling = -270. if ga is None else .5*(1200./11.-average)
    assert transformed.state.moment/(force_scale*length_scale) == pytest.approx(.11, rel=1e-12)
    assert transformed.state.shear_force/force_scale == pytest.approx(.001*average, rel=1e-12)
    assert transformed.effective_inertia/length_scale**4 == pytest.approx(.005, rel=1e-12)
    factors = np.array([[force_scale*length_scale**2, force_scale*length_scale],
                        [force_scale*length_scale, force_scale]])
    np.testing.assert_allclose(transformed.tangent/factors,
                               [[10., 0.], [coupling, average]], rtol=1e-12, atol=1e-12)


def test_shear_dominated_limit_does_not_underflow_to_a_false_zero_stiffness():
    state_type, evaluate = kernel()
    model = JRStiffnessReductionModel(JRStiffnessReductionParams.symmetric(
        .001, .003, .005, 1e299, 1.2e299, 1.3e299, beta=0.,
    ))
    # GA/(12B) underflows, but C approaches the representable positive GA.
    r = evaluate(model, state_type.initial(model), .002, .001,
                 length=1., young_modulus=2e303, shear_rigidity=1e-100)
    assert r.shear_coefficient == pytest.approx(1e-100, rel=1e-12, abs=0.)
    assert r.state.shear_force == pytest.approx(1e-103, rel=1e-12, abs=0.)
