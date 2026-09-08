"""Affine displacement patch tests, independent of the solver/stiffness."""
import numpy as np
import pytest
from src.fem.material import Material, MaterialProperty, ShellParameter
from src.fem.elements.shell_element import ShellElement
from src.fem.elements.solid_element import TetraElement, WedgeElement
from src.fem.elements.advanced_element import WedgeElement as AdvancedWedge


@pytest.mark.parametrize('cls', [TetraElement, WedgeElement, AdvancedWedge])
def test_solid_affine_stress_patch(cls):
    coords = np.array([[0,0,0],[2,0,0],[0,3,0],[0,0,4]], dtype=float)
    if cls != TetraElement:
        coords = np.array([[0,0,-1],[2,0,-1],[0,3,-1],[0,0,1],[2,0,1],[0,3,1]], dtype=float)
    coords = coords@np.array([[1,.2,.1],[0,1,.3],[0,0,1]])
    m = Material()
    m.add_material(1, MaterialProperty('patch', 1000, .25))
    e = cls(1, list(range(len(coords))), 1)
    e.set_material_properties(m)
    e.set_node_coordinates(dict(enumerate(coords)))
    strain = np.array([.001,.002,-.001,.003,.004,-.002])
    gradient = np.array([[.001,.003,0],[0,.002,.004],[-.002,0,-.001]])
    u = (coords@gradient.T+[.1,.2,.3]).ravel()
    r = e.calculate_stress_strain(u)
    # lambda=mu=400; sigma_normal=2mu*epsilon+lambda*trace.
    stress = np.r_[800*strain[:3]+400*sum(strain[:3]),400*strain[3:]]
    np.testing.assert_allclose(r['strain'], np.tile(strain,(len(r['strain']),1)), atol=1e-14)
    np.testing.assert_allclose(r['stress'], np.tile(stress,(len(r['stress']),1)), atol=1e-10)


@pytest.mark.parametrize('n', [3,4])
@pytest.mark.parametrize('vertical', [False, True])
def test_shell_rotated_membrane_patch(n, vertical):
    coords = np.array([[0,0,0],[2,0,0],[2,3,0],[0,3,0]],dtype=float)[:n]
    rotation = np.array([[0,0,1],[1,0,0],[0,1,0]]) if vertical else np.eye(3)
    u = np.zeros((n,6))
    u[:,0],u[:,1] = .001*coords[:,0]+.003*coords[:,1], .002*coords[:,1]
    u[:,:3] = u[:,:3]@rotation
    m = Material();m.add_material(1, MaterialProperty('patch',1000,.25))
    e = ShellElement(1,list(range(n)),1,.1)
    e.set_material_properties(m,ShellParameter(.1))
    e.set_node_coordinates(dict(enumerate(coords@rotation)))
    r = e.calculate_stress_strain(u.ravel())
    expected = [.001,.002,.003]
    np.testing.assert_allclose(r['strain'],np.tile(expected,(len(r['strain']),1)),atol=1e-14)
