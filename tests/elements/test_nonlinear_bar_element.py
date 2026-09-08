"""Phase 2: independent mechanics oracles, never generated sample results."""
import copy

import numpy as np
import pytest

from src.fem.elements.bar_element import BEBarElement, TBarElement
from src.fem.elements.nonlinear_bar_element import NonlinearBarElement
from src.fem.material import Material, MaterialProperty, BarParameter
from src.fem.model import FemModel
from src.fem.nonlinear.hysteresis import JRStiffnessReductionParams


RIGIDITIES = {'axial': 6000., 'torsion': 560., 'moment_y': 800., 'moment_z': 1800.}


def skeleton(k):
    # Slopes k, k/4, k/10. Breakpoints deliberately separated from FD probes.
    return JRStiffnessReductionParams.symmetric(
        0.001, 0.005, 0.02, k * 0.001, k * 0.002, k * 0.0035, beta=0.4)


def beam(cls=NonlinearBarElement, dofs=(), length=2., shear=True, rotated=False):
    kwargs = {} if cls is BEBarElement else {'shear_correction': shear}
    e = cls(1, [10, 307], 1, 1, angle=23 if rotated else 0, **kwargs)
    origin = np.array([1., -2., 3.])
    axis = np.array([2., -3., 6.]) / 7 if rotated else np.array([1., 0., 0.])
    e.set_node_coordinates({10: origin, 307: origin + length * axis})
    mat = Material()
    mat.add_material(1, MaterialProperty('test', 2000., 0.25))
    e.set_material_properties(mat, BarParameter(3., 0.4, 0.9, 0.7, 0.6, 0.8))
    for dof in dofs:
        e.set_hysteresis_model(dof, skeleton(RIGIDITIES[dof]))
    return e


def force(e, u):
    if isinstance(e, NonlinearBarElement):
        return e.get_internal_force(u)
    return e.get_stiffness_matrix() @ u


def elastic_matrix(length, shear):
    """Closed-form Timoshenko blocks in right-handed local coordinates."""
    k = np.zeros((12, 12))
    for indices, rigidity in [([0, 6], 6000), ([3, 9], 560)]:
        k[np.ix_(indices, indices)] = rigidity / length * np.array([[1, -1], [-1, 1]])
    for indices, ei, ga, sign in [([1, 5, 7, 11], 1800, 1440, 1),
                                  ([2, 4, 8, 10], 800, 1920, -1)]:
        l = length
        phi = 12 * ei / (ga * l**2) if shear else 0
        block = ei / (l**3 * (1 + phi)) * np.array([
            [12, sign*6*l, -12, sign*6*l],
            [sign*6*l, (4+phi)*l*l, -sign*6*l, (2-phi)*l*l],
            [-12, -sign*6*l, 12, -sign*6*l],
            [sign*6*l, (2-phi)*l*l, -sign*6*l, (4+phi)*l*l]])
        k[np.ix_(indices, indices)] = block
    return k


@pytest.mark.parametrize('cls', [BEBarElement, TBarElement, NonlinearBarElement])
@pytest.mark.parametrize('rotation', [False, True])
@pytest.mark.parametrize('mode', ['translation', 'rotation'])
def test_rigid_body_motion_has_zero_force(cls, rotation, mode):
    e = beam(cls, dofs=RIGIDITIES if cls is NonlinearBarElement else (), rotated=rotation)
    u = np.zeros(12)
    if mode == 'translation':
        u[:3] = u[6:9] = [0.02, -0.03, 0.04]
    else:
        omega = np.array([0.002, -0.003, 0.004])
        u[3:6] = u[9:12] = omega
        x = e.get_element_coordinates()
        u[6:9] = np.cross(omega, x[1] - x[0])
    np.testing.assert_allclose(force(e, u), 0, atol=2e-12)


