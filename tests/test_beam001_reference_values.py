"""Stored references are independent of the FEM solver and cover all steps."""
import copy
import json

import pytest

from beam001_reference_values import SOURCE, reference_values
from reference_solutions import beam001_reference


def test_stored_beam001_all_steps_against_independent_scalar_reference():
    data = json.loads(SOURCE.read_text(encoding='utf-8'))
    saved = data['result']
    n = data['load']['1']['n_load_steps']
    assert set(saved) == {str(i) for i in range(n+1)}
    assert saved == reference_values(data)
    for i in range(n+1):
        expected = beam001_reference(data, i/n)
        snapshot = saved[str(i)]
        for node, displacement in expected['node_displacements'].items():
            assert snapshot['disg'][node] == pytest.approx(displacement, abs=1e-12)
        reaction = expected['reaction_forces']['4']
        assert snapshot['reac']['4'] == pytest.approx(dict(
            tx=reaction['fx'], ty=reaction['fy'], tz=reaction['fz'],
            mx=reaction['mx'], my=reaction['my'], mz=reaction['mz']), abs=1e-12)
        force = i/n*data['load']['1']['load_node'][0]['tx']
        for member, values in snapshot['fsec'].items():
            ni, nj = (str(data['member'][member][key]) for key in ('ni', 'nj'))
            section = values['P1']
            assert section['fyi'] == pytest.approx(-force, abs=1e-12)
            assert section['fyj'] == pytest.approx(-force, abs=1e-12)
            assert section['mzi'] == pytest.approx(force*(5+data['node'][ni]['y']), abs=1e-12)
            assert section['mzj'] == pytest.approx(force*(5+data['node'][nj]['y']), abs=1e-12)


def test_beam001_hand_calculated_branch_crossings_and_tip_motion():
    data = json.loads(SOURCE.read_text(encoding='utf-8'))
    # F=10*s, |Mmid|=48.5*s: cross P1 between 20/21, P2 between 61/62.
    assert data['load']['1']['n_load_steps'] == 100
    assert data['load']['1']['load_node'][0]['tx'] == 1000
    for step, curvature in [(20, .0000097), (21, .0000108325),
                             (61, .0000981325), (62, .00010318605), (100, .0009420275)]:
        nodes = data['result'][str(step)]['disg']
        assert (nodes['2']['rz']-nodes['3']['rz'])/.1 == pytest.approx(curvature, abs=1e-14)
    assert all('G' not in mat for mat in data['element']['1'].values())
    assert data['result']['2']['disg']['1']['dx'] == pytest.approx(.0000005001212578616352, abs=1e-15)
    assert data['result']['100']['disg']['1']['dx'] == pytest.approx(.00045836690039308176, abs=1e-15)


@pytest.mark.parametrize('field', ['disg', 'reac', 'fsec'])
def test_late_step_reference_mutation_is_detected(tmp_path, field):
    from run_sample import run_sample
    data = json.loads(SOURCE.read_text(encoding='utf-8'))
    data = copy.deepcopy(data)
    step = data['result']['62']
    if field == 'disg':
        step[field]['1']['dx'] += .01
    elif field == 'reac':
        step[field]['4']['tx'] += 1
    else:
        step[field]['2']['P1']['mzj'] += 1
    path = tmp_path/'beam001.json'
    path.write_text(json.dumps(data), encoding='utf-8')
    before = path.read_bytes()
    with pytest.raises(AssertionError, match='step/62'):
        run_sample(path)
    assert path.read_bytes() == before
