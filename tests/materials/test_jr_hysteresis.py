"""materials / jr hysteresis contracts."""

import math
from dataclasses import asdict

import numpy as np
import pytest

from fem.material import NonlinearMaterialProperty
from fem.nonlinear.hysteresis import (
    JRStiffnessReductionModel as Model,
)
from fem.nonlinear.hysteresis import (
    JRStiffnessReductionParams as Params,
)
from tests.support.builders.jr_history import advance, history, parameters

pytestmark = pytest.mark.unit


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("sign", [1, -1])
@pytest.mark.parametrize(
    "delta,p,k", [(0.5, 5, 10), (1, 10, 2), (2, 12, 2), (4, 16, 1), (6, 18, 1), (10, 22, 0), (12, 22, 0)]
)
def test_monotonic_skeleton_all_segments_and_outgoing_tangent(sign, delta, p, k):
    m = Model(parameters())
    s = history(m, [sign * x for x in np.linspace(0, delta, 17)])
    assert (s.current_P, s.current_K) == pytest.approx((sign * p, k))


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("sign", [1, -1])
def test_reversal_uses_latest_committed_point_and_is_continuous(sign):
    m = Model(parameters(beta=1))
    s = history(m, [sign * 1, sign * 2])  # (2,12), Kd=10/2=5
    t = advance(m, s, sign * 1.9)
    assert (t.reversal_delta, t.reversal_P) == pytest.approx((sign * 2, sign * 12))
    assert (t.current_P, t.current_K) == pytest.approx((sign * 11.5, 5))
    eps = 1e-7
    assert m.get_force_and_stiffness(sign * (2 - eps), s)[:2] == pytest.approx((sign * (12 - 5 * eps), 5))
    assert m.get_force_and_stiffness(sign * (2 + eps), s)[:2] == pytest.approx((sign * (12 + 2 * eps), 2))


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("sign", [1, -1])
def test_zero_force_crossing_at_same_sign_displacement_and_target_continuity(sign):
    m = Model(parameters())
    s = history(m, [sign * 1, sign * 2])
    # Unload: P=12+10(delta-2); zero=.8; reload to (-1,-10): K=50/9.
    for x, p, k in [
        (1.0, 2.0, 10.0),
        (0.8, 0.0, 50 / 9),
        (0.5, -5 / 3, 50 / 9),
        (0.0, -40 / 9, 50 / 9),
        (-1.0, -10.0, 2.0),
        (-2.0, -12.0, 2.0),
    ]:
        s = advance(m, s, sign * x)
        assert (s.current_P, s.current_K) == pytest.approx((sign * p, k))
    assert not s.reversal_stack


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("sign", [1, -1])
def test_yielded_source_aims_at_opposite_second_breakpoint(sign):
    m = Model(parameters(beta=0, K_min=5))
    s = history(m, [sign * 6])  # (6,18), Kd floored to 5; zero=2.4
    assert m.get_force_and_stiffness(sign * 2.4, s)[:2] == pytest.approx((0, 2.5))
    assert m.get_force_and_stiffness(sign * (-0.8), s)[:2] == pytest.approx((-sign * 8, 2.5))
    assert m.get_force_and_stiffness(-sign * 4, s)[:2] == pytest.approx((-sign * 16, 1))


@pytest.mark.material_nonlinear
def test_zero_beyond_nominal_target_uses_forward_skeleton_intersection():
    m = Model(parameters(beta=1))
    s = history(m, [6])  # P=18, Kd=2*(6/4)^-1=4/3, zero=-7.5
    # Nominal (-4,-16) is behind zero. Unloading reaches plateau -22 at delta=-24.
    for x, p, k in [(-7.5, 0, 4 / 3), (-12, -6, 4 / 3), (-24, -22, 0), (-25, -22, 0)]:
        s = advance(m, s, x)
        assert (s.current_P, s.current_K) == pytest.approx((p, k))


