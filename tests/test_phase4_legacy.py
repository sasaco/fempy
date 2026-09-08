"""Independent statics for the legacy input and shared linear beam path."""
import copy
import json
from pathlib import Path

import numpy as np
import pytest
from scipy.sparse import csr_matrix

from src.fem.file_io import _read_json_model
from src.fem.model import FemModel
from src.fem.solver import Solver


def cantilever():
    return dict(node={'1': dict(x=0, y=0, z=0), '2': dict(x=2, y=0, z=0)},
                member={'1': dict(ni=1, nj=2, e=2, cg=0)},
                element={'1': {'1': dict(E=1000, G=1, A=9, Iy=9, Iz=9, J=9),
                               '2': dict(E=1000, G=1, A=2, Iy=3, Iz=4, J=5)}},
                fix_node={'1': [dict(n=1, tx=1, ty=1, tz=1, rx=1, ry=1, rz=1)]},
                load={'1': dict(load_node=[dict(n=2, ty=3)])})


def run(data):
    m = FemModel()
    m.read_json_model(_read_json_model(copy.deepcopy(data)))
    return m, m.run()


def test_legacy_bernoulli_section_and_cg():
    d = cantilever()
    _, r = run(d)
    assert r['node_displacements'][2]['dy'] == pytest.approx(3*8/(3*1000*4), abs=1e-12)
    d['member']['1']['cg'] = 90
    _, r = run(d)
    assert r['node_displacements'][2]['dy'] == pytest.approx(3*8/(3*1000*3), abs=1e-12)


@pytest.mark.parametrize('mark', [0, '0'])
def test_disabled_member_load_ignores_stale_values_and_does_not_split(mark):
    d = cantilever()
    _, expected = run(d)
    d['load']['1']['load_member'] = [dict(m=1, mark=mark, P1=99, P2=-23,
                                        L1='unused', L2=-10, direction='unused')]
    model, actual = run(d)
    assert len(model.mesh.nodes) == 2
    assert actual['node_displacements'] == expected['node_displacements']
    assert actual['reaction_forces'] == expected['reaction_forces']


def test_generated_point_coordinates_keep_submillimetre_precision():
    d = cantilever()
    x = 0.123456789012345
    d['notice_points'] = [dict(m=1, Points=[x])]
    model, result = run(d)
    generated = next(n for n in model.mesh.nodes if n not in (1, 2))
    assert model.mesh.nodes[generated][0] == x
    assert result['node_displacements'][generated]['dy'] == pytest.approx(
        3*x*x*(6-x)/(6*4000), rel=1e-8, abs=1e-10)


@pytest.mark.parametrize('q0,q1', [(3, 3), (0, 6), (6, 0)])
def test_uniform_and_triangular_load_consistent_forces(q0, q1):
    d = cantilever()
    d['load']['1'] = dict(load_member=[dict(m=1, mark=2, direction='y', P1=q0, P2=q1)])
    _, r = run(d)
    # Unit-load integration of M(x)*(L-x)/EI; triangle rising towards tip.
    expected = 2**4*(4*q0+11*q1)/(120*1000*4)
    assert r['node_displacements'][2]['dy'] == pytest.approx(expected, abs=1e-12)
    f = r['element_stresses'][1]
    np.testing.assert_allclose(f['j_end'], 0, atol=1e-10)
    assert f['i_end'][1] == pytest.approx(-(q0+q1), abs=1e-10)
    assert f['i_end'][5] == pytest.approx(-4*(q0+2*q1)/6, abs=1e-10)


def test_released_tip_propped_beam():
    d = cantilever()
    d['fix_node']['1'].append(dict(n=2, ty=1, rz=1))
    d['joint'] = {'1': [dict(m=1, zi=1, zj=0)]}
    d['load']['1'] = dict(load_member=[dict(m=1, mark=2, direction='y', P1=8, P2=8)])
    _, r = run(d)
    assert r['reaction_forces'][1]['fy'] == pytest.approx(-10, abs=1e-9)
    assert r['reaction_forces'][2]['fy'] == pytest.approx(-6, abs=1e-9)
    assert r['element_stresses'][1]['j_end'][5] == pytest.approx(0, abs=1e-9)
    assert r['element_stresses'][1]['i_end'][5] == pytest.approx(-4, abs=1e-9)


