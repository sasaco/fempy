"""Legacy names, modern files and the Python API share immutable spatial input."""
from copy import deepcopy
import json

import pytest

from fem.file_io import _read_json_model, model_to_jsonable
from fem.model import FemModel
from tests.support.builders.spatial_loads import legacy_panel

pytestmark = pytest.mark.integration


@pytest.mark.parametrize("area", [False, True])
def test_legacy_beam_panel_keeps_unexpanded_loads_and_source_unchanged(area):
    source = legacy_panel(area=area)
    original = deepcopy(source)
    data = _read_json_model(source)
    definitions = data['boundary'].spatial_loads
    assert definitions.panels[0].triangles == ((1, 2, 3), (1, 3, 4))
    assert definitions.loads[0].path_ids == ((1, 2) if area else (1,))
    assert definitions.paths[0].points == ((0., .5, 0.), (2., .5, 0.))
    assert data['boundary'].loads[2].forces.tolist() == [0, 0, -3, 0, 0, 0]
    assert len(data['mesh'].elements) == 4
    assert source == original


def test_legacy_shell_namespace_collision_and_canonical_roundtrip():
    source = legacy_panel(area=True)
    source['shell'] = {'1': dict(nodes=[1, 2, 3, 4], e=1)}
    source['inf_panel']['7'] = dict(nodes=[1, 2, 3, 4], elements=['1'])
    data = _read_json_model(source)
    panel = data['boundary'].spatial_loads.panels[0]
    assert panel.elements == (5,)
    assert data['mesh'].elements[1]['type'] == 'bar'
    assert data['mesh'].elements[5]['shell_id'] == 1
    restored = _read_json_model(model_to_jsonable(data))
    assert restored['boundary'].spatial_loads == data['boundary'].spatial_loads


def test_omitted_topology_infers_existing_shells_but_never_a_beam_convex_hull():
    source = legacy_panel()
    del source['inf_panel']['7']['triangles']
    with pytest.raises(ValueError, match="panel 7.*explicit topology"):
        _read_json_model(source)
    source['shell'] = {'1': dict(nodes=[1, 2, 3, 4], e=1)}
    assert _read_json_model(source)['boundary'].spatial_loads.panels[0].elements == (5,)


def test_only_selected_case_references_are_read():
    source = legacy_panel()
    source['load']['2'] = dict(inf_panel=999, load_inf=[dict(L1=999)])
    source['line']['unused'] = dict(position=[])
    definitions = _read_json_model(source)['boundary'].spatial_loads
    assert len(definitions.loads) == 1
    assert len(definitions.paths) == 1
    source['load']['1'].pop('load_inf')
    assert not _read_json_model(source)['boundary'].spatial_loads.loads


@pytest.mark.parametrize("mutation,match", [
    (lambda d: d['load']['1']['load_inf'][0].update(L1=999), "path 999"),
    (lambda d: d['load']['1'].update(inf_panel=999), "panel 999"),
    (lambda d: d['inf_panel']['7'].update(nodes=[1, 2, 3, 99]), "panel 7"),
    (lambda d: d['inf_panel']['7'].update(elements=[999], triangles=[]), "shell 999"),
    (lambda d: d['line']['1']['position'][1].update(x=float('inf')), "path 1"),
    (lambda d: d['load']['1']['load_inf'][0].update(P11=float('nan')), "load 1"),
])
def test_invalid_legacy_references_and_values_fail_on_read(mutation, match):
    source = legacy_panel()
    mutation(source)
    with pytest.raises(ValueError, match=match):
        _read_json_model(source)


def test_python_api_and_json_file_preserve_definitions_and_clear_on_reload(tmp_path):
    data = _read_json_model(legacy_panel(area=True))
    model = FemModel()
    model.read_json_model(data)
    definitions = model.boundary.spatial_loads
    model.set_spatial_loads(definitions)
    path = tmp_path / 'spatial.json'
    model.save_model(str(path))
    raw = json.loads(path.read_text(encoding='utf8'))
    assert raw['spatial_loads']['loads'][0]['end_intensities'] == [[10, 20], [30, 40]]
    restored = FemModel()
    restored.load_model(str(path))
    assert restored.boundary.spatial_loads == definitions
    source = legacy_panel()
    source['load']['1'].pop('load_inf')
    restored.read_json_model(_read_json_model(source))
    assert not restored.boundary.spatial_loads.loads


@pytest.mark.parametrize('analysis', ['material_nonlinear', 'modal'])
def test_nonstatic_spatial_loads_are_never_silently_ignored(monkeypatch, analysis):
    model = FemModel()
    model.read_json_model(_read_json_model(legacy_panel()))

    def unexpected(*args, **kwargs):
        pytest.fail('Solver must not run before spatial loading is supported')

    monkeypatch.setattr(model.solver, 'solve', unexpected)
    monkeypatch.setattr(model.solver, 'eigenvalue_analysis', unexpected)
    with pytest.raises(ValueError, match='spatial.*7') as caught:
        model.run(analysis)
    assert caught.value.error_code == 'unsupported_analysis'
    assert model.results is None