@pytest.mark.material_nonlinear
def test_asymmetric_loading_both_directions():
    m = Model(parameters(delta_1_neg=2, delta_2_neg=6, delta_3_neg=12, P_1_neg=12, P_2_neg=20, P_3_neg=26))
    s = history(m, [2])
    # +2,+12 -> zero .8 -> negative virgin (-2,-12), slope 30/7.
    for x, p, k in [(-0.6, -6, 30 / 7), (-2, -12, 2), (-4, -16, 2)]:
        s = advance(m, s, x)
        assert (s.current_P, s.current_K) == pytest.approx((p, k))
    # -4,-16 -> zero -4/3 at Kd=6 -> experienced (+2,+12), slope 18/5.
    for x, p, k in [(-3, -10, 6), (-4 / 3, 0, 18 / 5), (1 / 3, 6, 18 / 5), (2, 12, 2)]:
        s = advance(m, s, x)
        assert (s.current_P, s.current_K) == pytest.approx((p, k))


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("sign", [1, -1])
def test_nested_inner_loops_restore_return_points_and_suspended_outer_path(sign):
    m = Model(parameters())
    s = history(m, [sign * 2])
    # A=(-.1,-5), B=(1.2,6), C=(.25,-2.5), D=(.85,3).
    # Zero points .4,.6,.5,.55. Return D->C->A resumes slopes 50/7 then 50/9.
    cases = [
        (-0.1, -5, 50 / 9, 0),
        (1.2, 6, 7.5, 1),
        (0.25, -2.5, 50 / 7, 2),
        (0.85, 3, 60 / 7, 3),
        (0.25, -2.5, 50 / 7, 2),
        (-0.1, -5, 50 / 9, 0),
        (-1, -10, 2, 0),
        (-2, -12, 2, 0),
    ]
    for x, p, k, depth in cases:
        s = advance(m, s, sign * x)
        assert (s.current_P, s.current_K) == pytest.approx((sign * p, k))
        assert len(s.reversal_stack) == depth


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("path", [[2, 1.5, 1.8, 2, 2.5], [2, 1.5, 1.8, 1.6, 0.8, -0.1]])
def test_reversal_before_force_zero_retraces_unloading_line(path):
    m = Model(parameters())
    s = history(m, path)
    p, k = (13, 2) if path[-1] == 2.5 else (-5, 50 / 9)
    assert (s.current_P, s.current_K) == pytest.approx((p, k))
    assert not s.reversal_stack


@pytest.mark.material_nonlinear
@pytest.mark.parametrize(
    "d,beta,kmin,expected",
    [
        (0.5, 1, 0.1, 10),
        (1, 1, 0.1, 10),
        (2, 1, 0.1, 5),
        (4, 1, 0.1, 2.5),
        (2, 10, 0.1, 2),
        (2, 0, 0.1, 10),
        (6, 1, 0.1, 4 / 3),
        (12, 1, 0.1, 2 / 3),
        (1000, 1, 0.1, 0.1),
        (6, 1, 3, 3),
    ],
)
@pytest.mark.parametrize("sign", [1, -1])
def test_region_specific_reduced_stiffness_and_limits(d, beta, kmin, expected, sign):
    m = Model(parameters(beta=beta, K_min=kmin))
    s = history(m, [sign * d])
    assert m.get_reduced_stiffness(s, sign) == pytest.approx(expected)


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("path", [[2], [2, 1.5], [2, -0.1], [2, -0.1, 1.2, 0.25]])
def test_trial_reproducibility_input_immutability_and_hold(path):
    m = Model(parameters())
    s = history(m, path)
    before = asdict(s)
    a = m.get_force_and_stiffness(0.37, s)
    m.get_force_and_stiffness(-50, s)
    assert m.get_force_and_stiffness(0.37, s) == a
    assert asdict(s) == before
    for _ in range(3):
        held = advance(m, s, s.current_delta)
        assert held == s
        assert held is not s
        s = held
    # Evaluation of the committed deformation must itself reproduce the force.
    assert m.get_force_and_stiffness(s.current_delta, s)[:2] == (s.current_P, s.current_K)


@pytest.mark.material_nonlinear
@pytest.mark.parametrize(
    "path,x",
    [
        ([], 0.5),
        ([], 2),
        ([], 6),
        ([], 12),
        ([2], 1.5),
        ([2], 0.5),
        ([2, -0.1], 1),
        ([2, -0.1, 1.2], 0.25),
        ([2, -0.1, 1.2, 0.25, 0.85], -0.5),
    ],
)
def test_force_finite_difference_matches_tangent_on_smooth_branches(path, x):
    m = Model(parameters())
    s = history(m, path)
    h = 1e-6
    fd = (m.get_force_and_stiffness(x + h, s)[0] - m.get_force_and_stiffness(x - h, s)[0]) / (2 * h)
    assert m.get_force_and_stiffness(x, s)[1] == pytest.approx(fd, rel=2e-8, abs=2e-8)


