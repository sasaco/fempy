"""Legacy shell material selection and documented A-as-thickness input."""
import copy
import pytest
from src.fem.file_io import _read_json_model


def data():
    return dict(node={'1':dict(x=0,y=0,z=0),'2':dict(x=1,y=0,z=0),'3':dict(x=0,y=1,z=0)},
                shell={'7':dict(nodes=[1,2,3],e=1)},
                element={'1':{'1':dict(E=1000,nu=.25,A=.2)},
                         '2':{'1':dict(E=1000,nu=.25,A=.3)}},
                load={'1':dict(element=2)})


def test_selected_legacy_A_defines_shell_thickness():
    raw = data(); original = copy.deepcopy(raw)
    result = _read_json_model(raw)
    assert result['mesh'].elements[7]['thickness'] == .3
    assert result['mesh'].elements[7]['formulation'] == 'dkt'
    assert raw == original


def test_explicit_shell_thickness_and_mindlin_override_are_preserved():
    raw = data();raw['element']['2']['1']['thickness'] = .4
    raw['shell']['7']['formulation'] = 'mindlin'
    result = _read_json_model(raw)
    assert result['mesh'].elements[7]['thickness'] == .4
    assert result['mesh'].elements[7]['formulation'] == 'mindlin'


@pytest.mark.parametrize('value', [None,0.,-1.,float('nan')])
def test_missing_or_invalid_legacy_shell_thickness_is_rejected(value):
    raw = data()
    if value is None: del raw['element']['2']['1']['A']
    else: raw['element']['2']['1']['A'] = value
    with pytest.raises(ValueError, match='thickness'):
        _read_json_model(raw)
