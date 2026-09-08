"""Legacy schema conversions must preserve physical units and both surfaces."""
import copy
import numpy as np
import pytest

from src.fem.elements.shell_postprocess import legacy_shell_view
from test_phase4_dkt import triangle


def test_legacy_engineering_shear_energy_density_and_separate_surfaces():
    e, coords = triangle(.2)
    u = np.zeros((3, 6))
    u[:, 0] = .003*coords[:, 1]
    u[:, 4] = .002*coords[:, 0]
    u[:, 2] = -.001*coords[:, 0]**2
    modern = e.calculate_shell_results(u.ravel())
    saved = copy.deepcopy(modern)
    legacy = legacy_shell_view(e, u.ravel())
    assert set(legacy) == {'stress', 'strain', 'strain_energy', 'raw_result'}
    raw = legacy['raw_result']
    assert raw['nodeStrain1'][0][3] == pytest.approx(.003)
    assert raw['nodeStrain2'][0][3] == pytest.approx(.003)
    assert raw['elemStrain1'][0] == pytest.approx(.0002)
    assert raw['elemStrain2'][0] == pytest.approx(-.0002)
    assert legacy['strain_energy'] == pytest.approx(raw['elemEnergy1'])
    assert modern == saved


def test_legacy_view_uses_point_mean_not_area_weighted_mean():
    # Use direct construction here because this test exercises a distorted quad.
    from src.fem.elements.shell_element import ShellElement
    from src.fem.material import Material, MaterialProperty
    e = ShellElement(99, [10,20,30,40], 1, .2)
    e.set_node_coordinates(dict(zip(e.node_ids, [[0.,0.,0.],[3.,0.,0.],[2.,2.,0.],[0.,1.,0.]])))
    m = Material();m.add_material(1, MaterialProperty('plate',1000.,.25));e.set_material_properties(m)
    u = np.arange(24)*.001
    legacy = legacy_shell_view(e, u)
    modern = e.calculate_shell_results(u)
    assert legacy['strain_energy'] != pytest.approx(modern['raw_result']['elemEnergy1'], rel=1e-6)


def test_public_legacy_ids_are_insertion_indices_and_modern_ids_are_retained():
    from src.fem.model import FemModel
    from src.fem.material import MaterialProperty
    m = FemModel()
    for key, xyz in zip([10,20,30,40], [[0,0,0],[1,0,0],[1,1,0],[0,1,0]]):
        m.mesh.add_node(key, xyz)
        m.boundary.add_restraint(key, [True]*6)
    m.material.add_material(1, MaterialProperty('plate',1000.,.25))
    m.mesh.add_element(42,'shell',[10,20,30],1,thickness=.1,formulation='dkt')
    m.mesh.add_element(17,'shell',[10,30,40],1,thickness=.1,formulation='dkt')
    for mode in ['static','material_nonlinear']:
        result = m.run(mode)
        assert set(result['shell_results']) == {42,17}
        assert set(result['legacy_shell_results']) == {0,1}
        for step in result.get('step_results',[]):
            assert set(step['legacy_shell_results']) == {0,1}