@pytest.mark.material_nonlinear
@pytest.mark.parametrize(
    "path,x,direction,left_k,right_k,p",
    [
        ([], 1, 1, 10, 2, 10),
        ([], 4, 1, 2, 1, 16),
        ([], 10, 1, 1, 0, 22),
        ([2], 0.8, -1, 10, 50 / 9, 0),
        ([2], -1, -1, 50 / 9, 2, -10),
        ([2, -0.1, 1.2, 0.25, 0.85], 0.25, -1, 25 / 3, 50 / 7, -2.5),
        ([2, -0.1, 1.2, 0.25, 0.85], -0.1, -1, 50 / 7, 50 / 9, -5),
    ],
)
def test_transition_continuity_and_one_sided_tangent(path, x, direction, left_k, right_k, p):
    m = Model(parameters())
    s = history(m, path)
    h = 1e-7
    assert m.get_force_and_stiffness(x, s)[:2] == pytest.approx((p, right_k))
    before = m.get_force_and_stiffness(x - direction * h, s)
    after = m.get_force_and_stiffness(x + direction * h, s)
    assert before[:2] == pytest.approx((p - direction * h * left_k, left_k))
    assert after[:2] == pytest.approx((p + direction * h * right_k, right_k))


@pytest.mark.material_nonlinear
@pytest.mark.parametrize(
    "points", [[2, -0.1, 1.2, 0.25, 0.85, -2, 3], [6, -25, 12, -30], [2, 1.5, 1.8, 1.6, -2]]
)
@pytest.mark.parametrize("parts", [2, 13, 40])
def test_subdividing_same_path_preserves_response_and_future_history(points, parts):
    m = Model(parameters(beta=0 if points[0] == 2 else 1))
    coarse = fine = m.create_initial_state()
    last = 0
    for end in points:
        coarse = advance(m, coarse, end)
        for x in np.linspace(last, end, parts + 1)[1:]:
            fine = advance(m, fine, x)
        assert (fine.current_P, fine.current_K) == pytest.approx((coarse.current_P, coarse.current_K))
        assert fine.reversal_stack == pytest.approx(coarse.reversal_stack)
        for probe in [-3, -0.2, 0.1, 1, 8]:
            assert m.get_force_and_stiffness(probe, fine)[:2] == pytest.approx(
                m.get_force_and_stiffness(probe, coarse)[:2]
            )
        last = end


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("field", list(asdict(parameters())))
@pytest.mark.parametrize("bad", [math.nan, math.inf, -math.inf])
def test_nonfinite_parameters_are_explicitly_rejected(field, bad):
    with pytest.raises(ValueError):
        parameters(**{field: bad})


@pytest.mark.material_nonlinear
@pytest.mark.parametrize(
    "changes",
    [
        dict(delta_1_pos=0),
        dict(delta_2_neg=1),
        dict(P_1_neg=-1),
        dict(P_2_pos=9),
        dict(beta=-1),
        dict(K_min=0),
        dict(K_min=11),
        dict(P_2_pos=50, P_3_pos=60),
        dict(P_3_neg=40),
    ],
)
def test_invalid_parameter_order_and_incompatible_stiffness_rejected(changes):
    with pytest.raises(ValueError):
        parameters(**changes)


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("bad", [math.nan, math.inf, -math.inf])
def test_nonfinite_trial_deformation_is_rejected(bad):
    m = Model(parameters())
    with pytest.raises(ValueError):
        m.get_force_and_stiffness(bad, m.create_initial_state())
    with pytest.raises(ValueError):
        m.get_skeleton_force(bad, 1)


@pytest.mark.material_nonlinear
def test_zero_plateau_is_not_replaced_by_artificial_stiffness():
    m = Model(parameters(P_2_pos=10, P_3_pos=10, P_2_neg=10, P_3_neg=10))
    assert m.get_skeleton_force(100, 1) == (10, 0)
    assert m.get_reduced_stiffness(history(m, [100]), 1) == 0.1


@pytest.mark.material_nonlinear
@pytest.mark.parametrize(
    "changes",
    [
        dict(delta_1_pos=0),
        dict(P_3_neg=math.inf),
        dict(beta=math.nan),
        dict(K_min=math.inf),
        dict(E=math.nan),
        dict(E=0),
        dict(nu=-1),
        dict(nu=0.5),
        dict(density=-1),
        dict(density=math.inf),
    ],
)
def test_material_api_validates_before_division_or_analysis(changes):
    kwargs = dict(
        name="rc",
        E=2000,
        nu=0.2,
        delta_1_pos=1,
        delta_2_pos=4,
        delta_3_pos=10,
        P_1_pos=10,
        P_2_pos=16,
        P_3_pos=22,
    )
    kwargs.update(changes)
    with pytest.raises(ValueError):
        NonlinearMaterialProperty(**kwargs)