@pytest.mark.parametrize('rotation', [False, True])
def test_endpoint_forces_and_moments_balance(rotation):
    e = beam(dofs=RIGIDITIES, rotated=rotation)
    u = np.array([1, -2, 3, -4, 5, -6, 7, 8, -9, 10, -11, 12]) * 0.001
    f = force(e, u)
    x = e.get_element_coordinates()
    np.testing.assert_allclose(f[:3] + f[6:9], 0, atol=1e-12)
    np.testing.assert_allclose(f[3:6] + f[9:12] + np.cross(x[1]-x[0], f[6:9]),
                               0, atol=2e-12)


@pytest.mark.parametrize('shear', [False, True])
@pytest.mark.parametrize('cls', [BEBarElement, TBarElement, NonlinearBarElement])
def test_elastic_limit_matches_closed_form_matrix(cls, shear):
    e = beam(cls, dofs=RIGIDITIES if cls is NonlinearBarElement else (), shear=shear)
    expected = elastic_matrix(2., shear and cls is not BEBarElement)
    u = np.linspace(-0.0001, 0.0002, 12)
    np.testing.assert_allclose(force(e, u), expected @ u, atol=1e-12)
    k = e.get_tangent_stiffness_matrix(u) if cls is NonlinearBarElement else e.get_stiffness_matrix()
    np.testing.assert_allclose(k, expected, atol=2e-12)


@pytest.mark.parametrize('rotation', [False, True])
@pytest.mark.parametrize('scale', [0.00002, 0.0012])
def test_tangent_is_finite_difference_of_force(rotation, scale):
    e = beam(dofs=RIGIDITIES, rotated=rotation)
    t = e.get_transformation_matrix()
    u = t.T @ (np.array([1, -2, 3, -4, 5, -6, 7, 8, -9, 10, -11, 12]) * scale)
    k = e.get_tangent_stiffness_matrix(u)
    h = 1e-7
    fd = np.column_stack([(force(e, u + h*d) - force(e, u - h*d))/(2*h)
                          for d in np.eye(12)])
    np.testing.assert_allclose(k, fd, rtol=2e-8, atol=2e-7)
    np.testing.assert_allclose(k, k.T, atol=1e-12)


@pytest.mark.parametrize('dof,index', [('axial', 0), ('torsion', 3),
                                     ('moment_y', 4), ('moment_z', 5)])
@pytest.mark.parametrize('length', [0.5, 2., 5.])
def test_section_skeleton_uses_strain_or_curvature_not_endpoint_motion(dof, index, length):
    e = beam(dofs=[dof], length=length)
    u = np.zeros(12)
    q = 0.003
    u[index], u[index+6] = -q * length / 2, q * length / 2
    # Opposite end rotations: zero average rotation and zero shear distortion.
    expected = np.zeros(12)
    p = RIGIDITIES[dof] * (0.001 + 0.25 * (q - 0.001))
    expected[index], expected[index+6] = -p, p
    np.testing.assert_allclose(force(e, u), expected, atol=1e-12)


@pytest.mark.parametrize('active,other', [('moment_y', 5), ('moment_z', 4)])
def test_named_bending_law_leaves_other_plane_elastic(active, other):
    e = beam(dofs=[active])
    u = np.zeros(12)
    u[other], u[other+6] = -0.003, 0.003
    np.testing.assert_allclose(force(e, u), elastic_matrix(2, True) @ u, atol=1e-12)


def test_rotated_response_matches_independent_basis_rotation():
    plain, turned = beam(dofs=RIGIDITIES), beam(dofs=RIGIDITIES, rotated=True)
    # Construct frame from geometry, independently of element's transformation getter.
    ex = np.array([2., -3., 6.]) / 7
    ey = np.cross([0., 0., 1.], ex)
    ey /= np.linalg.norm(ey)
    ez = np.cross(ex, ey)
    a = np.deg2rad(23)
    r = np.array([ex, np.cos(a)*ey + np.sin(a)*ez, -np.sin(a)*ey + np.cos(a)*ez])
    t = np.kron(np.eye(4), r)
    u = np.linspace(-0.006, 0.012, 12)
    np.testing.assert_allclose(force(turned, t.T @ u), t.T @ force(plain, u), atol=2e-12)
    np.testing.assert_allclose(turned.get_tangent_stiffness_matrix(t.T @ u),
                               t.T @ plain.get_tangent_stiffness_matrix(u) @ t, atol=2e-12)


