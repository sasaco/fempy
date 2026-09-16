"""PQ-10: physical response and convergence must not depend on units."""

import numpy as np
import pytest

from fem.convergence import generalized_displacement_norm, generalized_force_norm
from tools.validation.unit_invariance import UNIT_SYSTEMS, run_benchmarks

pytestmark = [pytest.mark.oracle, pytest.mark.material_nonlinear]


def test_generalized_norms_follow_force_and_length_scaling():
    base_force = np.array([1, 2, 3, 4, 5, 6, -2, 1, -3, 7, -8, 9.0])
    base_displacement = np.array(
        [0.1, 0.2, -0.3, 0.01, -0.02, 0.03, 0.4, -0.5, 0.6, -0.04, 0.05, -0.06]
    )
    force_norm = generalized_force_norm(base_force, stride=6, length=2.0)
    displacement_norm = generalized_displacement_norm(
        base_displacement, stride=6, length=2.0
    )
    for units in UNIT_SYSTEMS:
        force = base_force.copy()
        force.reshape(-1, 6)[:, :3] *= units.force_scale
        force.reshape(-1, 6)[:, 3:] *= units.force_scale * units.length_scale
        displacement = base_displacement.copy()
        displacement.reshape(-1, 6)[:, :3] *= units.length_scale
        assert generalized_force_norm(
            force, stride=6, length=units.length(2.0)
        ) == pytest.approx(units.force_scale * force_norm)
        assert generalized_displacement_norm(
            displacement, stride=6, length=units.length(2.0)
        ) == pytest.approx(displacement_norm)


def _assert_same(case: dict, fields: tuple[str, ...]) -> None:
    reference = case["N-m"]
    for name in ("N-mm", "kN-m"):
        assert case[name]["iterations"] == reference["iterations"]
        for field in fields:
            np.testing.assert_allclose(
                case[name][field], reference[field], rtol=2e-11, atol=2e-12
            )
        assert max(case[name]["relative_residual"]) <= 1.01


def test_load_control_mixed_translation_rotation_has_same_decision_and_response():
    result = run_benchmarks()["bending_load_control"]
    _assert_same(
        result,
        (
            "tip_dy_m",
            "tip_rz_rad",
            "curvature_per_m",
            "reaction_moment_Nm",
            "characteristic_length_m",
        ),
    )
    assert result["N-m"]["iterations"] == [4]
    assert result["N-m"]["characteristic_length_source"] == "geometry_span"
    assert result["N-m"]["convergence_measure"] == "dimensionless_generalized_l2"


def test_cyclic_spring_and_prescribed_motion_are_unit_invariant():
    result = run_benchmarks()
    _assert_same(
        result["axial_cyclic_with_spring"],
        ("tip_dx_m", "reaction_force_N", "spring_stiffness_N_per_m"),
    )
    _assert_same(
        result["prescribed_displacement"], ("tip_dx_m", "reaction_force_N")
    )


def test_displacement_control_decision_is_unit_invariant():
    result = run_benchmarks()["displacement_control"]
    _assert_same(result, ("tip_dx_m", "load_factor"))


def test_density_and_translational_rotational_springs_preserve_frequency():
    result = run_benchmarks()["modal_with_springs"]
    reference = result["N-m"]
    for name in ("N-mm", "kN-m"):
        assert result[name]["frequency_hz"] == pytest.approx(
            reference["frequency_hz"], rel=2e-10
        )
        assert result[name]["density_base"] == pytest.approx(7850.0)
        assert result[name]["relative_residual"] < 1e-8
