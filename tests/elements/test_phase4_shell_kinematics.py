"""Physical shell invariants; no reference values come from a FEM solver."""
import numpy as np
import pytest

from src.fem.elements.shell_element import ShellElement
from src.fem.material import Material, MaterialProperty


def shell(n, rotation=np.eye(3)):
    coords = np.array([[0., 0., 0.], [2., 0., 0.], [2., 3., 0.], [0., 3., 0.]])[:n]
    coords = coords @ rotation.T
    e = ShellElement(1, list(range(n)), 1, .2)
    m = Material()
    m.add_material(1, MaterialProperty('patch', 1000., .25, density=2.))
    e.set_material_properties(m)
    e.set_node_coordinates(dict(enumerate(coords)))
    return e, coords


@pytest.mark.parametrize('n', [3, 4])
@pytest.mark.parametrize('axis', range(3))
@pytest.mark.parametrize('rotation', [False, True])
def test_shell_six_rigid_body_modes_have_no_internal_force(n, axis, rotation):
    e, coords = shell(n)
    u = np.zeros((n, 6))
    direction = np.eye(3)[axis]
    if rotation:
        u[:, :3] = np.cross(direction, coords)
        u[:, 3:] = direction
    else:
        u[:, :3] = direction
    np.testing.assert_allclose(e.get_stiffness_matrix() @ u.ravel(), 0., atol=2e-12)


@pytest.mark.parametrize('n', [3, 4])
def test_shell_stiffness_and_mass_rotate_with_the_plane(n):
    # Orthogonal rotation has no aligned global shell normal.
    r = np.array([[2., -2., 1.], [1., 2., 2.], [-2., -1., 2.]]) / 3
    e, _ = shell(n)
    rotated, _ = shell(n, r)
    t = np.kron(np.eye(2*n), r)
    np.testing.assert_allclose(rotated.get_stiffness_matrix(), t @ e.get_stiffness_matrix() @ t.T,
                               rtol=1e-12, atol=2e-12)
    np.testing.assert_allclose(rotated.get_mass_matrix(), t @ e.get_mass_matrix() @ t.T,
                               rtol=1e-12, atol=2e-12)


@pytest.mark.parametrize('n', [3, 4])
def test_shell_affine_membrane_energy(n):
    e, coords = shell(n)
    strain = np.array([.001, .002, .003])
    u = np.zeros((n, 6))
    u[:, 0] = strain[0]*coords[:, 0]+strain[2]*coords[:, 1]
    u[:, 1] = strain[1]*coords[:, 1]
    stress = 1000/(1-.25**2)*np.array([.001+.25*.002, .002+.25*.001, .375*.003])
    area = 3. if n == 3 else 6.
    expected = area*.2*np.dot(strain, stress)/2
    assert u.ravel() @ e.get_stiffness_matrix() @ u.ravel()/2 == pytest.approx(expected, rel=1e-12)


@pytest.mark.parametrize('n', [3, 4])
def test_degenerate_shell_is_rejected_without_fallback(n):
    e, coords = shell(n)
    coords[:, 1:] = 0.
    e.set_node_coordinates(dict(enumerate(coords)))
    with pytest.raises(ValueError, match='[Dd]egenerate'):
        e.get_stiffness_matrix()


@pytest.mark.parametrize('n', [3, 4])
def test_shell_has_only_rigid_and_constant_drilling_zero_modes(n):
    e, _ = shell(n)
    eigenvalues = np.linalg.eigvalsh(e.get_stiffness_matrix())
    assert np.min(eigenvalues) > -1e-10
    # Six physical rigid motions plus the unobservable constant drill angle.
    assert np.count_nonzero(eigenvalues < 1e-10) == 7


@pytest.mark.parametrize('axis', [0, 1])
@pytest.mark.parametrize('thickness', [.02, .2, 2.])
def test_quad_constant_bending_energy_has_no_shear_locking(axis, thickness):
    e, coords = shell(4)
    e.thickness = thickness
    curvature = .003
    u = np.zeros((4, 6))
    u[:, 2] = -.5*curvature*coords[:, axis]**2
    u[:, 4 if axis == 0 else 3] = curvature*coords[:, axis]*(1 if axis == 0 else -1)
    # Unit-width plate bending stiffness E*t^3/[12(1-nu^2)].
    energy = 6.*1000*thickness**3/(12*(1-.25**2))*curvature**2/2
    assert u.ravel()@e.get_stiffness_matrix()@u.ravel()/2 == pytest.approx(energy, rel=1e-10)


@pytest.mark.parametrize('vertical', [False, True])
@pytest.mark.parametrize('segments', [1, 4])
@pytest.mark.parametrize('thickness', [.02, .2])
def test_quad_cantilever_constant_moment_full_solver(segments, thickness, vertical):
    from src.fem.model import FemModel
    m = FemModel()
    m.material.add_material(1, MaterialProperty('plate', 1000., 0.))
    rotation = np.array([[0., 0., 1.], [1., 0., 0.], [0., 1., 0.]]) if vertical else np.eye(3)
    for i in range(segments+1):
        for side in (0, 1):
            m.mesh.add_node(2*i+side+1, rotation@np.array([2*i/segments, side, 0.]))
    for i in range(segments):
        m.mesh.add_element(i+1, 'shell', [2*i+1, 2*i+3, 2*i+4, 2*i+2], 1, thickness=thickness)
    for n in (1, 2):
        m.boundary.add_restraint(n, [True]*6)
    moment = 1e-5
    for n in (2*segments+1, 2*segments+2):
        m.boundary.add_load(n, np.r_[np.zeros(3), rotation@np.array([0., moment/2, 0.])])
    result = m.run()
    curvature = moment/(1000*thickness**3/12)
    for n, actual in result['node_displacements'].items():
        x = 2*((n-1)//2)/segments
        expected = np.r_[rotation@np.array([0., 0., -.5*curvature*x*x]),
                         rotation@np.array([0., curvature*x, 0.])]
        np.testing.assert_allclose(list(actual.values()), expected, rtol=1e-8, atol=1e-11)


@pytest.mark.parametrize('n', [3, 4])
@pytest.mark.parametrize('face,sign', [('F1', -1), ('F2', 1)])
@pytest.mark.parametrize('vertical', [False, True])
def test_surface_pressure_force_and_moment_follow_v0_faces(n, face, sign, vertical):
    rotation = np.array([[0., 0., 1.], [1., 0., 0.], [0., 1., 0.]]) if vertical else np.eye(3)
    e, coords = shell(n, rotation)
    loads = e.get_equivalent_nodal_loads('pressure', [7.], face).reshape(n, 6)
    area = 3. if n == 3 else 6.
    resultant = rotation@np.array([0., 0., sign*7*area])
    np.testing.assert_allclose(loads[:, :3].sum(axis=0), resultant, atol=1e-12)
    np.testing.assert_allclose(np.cross(coords, loads[:, :3]).sum(axis=0),
                               np.cross(coords.mean(axis=0), resultant), atol=1e-12)
    np.testing.assert_allclose(loads[:, 3:], 0., atol=1e-12)
    np.testing.assert_allclose(loads[:, :3], np.tile(resultant/n, (n, 1)), atol=1e-12)


@pytest.mark.parametrize('face,values', [('F3', [1.]), ('bad', [1.]), ('F1', [float('nan')]), ('F1', [1., 2.])])
def test_invalid_surface_pressure_never_falls_back(face, values):
    e, _ = shell(4)
    with pytest.raises(ValueError):
        e.get_equivalent_nodal_loads('pressure', values, face)
