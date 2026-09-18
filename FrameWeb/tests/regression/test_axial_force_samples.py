"""Saved Nd-dependent beams: independent whole-path response and equilibrium."""

import json

import pytest

from fem.model import FemModel
from tests.support.assertions import assert_dict_almost_equal
from tests.support.oracles.axial_force_samples import axial_force_sample_history, saved_results
from tests.support.paths import DATA
from tests.support.section_cut_view import section_cut_result_view

pytestmark = pytest.mark.regression

SAMPLES = ('axial_force_shear_load_control', 'axial_force_shear_displacement_control')


@pytest.mark.parametrize('name', SAMPLES)
def test_saved_Nd_sample_matches_independent_reference_at_every_step(name):
    path = DATA / 'snap' / (name + '.json')
    original = path.read_bytes()
    data = json.loads(original)
    expected = axial_force_sample_history(data)
    controlled = 'displacement_control' in data['load']['1']
    assert data['result'] == saved_results(expected, displacement_control=controlled)

    model = FemModel()
    model.load_model(str(path))
    result = model._run_solver_snapshot()
    assert result['converged']
    assert len(result['step_results']) == len(expected)
    assert result['metadata']['analysis']['beam_formulation'] == 'jr_axial_force_updated_inertia_v1'
    for index, actual in enumerate(result['step_results'], 1):
        reference = expected[str(index)]
        view = section_cut_result_view(actual, model, data)
        for field in ('disg', 'reac', 'fsec', 'curvature'):
            assert_dict_almost_equal(view[field], reference[field], f'{name}/{index}/{field}')
        assert actual['lambda'] == pytest.approx(reference['lambda'], rel=1e-8, abs=1e-10)
        section = actual['section_response'][7]['center']['z']
        for field, value in reference['section'].items():
            assert section[field] == pytest.approx(value, rel=1e-8, abs=1e-10), (name, index, field)
        assert section['interpolation']['fraction'] == pytest.approx(reference['section']['Nd']/2)
        # The endpoints and support close all force/moment resultants.
        end = actual['element_stresses'][7]
        assert end['i_end'][0] + end['j_end'][0] == pytest.approx(0., abs=1e-10)
        assert end['i_end'][1] + end['j_end'][1] == pytest.approx(0., abs=1e-10)
        assert end['i_end'][5] + end['j_end'][5] + end['j_end'][1] == pytest.approx(0., abs=1e-10)
    if controlled:
        sections = [step['section_response'][7]['center']['z'] for step in result['step_results']]
        assert sections[-1]['effective_inertia'] < 0
        assert max(step['lambda'] for step in result['step_results']) > result['step_results'][-1]['lambda']
    else:
        assert result['node_displacements'][30]['dx'] == pytest.approx(-.0008, abs=1e-10)
    assert path.read_bytes() == original


@pytest.mark.parametrize('name', SAMPLES)
def test_saved_Nd_sample_normalized_roundtrip_repeats_the_history(name, tmp_path):
    model = FemModel()
    model.load_model(str(DATA / 'snap' / (name + '.json')))
    first = model._run_solver_snapshot()
    saved = tmp_path / (name + '.json')
    model.save_model(str(saved))
    restored = FemModel()
    restored.load_model(str(saved))
    second = restored._run_solver_snapshot()
    for left, right in zip(first['step_results'], second['step_results'], strict=True):
        for field in ('lambda', 'node_displacements', 'reaction_forces', 'section_response'):
            assert_dict_almost_equal(left[field], right[field], f'{name}/roundtrip/{field}')


def test_Nd_condensation_pole_stops_public_solver_and_rolls_back_all_state():
    from fem.diagnostics import NumericalConditionError
    from fem.file_io import _read_json_model

    data = json.loads((DATA / 'snap' / (SAMPLES[1]+'.json')).read_text(encoding='utf8'))
    data['member']['7']['shear_correction'] = True
    data['element']['1']['1']['G'] = 337.0370367*6/5
    targets = [.002, .006, .009, .013]
    data['load']['1']['displacement_control']['targets'] = targets
    model = FemModel()
    model.read_json_model(_read_json_model(data))
    with pytest.raises(NumericalConditionError) as failure:
        model._run_solver_snapshot()
    assert failure.value.details['reason'] == 'singular_shear_condensation'
    assert failure.value.details['element_id'] == 7
    assert failure.value.details['axis'] == 'z'
    assert failure.value.details['step'] == 4
    assert failure.value.details['Nd'] == pytest.approx(.123456789, abs=1e-12)
    beam = model.elements[7]
    assert beam.committed_bending_states['moment_z'].curvature == pytest.approx(.009)
    assert beam.current_states == beam.committed_states
    assert beam.current_bending_states == beam.committed_bending_states
    assert beam._trial_section_response == beam._committed_section_response
    assert model.results is None
    model.analysis_params['displacement_control']['targets'] = targets[:-1]
    assert model._run_solver_snapshot()['converged']
