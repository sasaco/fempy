"""Polynomial patch, work, rigid modes and public quadratic-solid paths."""
import itertools
import numpy as np
import pytest
from src.fem.model import FemModel


# Node order independently transcribed from docs/v0/src/SolidElement.js.
TET = [[0,0,0],[1,0,0],[0,1,0],[0,0,1],[.5,0,0],[.5,.5,0],
       [0,.5,0],[0,0,.5],[.5,0,.5],[0,.5,.5]]
WEDGE = [[0,0,-1],[1,0,-1],[0,1,-1],[0,0,1],[1,0,1],[0,1,1],
         [.5,0,-1],[.5,.5,-1],[0,.5,-1],[.5,0,1],[.5,.5,1],[0,.5,1],
         [0,0,0],[1,0,0],[0,1,0]]
HEX = [[-1,-1,-1],[1,-1,-1],[1,1,-1],[-1,1,-1],[-1,-1,1],[1,-1,1],[1,1,1],[-1,1,1],
       [0,-1,-1],[1,0,-1],[0,1,-1],[-1,0,-1],[0,-1,1],[1,0,1],[0,1,1],[-1,0,1],
       [-1,-1,0],[1,-1,0],[1,1,0],[-1,1,0]]
CASES = [('tetra2', TET, 1/6), ('wedge2', WEDGE, 1.), ('hexa2', HEX, 8.)]


def make_model(kind, coords):
    m = FemModel()
    for i, xyz in enumerate(coords): m.add_node(10+i*3, *xyz)
    m.add_material(1, 'test', 1000, .25, density=2)
    m.add_element(8, kind, list(m.mesh.nodes), 1)
    m._create_elements()
    return m, m.elements[8]


@pytest.mark.parametrize('kind,reference,volume', CASES)
def test_quadratic_partition_polynomials_derivatives(kind, reference, volume):
    _, e = make_model(kind, reference)
    np.testing.assert_allclose([e.get_shape_functions(p) for p in reference], np.eye(len(reference)), atol=1e-14)
    point = np.array([.17,.21,.13])
    shapes = e.get_shape_functions(point)
    derivatives = e.get_shape_derivatives(point)
    for powers in itertools.product(range(3), repeat=3):
        if sum(powers) > 2: continue
        values = np.prod(np.asarray(reference)**powers, axis=1)
        assert shapes@values == pytest.approx(np.prod(point**powers), abs=1e-14)
    assert shapes.sum() == pytest.approx(1.)
    np.testing.assert_allclose(derivatives.sum(axis=1), 0, atol=1e-14)
    for axis in range(3):
        dx = np.eye(3)[axis]*1e-6
        fd = (e.get_shape_functions(point+dx)-e.get_shape_functions(point-dx))/(2e-6)
        np.testing.assert_allclose(derivatives[axis], fd, atol=2e-10)


@pytest.mark.parametrize('kind,reference,volume', CASES)
def test_quadratic_affine_energy_mass_and_six_rigid_modes(kind, reference, volume):
    transform = np.array([[2.,.3,.1],[0,3.,.2],[0,0,4.]])
    coords = np.asarray(reference)@transform.T+[.1,.2,.3]
    m, e = make_model(kind, coords)
    gradient = np.array([[.001,.003,0],[0,.002,.004],[-.002,0,-.001]])
    u = (coords@gradient.T).ravel()
    strain = np.array([.001,.002,-.001,.003,.004,-.002])
    stress = np.r_[800*strain[:3]+400*sum(strain[:3]),400*strain[3:]]
    r = e.calculate_stress_strain(u)
    np.testing.assert_allclose(r['strain'], np.tile(strain,(len(r['strain']),1)), atol=1e-14)
    np.testing.assert_allclose(r['stress'], np.tile(stress,(len(r['stress']),1)), atol=1e-11)
    k = e.get_stiffness_matrix()
    assert .5*u@k@u == pytest.approx(.5*strain@stress*volume*24, rel=1e-12)
    assert np.count_nonzero(np.linalg.eigvalsh(k) < np.linalg.norm(k)*1e-10) == 6
    for axis in np.eye(3):
        np.testing.assert_allclose(k@np.tile(axis,len(coords)), 0, atol=2e-10)
        np.testing.assert_allclose(k@np.cross(axis,coords).ravel(), 0, atol=2e-10)
        rigid = np.tile(axis,len(coords))
        assert rigid@e.get_mass_matrix()@rigid == pytest.approx(2*volume*24, rel=1e-12)
    assert m.solver._get_max_dof_per_node(m.mesh) == 3


@pytest.mark.parametrize('kind,reference,volume', CASES)
def test_quadratic_public_prescribed_affine_solution(kind, reference, volume):
    m, e = make_model(kind, reference)
    for nid, xyz in m.mesh.nodes.items():
        m.add_restraint(nid, True, True, True)
        m.add_forced_displacement(nid, dx=.001*xyz[0], dy=.002*xyz[1], dz=-.001*xyz[2])
    result = m.run()
    np.testing.assert_allclose(result['element_stresses'][8]['stress'],
                               np.tile([1.6,2.4,0.,0.,0.,0.], (len(e.get_gauss_points()[0]),1)), atol=1e-11)


@pytest.mark.parametrize('kind,reference,volume', CASES)
def test_quadratic_inverted_geometry_is_rejected(kind, reference, volume):
    coords = np.asarray(reference, dtype=float); coords[:,0] *= -1
    _, e = make_model(kind, coords)
    with pytest.raises(ValueError, match='Jacobian'): e.get_stiffness_matrix()
    with pytest.raises(ValueError, match='Jacobian'): e.calculate_stress_strain(np.zeros(len(coords)*3))
