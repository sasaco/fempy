"""Reject unrelated input and unknown gold values before completing references."""
import copy
import json

import pytest

from restore_phase4_sources import ROOT, read_v0
from restore_phase4_supports import completed_reference
from v0_refined_reference import solve_source


@pytest.fixture(scope='module')
def source_reference():
    path = ROOT/'docs/v0/testdata/bend/sampleBendHexa1.out'
    return read_v0(path), solve_source(path)


@pytest.mark.parametrize('change', ['load', 'hash', 'residual', 'reaction', 'node', 'existing'])
def test_support_reference_repair_rejects_unverified_evidence(source_reference, change):
    source, reference = copy.deepcopy(source_reference)
    data = json.loads((ROOT/'tests/data/bend/sampleBendHexa1.json').read_text(encoding='utf8'))
    if change == 'load': data['load']['1']['load_node'][0]['tz'] += 1
    elif change == 'hash': reference['hashes'][source['path']] = 'unknown'
    elif change == 'residual': reference['maximum_free_force_residual'] = 1e-5
    elif change == 'reaction': reference['reac'].pop(next(iter(reference['reac'])))
    elif change == 'node': reference['node_ids'].pop()
    else: data['result']['1']['reac'] = {'unknown': {}}
    with pytest.raises(AssertionError): completed_reference(data, source, reference)


def test_support_reference_completion_preserves_inputs_and_is_idempotent(source_reference):
    source, reference = source_reference
    data = json.loads((ROOT/'tests/data/bend/sampleBendHexa1.json').read_text(encoding='utf8'))
    original = copy.deepcopy(data)
    fixed = completed_reference(data, source, reference)
    assert data == original
    assert {k:v for k,v in fixed.items() if k != 'result'} == {k:v for k,v in data.items() if k != 'result'}
    assert fixed['result']['1']['disg'] == data['result']['1']['disg']
    assert completed_reference(fixed, source, reference) == fixed
    assert set(fixed['result']['1']) == {'disg','reac','size','fsec','shell_fsec','shell_results'}
