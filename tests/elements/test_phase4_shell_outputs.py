"""Independent surface tensor, resultant and energy acceptance tests."""
import numpy as np
import pytest
from src.fem.elements.shell_element import ShellElement
from src.fem.material import Material, MaterialProperty
from src.fem.model import FemModel
from src.fem.file_io import result_to_jsonable


def shell(n=4, rotation=None):
    xy = np.array([[0, 0, 0], [2, 0, 0], [2, 3, 0], [0, 3, 0.]])[:n]
    rotation = np.eye(3) if rotation is None else rotation
    e = ShellElement(17, list(range(10, 10+n)), 1, .2)
    m = Material(); m.add_material(1, MaterialProperty('test', 1200., .2))
    e.set_material_properties(m)
    e.set_node_coordinates(dict(zip(e.node_ids, xy@rotation.T)))
    return e, xy


@pytest.mark.parametrize('n', [3, 4])
@pytest.mark.parametrize('rotated', [False, True])
@pytest.mark.parametrize('mode', ['membrane', 'bending', 'shear', 'rigid'])
def test_surface_tensors(n, rotated, mode):
    rotation = np.array([[0., 0, 1], [1, 0, 0], [0, 1, 0]]) if rotated else np.eye(3)
    e, xy = shell(n, rotation)
    u = np.zeros((n, 6)); eps = np.zeros((3, 3)); sig = np.zeros((3, 3))
    if mode == 'membrane':
        u[:, 0] = .002*xy[:, 0]+.006*xy[:, 1]; u[:, 1] = -.001*xy[:, 1]
        eps = np.array([[.002, .003, 0], [.003, -.001, 0], [0, 0, 0]])
        sig = np.array([[2.25, 3., 0], [3., -.75, 0], [0, 0, 0]])
    elif mode == 'bending':
        u[:, 4] = .02*xy[:, 0]; u[:, 2] = -.01*xy[:, 0]**2
        eps[0, 0] = .002; sig[0, 0] = 2.5; sig[1, 1] = .5
    elif mode == 'shear':
        u[:, 2] = .003*xy[:, 0]+.004*xy[:, 1]
        eps[0, 2] = eps[2, 0] = .0015; eps[1, 2] = eps[2, 1] = .002
        sig[0, 2] = sig[2, 0] = 1.25; sig[1, 2] = sig[2, 1] = 5/3
    else:
        omega = np.array([.02, -.01, .03])
        u[:, :3] = np.cross(omega, xy)+[1., 2., 3.]; u[:, 3:] = omega
    u[:, :3] = u[:, :3]@rotation.T; u[:, 3:] = u[:, 3:]@rotation.T
    r = e.calculate_shell_results(u.ravel())['raw_result']
    for side, sign in [(1, 1), (2, -1)]:
        # Tri Mindlin interpolation cannot reproduce zero bending shear at
        # every vertex. Verify its in-plane tensor separately in that case.
        a, b = eps.copy(), sig.copy()
        if mode == 'bending': a *= sign; b *= sign
        a, b = rotation@a@rotation.T, rotation@b@rotation.T
        av = a[[0, 1, 2, 0, 1, 2], [0, 1, 2, 1, 2, 0]]
        bv = b[[0, 1, 2, 0, 1, 2], [0, 1, 2, 1, 2, 0]]
        indices = [1, 2, 4] if rotated else [0, 1, 3]
        if mode != 'bending' or n == 4: indices = list(range(6))
        for prefix in ['node', 'elem']:
            actual_a = np.atleast_2d(r[f'{prefix}Strain{side}'])
            actual_b = np.atleast_2d(r[f'{prefix}Stress{side}'])
            np.testing.assert_allclose(actual_a[:, indices], np.tile(av[indices], (len(actual_a), 1)), atol=2e-14)
            np.testing.assert_allclose(actual_b[:, indices], np.tile(bv[indices], (len(actual_b), 1)), atol=2e-11)


def test_shell_integrated_energy_and_resultants():
    e, xy = shell(); u = np.zeros((4, 6))
    u[:, 4] = .02*xy[:, 0]; u[:, 2] = -.01*xy[:, 0]**2
    r = e.calculate_shell_results(u.ravel())
    # Dxx=1250, area=6, kappa=.02, t=.2.
    expected = .5*1250*.2**3/12*.02**2*6
    assert r['strain_energy'] == pytest.approx(expected, abs=1e-12)
    assert r['strain_energy'] == pytest.approx(.5*u.ravel()@e.get_stiffness_matrix()@u.ravel(), abs=1e-12)
    np.testing.assert_allclose(r['resultants']['moment'], [1/60, 1/300, 0], atol=1e-13)
    assert r['raw_result']['elemStrain1'][0] == pytest.approx(.002)
    assert r['raw_result']['elemStrain2'][0] == pytest.approx(-.002)


