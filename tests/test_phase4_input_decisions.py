"""Element-specific input semantics and the explicitly fixed pressure support."""
import copy
import json
from pathlib import Path

import numpy as np
import pytest

from src.fem.file_io import _read_json_model
from src.fem.model import FemModel
from src.fem.v0_io import read_v0_model

ROOT = Path(__file__).resolve().parents[1]


def test_pressure_saved_reference_is_independent_exact_polynomial_solution():
    from restore_phase4_pressure import completed_data
    from run_sample import assert_dict_almost_equal
    data = json.loads((ROOT/'tests/data/shell/shellPressureTest1.json').read_text(encoding='utf8'))
    assert_dict_almost_equal(data['result'], completed_data(data))


def test_tri1_conditions_match_the_original_fem():
    from phase4_source_evidence import read_v0
    source = read_v0(ROOT/'docs/v0/testdata/bend/sampleBendTri1.fem')
    data = json.loads((ROOT/'tests/data/bend/sampleBendTri1.json').read_text(encoding='utf8'))
    assert data['shell'] == {k:dict(e=int(v['material']),nodes=list(map(int,v['nodes'])),formulation='dkt')
                             for k,v in source['elements'].items()}
    assert data['element']['1']['1']['thickness'] == 15
    assert data['element']['1']['1']['G'] == source['materials']['1']['G']
    assert data['fix_node']['1'] == [dict(n=n,**{k:int(v[2*i]) for i,k in enumerate(('tx','ty','tz','rx','ry','rz'))})
                                   for n,v in source['restraints'].items()]


@pytest.mark.parametrize('kind', ['shell', 'solid'])
@pytest.mark.parametrize('sections', [{}, dict(A=0, Iy=0, Iz=0, J=0)])
def test_nonbeam_elements_require_no_beam_section(kind, sections):
    coords = [[0,0,0], [1,0,0], [0,1,0], [0,0,1]]
    data = dict(node={str(i+1):dict(zip('xyz',p)) for i,p in enumerate(coords)},
                element={'1':{'1':dict(E=1000,nu=.25, thickness=.2, **sections)}})
    data[kind] = {'1':dict(e=1,nodes=[1,2,3] if kind == 'shell' else [1,2,3,4])}
    parsed = _read_json_model(data)
    assert parsed['material'].bar_params == {}
    m = FemModel(); m.read_json_model(parsed)
    k = m.elements[1].get_stiffness_matrix()
    assert np.isfinite(k).all() and np.linalg.norm(k) > 0
    if kind == 'shell':
        assert m.elements[1].thickness == .2


def test_legacy_shell_A_is_thickness_not_area():
    data = dict(node={'1':dict(x=0,y=0,z=0),'2':dict(x=1,y=0,z=0),'3':dict(x=0,y=1,z=0)},
                shell={'1':dict(e=1,nodes=[1,2,3])}, element={'1':{'1':dict(E=1000,nu=.25,A=.2)}})
    parsed = _read_json_model(data)
    assert parsed['mesh'].elements[1]['thickness'] == .2
    data['element']['1']['1']['A'] = 0
    with pytest.raises(ValueError, match='thickness'):
        _read_json_model(data)


def test_pressure_fix_is_identical_in_json_and_original_format_and_balances_load():
    base = ROOT/'tests/data/shell/shellPressureTest1'
    data = json.loads(base.with_suffix('.json').read_text(encoding='utf8'))
    sources = [_read_json_model(copy.deepcopy(data)),
               read_v0_model(base.with_suffix('.fem').read_text(encoding='utf8').splitlines())]
    results = []
    for parsed in sources:
        m = FemModel(); m.read_json_model(parsed)
        assert all(m.boundary.restraints[1].dof_restraints)
        result = m.run(); results.append(result)
        assert list(result['node_displacements'][1].values()) == [0.]*6
        # Integral of -1000 ez over the unit square and moments about node 1.
        r = result['reaction_forces'][1]
        np.testing.assert_allclose([r[k] for k in ('fx','fy','fz','mx','my','mz')],
                                   [0,0,1000,500,-500,0], rtol=1e-8, atol=1e-10)
    for node in results[0]['node_displacements']:
        np.testing.assert_allclose(list(results[0]['node_displacements'][node].values()),
                                   list(results[1]['node_displacements'][node].values()),
                                   rtol=1e-8, atol=1e-10)