@pytest.mark.material_nonlinear
def test_symmetric_constructor_rejects_zero_before_default_stiffness_division():
    with pytest.raises(ValueError):
        Params.symmetric(0, 4, 10, 10, 16, 22, 0.4)


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("sign", [1, -1])
def test_reversing_at_exact_zero_retraces_and_restores_zero_crossing_path(sign):
    m = Model(parameters())
    s = history(m, [sign * x for x in [2, 0.8, 1.2, 0.8]])
    assert (s.current_P, s.current_K) == pytest.approx((0, 50 / 9))
    s = advance(m, s, -sign * 0.1)
    assert (s.current_P, s.current_K) == pytest.approx((-sign * 5, 50 / 9))
    assert not s.reversal_stack


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("sign", [1, -1])
def test_internal_unloading_retrace_does_not_leave_a_stale_reversal(sign):
    m = Model(parameters())
    s = history(m, [sign * x for x in [2, -0.1, 0.1, -0.1, -0.5]])
    # A=-.1,-5 -> +.1,-3 (before zero .4) -> A -> resume outer line.
    assert (s.current_P, s.current_K) == pytest.approx((-sign * 65 / 9, 50 / 9))
    assert not s.reversal_stack


@pytest.mark.material_nonlinear
def test_small_nonzero_increment_is_not_treated_as_hold():
    m = Model(parameters())
    s = history(m, [2])
    t = advance(m, s, 2 - 1e-13)
    assert t.loading_direction == -1
    assert t.current_K == 10
    assert t.current_P < s.current_P


@pytest.mark.material_nonlinear
def test_asymmetric_elastic_reversal_and_origin_one_sided_tangent():
    m = Model(parameters(delta_1_neg=2, delta_2_neg=6, delta_3_neg=12, P_1_neg=12, P_2_neg=20, P_3_neg=26))
    s = history(m, [0.5, -0.5, 0])
    assert (s.current_P, s.current_K) == (0, 10)
    s = history(m, [-0.5, 0.5, 0])
    assert (s.current_P, s.current_K) == (0, 6)
    assert not s.reversal_stack


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("seed", [7, 23])
def test_irregular_cyclic_path_refinement_with_asymmetry(seed):
    # Seeded reversal locations exercise events; oracle is path invariance,
    # supplemented above by independent absolute force/tangent hand calculations.
    m = Model(
        parameters(beta=0.4, delta_1_neg=2, delta_2_neg=6, delta_3_neg=12, P_1_neg=12, P_2_neg=20, P_3_neg=26)
    )
    points = np.random.default_rng(seed).uniform(-15, 15, 25)
    coarse = fine = m.create_initial_state()
    last = 0
    for end in points:
        coarse = advance(m, coarse, end)
        for x in np.linspace(last, end, 12)[1:]:
            fine = advance(m, fine, x)
        assert (fine.current_P, fine.current_K) == pytest.approx((coarse.current_P, coarse.current_K))
        last = end


@pytest.mark.material_nonlinear
def test_maximum_deformation_api_includes_off_skeleton_experience():
    m = Model(parameters())
    s = history(m, [2, -0.5])
    assert (s.delta_max_pos, s.delta_max_neg) == (2, 0.5)
    assert s.P_max_neg == pytest.approx(65 / 9)


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("sign", [1, -1])
def test_reduced_stiffness_inside_loop_keeps_outer_damage_and_returns_exactly(sign):
    m = Model(parameters(beta=1))
    s = history(m, [sign * 2])
    # Outer Kd=5, zero=-.4, reload slope 50/3.
    # A=(-.7,-5), Kd=10 -> zero=-.2 -> B=(.9,6).
    # At B retain positive damage dmax=2: Kd=5, zero=-.3 -> A, slope 12.5.
    for x, p, k in [
        (-0.7, -5, 50 / 3),
        (0.9, 6, 60 / 11),
        (0.8, 5.5, 5),
        (-0.5, -2.5, 12.5),
        (-0.7, -5, 50 / 3),
    ]:
        s = advance(m, s, sign * x)
        assert (s.current_P, s.current_K) == pytest.approx((sign * p, k))
    assert not s.reversal_stack