@pytest.mark.parametrize('analysis', ['static', 'material_nonlinear'])
def test_public_shell_output_with_noncontiguous_ids(analysis):
    m = FemModel()
    for nid, xyz in {10: [0,0,0], 20: [2,0,0], 30: [2,3,0], 40: [0,3,0]}.items():
        m.add_node(nid, *xyz); m.add_restraint(nid, True, True, True, True, True, True)
        m.add_forced_displacement(nid, dx=.002*xyz[0])
    m.add_material(1, 'test', 1200, .2)
    m.add_element(17, 'shell', [10,20,30,40], 1, thickness=.2)
    m.analysis_params['load_factors'] = [.5, 1.]
    r = result_to_jsonable(m.run(analysis))
    assert set(r['shell_results']) == {'17'}
    assert r['shell_results']['17']['raw_result']['elemStress1'][0] == pytest.approx(2.5)
    assert np.isfinite(r['shell_results']['17']['strain_energy'])
    if analysis == 'material_nonlinear':
        assert r['step_results'][0]['shell_results']['17']['raw_result']['elemStress1'][0] == pytest.approx(1.25)
        assert r['step_results'][1]['shell_results'] == r['shell_results']


def test_drilling_energy_is_separate_from_physical_energy():
    e, _ = shell(); u = np.zeros((4,6)); u[:,5] = [0,.01,.02,.03]
    r = e.calculate_shell_results(u.ravel())
    assert r['strain_energy'] == 0
    assert r['drilling_energy'] > 0
    assert r['drilling_energy'] == pytest.approx(.5*u.ravel()@e.get_stiffness_matrix()@u.ravel())


@pytest.mark.parametrize('u', [np.zeros(23), np.full(24, np.nan), np.full(24, np.inf)])
def test_shell_output_rejects_incomplete_or_nonfinite_displacement(u):
    e, _ = shell()
    with pytest.raises(ValueError, match='finite displacement'): e.calculate_shell_results(u)


@pytest.mark.parametrize('n', [3,4])
@pytest.mark.parametrize('rotated', [False,True])
def test_edge_tractions_from_uniform_membrane_stress(n, rotated):
    rotation = np.array([[0.,0,1],[1,0,0],[0,1,0]]) if rotated else np.eye(3)
    e, xyz = shell(n, rotation); u = np.zeros((n,6)); u[:,0] = .002*xyz[:,0]
    u[:,:3] = u[:,:3]@rotation.T
    result = e.calculate_shell_results(u.ravel())
    # sigma_x=2.5, sigma_y=.5; thickness=.2.
    tensor = rotation@np.diag([2.5,.5,0.])@rotation.T
    forces, moments = np.zeros(3), np.zeros(3)
    for edge in result['edge_resultants'].values():
        i,j = [e.node_ids.index(nid) for nid in edge['node_ids']]
        length = np.linalg.norm(xyz[j]-xyz[i])
        expected = tensor@edge['outward_normal']*.2*length/2
        for node, end in zip([i,j], ['i_end','j_end']):
            np.testing.assert_allclose(edge[end][:3], expected, atol=1e-13)
            np.testing.assert_allclose(edge[end][3:], 0, atol=1e-13)
            forces += edge[end][:3]
            moments += np.cross(rotation@xyz[node],edge[end][:3])
    np.testing.assert_allclose(forces,0,atol=1e-13)
    np.testing.assert_allclose(moments,0,atol=1e-13)


def test_edge_bending_couple_has_physical_rotation_sign():
    e, xy = shell(); u=np.zeros((4,6))
    u[:,4]=.02*xy[:,0]; u[:,2]=-.01*xy[:,0]**2
    edges=e.calculate_shell_results(u.ravel())['edge_resultants']
    # Right edge outward normal +x: couple +y, Mxx=1/60, length=3.
    for end in ['i_end','j_end']:
        np.testing.assert_allclose(edges['11-12'][end], [0,0,0,0,.025,0],atol=1e-13)
