"""Source equivalence, repair mutation guards and external displacement checks."""
import copy
import json
import numpy as np
import pytest
from restore_phase4_sources import ROOT, STEMS, read_v0, repaired_data
from src.fem.model import FemModel
from src.fem.file_io import _read_json_model, result_to_jsonable
from run_sample import assert_dict_almost_equal


@pytest.mark.parametrize('field', ['node','load','fix_node','element','result','type','dimension','case'])
def test_source_repair_rejects_changed_model_or_unknown_reference(field):
    data=json.loads((ROOT/'tests/data/bend/sampleBendHexa2.json').read_text(encoding='utf-8'))
    source=read_v0(ROOT/'docs/v0/testdata/bend/sampleBendHexa2.out')
    if field == 'node': data[field]['1']['x'] += 1
    elif field == 'load': data[field]['1']['load_node'][0]['tz'] += 1
    elif field == 'fix_node': data[field]['1'][0]['tx'] = 0
    elif field == 'element': data[field]['1']['1']['E'] += 1
    elif field == 'type': next(iter(data['solid'].values()))['type'] = 'tetra2'
    elif field == 'dimension': data[field] = 2
    elif field == 'case': data['load']['1']['element'] = '999'
    else: data['result']['1']['disg']['1']['dx'] += 1
    with pytest.raises(AssertionError): repaired_data(data, source)


@pytest.mark.parametrize('stem', STEMS)
def test_repaired_solid_against_all_external_displacements(stem):
    source=read_v0(ROOT/'docs/v0/testdata/bend'/(stem+'.out'))
    data=json.loads((ROOT/'tests/data/bend'/(stem+'.json')).read_text(encoding='utf-8'))
    original = copy.deepcopy(data)
    fixed = repaired_data(data, source)
    assert data == original  # Ordinary validation never writes reference files.
    assert repaired_data(fixed, source) == fixed
    assert fixed['result']['1'].keys() == original['result']['1'].keys()
    m=FemModel(); m.read_json_model(_read_json_model(fixed)); result=m.run()
    assert_dict_almost_equal(result_to_jsonable(result['node_displacements']),source['displacements'])
    # Independent global force and moment balance does not replace missing
    # individual support reaction references.
    force, moment = np.zeros(3), np.zeros(3)
    for nid, xyz in m.mesh.nodes.items():
        load=m.boundary.loads.get(nid)
        f=np.zeros(3) if load is None else load.forces[:3].copy()
        r=result['reaction_forces'].get(nid,{})
        f += [r.get(k,0) for k in ('fx','fy','fz')]
        force += f; moment += np.cross(xyz,f)
    np.testing.assert_allclose(force,0,atol=1e-7)
    np.testing.assert_allclose(moment,0,atol=1e-5)
