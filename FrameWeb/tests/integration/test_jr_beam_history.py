"""integration / jr beam history contracts."""

import copy

import numpy as np
import pytest

from fem.elements.nonlinear_bar_element import NonlinearBarElement
from tests.support.assertions import assert_axial
from tests.support.builders.input_routes import python_axial
from tests.support.builders.jr_beam import beam, motion

pytestmark = pytest.mark.integration


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("dof", list(NonlinearBarElement.DOF_MAPPING))
@pytest.mark.parametrize("sign", [1, -1])
def test_jr_beam_reversal_commit_rollback_and_saved_output(dof, sign):
    e = beam(dof)
    i, j = e.DOF_MAPPING[dof]
    cases = [(2, 12), (-0.1, -5), (1.2, 6), (0.25, -2.5), (0.85, 3), (0.25, -2.5), (-0.1, -5), (-2, -12)]
    for x, p in cases:
        u = motion(dof, sign * x)
        committed = copy.deepcopy(e.committed_states)
        f = e.get_internal_force(u)
        expected = np.zeros(12)
        expected[i], expected[j] = -sign * p, sign * p
        np.testing.assert_allclose(f, expected, atol=2e-12)
        assert e.committed_states == committed
        candidate = copy.deepcopy(e.current_states)
        # Read APIs at another displacement must not replace the commit candidate.
        e.get_tangent_stiffness_matrix(motion(dof, -9))
        e.calculate_forces(motion(dof, 9))
        assert e.current_states == candidate
        e.commit_state()
        accepted = copy.deepcopy(e.committed_states)
        # Direct JR reevaluation at the committed point, separate from saved output.
        model = e.hysteresis_models[dof]
        s = accepted[dof]["center"]
        assert model.get_force_and_stiffness(s.current_delta, s)[0] == pytest.approx(sign * p)
        np.testing.assert_allclose(e.get_internal_force(u), expected, atol=2e-12)
        assert e.current_states == accepted
        e.get_internal_force(motion(dof, 9))
        e.rollback_state()
        assert e.current_states == e.committed_states == accepted
        for _ in range(2):
            out = e.calculate_forces(u)
            np.testing.assert_allclose(np.r_[out["i_end"], out["j_end"]], expected, atol=2e-12)
            out["j_end"][:] = 999
        assert e.committed_states == accepted
    assert set(e.get_max_displacement(dof)) == {"center_pos", "center_neg"}
    e.reset_states()
    assert e.committed_states[dof]["center"] == e.hysteresis_models[dof].create_initial_state()
    np.testing.assert_array_equal(e.get_internal_force(np.zeros(12)), np.zeros(12))


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("dof", list(NonlinearBarElement.DOF_MAPPING))
@pytest.mark.parametrize("x", [1.5, 0.5, -0.5])
def test_jr_beam_history_tangent_matches_all_force_columns(dof, x):
    e = beam(dof)
    e.get_internal_force(motion(dof, 2))
    e.commit_state()
    committed = copy.deepcopy(e.committed_states)
    u, h = motion(dof, x), 1e-8
    k = e.get_tangent_stiffness_matrix(u)
    fd = np.column_stack(
        [(e.get_internal_force(u + h * d) - e.get_internal_force(u - h * d)) / (2 * h) for d in np.eye(12)]
    )
    np.testing.assert_allclose(k, fd, rtol=2e-8, atol=2e-7)
    assert e.committed_states == committed


@pytest.mark.material_nonlinear
def test_real_jr_failure_restores_last_commit_and_fresh_run():
    m = python_axial(30)
    m.analysis_params["load_factors"] = [0.4, 1]
    with pytest.raises(RuntimeError, match="converge") as failure:
        m.run()
    assert failure.value.step == 2
    assert m.results is None

    assert m.nonlinear_solver.displacement[6] == pytest.approx(0.004, abs=1e-12)
    element = m.elements[7]
    committed = element.committed_states["axial"]["center"]
    assert committed.current_delta == pytest.approx(0.002, abs=1e-12)
    assert committed.current_P == pytest.approx(12, abs=1e-10)
    assert element.current_states == element.committed_states
    np.testing.assert_allclose(failure.value.displacement, m.nonlinear_solver.displacement)
    m.boundary.loads.clear()
    m.add_load(30, fx=12)
    m.analysis_params.pop("load_factors")
    assert_axial(m.run())
    m.analysis_params["max_iterations"] = 1
    with pytest.raises(RuntimeError):
        m.run()
    assert m.results is None
