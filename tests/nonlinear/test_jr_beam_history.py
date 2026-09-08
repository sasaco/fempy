"""Phase 3 integration: hand-calculated JR cycles at the central section."""
import copy

import numpy as np
import pytest

from src.fem.elements.nonlinear_bar_element import NonlinearBarElement
from src.fem.material import Material, MaterialProperty, BarParameter
from src.fem.nonlinear.hysteresis import JRStiffnessReductionParams


def beam(dof):
    e = NonlinearBarElement(1, [10, 307], 1, 1)
    e.set_node_coordinates({10: np.zeros(3), 307: np.array([2., 0., 0.])})
    mat = Material()
    mat.add_material(1, MaterialProperty('test', 2000., .25))
    e.set_material_properties(mat, BarParameter(3., .4, .9, .7, .6, .8))
    # Small strain/curvature: x=delta/.001, slopes are 10000,2000,1000,0.
    e.set_hysteresis_model(dof, JRStiffnessReductionParams.symmetric(
        .001, .004, .01, 10, 16, 22, beta=0))
    return e


def motion(dof, x):
    u = np.zeros(12)
    i, j = NonlinearBarElement.DOF_MAPPING[dof]
    u[i], u[j] = -x*.001, x*.001  # L=2; opposing rotations cancel shear.
    return u


@pytest.mark.parametrize('dof', list(NonlinearBarElement.DOF_MAPPING))
@pytest.mark.parametrize('sign', [1, -1])
def test_jr_beam_reversal_commit_rollback_and_saved_output(dof, sign):
    e = beam(dof)
    i, j = e.DOF_MAPPING[dof]
    cases = [(2, 12), (-.1, -5), (1.2, 6), (.25, -2.5), (.85, 3),
             (.25, -2.5), (-.1, -5), (-2, -12)]
    for x, p in cases:
        u = motion(dof, sign*x)
        committed = copy.deepcopy(e.committed_states)
        f = e.get_internal_force(u)
        expected = np.zeros(12)
        expected[i], expected[j] = -sign*p, sign*p
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
        s = accepted[dof]['center']
        assert model.get_force_and_stiffness(s.current_delta, s)[0] == pytest.approx(sign*p)
        np.testing.assert_allclose(e.get_internal_force(u), expected, atol=2e-12)
        assert e.current_states == accepted
        e.get_internal_force(motion(dof, 9))
        e.rollback_state()
        assert e.current_states == e.committed_states == accepted
        for _ in range(2):
            out = e.calculate_forces(u)
            np.testing.assert_allclose(np.r_[out['i_end'], out['j_end']], expected, atol=2e-12)
            out['j_end'][:] = 999
        assert e.committed_states == accepted
    assert set(e.get_max_displacement(dof)) == {'center_pos', 'center_neg'}
    e.reset_states()
    assert e.committed_states[dof]['center'] == e.hysteresis_models[dof].create_initial_state()
    np.testing.assert_array_equal(e.get_internal_force(np.zeros(12)), np.zeros(12))


@pytest.mark.parametrize('dof', list(NonlinearBarElement.DOF_MAPPING))
@pytest.mark.parametrize('x', [1.5, .5, -.5])
def test_jr_beam_history_tangent_matches_all_force_columns(dof, x):
    e = beam(dof)
    e.get_internal_force(motion(dof, 2))
    e.commit_state()
    committed = copy.deepcopy(e.committed_states)
    u, h = motion(dof, x), 1e-8
    k = e.get_tangent_stiffness_matrix(u)
    fd = np.column_stack([(e.get_internal_force(u+h*d)-e.get_internal_force(u-h*d))/(2*h)
                          for d in np.eye(12)])
    np.testing.assert_allclose(k, fd, rtol=2e-8, atol=2e-7)
    assert e.committed_states == committed
