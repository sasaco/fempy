from copy import deepcopy
import json

import pytest

from fem.boundary_condition import BoundaryCondition
from fem.file_io import _read_json_model, model_to_jsonable, write_model
from fem.model import FemModel
from fem.nonlinear.hysteresis.slip import SlipSpringParams
from tests.support.builders.slip_support import slip_definition, slip_model_data, legacy_slip_data

pytestmark = pytest.mark.integration


@pytest.mark.parametrize('legacy', [False, True])
def test_definition_is_normalized_roundtripped_and_owned(legacy):
    raw = legacy_slip_data() if legacy else slip_model_data()
    data = _read_json_model(raw)
    definition = data['boundary'].nonlinear_spring_supports[30]['x']
    assert definition == SlipSpringParams(1000, 100, .01)
    saved = model_to_jsonable(data)
    assert saved['boundary_conditions']['nonlinear_spring_supports']['30']['x'] == slip_definition()
    loaded = _read_json_model(json.loads(json.dumps(saved)))
    assert loaded['boundary'].nonlinear_spring_supports[30]['x'] == definition
    data['boundary'].clear()
    assert data['boundary'].nonlinear_spring_supports == data['boundary'].spring_supports == {}
    assert loaded['boundary'].nonlinear_spring_supports[30]['x'] == definition


@pytest.mark.parametrize('direction', ['x', 'y', 'z', 'rx', 'ry', 'rz'])
def test_python_api_has_same_definition_and_rejects_duplicates(direction):
    raw = slip_model_data(direction)
    raw['boundary_conditions'].pop('nonlinear_spring_supports')
    model = FemModel()
    model.read_json_model(_read_json_model(raw))
    model.add_slip_spring_support(30, direction, K1=1, K2=0, delta_1=.01)
    assert model.boundary.nonlinear_spring_supports[30][direction].K1 == 1.
    with pytest.raises(ValueError, match='30.*'+direction):
        model.add_slip_spring_support(30, direction, K1=1, K2=0, delta_1=.01)


@pytest.mark.parametrize('bad', [
    dict(type='unknown', K1=1000, K2=100, delta_1=.01),
    dict(type='slip', K1=1000, delta_1=.01),
    dict(type='slip', K1=1000, K2=100, delta_1=.01, gap=0),
    dict(type='slip', K1=True, K2=0, delta_1=.01),
    dict(type='slip', K1='1000', K2=100, delta_1=.01),
    dict(type='slip', K1=1000, K2=float('inf'), delta_1=.01),
    dict(type='slip', K1=1000, K2=-1, delta_1=.01),
    dict(type='slip', K1=1000, K2=1001, delta_1=.01),
    dict(type='slip', K1=1000, K2=100, delta_1=0),
    [], 1000, None,
])
def test_normalized_bad_definition_has_location(bad):
    raw = slip_model_data()
    raw['boundary_conditions']['nonlinear_spring_supports']['30']['x'] = bad
    with pytest.raises(ValueError, match='30.*x'):
        _read_json_model(raw)


@pytest.mark.parametrize('conflict', ['fixed', 'prescribed', 'linear'])
def test_conflicting_supports_are_rejected_after_normalization(conflict):
    raw = slip_model_data()
    bc = raw['boundary_conditions']
    if conflict == 'linear':
        bc['spring_supports'] = {'30': {'x': 2}}
    else:
        bc['restraints']['30']['dof'][0] = True
        if conflict == 'prescribed':
            bc['restraints']['30']['values'] = [.02, 0, 0, 0, 0, 0]
    with pytest.raises(ValueError, match='30.*x'):
        _read_json_model(raw)


def test_legacy_case_selection_and_explicit_restraint_replacement():
    raw = legacy_slip_data()
    raw['fix_node']['2'] = [dict(n=30, tx={'type': 'ignored'})]
    assert _read_json_model(raw)['boundary'].nonlinear_spring_supports[30]['x'].K1 == 1000
    raw['fix_node']['1'][1]['tx'] = 1
    raw['boundary_conditions'] = slip_model_data()['boundary_conditions']
    assert _read_json_model(raw)['boundary'].nonlinear_spring_supports[30]['x'].K1 == 1000
    raw = legacy_slip_data()
    raw['boundary_conditions'] = {'nonlinear_spring_supports': {'30': {'x': slip_definition()}}}
    with pytest.raises(ValueError, match='30.*x'):
        _read_json_model(raw)


@pytest.mark.parametrize('node, direction', [(999, 'x'), (30, 'dx'), (30, 'invalid')])
def test_node_and_direction_validation(node, direction):
    raw = slip_model_data()
    raw['boundary_conditions']['nonlinear_spring_supports'] = {str(node): {direction: slip_definition()}}
    with pytest.raises(ValueError, match=str(node)):
        _read_json_model(raw)


def test_fw3_fails_before_overwriting_file(tmp_path):
    target = tmp_path/'protected.fw3'
    target.write_text('original', encoding='utf8')
    with pytest.raises(ValueError, match='JSON'):
        write_model(_read_json_model(slip_model_data()), str(target))
    assert target.read_text(encoding='utf8') == 'original'


@pytest.mark.parametrize('value', [0, 1, .5, -2])
def test_numeric_legacy_support_compatibility(value):
    raw = legacy_slip_data()
    raw['fix_node']['1'][1]['tx'] = value
    bc = _read_json_model(raw)['boundary']
    assert bc.restraints[30].dof_restraints[0] == (value == 1)
    assert bc.spring_supports.get(30, {}).get('x') == (abs(value) if value not in (0, 1) else None)


def test_old_app_support_routes_explicitly_reject_objects():
    from app.components.support import Support
    from app.inputDataUtils import make_supports
    from fem.models.fa_support import FA_Support
    with pytest.raises(ValueError, match='FemModel'):
        Support(0, slip_definition(), 0, 0, 0, 0, 0, False)
    with pytest.raises(ValueError, match='FemModel'):
        FA_Support(0, False, False, False, False, False, False, slip_definition(), 0, 0, 0, 0, 0)
    with pytest.raises(ValueError, match='FemModel'):
        make_supports([dict(n=1, tx=slip_definition())], [], 3)


@pytest.mark.parametrize('first', [True, False])
def test_duplicate_legacy_fixed_and_slip_rows_cannot_overwrite_each_other(first):
    raw = legacy_slip_data()
    support = raw['fix_node']['1'][1]
    fixed = dict(n=30, tx=1)
    raw['fix_node']['1'] = [support, fixed] if first else [fixed, support]
    with pytest.raises(ValueError, match='30.*x'):
        _read_json_model(raw)


def test_api_rejects_fixed_conflict_and_failed_add_is_atomic():
    model = FemModel()
    model.read_json_model(_read_json_model(slip_model_data()))
    before = deepcopy(model.boundary.nonlinear_spring_supports)
    with pytest.raises(ValueError, match='30.*y'):
        model.add_slip_spring_support(30, 'y', 1000, 100, .01)
    assert model.boundary.nonlinear_spring_supports == before
    with pytest.raises(ValueError, match='30.*x'):
        model.add_spring_support(30, 'x', 1000)
