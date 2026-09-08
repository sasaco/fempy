"""G omission disables shear deformation for the assigned legacy material."""
import copy
import json

import pytest

from test_phase4_legacy import cantilever, run
from src.fem.file_io import _read_json_model
from src.fem.model import FemModel


@pytest.mark.parametrize('nonlinear', [False, True])
@pytest.mark.parametrize('flag', [None, False, True])
@pytest.mark.parametrize('has_g', [False, True])
def test_g_omission_and_explicit_shear_flag(nonlinear, flag, has_g):
    data = cantilever()
    mat = data['element']['1']['2']
    if has_g:
        mat['G'] = 100
    else:
        del mat['G']
    if nonlinear:
        mat['nonlinear'] = dict(type='jr_stiffness_reduction', delta_1=.01, delta_2=.02,
                               delta_3=.03, P_1=20, P_2=30, P_3=35, hysteresis_dofs=['axial'])
    if flag is not None:
        data['member']['1']['shear_correction'] = flag
    model, result = run(data)
    enabled = has_g and (nonlinear if flag is None else flag)
    assert model.mesh.elements[1]['shear_correction'] is enabled
    # Bending is elastic in both models: FL^3/(3EI) + enabled*FL/(kGA).
    assert result['node_displacements'][2]['dy'] == pytest.approx(
        .002 + (.036 if enabled else 0), abs=1e-11)
    assert result['reaction_forces'][1]['fy'] == pytest.approx(-3, abs=1e-9)


def test_selected_case_and_rigid_zone_use_their_own_g():
    data = cantilever()
    data['member']['1']['shear_correction'] = True
    data['element']['2'] = copy.deepcopy(data['element']['1'])
    del data['element']['2']['2']['G']
    data['load']['1']['element'] = 2
    data['rigid'] = [dict(m=1, e=1, Ilength=1, Jlength=0)]
    model = _read_json_model(data)
    assert [(e['material_id'], e['shear_correction']) for e in model['mesh'].elements.values()] == [(1, True), (2, False)]


def test_omitted_g_file_http_and_saved_model_agree(tmp_path):
    from main import app
    data = cantilever()
    del data['element']['1']['2']['G']
    data['member']['1']['shear_correction'] = True
    path = tmp_path/'beam.json'
    path.write_text(json.dumps(data), encoding='utf-8')
    model = FemModel(); model.load_model(str(path))
    result = model.run()
    assert result['node_displacements'][2]['dy'] == pytest.approx(.002, abs=1e-12)
    saved = tmp_path/'saved.json'
    model.save_model(str(saved))
    restored = FemModel(); restored.load_model(str(saved))
    assert restored.run()['node_displacements'][2]['dy'] == pytest.approx(.002, abs=1e-12)
    response = app.test_client().post('/', json=data)
    assert response.status_code == 200
    assert json.loads(response.data)['node_displacements']['2']['dy'] == pytest.approx(.002, abs=1e-12)
