"""Kirchhoff triangle: quadratic displacement and thin-plate work oracles."""
import numpy as np
import pytest

from src.fem.elements.shell_element import ShellElement
from src.fem.material import Material, MaterialProperty


def triangle(thickness, rotation=np.eye(3)):
    xy = np.array([[0., 0., 0.], [3., 0., 0.], [.7, 2., 0.]])
    e = ShellElement(17, [5, 8, 20], 1, thickness, formulation='dkt')
    e.set_node_coordinates(dict(zip(e.node_ids, xy@rotation.T)))
    m = Material(); m.add_material(1, MaterialProperty('plate', 1000., .25))
    e.set_material_properties(m)
    return e, xy


@pytest.mark.parametrize('t', [.002, .02, .2])
@pytest.mark.parametrize('curvature', [[.003, 0., 0.], [0., .002, 0.], [0., 0., .004], [.003, -.002, .004]])
def test_dkt_quadratic_bending_has_exact_thickness_cubed_energy(t, curvature):
    e, xyz = triangle(t)
    x, y = xyz[:, 0], xyz[:, 1]
    kx, ky, kxy = curvature
    u = np.zeros((3, 6))
    u[:, 2] = -.5*(kx*x*x+ky*y*y+kxy*x*y)
    u[:, 3] = -ky*y-kxy*x/2
    u[:, 4] = kx*x+kxy*y/2
    elastic = 1000/(1-.25**2)*np.array([[1., .25, 0.], [.25, 1., 0.], [0., 0., (1-.25)/2]])
    expected = 3*t**3/24*np.array(curvature)@elastic@curvature
    assert u.ravel()@e.get_stiffness_matrix()@u.ravel()/2 == pytest.approx(expected, rel=1e-10)
    result = e.calculate_shell_results(u.ravel())
    assert result['strain_energy'] == pytest.approx(expected, rel=1e-10)
    np.testing.assert_allclose(result['resultants']['moment'], t**3/12*elastic@curvature, rtol=1e-10, atol=1e-15)


def test_dkt_covariance_and_exactly_six_null_modes():
    rotation = np.array([[1., 2., 2.], [2., 1., -2.], [-2., 2., -1.]])/3
    e, _ = triangle(.2)
    rotated, _ = triangle(.2, rotation)
    transform = np.kron(np.eye(6), rotation)
    np.testing.assert_allclose(rotated.get_stiffness_matrix(), transform@e.get_stiffness_matrix()@transform.T, atol=1e-11)
    eigenvalues = np.linalg.eigvalsh(e.get_stiffness_matrix())
    assert np.min(eigenvalues) > -1e-10
    assert np.count_nonzero(eigenvalues < 1e-10) == 6


def test_dkt_requires_a_triangle():
    with pytest.raises(ValueError, match='dkt'):
        ShellElement(1, [1,2,3,4], 1, .1, formulation='dkt')


def test_dkt_transverse_resultants_follow_moment_equilibrium():
    e, coords = triangle(.2)
    result = e.calculate_shell_results(np.arange(18)*.001)
    force, moment = np.zeros(3), np.zeros(3)
    nodes = dict(zip(e.node_ids, coords))
    for edge in result['edge_resultants'].values():
        for node, end in zip(edge['node_ids'], ('i_end', 'j_end')):
            values = np.asarray(edge[end])
            force += values[:3]
            moment += values[3:]+np.cross(nodes[node], values[:3])
    np.testing.assert_allclose(force, 0., atol=1e-12)
    np.testing.assert_allclose(moment, 0., atol=1e-12)
    assert np.linalg.norm(result['resultants']['shear']) > 1e-6


@pytest.mark.parametrize('segments', [1, 4])
@pytest.mark.parametrize('thickness', [.002, .2])
def test_dkt_cantilever_constant_moment(segments, thickness):
    from src.fem.model import FemModel
    m = FemModel()
    m.material.add_material(1, MaterialProperty('plate', 1000., 0.))
    for i in range(segments+1):
        for side in (0, 1):
            m.mesh.add_node(2*i+side+1, [2*i/segments, side, 0.])
    for i in range(segments):
        for j, nodes in enumerate(([2*i+1,2*i+3,2*i+4], [2*i+1,2*i+4,2*i+2])):
            m.mesh.add_element(2*i+j+1, 'shell', nodes, 1, thickness=thickness, formulation='dkt')
    for n in (1, 2):
        m.boundary.add_restraint(n, [True]*6)
    curvature = .003
    moment = curvature*1000*thickness**3/12
    for n in (2*segments+1, 2*segments+2):
        m.boundary.add_load(n, [0., 0., 0., 0., moment/2, 0.])
    result = m.run()
    for n, actual in result['node_displacements'].items():
        x = 2*((n-1)//2)/segments
        assert actual['dz'] == pytest.approx(-.5*curvature*x*x, rel=1e-8, abs=1e-11)
        assert actual['ry'] == pytest.approx(curvature*x, rel=1e-8, abs=1e-11)