@pytest.mark.parametrize('k', [.5, 20, 2000])
def test_legacy_support_spring_is_not_prescribed_motion(k):
    d = cantilever()
    d['fix_node']['1'].append(dict(n=2, ty=k))
    _, r = run(d)
    u = 3/(3*1000*4/8+k)
    assert r['node_displacements'][2]['dy'] == pytest.approx(u, abs=1e-12)
    assert r['reaction_forces'][2]['fy'] == pytest.approx(-k*u, abs=1e-9)


def test_rigid_zone_uses_its_assigned_section():
    d = cantilever()
    d['element']['1']['1'].update(E=2000, Iz=4)
    d['rigid'] = [dict(m=1, e=1, Ilength=1, Jlength=0)]
    _, r = run(d)
    # Integral F*(L-x)^2/EI across [0,1] and [1,2].
    assert r['node_displacements'][2]['dy'] == pytest.approx(7/8000+1/4000, abs=1e-12)


def test_point_load_and_negative_width_split():
    d = cantilever()
    d['load']['1'] = dict(load_member=[dict(m=1, mark=1, direction='y', P1=3, L1=1)])
    _, r = run(d)
    assert r['node_displacements'][2]['dy'] == pytest.approx(3*1**2*(6-1)/(6*4000), abs=1e-12)
    d['load']['1'] = dict(load_member=[dict(m=1, mark=2, direction='y', P1=3, P2=3, L1=.5, L2=-1)])
    _, r = run(d)
    # Integral q*a^2*(3L-a)/(6EI) da for a=.5..1.5.
    primitive = lambda a: 3*(2*a**3-a**4/4)/(6*4000)
    assert r['node_displacements'][2]['dy'] == pytest.approx(primitive(1.5)-primitive(.5), abs=1e-12)


def test_distributed_axial_foundation_exact_solution():
    d = cantilever()
    d['load']['1'] = dict(load_node=[dict(n=2, tx=3)])
    d['fix_member'] = {'1': [dict(m=1, tx=500)]}
    _, r = run(d)
    # EA u''=k u; u(0)=0; EA u'(L)=F.
    a = np.sqrt(500/2000)
    assert r['node_displacements'][2]['dx'] == pytest.approx(3*np.tanh(2*a)/(2000*a), abs=1e-12)


def test_public_distributed_load_and_joint_apis():
    d = cantilever()
    d['load']['1'] = {}
    m, _ = run(d)
    m.add_distributed_load(1, 'local_y', 8, 8)
    m.add_restraint(2, dy=True, rz=True)
    m.add_joint_condition(1, zj=0)
    r = m.run()
    assert r['reaction_forces'][1]['fy'] == pytest.approx(-10, abs=1e-9)
    assert r['element_stresses'][1]['j_end'][5] == pytest.approx(0, abs=1e-9)
    r2 = m.run()
    np.testing.assert_allclose(r2['displacement'], r['displacement'], atol=1e-14)


def test_timoshenko_uniform_load_includes_shear_and_fixed_end_forces():
    d = cantilever()
    d['member']['1']['shear_correction'] = True
    d['element']['1']['2']['G'] = 100
    d['load']['1'] = dict(load_member=[dict(m=1, mark=2, direction='y', P1=3, P2=3)])
    _, r = run(d)
    assert r['node_displacements'][2]['dy'] == pytest.approx(3*16/(8*4000)+3*4/(2*100*2*5/6), abs=1e-12)
    np.testing.assert_allclose(r['element_stresses'][1]['j_end'], 0, atol=1e-10)


