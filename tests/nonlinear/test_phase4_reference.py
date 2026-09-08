"""Independent algebraic/virtual-work references, not calls to constitutive code.

Synthetic generalized deformations are small (strain or rad/length).
beam001 is also checked mathematically; its large shear deformation does not
establish physical applicability of the small-displacement model.
The cyclic polygons below are derived in the phase-four validation report.
"""
import copy
import json
import numpy as np
import pytest
from pathlib import Path

from main import app
from test_phase4_io import axial_json, json_model, python_axial, wire

MODES = {'axial': (0, 'tx', 'dx'), 'torsion': (3, 'rx', 'rx'),
         'moment_y': (4, 'ry', 'ry'), 'moment_z': (5, 'rz', 'rz')}
DISP = ['dx', 'dy', 'dz', 'rx', 'ry', 'rz']
FORCE = ['fx', 'fy', 'fz', 'mx', 'my', 'mz']


def configuration(mode='axial', n=1, force=12, asymmetric=False):
    d = axial_json(force)
    d['node'] = {str(10+20*i): dict(x=2*i/n, y=0, z=0) for i in range(n+1)}
    d['member'] = {str(7+i): dict(ni=10+20*i, nj=30+20*i, e=1, cg=0) for i in range(n)}
    nl = d['element']['1']['1']['nonlinear']
    nl['hysteresis_dofs'] = [mode]
    if asymmetric:
        nl.update(symmetric=False, delta_1_neg=.002, delta_2_neg=.006,
                  delta_3_neg=.012, P_1_neg=12, P_2_neg=20, P_3_neg=26)
    d['load']['1']['load_node'] = [dict(n=10+20*n, **{MODES[mode][1]: force})]
    return d


def solve(data, route):
    if route == 'http':
        response = app.test_client().post('/', json=data)
        assert response.status_code == 200, response.data
        return json.loads(response.data)
    if route == 'python':
        # Construct directly, independently of JSON deserialization.
        m = python_axial()
        m.mesh.nodes.clear()
        m.mesh.elements.clear()
        m.boundary.loads.clear()
        for key, xyz in data['node'].items():
            m.add_node(int(key), **xyz)
        nl = data['element']['1']['1']['nonlinear']
        params = {k: v for k, v in nl.items() if k not in ('type', 'hysteresis_dofs')}
        m.add_nonlinear_material(1, 'reference', data['element']['1']['1']['E'], nu=.25,
                                 **({'shear_modulus': data['element']['1']['1']['G']}
                                    if 'G' in data['element']['1']['1'] else {}), **params)
        for key, elem in data['member'].items():
            m.add_nonlinear_bar_element(int(key), [elem['ni'], elem['nj']], 1, 1, nl['hysteresis_dofs'])
        for load in data['load']['1']['load_node']:
            m.boundary.add_load(int(load['n']), [load.get(k, 0) for k in ('tx','ty','tz','rx','ry','rz')])
        for node, bc in data.get('boundary_conditions', {}).get('restraints', {}).items():
            m.boundary.add_restraint(int(node), bc['dof'], bc.get('values'))
        for node, supports in data.get('boundary_conditions', {}).get('spring_supports', {}).items():
            for direction, stiffness in supports.items():
                m.add_spring_support(int(node), direction, stiffness)
        m.analysis_params.update({k: v for k, v in data['load']['1'].items() if k != 'load_node'})
        m.analysis_params.update(data.get('analysis_params', {}))
    else:
        m = json_model(data)
    return wire(m.run())