def axial_motion(q):
    u = np.zeros(12)
    u[6] = 2 * q
    return u


def test_trial_order_cannot_change_result_or_committed_history():
    e = beam(dofs=['axial'])
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


def test_tangent_and_output_do_not_replace_trial_to_be_committed():
    e = beam(dofs=['axial'])
    force(e, axial_motion(0.003))
    trial = copy.deepcopy(e.current_states)
    e.get_tangent_stiffness_matrix(axial_motion(0.01))
    e.calculate_forces(axial_motion(-0.01))
    assert e.current_states == trial
    e.commit_state()
    assert e.committed_states == trial
    e.reset_states()
    assert all(s.current_delta == 0 for states in e.committed_states.values() for s in states.values())


def test_output_preserves_converged_force_after_commit_and_other_trials():
    e = beam(dofs=['axial'])
    for q in [0.001, 0.005, 0.004]:
        u = axial_motion(q)
        converged_force = force(e, u).copy()
        e.commit_state()
    force(e, axial_motion(-0.012))
    before = copy.deepcopy((e.current_states, e.committed_states))
    for _ in range(3):
        output = e.calculate_forces(u)
        np.testing.assert_allclose(np.r_[output['i_end'], output['j_end']], converged_force, atol=1e-12)
    assert (e.current_states, e.committed_states) == before
    e.rollback_state()
    output = e.calculate_forces(u)
    np.testing.assert_allclose(np.r_[output['i_end'], output['j_end']], converged_force, atol=1e-12)


@pytest.mark.parametrize('shear', [False, True])
@pytest.mark.parametrize('count', [1, 4])
@pytest.mark.parametrize('axis,rotation,ei,ga,sign', [(1, 5, 1800, 1440, 1), (2, 4, 800, 1920, -1)])
def test_cantilever_tip_load_matches_hand_solution_and_mesh_refinement(shear, count, axis, rotation, ei, ga, sign):
    length, load = 2., 0.01
    k = np.zeros((6*(count+1), 6*(count+1)))
    for n in range(count):
        e = beam(dofs=RIGIDITIES, length=length/count, shear=shear)
        k[6*n:6*n+12, 6*n:6*n+12] += e.get_tangent_stiffness_matrix(np.zeros(12))
    f = np.zeros(6*(count+1))
    f[-6+axis] = load
    u = np.zeros_like(f)
    u[6:] = np.linalg.solve(k[6:, 6:], f[6:])
    expected = load * length**3 / (3*ei) + (load*length/ga if shear else 0)
    assert u[-6+axis] == pytest.approx(expected, rel=1e-11)
    assert u[-6+rotation] == pytest.approx(sign*load*length**2/(2*ei), rel=1e-11)


@pytest.mark.parametrize('analysis', ['static', 'material_nonlinear'])
def test_fem_model_publishes_section_forces_with_nonconsecutive_nodes(analysis):
    model = FemModel()
    model.mesh.add_node(10, [0, 0, 0])
    model.mesh.add_node(307, [2, 0, 0])
    model.material.add_bar_parameter(1, BarParameter(3, 0.4, 0.9, 0.7))
    model.boundary.add_restraint(10, [True]*6)
    model.boundary.add_restraint(307, [False, True, True, True, True, True])
    model.boundary.add_load(307, [9, 0, 0, 0, 0, 0])
    if analysis == 'material_nonlinear':
        model.add_nonlinear_material(1, 'test', 2000, .001, .005, .02, 6, 12, 21)
        model.add_nonlinear_bar_element(1, [10, 307], 1, 1, ['axial'])
    else:
        model.material.add_material(1, MaterialProperty('test', 2000, .25))
        model.mesh.add_element(1, 'bar', [10, 307], 1, section_id=1)
    model.analysis_params.update(n_load_steps=3, tolerance=1e-10)
    result = model.run(analysis)
    expected_u = .006 if analysis == 'material_nonlinear' else .003
    assert result['node_displacements'][307]['dx'] == pytest.approx(expected_u, abs=1e-12)
    output = result['element_stresses'][1]
    np.testing.assert_allclose(output['i_end'], [-9, 0, 0, 0, 0, 0], atol=1e-10)
    np.testing.assert_allclose(output['j_end'], [9, 0, 0, 0, 0, 0], atol=1e-10)
    assert result['reaction_forces'][10]['fx'] == pytest.approx(-9)