def test_foundation_constant_patch_and_linear_nonlinear_route():
    d = cantilever()
    d['fix_node']['1'].append(dict(n=2, tx=1, ty=1, tz=1, rx=1, ry=1, rz=1))
    d['boundary_conditions'] = {'restraints': {str(n): dict(dof=[True]*6, values=[0,.002,0,0,0,0]) for n in (1,2)}}
    d['fix_member'] = {'1': [dict(m=1, ty=500)]}
    d['load']['1'] = dict(load_member=[dict(m=1, mark=2, direction='y', P1=1, P2=1)])
    for analysis_type in ('static','material_nonlinear'):
        d['analysis_type'] = analysis_type
        _, r = run(d)
        steps = r.get('step_results', [r])
        for step in steps:
            for forces in step['element_stresses'].values():
                np.testing.assert_allclose(forces['i_end'],0,atol=1e-9)
                np.testing.assert_allclose(forces['j_end'],0,atol=1e-9)


def test_fully_released_unloaded_rotation_is_absent_not_stabilized():
    d = cantilever()
    d['fix_node']['1'].append(dict(n=2, ty=1))
    d['joint'] = {'1': [dict(m=1, zj=0)]}
    d['load']['1'] = dict(load_member=[dict(m=1, mark=2, direction='y', P1=8, P2=8)])
    _, r = run(d)
    assert r['reaction_forces'][1]['fy'] == pytest.approx(-10, abs=1e-9)
    assert r['reaction_forces'][2]['fy'] == pytest.approx(-6, abs=1e-9)


def test_split_roundoff_and_other_case_subdivisions_keep_loads_separate():
    d = cantilever()
    d['rigid'] = [dict(m=1, e=2, Jlength=.2)]
    d['notice_points'] = [dict(m=1, Points=[1.8000000000000003])]
    d['load']['2'] = dict(load_member=[dict(m=1, mark=1, L1=1, P1=999, direction='y')])
    m, r = run(d)
    assert len(m.mesh.nodes) == 4
    assert min(e.length for e in m.elements.values()) > .1
    assert r['node_displacements'][2]['dy'] == pytest.approx(.002, abs=1e-12)


def test_saved_legacy_member_features_retain_the_same_physical_model(tmp_path):
    d = cantilever()
    d['dimension'] = 2
    d['fix_member'] = {'1': [dict(m=1, ty=500)]}
    d['load']['1'] = dict(load_member=[dict(m=1, mark=2, direction='y', P1=2, P2=3)])
    m, expected = run(d)
    path = tmp_path/'member.json'
    m.save_model(str(path))
    restored = FemModel();restored.load_model(str(path))
    actual = restored.run()
    np.testing.assert_allclose(actual['displacement'], expected['displacement'], atol=1e-12)
    assert actual['reaction_forces'].keys() == expected['reaction_forces'].keys()
    for key in expected['element_stresses']:
        for end in ('i_end','j_end'):
            np.testing.assert_allclose(actual['element_stresses'][key][end], expected['element_stresses'][key][end],atol=1e-10)


def test_member_and_shell_number_namespaces_do_not_overwrite_each_other():
    d = cantilever()
    d['node']['3'] = dict(x=0,y=1,z=0)
    d['shell'] = {'1': dict(nodes=[1,2,3],e=1)}
    m = FemModel();m.read_json_model(_read_json_model(d))
    assert m.mesh.elements[1]['nodes'] == [1,2]
    shells = [e for e in m.mesh.elements.values() if e['type']=='shell']
    assert len(shells) == 1 and shells[0]['nodes'] == [1,2,3]


@pytest.mark.parametrize('restrained', [False,True])
def test_temperature_load_free_expansion_and_restrained_force(restrained):
    d = cantilever()
    d['element']['1']['2']['Xp'] = 1e-5
    d['load']['1'] = {}
    m, _ = run(d)
    m.add_temperature_load(1, 20)
    if restrained:
        m.add_restraint(2,dx=True)
    r = m.run()
    assert r['node_displacements'][2]['dx'] == pytest.approx(0 if restrained else .0004,abs=1e-12)
    assert r['element_stresses'][1]['j_end'][0] == pytest.approx(-.4 if restrained else 0,abs=1e-10)