def assert_uniform(r, mode, n, p, e):
    j, _, name = MODES[mode]
    errors = dict(displacement=0., end_force=0., reaction=0.)
    for i in range(n+1):
        x = 2*i/n
        expected = np.zeros(6)
        expected[j] = x*e
        if mode == 'moment_y':
            expected[2] = -x*x*e/2
        if mode == 'moment_z':
            expected[1] = x*x*e/2
        np.testing.assert_allclose([r['node_displacements'][str(10+20*i)][k] for k in DISP], expected,
                                   rtol=1e-8, atol=1e-10)
        errors['displacement'] = max(errors['displacement'], float(np.max(np.abs(
            np.array([r['node_displacements'][str(10+20*i)][k] for k in DISP])-expected))))
    for k in range(n):
        expected = np.zeros(6)
        expected[j] = p
        np.testing.assert_allclose(r['element_stresses'][str(7+k)]['j_end'], expected, atol=1e-8)
        np.testing.assert_allclose(r['element_stresses'][str(7+k)]['i_end'], -expected, atol=1e-8)
        errors['end_force'] = max(errors['end_force'], float(np.max(np.abs(
            np.array(r['element_stresses'][str(7+k)]['j_end'])-expected))),
            float(np.max(np.abs(np.array(r['element_stresses'][str(7+k)]['i_end'])+expected))))
    expected = np.zeros(6)
    expected[j] = -p
    np.testing.assert_allclose([r['reaction_forces']['10'][k] for k in FORCE], expected, atol=1e-8)
    errors['reaction'] = float(np.max(np.abs(np.array([r['reaction_forces']['10'][k] for k in FORCE])-expected)))
    print('PHASE4_METRIC ' + json.dumps(dict(mode=mode, **errors)))
    # Nodal balance, including r cross F, follows from these absolute end forces.
    assert r['converged'] is True
    assert all(s['converged'] for s in r['step_results'])
    for s in r['step_results']:
        c = [c for c in r['convergence_history'] if c['step'] == s['step']][-1]
        assert c['relative_residual'] < 1e-8
    return errors


@pytest.mark.parametrize('route', ['python', 'json', 'http'])
@pytest.mark.parametrize('mode', MODES)
@pytest.mark.parametrize('n', [1, 4])
@pytest.mark.parametrize('p,e,asymmetric', [(5,.0005,False), (12,.002,False), (-16,-.004,True), (18,.006,False)])
def test_uniform_modes_independent_inverse_skeleton(route, mode, n, p, e, asymmetric):
    assert_uniform(solve(configuration(mode, n, p, asymmetric), route), mode, n, p, e)


@pytest.mark.parametrize('route', ['python', 'json', 'http'])
@pytest.mark.parametrize('mode', MODES)
@pytest.mark.parametrize('control', ['load', 'displacement'])
@pytest.mark.parametrize('asymmetric', [False, True])
@pytest.mark.parametrize('subdivisions', [1, 7])
def test_cyclic_polygon_residual_deformation_and_work(route, mode, control, asymmetric, subdivisions):
    # Kd+=10000; Kd-=6000 for asymmetric. Every event has a hand-derived ordinate.
    if asymmetric:
        strains = np.array([0, 1, 2, .8, -2, -4, -4/3, 2])*1e-3
        forces = np.array([0, 10, 12, 0, -12, -16, 0, 12])
        loop_work = 2 * (544/15) * 1e-3
    else:
        strains = np.array([0, 1, 2, .8, -1, -2, -.8, 2])*1e-3
        forces = np.array([0, 10, 12, 0, -10, -12, 0, 12])
        loop_work = 2 * 22.4 * 1e-3
    if subdivisions > 1:
        strains = np.r_[strains[0], np.concatenate([np.linspace(a,b,subdivisions+1)[1:]
                                                   for a,b in zip(strains[:-1], strains[1:])])]
        forces = np.r_[forces[0], np.concatenate([np.linspace(a,b,subdivisions+1)[1:]
                                                 for a,b in zip(forces[:-1], forces[1:])])]
    d = configuration(mode, force=1, asymmetric=asymmetric)
    if control == 'load':
        factors = forces.tolist()
    else:
        # Unit target generalized deformation .001, hence tip motion .002.
        j = MODES[mode][0]
        values = [0.]*6
        values[j] = .002
        flags = [False]*6
        flags[j] = True
        d['boundary_conditions'] = {'restraints': {'30': {'dof': flags, 'values': values}}}
        d['load']['1']['load_node'] = []
        factors = (strains/.001).tolist()
    d['analysis_params'] = {'load_factors': factors}
    r = solve(d, route)
    assert len(r['step_results']) == len(strains)
    observed_e, observed_p = [], []
    for step, e, p in zip(r['step_results'], strains, forces):
        assert_uniform(dict(step, convergence_history=r['convergence_history'], step_results=[step]), mode, 1, p, e)
        observed_e.append(step['node_displacements']['30'][MODES[mode][2]]/2)
        observed_p.append(step['element_stresses']['7']['j_end'][MODES[mode][0]])
    # Closed deformation-force loop +2 -> ... -> +2. No claim that all internal
    # history variables return to initial values; signed external work is tested.
    start = 2*subdivisions
    work = 2*np.sum(np.diff(observed_e[start:])*(np.array(observed_p[start:-1])+observed_p[start+1:])/2)
    assert work == pytest.approx(loop_work, rel=1e-8, abs=1e-10)
    assert work > 0
    print('PHASE4_METRIC ' + json.dumps(dict(mode=mode, work=abs(work-loop_work))))


