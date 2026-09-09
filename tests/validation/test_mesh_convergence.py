"""PQ-09 convergence rates and measured applicability limits."""

import pytest

from tools.validation.mesh_convergence import run_benchmarks

pytestmark = [pytest.mark.oracle, pytest.mark.slow]


@pytest.fixture(scope="module")
def measurements():
    return run_benchmarks()


def test_closed_form_references_and_expected_asymptotic_rates(measurements):
    assert measurements["reference"] == {
        "beam": "Euler--Bernoulli closed form: EI w'''' = q",
        "shell": "Kirchhoff or Reissner--Mindlin cantilever strip, nu=0",
        "solid": "manufactured u=(a*x^2,-2*a*x*y,0), b=(-2*mu*a,0,0)",
    }
    expected = {
        "beam": {"displacement": (3.9, 4.1), "stress": (1.9, 2.1), "energy": (3.9, 4.1)},
        "dkt": {"displacement": (1.8, 2.2), "stress": (0.9, 1.2), "energy": (1.8, 2.3)},
        "mitc4": {"displacement": (1.7, 2.2), "stress": (0.9, 1.2), "energy": (3.5, 4.5)},
        "hexa8": {"displacement": (1.9, 2.1), "stress": (0.9, 1.1), "energy": (1.9, 2.1)},
    }
    for formulation, bounds in expected.items():
        rows = measurements[formulation]
        for error in ("displacement_error", "stress_error", "energy_error"):
            assert rows[0][error] > rows[1][error] > rows[2][error]
        for row in rows[1:]:
            for norm, (lower, upper) in bounds.items():
                assert lower <= row["orders"][norm] <= upper


def test_distortion_and_aspect_sweeps_remain_within_measured_scope(measurements):
    limits = measurements["limits"]
    for name in ("dkt_distortion", "mitc4_distortion", "hexa8_distortion"):
        regular, *distorted = limits[name]
        for row in distorted:
            assert row["displacement_error"] <= 1.1 * regular["displacement_error"]
            # The severe 0.4h skew is an applicability boundary: DKT's moment
            # error grows by about 20%, while the other two remain below 3%.
            assert row["stress_error"] <= 1.2 * regular["stress_error"]
    for name in ("shell_aspect", "hexa8_aspect"):
        rows = limits[name]
        for error in ("displacement_error", "stress_error", "energy_error"):
            values = [row[error] for row in rows]
            assert max(values) <= min(values) * 1.001


def test_mitc4_thickness_sweep_has_no_shear_locking_in_strip_problem(measurements):
    thick, reference, thin = measurements["limits"]["mitc4_thickness"]
    assert thick["thickness"] / thin["thickness"] == pytest.approx(100)
    assert thin["displacement_error"] <= 1.02 * reference["displacement_error"]
    assert thin["stress_error"] <= 1.001 * reference["stress_error"]
    assert thin["energy_error"] < 1.0e-4


def test_hexa8_nearly_incompressible_stress_and_energy_are_an_explicit_limit(measurements):
    ordinary, near, extreme = measurements["limits"]["hexa8_poisson"]
    assert [row["poisson"] for row in (ordinary, near, extreme)] == [0.25, 0.49, 0.499]
    # The exact field is divergence-free, but its Q1 interpolant is not.  The
    # rapidly growing stress/energy errors are volumetric locking, not a
    # relaxed tolerance or an assertion of near-incompressible suitability.
    assert near["stress_error"] > 20 * ordinary["stress_error"]
    assert extreme["stress_error"] > 200 * ordinary["stress_error"]
    assert extreme["energy_error"] > 0.3