def test_audit_visits_later_case_after_first_error_and_never_writes_reference(tmp_path):
    from audit_phase4_samples import audit
    d = cantilever()
    d['load']['2'] = copy.deepcopy(d['load']['1'])
    d['load']['1'] = dict(load_member=[dict(m=1,mark=1,L1=9,P1=3,direction='y')])
    d['result'] = {'1': {}, '2': {}}
    path = tmp_path/'audit.json';path.write_text(json.dumps(d),encoding='utf-8')
    before = path.read_bytes()
    entries = audit(path)
    assert [entry['case'] for entry in entries] == ['1','2']
    assert entries[0]['status']=='error'
    assert entries[1]['fields']['disg']['status']=='missing reference'
    assert path.read_bytes()==before


def test_legacy_cut_signs_and_labels_have_independent_physical_values():
    from run_sample import legacy_result_view, comparison_errors
    d = cantilever();d['notice_points']=[dict(m=1,Points=[1])]
    m,r = run(d)
    view = legacy_result_view(r,m,d)
    assert view['size']==3
    assert set(view['disg'])=={'1','2','1n1'}
    assert view['disg']['1n1']['dy']==pytest.approx(3*1**2*(6-1)/(6*4000),abs=1e-12)
    for segment, mi, mj in [('P1',-6,-3),('P2',-3,0)]:
        expected = dict.fromkeys(('fxi','fzi','mxi','myi','fxj','fzj','mxj','myj'),0.)
        expected.update(fyi=-3,fyj=-3,mzi=mi,mzj=mj,L=1.)
        assert not comparison_errors(view['fsec']['1'][segment],expected)
        broken = dict(expected,mzi=99)
        assert comparison_errors(view['fsec']['1'][segment],broken)


@pytest.mark.parametrize('mutation', [None, 'displacement', 'missing_node', 'unknown_step'])
def test_beam001_step_zero_reference_is_compared_in_addition_to_all_step_oracle(tmp_path, mutation):
    from run_sample import run_sample
    source = Path(__file__).parent/'data/snap/beam001.json'
    data = json.loads(source.read_text(encoding='utf-8'))
    zero = dict(disg={k:dict.fromkeys(('dx','dy','dz','rx','ry','rz'),0.) for k in data['node']},
                reac={'4':dict.fromkeys(('tx','ty','tz','mx','my','mz'),0.)},fsec={})
    for key, member in data['member'].items():
        length = data['node'][str(member['nj'])]['y']-data['node'][str(member['ni'])]['y']
        values = dict.fromkeys((mode+end for mode in ('fx','fy','fz','mx','my','mz') for end in ('i','j')),0.)
        zero['fsec'][key] = {'P1':dict(values,L=length)}
    data['result'] = {'0':zero}
    if mutation=='displacement':
        zero['disg']['1']['dx'] = 1.
    elif mutation=='missing_node':
        del zero['disg']['1']
    elif mutation=='unknown_step':
        data['result']['999999'] = copy.deepcopy(zero)
    path = tmp_path/'beam001.json';path.write_text(json.dumps(data),encoding='utf-8')
    before = path.read_bytes()
    if mutation:
        with pytest.raises(AssertionError):
            run_sample(path)
    else:
        assert run_sample(path)['converged']
    assert path.read_bytes()==before


def test_linear_solver_scales_without_artificial_stiffness():
    s = Solver()
    np.testing.assert_allclose(s.solve_linear_system(csr_matrix(np.diag([1e-15, 1e15])), np.ones(2)),
                               [1e15, 1e-15], rtol=1e-12, atol=0)


@pytest.mark.parametrize('force', [[0, 0], [1, -1], [1, 0]])
def test_linear_solver_rejects_rigid_mode(force):
    with pytest.raises((ValueError, np.linalg.LinAlgError), match='[Ss]ingular|rank'):
        Solver().solve_linear_system(csr_matrix([[1., -1.], [-1., 1.]]), np.array(force))