def test_bernoulli_euler_force_output_uses_right_handed_moments():
    e = beam(BEBarElement)
    u = np.linspace(-0.003, 0.004, 12)
    result = e.calculate_forces(u)
    np.testing.assert_allclose(np.r_[result['i_end'], result['j_end']],
                               elastic_matrix(2, False) @ u, atol=1e-12)


@pytest.mark.parametrize('axis,rotation,ei,sign', [('moment_y', 4, 800, -1), ('moment_z', 5, 1800, 1)])
@pytest.mark.parametrize('count', [1, 4])
def test_nonlinear_pure_bending_solution_and_output_under_refinement(axis, rotation, ei, sign, count):
    model = FemModel()
    model.material.add_bar_parameter(1, BarParameter(3, 0.4, 0.9, 0.7, 0.6, 0.8))
    model.add_nonlinear_material(1, 'test', 2000, .001, .005, .02,
                                 ei*.001, ei*.002, ei*.0035, nu=.25)
    for n in range(count+1):
        model.mesh.add_node(10 + n*100, [2*n/count, 0, 0])
    for n in range(count):
        model.add_nonlinear_bar_element(n+1, [10+n*100, 110+n*100], 1, 1, [axis])
    model.boundary.add_restraint(10, [True]*6)
    load = np.zeros(6)
    # M/EI=.0015 -> kappa=.001 + (.0015-.001)/.25 = .003.
    load[rotation] = ei*.0015
    model.boundary.add_load(10+count*100, load)
    model.analysis_params.update(n_load_steps=3, tolerance=1e-10)
    result = model.run('material_nonlinear')
    displacement = result['displacement']
    transverse = 2 if rotation == 4 else 1
    assert displacement[-6+rotation] == pytest.approx(.006, abs=1e-12)
    assert displacement[-6+transverse] == pytest.approx(sign*.006, abs=1e-12)
    for elem_id, output in result['element_stresses'].items():
        expected = np.zeros(6)
        expected[rotation] = ei*.0015
        np.testing.assert_allclose(output['i_end'], -expected, atol=2e-10)
        np.testing.assert_allclose(output['j_end'], expected, atol=2e-10)
        assert model.elements[elem_id].committed_states[axis]['center'].current_delta == pytest.approx(.003)
    before = copy.deepcopy([e.committed_states for e in model.elements.values()])
    model._post_process_results()
    assert [e.committed_states for e in model.elements.values()] == before


@pytest.mark.parametrize('dof,index', [('axial', 0), ('moment_y', 4), ('moment_z', 5)])
@pytest.mark.parametrize('q', [.004, .0005])
def test_tangent_with_fixed_committed_history_on_smooth_branches(dof, index, q):
    e = beam(dofs=[dof], rotated=True)
    t = e.get_transformation_matrix()
    def motion(value):
        u = np.zeros(12)
        u[index], u[index+6] = -value, value
        return t.T @ u
    for value in [.001, .005]:
        force(e, motion(value))
        e.commit_state()
    committed = copy.deepcopy(e.committed_states)
    u = motion(q)
    h = 1e-7
    fd = np.column_stack([(force(e, u+h*d)-force(e, u-h*d))/(2*h) for d in np.eye(12)])
    np.testing.assert_allclose(e.get_tangent_stiffness_matrix(u), fd, rtol=2e-8, atol=2e-7)
    assert e.committed_states == committed