@pytest.mark.parametrize('route', ['python', 'json', 'http'])
def test_public_spring_api_participates_in_equilibrium(route):
    d = configuration(force=20)
    # .004 displacement -> N=12, spring=2000*.004=8, total=20.
    d['boundary_conditions'] = {'spring_supports': {'30': {'x': 2000}}}
    r = solve(d, route)
    assert r['node_displacements']['30']['dx'] == pytest.approx(.004, abs=1e-10)
    assert r['reaction_forces']['10']['fx'] == pytest.approx(-12, abs=1e-8)
    assert r['reaction_forces']['30']['fx'] == pytest.approx(-8, abs=1e-8)
    assert r['reaction_forces']['10']['fx'] + r['reaction_forces']['30']['fx'] + 20 == pytest.approx(0, abs=1e-8)


@pytest.mark.parametrize('route', ['python', 'json', 'http'])
@pytest.mark.parametrize('mode', MODES)
@pytest.mark.parametrize('control', ['load', 'displacement'])
@pytest.mark.parametrize('beta', [0, 1])
@pytest.mark.parametrize('sign', [1, -1])
@pytest.mark.parametrize('subdivisions', [1, 3])
def test_nested_reversal_and_damage_independent_polygon(route, mode, control, beta, sign, subdivisions):
    # Hand-derived branch intersections, with every zero/return point included.
    # beta=1: Kd+=10000*(2)^-1=5000, outer zero=-.4e-3;
    # A=(-.7,-5), virgin negative Kd=10000, zero=-.2;
    # B=(.9,6), retained positive Kd=5000, zero=-.3;
    # reload to A has slope 12500, then resumes outer slope 50000/3.
    if beta:
        vertices = [(0,0),(1,10),(2,12),(-.4,0),(-.7,-5),(-.2,0),
                    (.9,6),(-.3,0),(-.5,-2.5),(-.7,-5),(-1,-10),(-2,-12)]
    else:
        vertices = [(0,0),(1,10),(2,12),(.8,0),(-.1,-5),(.4,0),(1.2,6),
                    (.6,0),(.25,-2.5),(.5,0),(.85,3),(.55,0),(.25,-2.5),
                    (-.1,-5),(-1,-10),(-2,-12)]
    base = np.array(vertices)*[sign*.001, sign]
    # Signed external work includes the initial loading; no closed-cycle claim.
    expected_work = 2*np.sum(np.diff(base[:,0])*(base[:-1,1]+base[1:,1])/2)
    path = np.vstack([base[0], *[np.linspace(a,b,subdivisions+1)[1:]
                               for a,b in zip(base[:-1],base[1:])]])
    d = configuration(mode, force=1)
    d['element']['1']['1']['nonlinear']['beta'] = beta
    if control == 'load':
        factors = path[:,1].tolist()
    else:
        j = MODES[mode][0]
        flags, values = [False]*6, [0.]*6
        flags[j], values[j] = True, .002
        d['boundary_conditions'] = {'restraints': {'30': dict(dof=flags, values=values)}}
        d['load']['1']['load_node'] = []
        factors = (path[:,0]/.001).tolist()
    d['analysis_params'] = dict(load_factors=factors)
    r = solve(d, route)
    observed = []
    max_errors = dict(displacement=0., end_force=0., reaction=0.)
    for step, (e,p) in zip(r['step_results'], path):
        errors = assert_uniform(dict(step, convergence_history=r['convergence_history'], step_results=[step]), mode, 1, p, e)
        max_errors = {key:max(value,errors[key]) for key,value in max_errors.items()}
        observed.append((step['node_displacements']['30'][MODES[mode][2]]/2,
                         step['element_stresses']['7']['j_end'][MODES[mode][0]]))
    observed = np.array(observed)
    work = 2*np.sum(np.diff(observed[:,0])*(observed[:-1,1]+observed[1:,1])/2)
    assert work == pytest.approx(expected_work, rel=1e-8, abs=1e-10)
    print('PHASE4_NESTED_METRIC '+json.dumps(dict(route=route,mode=mode,control=control,beta=beta,sign=sign,
          subdivisions=subdivisions, work_error=abs(work-expected_work), **max_errors)))


