"""Test Newton equilibrium independently of the JR constitutive implementation."""
import numpy as np
import pytest

from src.fem.boundary_condition import BoundaryCondition
from src.fem.material import Material, MaterialProperty, BarParameter
from src.fem.mesh import MeshModel
from src.fem.model import FemModel
from src.fem.elements.bar_element import TBarElement
from src.fem.nonlinear.nonlinear_solver import NonlinearSolver


def axial_bar(prescribed=None, spring=None):
    mesh = MeshModel()
    mesh.add_node(1, [0, 0, 0])
    mesh.add_node(2, [2, 0, 0])
    mesh.add_element(1, 'bar', [1, 2], 1)
    material = Material()
    material.add_material(1, MaterialProperty('test', 2000, 0.25))
    element = TBarElement(1, [1, 2], 1, 1)
    element.set_node_coordinates(mesh.nodes)
    element.set_material_properties(material, BarParameter(3, 1, 1, 1))
    boundary = BoundaryCondition()
    boundary.add_restraint(1, [True] * 6)
    boundary.add_restraint(2, [False, True, True, True, True, True])
    if prescribed is not None:
        boundary.add_restraint(2, [True] * 6, [prescribed, 0, 0, 0, 0, 0])
    elif spring is not None:
        boundary.add_restraint(2, [True] * 6, [spring, 0, 0, 0, 0, 0])
    else:
        boundary.add_load(2, [30, 0, 0, 0, 0, 0])
    return mesh, material, boundary, {1: element}


def test_linear_bar_through_newton_matches_hand_calculation():
    # EA/L=3000, u=P/k=0.01, fixed reaction=-30.
    result = NonlinearSolver().solve_nonlinear(*axial_bar(), n_steps=3)
    assert result['node_displacements'][2]['dx'] == pytest.approx(0.01)
    assert result['reaction_forces'][1]['fx'] == pytest.approx(-30)
    np.testing.assert_allclose(result['displacement'][:6], 0, atol=1e-14)
    assert all(step['converged'] for step in result['step_results'])


def test_prescribed_motion_without_external_force_is_applied_once_per_step():
    result = NonlinearSolver().solve_nonlinear(*axial_bar(prescribed=0.02), n_steps=4)
    np.testing.assert_allclose([s['displacement'][6] for s in result['step_results']],
                               [0.005, 0.01, 0.015, 0.02], atol=1e-14)
    assert result['reaction_forces'][1]['fx'] == pytest.approx(-60)
    assert result['reaction_forces'][2]['fx'] == pytest.approx(60)


def test_spring_force_participates_in_residual_and_reactions():
    args = axial_bar(spring=2000)
    args[2].add_load(2, [50, 0, 0, 0, 0, 0])
    result = NonlinearSolver().solve_nonlinear(*args, n_steps=2)
    assert result['node_displacements'][2]['dx'] == pytest.approx(50 / (3000 + 2000))
    assert result['reaction_forces'][1]['fx'] == pytest.approx(-30)
    assert result['reaction_forces'][2]['fx'] == pytest.approx(-20)


class CubicBar:
    """Conservative spring: N = delta + delta**3 (not a JR-model mock)."""
    def __init__(self):
        self.committed = 0.0
        self.trial = 0.0
        self.rollbacks = 0

    def get_dof_per_node(self):
        return 6

    def get_internal_force(self, u):
        self.trial = u[6] - u[0]
        f = np.zeros(12)
        f[6] = self.trial + self.trial**3
        f[0] = -f[6]
        return f

    def get_tangent_stiffness_matrix(self, u):
        b = np.zeros(12)
        b[0], b[6] = -1, 1
        return (1 + 3 * (u[6] - u[0])**2) * np.outer(b, b)

    def commit_state(self):
        self.committed = self.trial

    def rollback_state(self):
        self.trial = self.committed
        self.rollbacks += 1


def test_unconverged_step_rolls_back_and_stops_before_callback():
    args = axial_bar()
    element = CubicBar()
    args[3][1] = element
    solver = NonlinearSolver()
    callbacks = []
    with pytest.raises(RuntimeError, match='converg'):
        solver.solve_nonlinear(*args, n_steps=3, max_iter=1, callback=callbacks.append)
    assert callbacks == []
    assert element.trial == element.committed == 0
    assert element.rollbacks >= 1
    np.testing.assert_array_equal(solver.displacement, np.zeros(12))


def test_unrestrained_model_is_rejected_not_regularized():
    args = axial_bar()
    args[2].restraints.clear()
    with pytest.raises(RuntimeError, match='converg'):
        NonlinearSolver().solve_nonlinear(*args, n_steps=1, max_iter=3)


