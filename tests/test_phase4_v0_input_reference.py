"""Independent source-input solves must not need or trust stored displacements."""
from subprocess import CalledProcessError
import copy
import json

import pytest

from v0_refined_reference import solve_source


def test_source_input_only_tetra_solves_hand_axial_patch(tmp_path):
    source = tmp_path/'patch.fem'
    source.write_text('''Material 1 1000 0 500 1 0 0
Node 1 0 0 0
Node 2 1 0 0
Node 3 0 1 0
Node 4 0 0 1
TetraElement1 1 1 1 2 3 4
Restraint 1 1 0 1 0 1 0
Restraint 2 0 0 1 0 1 0
Restraint 3 1 0 1 0 1 0
Restraint 4 1 0 1 0 1 0
Load 2 .5 0 0
''', encoding='utf8')
    before = source.read_bytes()
    ref = solve_source(source, input_only=True)
    assert ref['disg']['2']['dx'] == pytest.approx(.003, rel=1e-13)
    assert ref['reac']['1']['tx'] == pytest.approx(-.5, abs=1e-13)
    assert ref['maximum_free_force_residual'] < 1e-13
    assert 'original_reac' not in ref
    assert source.read_bytes() == before


@pytest.mark.parametrize('record', ['Unknown 1 2', 'Load 9 1 0 0', 'Node 1 0 0 0'])
def test_source_input_rejects_unsupported_dangling_and_duplicate_records(tmp_path, record):
    source = tmp_path/'invalid.fem'
    source.write_text('''Material 1 1000 0 500 1 0 0
Node 1 0 0 0
Node 2 1 0 0
Node 3 0 1 0
Node 4 0 0 1
TetraElement1 1 1 1 2 3 4
Restraint 1 1 0 1 0 1 0
'''+record+'\n', encoding='utf8')
    with pytest.raises(CalledProcessError):
        solve_source(source, input_only=True)


@pytest.fixture(scope='module')
def tetra_reference():
    from restore_phase4_tetra1 import ROOT, SAMPLE, SOURCE, read_v0
    return (json.loads((ROOT/SAMPLE).read_text(encoding='utf8')), read_v0(ROOT/SOURCE),
            solve_source(ROOT/SOURCE, input_only=True))


@pytest.mark.parametrize('change', ['load', 'support', 'material', 'node', 'topology',
                                   'source', 'hash', 'coverage', 'residual'])
def test_tetra_input_repair_rejects_unverified_conditions(tetra_reference, change):
    from restore_phase4_tetra1 import completed_data
    data, source, ref = copy.deepcopy(tetra_reference)
    if change == 'load': data['load']['1']['load_node'][0]['tz'] += 1
    elif change == 'support': data['fix_node']['1'][0]['tx'] = 0
    elif change == 'material': data['element']['1']['1']['E'] += 1
    elif change == 'node': data['node']['1']['x'] += 1
    elif change == 'topology': next(iter(data['solid'].values()))['nodes'][0] = 99999
    elif change == 'source': source['sha256'] = 'unknown'
    elif change == 'hash': ref['hashes'][source['path']] = 'unknown'
    elif change == 'coverage': ref['disg'].pop('1')
    else: ref['maximum_free_force_residual'] = 1e-5
    with pytest.raises(AssertionError): completed_data(data, source, ref)


def test_tetra_complete_reference_all_outputs_and_input_preservation(tetra_reference):
    from restore_phase4_tetra1 import completed_data
    from run_sample import FemModel, _read_json_model, compare_legacy_result
    data, source, ref = tetra_reference
    original = copy.deepcopy(data)
    fixed = completed_data(data, source, ref)
    assert data == original
    assert {k:v for k,v in fixed.items() if k != 'result'} == {k:v for k,v in data.items() if k != 'result'}
    assert completed_data(fixed, source, ref) == fixed
    model = FemModel(); model.read_json_model(_read_json_model(copy.deepcopy(data)))
    result = model.run()
    compare_legacy_result(result, fixed['result']['1'], model, data)