def test_fw3_rejection_happens_before_existing_file_is_opened(tmp_path):
    model = FemModel()
    model.read_json_model(_read_json_model(legacy_panel()))
    path = tmp_path / 'model.fw3'
    path.write_text('existing model', encoding='utf8')
    with pytest.raises(ValueError, match='spatial.*JSON'):
        model.save_model(str(path))
    assert path.read_text(encoding='utf8') == 'existing model'


def test_unknown_modern_keys_and_ambiguous_dual_input_are_rejected():
    data = _read_json_model(legacy_panel())
    wire = model_to_jsonable(data)
    wire['spatial_loads']['loads'][0]['unexpected_direction'] = [0, 0, -1]
    with pytest.raises(ValueError, match='unknown.*unexpected_direction'):
        _read_json_model(wire)
    source = legacy_panel()
    source['spatial_loads'] = model_to_jsonable(data)['spatial_loads']
    with pytest.raises(ValueError, match='both'):
        _read_json_model(source)


def test_input_without_spatial_definitions_retains_its_saved_schema():
    source = legacy_panel()
    source['load']['1'].pop('load_inf')
    assert 'spatial_loads' not in model_to_jsonable(_read_json_model(source))


@pytest.mark.parametrize('area', [False, True])
@pytest.mark.parametrize('analysis', ['modal', 'material_nonlinear'])
def test_http_reports_unsupported_load_with_panel_and_load_ids(area, analysis):
    from main import app

    data = legacy_panel(area=area)
    data['analysis_type'] = analysis
    response = app.test_client().post('/', json=data)
    assert response.status_code == 400
    body = response.get_json()
    assert body['error_code'] == 'unsupported_analysis'
    assert body['details']['panel_ids'] == [7]
    assert body['details']['load_ids'] == [1]
    assert body['converged'] is False
    assert 'node_displacements' not in body


def test_python_setter_checks_references_before_replacing_current_definitions():
    from fem.spatial_loads import SpatialLoadDefinitions, SpatialLoadPanel

    model = FemModel()
    model.read_json_model(_read_json_model(legacy_panel()))
    previous = model.boundary.spatial_loads
    invalid = SpatialLoadDefinitions([
        SpatialLoadPanel(8, [1, 2, 999], triangles=[[1, 2, 999]])])
    with pytest.raises(ValueError, match='panel 8.*node 999'):
        model.set_spatial_loads(invalid)
    assert model.boundary.spatial_loads is previous


@pytest.mark.parametrize('kind', ['panel', 'path'])
def test_legacy_id_aliases_cannot_silently_select_one_definition(kind):
    source = legacy_panel()
    if kind == 'panel':
        source['inf_panel']['07'] = deepcopy(source['inf_panel']['7'])
    else:
        source['line']['01'] = deepcopy(source['line']['1'])
    with pytest.raises(ValueError, match=f'duplicate normalized ID for {kind}'):
        _read_json_model(source)


def test_modern_shell_reference_never_accepts_a_beam_id():
    raw = model_to_jsonable(_read_json_model(legacy_panel()))
    raw['spatial_loads']['panels'][0].update(elements=[1], triangles=[])
    with pytest.raises(ValueError, match='panel 7.*shell element 1'):
        _read_json_model(raw)


def test_custom_tolerance_and_fractional_loads_survive_save_without_rounding():
    source = legacy_panel()
    source['inf_panel']['7']['tolerance'] = dict(absolute_length=1e-12, relative_length=1e-7)
    source['load']['1']['load_inf'][0]['P11'] = 1.234567890123e-13
    data = _read_json_model(source)
    restored = _read_json_model(model_to_jsonable(data))
    assert restored['boundary'].spatial_loads == data['boundary'].spatial_loads
    assert restored['boundary'].spatial_loads.loads[0].end_intensities[0][0] == 1.234567890123e-13


def test_boundary_clear_removes_spatial_definitions():
    boundary = _read_json_model(legacy_panel())['boundary']
    previous = boundary.spatial_loads
    boundary.clear()
    assert not boundary.spatial_loads.has_definitions
    assert not boundary.loads
    assert previous.loads  # Clearing the owner must not mutate shared definitions.


def test_python_constructed_definitions_match_legacy_adapter():
    from fem.spatial_loads import (
        SpatialLoad, SpatialLoadDefinitions, SpatialLoadPanel, SpatialLoadPath,
    )

    expected = _read_json_model(legacy_panel(area=True))['boundary'].spatial_loads
    source = legacy_panel()
    source['load']['1'].pop('load_inf')
    model = FemModel()
    model.read_json_model(_read_json_model(source))
    definitions = SpatialLoadDefinitions(
        panels=[SpatialLoadPanel(7, [1, 2, 3, 4], triangles=[[1, 2, 3], [1, 3, 4]])],
        paths=[SpatialLoadPath(1, [(0, .5, 0), (2, .5, 0)]),
               SpatialLoadPath(2, [(0, 1.5, 0), (2, 1.5, 0)])],
        loads=[SpatialLoad(1, 7, [1, 2], [[10, 20], [30, 40]])],
    )
    model.set_spatial_loads(definitions)
    assert model.boundary.spatial_loads == expected