def test_fixed_node_load_does_not_hide_free_dof_imbalance():
    args = axial_bar()
    args[2].add_load(1, [1e12, 0, 0, 0, 0, 0])
    result = NonlinearSolver().solve_nonlinear(*args, n_steps=1)
    assert result['node_displacements'][2]['dx'] == pytest.approx(0.01)
    assert result['reaction_forces'][1]['fx'] == pytest.approx(-1e12 - 30)


def test_cubic_spring_converges_to_known_nonlinear_root():
    args = axial_bar()
    args[2].loads.clear()
    args[2].add_load(2, [2, 0, 0, 0, 0, 0])
    element = CubicBar()
    args[3][1] = element
    result = NonlinearSolver().solve_nonlinear(*args, n_steps=2)
    # N=u+u**3=2 has the unique real root u=1.
    assert result['node_displacements'][2]['dx'] == pytest.approx(1, abs=1e-7)
    assert element.committed == pytest.approx(1, abs=1e-7)
    assert result['reaction_forces'][1]['fx'] == pytest.approx(-2)


class CappedBar(CubicBar):
    """Elastic/perfectly-plastic spring with a unit force capacity."""
    def get_internal_force(self, u):
        self.trial = u[6] - u[0]
        force = np.zeros(12)
        force[6] = np.clip(self.trial, -1, 1)
        force[0] = -force[6]
        return force

    def get_tangent_stiffness_matrix(self, u):
        b = np.zeros(12)
        b[0], b[6] = -1, 1
        return np.outer(b, b) if abs(u[6] - u[0]) <= 1 else np.zeros((12, 12))


def test_failed_second_step_restores_first_converged_state():
    args = axial_bar()
    args[2].loads.clear()
    args[2].add_load(2, [3, 0, 0, 0, 0, 0])
    element = CappedBar()
    args[3][1] = element
    solver = NonlinearSolver()
    callbacks = []
    with pytest.raises(RuntimeError, match='converg') as error:
        solver.solve_nonlinear(*args, n_steps=3, callback=callbacks.append)
    assert error.value.step == 2
    assert len(callbacks) == 1
    np.testing.assert_allclose(solver.displacement, callbacks[0]['displacement'])
    np.testing.assert_allclose(error.value.displacement, callbacks[0]['displacement'])
    assert element.trial == element.committed == pytest.approx(1)
    assert {record['step'] for record in solver.convergence_history} == {1, 2}


def test_failed_fem_run_clears_previous_results():
    model = FemModel()
    model.results = {'converged': True}
    mesh, material, boundary, _ = axial_bar()
    model.mesh, model.material, model.boundary = mesh, material, boundary
    material.add_bar_parameter(1, BarParameter(3, 1, 1, 1))
    model.analysis_params['max_iterations'] = 1
    with pytest.raises(RuntimeError, match='converg'):
        model.run('material_nonlinear')
    assert model.get_results() is None


@pytest.mark.parametrize('kwargs', [
    {'n_steps': 0}, {'n_steps': -1}, {'n_steps': 1.5}, {'max_iter': 0},
    {'tol': 0}, {'tol': float('nan')}, {'tol': float('inf')},
])
def test_invalid_controls_rejected_before_analysis(kwargs):
    with pytest.raises(ValueError):
        NonlinearSolver().solve_nonlinear(*axial_bar(), **kwargs)


def test_programmatic_fem_model_has_analysis_defaults():
    model = FemModel()
    mesh, material, boundary, _ = axial_bar()
    model.mesh, model.material, model.boundary = mesh, material, boundary
    model.material.add_bar_parameter(1, BarParameter(3, 1, 1, 1))
    result = model.run('material_nonlinear')
    assert result['node_displacements'][2]['dx'] == pytest.approx(0.01)


@pytest.mark.parametrize('analysis_type', ['static', 'material_nonlinear'])
def test_nonconsecutive_node_ids_preserve_reactions_and_displacements(analysis_type):
    mesh, material, _, _ = axial_bar()
    mesh.nodes = {307: mesh.nodes[2], 10: mesh.nodes[1]}  # Also deliberately unsorted.
    mesh.elements[1]['nodes'] = [10, 307]
    boundary = BoundaryCondition()
    boundary.add_restraint(10, [True] * 6)
    boundary.add_restraint(307, [False, True, True, True, True, True])
    boundary.add_load(307, [30, 0, 0, 0, 0, 0])
    model = FemModel()
    model.mesh, model.material, model.boundary = mesh, material, boundary
    material.add_bar_parameter(1, BarParameter(3, 1, 1, 1))
    result = model.run(analysis_type)
    assert result['node_displacements'][307]['dx'] == pytest.approx(0.01)
    assert result['node_displacements'][10]['dx'] == pytest.approx(0, abs=1e-14)
    assert result['reaction_forces'][10]['fx'] == pytest.approx(-30)
