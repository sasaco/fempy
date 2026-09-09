"""solvers / boundary conditions contracts."""

import numpy as np
import pytest
from scipy.sparse import csr_matrix, eye
from scipy.sparse.linalg import spsolve

from fem.boundary_condition import BoundaryCondition
from fem.solver import Solver

pytestmark = pytest.mark.unit


@pytest.mark.material_nonlinear
def test_only_last_node_is_fixed_in_four_node_frame():
    boundary = BoundaryCondition()
    boundary.add_restraint(4, [True] * 6)
    K = eye(24, format="csr") * 10
    F = np.ones(24)
    modified, rhs = Solver().apply_boundary_conditions(K, F, boundary)
    u = spsolve(modified, rhs)
    np.testing.assert_allclose(u[:18], 0.1)
    np.testing.assert_array_equal(u[18:], 0)
    np.testing.assert_array_equal(K.diagonal(), 10)
    np.testing.assert_array_equal(F, 1)


@pytest.mark.material_nonlinear
def test_prescribed_displacement_corrects_free_rhs_and_preserves_symmetry():
    # k=10 between nodes, with k=20 from node 2 to ground.
    # u1=0.2, F2=4 => u2=(4+10*0.2)/(10+20)=0.2.
    K = np.eye(12)
    K[np.ix_([0, 6], [0, 6])] = [[10, -10], [-10, 30]]
    F = np.zeros(12)
    F[6] = 4
    boundary = BoundaryCondition()
    boundary.add_restraint(1, [True] * 6, [0.2, 0, 0, 0, 0, 0])
    modified, rhs = Solver().apply_boundary_conditions(csr_matrix(K), F, boundary)
    np.testing.assert_array_equal(modified.toarray(), modified.toarray().T)
    u = spsolve(modified, rhs)
    np.testing.assert_allclose(u[[0, 6]], [0.2, 0.2], atol=1e-14)
    assert (K @ u - F)[6] == pytest.approx(0, abs=1e-14)


@pytest.mark.material_nonlinear
def test_solid_boundary_uses_explicit_three_dof_stride():
    boundary = BoundaryCondition()
    boundary.add_restraint(4, [True] * 6)
    modified, rhs = Solver().apply_boundary_conditions(
        eye(12, format="csr"), np.ones(12), boundary, max_dof_per_node=3
    )
    u = spsolve(modified, rhs)
    np.testing.assert_array_equal(u[:9], 1)
    np.testing.assert_array_equal(u[9:], 0)


@pytest.mark.material_nonlinear
def test_spring_support_is_not_a_fixed_displacement():
    boundary = BoundaryCondition()
    boundary.add_restraint(2, [True, False, False, False, False, False], [2000, 0, 0, 0, 0, 0])
    F = np.zeros(12)
    F[6] = 30
    modified, rhs = Solver().apply_boundary_conditions(eye(12, format="csr") * 1000, F, boundary)
    assert spsolve(modified, rhs)[6] == pytest.approx(0.01)


@pytest.mark.material_nonlinear
def test_reaction_uses_actual_node_stride():
    boundary = BoundaryCondition()
    boundary.add_restraint(4, [True] * 6)
    F = np.zeros(24)
    F[18] = 12
    reactions = Solver()._calculate_reaction_forces(eye(24, format="csr"), np.zeros(24), F, boundary)
    assert reactions[4]["fx"] == -12


@pytest.mark.material_nonlinear
def test_newton_bc_enforces_remaining_prescribed_motion_and_spring_force():
    boundary = BoundaryCondition()
    boundary.add_restraint(1, [True, False, False, False, False, False], [0.02, 0, 0, 0, 0, 0])
    boundary.add_restraint(2, [True, False, False, False, False, False], [2000, 0, 0, 0, 0, 0])
    u = np.zeros(12)
    u[0], u[6] = 0.004, 0.003
    R = np.zeros(12)
    R[6] = 12
    modified, rhs = Solver().apply_boundary_conditions(
        eye(12, format="csr") * 1000, R, boundary, current_displacement=u, load_factor=0.5
    )
    du = spsolve(modified, rhs)
    assert du[0] == pytest.approx(0.01 - 0.004)
    assert du[6] == pytest.approx((12 - 2000 * 0.003) / 3000)
