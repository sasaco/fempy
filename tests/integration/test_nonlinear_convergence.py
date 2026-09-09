"""integration / nonlinear convergence contracts."""

import numpy as np
import pytest

from tests.support.builders.nonlinear_reference import configuration, solve
from tests.support.oracles.uniform_beam import assert_uniform

pytestmark = pytest.mark.integration


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("n_steps,tolerance", [(1, 1e-6), (7, 1e-8), (40, 1e-10)])
def test_load_increment_and_tolerance_sensitivity(n_steps, tolerance):
    d = configuration("moment_z", n=4, force=18)
    d["analysis_params"] = dict(n_load_steps=n_steps, tolerance=tolerance)
    assert_uniform(solve(d, "json"), "moment_z", 4, 18, 0.006)


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("n", [1, 2, 4, 8, 16])
def test_nonuniform_bending_discrete_compliance_and_continuum_limit(n):
    # A tip force gives a *nonuniform* moment, with two skeleton breakpoints.
    # Independent continuous-section reference: integrate kappa(M=9*t).
    p, length, ei, ga = 9.0, 2.0, 10000.0, 4000 * 5 / 6
    theta_cont = v_cont = 0.0
    for a, b, slope, intercept in [
        (0, 10 / 9, 1 / 10000, 0),
        (10 / 9, 16 / 9, 1 / 2000, -8 / 2000),
        (16 / 9, 2, 1 / 1000, -12 / 1000),
    ]:
        theta_cont += slope * p * (b * b - a * a) / 2 + intercept * (b - a)
        v_cont += slope * p * (b**3 - a**3) / 3 + intercept * (b * b - a * a) / 2
    v_cont += p * length / ga
    h = length / n
    t = (np.arange(n) + 0.5) * h
    moment = p * t
    kappa = np.where(
        moment <= 10, moment / 10000, np.where(moment <= 16, (moment - 8) / 2000, (moment - 12) / 1000)
    )
    theta_discrete = h * np.sum(kappa)
    v_discrete = h * np.dot(t, kappa) + p * length / ga + n * p * h**3 / (12 * ei)
    d = configuration("moment_z", n=n, force=0)
    d["load"]["1"]["load_node"] = [dict(n=10 + 20 * n, ty=p)]
    r = solve(d, "json")
    tip = r["node_displacements"][str(10 + 20 * n)]
    assert tip["dy"] == pytest.approx(v_discrete, rel=1e-8, abs=1e-10)
    assert tip["rz"] == pytest.approx(theta_discrete, rel=1e-8, abs=1e-10)
    # No assertion that one central section equals distributed plasticity.
    # Piecewise-linear midpoint integration error is O(h**2), bounded using
    # maximum slope and the two slope jumps of the inverse skeleton.
    assert abs(tip["dy"] - v_cont) <= 0.02 * h * h
    assert abs(tip["rz"] - theta_cont) <= 0.01 * h * h
    print(
        f"nonuniform n={n}: u={tip['dy']:.12g}, continuum={v_cont:.12g}, "
        f"rel_error={(tip['dy'] - v_cont) / v_cont:.8g}, theta_error={tip['rz'] - theta_cont:.8g}"
    )
