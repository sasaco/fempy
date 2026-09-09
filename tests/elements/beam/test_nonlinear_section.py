"""elements/beam / nonlinear section contracts."""

import copy

import numpy as np
import pytest

from tests.support.builders.nonlinear_beam import RIGIDITIES, axial_motion, beam, force
from tests.support.oracles.elastic_beam import elastic_matrix

pytestmark = pytest.mark.unit


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("rotated", [False, True])
@pytest.mark.parametrize("length", [0.5, 2.0, 5.0])
def test_response_curvature_is_local_total_and_does_not_change_history(rotated, length):
    e = beam(dofs=["moment_y", "moment_z"], length=length, rotated=rotated)
    transform = e.get_transformation_matrix(12)
    local = np.zeros(12)
    local[[4, 10]] = [-0.003 * length / 2, 0.003 * length / 2]
    local[[5, 11]] = [0.002 * length / 2, -0.002 * length / 2]
    u = transform.T @ local
    e.get_internal_force(u)
    e.commit_state()
    committed = copy.deepcopy(e.committed_states)
    e.get_internal_force(2 * u)  # An uncommitted candidate must stay untouched by output.
    candidate = copy.deepcopy(e.current_states)
    assert e.calculate_curvature(u) == pytest.approx(dict(y=0.003, z=-0.002), abs=1e-14)
    assert e.calculate_curvature(2 * u) == pytest.approx(dict(y=0.006, z=-0.004), abs=1e-14)
    assert e.current_states == candidate
    assert e.committed_states == committed
    e.rollback_state()
    assert e.calculate_curvature(u) == pytest.approx(dict(y=0.003, z=-0.002), abs=1e-14)


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("rotation", [False, True])
@pytest.mark.parametrize("scale", [0.00002, 0.0012])
def test_tangent_is_finite_difference_of_force(rotation, scale):
    e = beam(dofs=RIGIDITIES, rotated=rotation)
    t = e.get_transformation_matrix()
    u = t.T @ (np.array([1, -2, 3, -4, 5, -6, 7, 8, -9, 10, -11, 12]) * scale)
    k = e.get_tangent_stiffness_matrix(u)
    h = 1e-7
    fd = np.column_stack([(force(e, u + h * d) - force(e, u - h * d)) / (2 * h) for d in np.eye(12)])
    np.testing.assert_allclose(k, fd, rtol=2e-8, atol=2e-7)
    np.testing.assert_allclose(k, k.T, atol=1e-12)


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("dof,index", [("axial", 0), ("torsion", 3), ("moment_y", 4), ("moment_z", 5)])
@pytest.mark.parametrize("length", [0.5, 2.0, 5.0])
def test_section_skeleton_uses_strain_or_curvature_not_endpoint_motion(dof, index, length):
    e = beam(dofs=[dof], length=length)
    u = np.zeros(12)
    q = 0.003
    u[index], u[index + 6] = -q * length / 2, q * length / 2
    # Opposite end rotations: zero average rotation and zero shear distortion.
    expected = np.zeros(12)
    p = RIGIDITIES[dof] * (0.001 + 0.25 * (q - 0.001))
    expected[index], expected[index + 6] = -p, p
    np.testing.assert_allclose(force(e, u), expected, atol=1e-12)


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("active,other", [("moment_y", 5), ("moment_z", 4)])
def test_named_bending_law_leaves_other_plane_elastic(active, other):
    e = beam(dofs=[active])
    u = np.zeros(12)
    u[other], u[other + 6] = -0.003, 0.003
    np.testing.assert_allclose(force(e, u), elastic_matrix(2, True) @ u, atol=1e-12)


@pytest.mark.material_nonlinear
def test_trial_order_cannot_change_result_or_committed_history():
    e = beam(dofs=["axial"])
    for q in [0.001, 0.005]:
        force(e, axial_motion(q))
        e.commit_state()
    committed = copy.deepcopy(e.committed_states)
    target = axial_motion(0.004)
    f1 = force(e, target)
    k1 = e.get_tangent_stiffness_matrix(target)
    trial = copy.deepcopy(e.current_states)
    force(e, axial_motion(-0.012))
    e.get_tangent_stiffness_matrix(axial_motion(0.018))
    np.testing.assert_array_equal(force(e, target), f1)
    np.testing.assert_array_equal(e.get_tangent_stiffness_matrix(target), k1)
    assert e.committed_states == committed
    assert e.current_states == trial
    e.rollback_state()
    assert e.current_states == committed


@pytest.mark.material_nonlinear
def test_tangent_and_output_do_not_replace_trial_to_be_committed():
    e = beam(dofs=["axial"])
    force(e, axial_motion(0.003))
    trial = copy.deepcopy(e.current_states)
    e.get_tangent_stiffness_matrix(axial_motion(0.01))
    e.calculate_forces(axial_motion(-0.01))
    assert e.current_states == trial
    e.commit_state()
    assert e.committed_states == trial
    e.reset_states()
    assert all(s.current_delta == 0 for states in e.committed_states.values() for s in states.values())


@pytest.mark.material_nonlinear
def test_output_preserves_converged_force_after_commit_and_other_trials():
    e = beam(dofs=["axial"])
    for q in [0.001, 0.005, 0.004]:
        u = axial_motion(q)
        converged_force = force(e, u).copy()
        e.commit_state()
    force(e, axial_motion(-0.012))
    before = copy.deepcopy((e.current_states, e.committed_states))
    for _ in range(3):
        output = e.calculate_forces(u)
        np.testing.assert_allclose(np.r_[output["i_end"], output["j_end"]], converged_force, atol=1e-12)
    assert (e.current_states, e.committed_states) == before
    e.rollback_state()
    output = e.calculate_forces(u)
    np.testing.assert_allclose(np.r_[output["i_end"], output["j_end"]], converged_force, atol=1e-12)


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("dof,index", [("axial", 0), ("moment_y", 4), ("moment_z", 5)])
@pytest.mark.parametrize("q", [0.004, 0.0005])
def test_tangent_with_fixed_committed_history_on_smooth_branches(dof, index, q):
    e = beam(dofs=[dof], rotated=True)
    t = e.get_transformation_matrix()

    def motion(value):
        u = np.zeros(12)
        u[index], u[index + 6] = -value, value
        return t.T @ u

    for value in [0.001, 0.005]:
        force(e, motion(value))
        e.commit_state()
    committed = copy.deepcopy(e.committed_states)
    u = motion(q)
    h = 1e-7
    fd = np.column_stack([(force(e, u + h * d) - force(e, u - h * d)) / (2 * h) for d in np.eye(12)])
    np.testing.assert_allclose(e.get_tangent_stiffness_matrix(u), fd, rtol=2e-8, atol=2e-7)
    assert e.committed_states == committed