@pytest.mark.parametrize('route', ['python', 'json', 'http'])
@pytest.mark.parametrize('direction', ['ty', 'tz'])
def test_elastic_cantilever_force_moment_balance(route, direction):
    d = configuration('moment_z' if direction == 'ty' else 'moment_y', force=0)
    d['load']['1']['load_node'] = [dict(n=30, **{direction: 1})]
    r = solve(d, route)
    # E I=10000, G k A=4000*5/6, L=2.
    v = 8/30000 + 2/(4000*5/6)
    disp = r['node_displacements']['30']
    assert disp['dy' if direction == 'ty' else 'dz'] == pytest.approx(v, abs=1e-10)
    assert disp['rz' if direction == 'ty' else 'ry'] == pytest.approx((1 if direction == 'ty' else -1)*.0002, abs=1e-10)
    fixed = r['reaction_forces']['10']
    assert fixed['fy' if direction == 'ty' else 'fz'] == pytest.approx(-1, abs=1e-10)
    assert fixed['mz' if direction == 'ty' else 'my'] == pytest.approx(-2 if direction == 'ty' else 2, abs=1e-10)


@pytest.mark.parametrize('n_steps,tolerance', [(1,1e-6), (7,1e-8), (40,1e-10)])
def test_load_increment_and_tolerance_sensitivity(n_steps, tolerance):
    d = configuration('moment_z', n=4, force=18)
    d['analysis_params'] = dict(n_load_steps=n_steps, tolerance=tolerance)
    assert_uniform(solve(d, 'json'), 'moment_z', 4, 18, .006)


@pytest.mark.parametrize('route', ['json', 'http'])
def test_beam001_independent_flexibility_reference(route):
    from reference_solutions import assert_beam001
    d = json.loads((Path(__file__).parents[1]/'data/snap/beam001.json').read_text(encoding='utf-8'))
    if route == 'http':
        response = app.test_client().post('/', json=d)
        assert response.status_code == 200, response.data
        r = json.loads(response.data)
    else:
        r = wire(json_model(d).run())
    assert_beam001(r, d)


@pytest.mark.parametrize('moment,curvature', [
    (0., 0.), (1000., -1e-5), (2000., -5.5e-5),
    (3000., -.0001), (4850., -.0009325),
])
def test_beam001_reference_inverse_at_hand_calculated_ordinates(moment, curvature):
    from reference_solutions import beam001_reference
    d = json.loads((Path(__file__).parents[1]/'data/snap/beam001.json').read_text(encoding='utf-8'))
    # Mmid=-4.85*F; third-branch kappa=-(.0001+(4850-3000)*.0009/2000).
    factor = moment/(4.85*d['load']['1']['load_node'][0]['tx'])
    nodes = beam001_reference(d, factor)['node_displacements']
    assert (nodes['3']['rz']-nodes['2']['rz'])/.1 == pytest.approx(curvature, abs=1e-14)


@pytest.mark.parametrize('n', [1, 2, 4, 8, 16])
def test_nonuniform_bending_discrete_compliance_and_continuum_limit(n):
    # A tip force gives a *nonuniform* moment, with two skeleton breakpoints.
    # Independent continuous-section reference: integrate kappa(M=9*t).
    p, length, ei, ga = 9., 2., 10000., 4000*5/6
    theta_cont = v_cont = 0.
    for a, b, slope, intercept in [(0,10/9,1/10000,0),
                                  (10/9,16/9,1/2000,-8/2000),
                                  (16/9,2,1/1000,-12/1000)]:
        theta_cont += slope*p*(b*b-a*a)/2 + intercept*(b-a)
        v_cont += slope*p*(b**3-a**3)/3 + intercept*(b*b-a*a)/2
    v_cont += p*length/ga
    h = length/n
    t = (np.arange(n)+.5)*h
    moment = p*t
    kappa = np.where(moment <= 10, moment/10000,
                     np.where(moment <= 16, (moment-8)/2000, (moment-12)/1000))
    theta_discrete = h*np.sum(kappa)
    v_discrete = h*np.dot(t,kappa)+p*length/ga+n*p*h**3/(12*ei)
    d = configuration('moment_z', n=n, force=0)
    d['load']['1']['load_node'] = [dict(n=10+20*n, ty=p)]
    r = solve(d, 'json')
    tip = r['node_displacements'][str(10+20*n)]
    assert tip['dy'] == pytest.approx(v_discrete, rel=1e-8, abs=1e-10)
    assert tip['rz'] == pytest.approx(theta_discrete, rel=1e-8, abs=1e-10)
    # No assertion that one central section equals distributed plasticity.
    # Piecewise-linear midpoint integration error is O(h**2), bounded using
    # maximum slope and the two slope jumps of the inverse skeleton.
    assert abs(tip['dy']-v_cont) <= .02*h*h
    assert abs(tip['rz']-theta_cont) <= .01*h*h
    print(f'nonuniform n={n}: u={tip["dy"]:.12g}, continuum={v_cont:.12g}, '
          f'rel_error={(tip["dy"]-v_cont)/v_cont:.8g}, theta_error={tip["rz"]-theta_cont:.8g}')


