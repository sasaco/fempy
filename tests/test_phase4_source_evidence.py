"""Provenance is tested independently from acceptance of the legacy samples."""
import copy
import json
from pathlib import Path

import pytest

from phase4_source_evidence import ROOT, input_findings, read_v0, source_evidence
from src.fem.legacy_beam import select_case


@pytest.mark.parametrize('name', ['sampleBendHexa1', 'sampleBendWedge1', 'sampleBendHexa2', 'sampleBendWedge2', 'sampleBendTetra2'])
def test_complete_external_solid_input_and_repaired_displacement(name):
    path = ROOT/'tests/data/bend'/f'{name}.json'
    original = path.read_bytes()
    data = select_case(json.loads(original))
    out = source_evidence(path, data)[1]
    assert set(out['input_comparison']) == {'nodes', 'topology', 'static_material', 'nodal_loads', 'restraints'}
    assert all(v['mismatches'] == 0 for v in out['input_comparison'].values())
    assert out['reference_coverage']['missing_source_nodes'] == []
    scales = out['embedded_displacement_divided_by']
    assert scales['1']['mismatches'] == 0
    assert scales['1000']['mismatches'] > 1000
    # Reintroducing the old transfer defect must still be diagnosed.
    for values in data['result']['1']['disg'].values():
        for key in values: values[key] *= 1000
    changed = source_evidence(path, data)[1]['embedded_displacement_divided_by']
    assert changed['1']['mismatches'] > 1000
    assert changed['1000']['mismatches'] == 0
    assert path.read_bytes() == original


@pytest.mark.parametrize('name', ['sampleBendHexa2', 'sampleBendTetra2', 'sampleBendWedge2', 'sampleBendTri1'])
def test_missing_json_connectivity_is_proved_against_original_fem(name):
    path = ROOT/'tests/data/bend'/f'{name}.json'
    data = select_case(json.loads(path.read_text(encoding='utf-8')))
    field = 'shell' if name == 'sampleBendTri1' else 'solid'
    assert data[field]  # Restored topology is now present in the fixture.
    data.pop(field)  # Recreate the historical migration defect.
    assert any(v['code'] == 'missing_element_topology' for v in input_findings(data))
    source = read_v0(ROOT/'docs/v0/testdata/bend'/f'{name}.fem')
    assert source['elements']
    assert len(source['nodes']) == len(data['node'])


def test_tetra_printed_reference_is_incomplete_and_not_same_model_proof():
    path = ROOT/'tests/data/bend/sampleBendTetra1.json'
    out = source_evidence(path, select_case(json.loads(path.read_text(encoding='utf-8'))))[1]
    assert out['counts']['displacements'] == 10
    assert len(out['reference_coverage']['missing_source_nodes']) == 610
    assert out['input_comparison'] == {}


def test_source_audit_detects_input_change_instead_of_trusting_same_file_name():
    path = ROOT/'tests/data/bend/sampleBendHexa1.json'
    data = select_case(json.loads(path.read_text(encoding='utf-8')))
    data['element']['1']['1']['E'] *= 2
    out = source_evidence(path, data)[1]
    assert out['input_comparison']['static_material']['mismatches'] == 1


def test_stale_notice_record_does_not_stop_the_numerical_audit():
    data = json.loads((ROOT/'tests/data/bar/2D_Sample06.json').read_text(encoding='utf-8'))
    findings = input_findings(select_case(data, '1'))
    assert any(v['code'] == 'unknown_notice_member' and v['member'] == '39' for v in findings)


def test_selected_material_case_and_rounded_geometry_are_reported():
    data = json.loads((ROOT/'tests/data/bar/2D_Sample13.json').read_text(encoding='utf-8'))
    findings = input_findings(select_case(data, '1'))
    zero = next(v for v in findings if v['code'] == 'zero_member_properties')
    assert any(v['material'] == '9' and 'Iz' in v['fields'] for v in zero['members'])
    # The display/default material case has a nonzero inertia; audit the selected case.
    assert data['element']['1']['9']['Iz'] > 0
    data = json.loads((ROOT/'tests/data/bar/3D_Sample10.json').read_text(encoding='utf-8'))
    findings = input_findings(select_case(data, '6'))
    rounding = next(v for v in findings if v['code'] == 'legacy_millimetre_rounding')
    assert rounding['count'] == 4
    assert rounding['max_position_shift'] > 1e-4


@pytest.mark.parametrize('analysis', ['static', 'modal', 'material_nonlinear'])
def test_missing_topology_has_an_input_error_before_linear_algebra(analysis):
    from src.fem.model import FemModel
    m = FemModel()
    m.mesh.add_node(1, [0., 0., 0.])
    m.results = {'stale': True}
    with pytest.raises(ValueError, match='structural elements.*topology'):
        m.run(analysis)
    assert m.results is None


@pytest.mark.parametrize('name', ['sampleBendHexa1', 'sampleBendWedge1'])
def test_v0_solid_full_displacements_and_force_moment_equilibrium(name):
    import numpy as np
    from src.fem.file_io import _read_json_model, result_to_jsonable
    from src.fem.model import FemModel
    from run_sample import assert_dict_almost_equal
    path = ROOT/'tests/data/bend'/f'{name}.json'
    original = path.read_bytes()
    data = select_case(json.loads(original))
    source = read_v0(ROOT/'docs/v0/testdata/bend'/f'{name}.out')
    model = FemModel()
    model.read_json_model(_read_json_model(copy.deepcopy(data)))
    result = result_to_jsonable(model.run())
    assert_dict_almost_equal(result['node_displacements'], source['displacements'])
    force, moment = np.zeros(3), np.zeros(3)
    for n, load in source['loads'].items():
        force += load[:3]
        moment += np.cross(source['nodes'][n], load[:3])+load[3:]
    for n, reaction in result['reaction_forces'].items():
        r = np.array([reaction[k] for k in ('fx', 'fy', 'fz')])
        force += r
        moment += np.cross(source['nodes'][n], r)
    np.testing.assert_allclose(force, 0., atol=1e-8)
    np.testing.assert_allclose(moment, 0., atol=1e-7)
    assert path.read_bytes() == original


def test_releasing_pressure_fixture_rotations_creates_a_loaded_rigid_mode():
    import numpy as np
    from src.fem.file_io import _read_json_model
    from src.fem.model import FemModel
    data = json.loads((ROOT/'tests/data/shell/shellPressureTest1.json').read_text(encoding='utf-8'))
    data['boundary_conditions']['restraints']['1']['dof'][3:] = [False]*3
    m = FemModel()
    m.read_json_model(_read_json_model(data))
    element = m.elements[1]
    coordinates = element.get_element_coordinates()
    rigid = np.zeros((4, 6))
    rigid[:, :3] = np.cross([0., 1., 0.], coordinates)
    rigid[:, 4] = 1.
    # A rotation about node 1 satisfies its three translation restraints.
    np.testing.assert_array_equal(rigid[0, :3], 0.)
    scaled_stiffness = element.get_stiffness_matrix()/(205e9*.01)
    np.testing.assert_allclose(scaled_stiffness@rigid.ravel(), 0., atol=1e-12)
    pressure = element.get_equivalent_nodal_loads('pressure', [1000.], 'F1')
    assert pressure@rigid.ravel() == pytest.approx(500., abs=1e-10)
    with pytest.raises(ValueError, match='Singular'):
        m.run()
