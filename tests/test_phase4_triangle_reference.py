"""Complete independent DKT outputs and input-identity rejection tests."""
import copy
import json

import pytest

from restore_phase4_tri1 import ROOT, SAMPLE, SOURCE, check_input, completed_data, read_v0
from run_sample import assert_dict_almost_equal


def test_all_tri1_outputs_match_original_operators_without_production_imports():
    data = json.loads((ROOT/SAMPLE).read_text(encoding='utf8'))
    reference, proof = completed_data(data)
    assert proof['maximum_free_force_residual'] < 1e-10
    assert_dict_almost_equal(data['result'], reference)
    source = read_v0(ROOT/'docs/v0/testdata/bend/sampleBendTri1.out')
    assert_dict_almost_equal(reference['1']['disg'], source['displacements'])
    assert len(reference['1']['shell_results']) == 240


@pytest.mark.parametrize('change',['material','thickness','support','load','node','topology','member_load'])
def test_tri1_reference_rejects_changed_input_conditions(change):
    data = json.loads((ROOT/SAMPLE).read_text(encoding='utf8'))
    if change == 'material': data['element']['1']['1']['E'] *= 2
    elif change == 'thickness': data['element']['1']['1']['thickness'] = 2
    elif change == 'support': data['fix_node']['1'][0]['rx'] = 0
    elif change == 'load': data['load']['1']['load_node'][0]['tz'] *= 2
    elif change == 'node': data['node']['1']['x'] += .1
    elif change == 'topology': next(iter(data['shell'].values()))['nodes'][0] = 999
    else: data['load']['1']['load_member'] = [dict(m=1,mark=2,P1=3)]
    with pytest.raises(AssertionError): check_input(data,read_v0(ROOT/SOURCE))