@pytest.mark.parametrize('route', ['python', 'json', 'http'])
def test_explicit_G_elastic_torsion(route):
    d = configuration('torsion', force=1)
    d['element']['1']['1']['G'] = 2500
    nl = d['element']['1']['1']['nonlinear']
    nl.update(P_1=2.5, P_2=4, P_3=5.5)
    assert_uniform(solve(d, route), 'torsion', 1, 1, 1/2500)


@pytest.mark.parametrize('route', ['python', 'json', 'http'])
def test_two_members_with_intermediate_load_have_independent_equilibrium(route):
    d = configuration(n=2)
    d['node']['30']['x'] = .5
    d['load']['1']['load_node'].append(dict(n=30, tx=4))
    r = solve(d, route)
    assert r['node_displacements']['30']['dx'] == pytest.approx(.5*.004, abs=1e-10)
    assert r['node_displacements']['50']['dx'] == pytest.approx(.5*.004+1.5*.002, abs=1e-10)
    assert r['reaction_forces']['10']['fx'] == pytest.approx(-16, abs=1e-9)
    assert r['element_stresses']['7']['j_end'][0] == pytest.approx(16, abs=1e-9)
    assert r['element_stresses']['8']['i_end'][0] == pytest.approx(-12, abs=1e-9)
    assert r['element_stresses']['7']['j_end'][0]+r['element_stresses']['8']['i_end'][0]-4 == pytest.approx(0, abs=1e-9)


@pytest.mark.parametrize('mode', MODES)
@pytest.mark.parametrize('route', ['python', 'json', 'http'])
@pytest.mark.parametrize('vertical', [False, True])
def test_rotated_member_global_displacements_and_equilibrium(mode, route, vertical):
    # Columns are local unit vectors in global coordinates, independently fixed.
    c = 1/np.sqrt(2)
    q = np.array([[0,1,0],[0,0,1],[1,0,0]]) if vertical else np.array([[c,-c,0],[c,c,0],[0,0,1]])
    d = configuration(mode)
    d['node']['30'] = dict(zip(('x','y','z'), (q @ [2,0,0]).astype(float).tolist()))
    force = np.zeros(6)
    force[MODES[mode][0]] = 12
    global_force = np.r_[q@force[:3], q@force[3:]]
    d['load']['1']['load_node'] = [dict(n=30, **dict(zip(('tx','ty','tz','rx','ry','rz'),global_force)))]
    r = solve(d, route)
    local_u = np.zeros(6)
    local_u[MODES[mode][0]] = .004
    if mode == 'moment_y':
        local_u[2] = -.004
    if mode == 'moment_z':
        local_u[1] = .004
    expected_u = np.r_[q@local_u[:3], q@local_u[3:]]
    np.testing.assert_allclose([r['node_displacements']['30'][k] for k in DISP], expected_u, atol=1e-10)
    reaction = np.array([r['reaction_forces']['10'][k] for k in FORCE])
    np.testing.assert_allclose(reaction, -global_force, atol=1e-8)
    np.testing.assert_allclose(reaction[:3]+global_force[:3], 0, atol=1e-8)
    np.testing.assert_allclose(reaction[3:]+global_force[3:]+np.cross(q@[2,0,0],global_force[:3]), 0, atol=1e-8)
    np.testing.assert_allclose(r['element_stresses']['7']['j_end'], force, atol=1e-8)
    np.testing.assert_allclose(r['element_stresses']['7']['i_end'], -force, atol=1e-8)
