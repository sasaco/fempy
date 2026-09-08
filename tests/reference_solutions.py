"""Independent hand calculations. This module must not import src/fem.

Reference values are evaluated from equilibrium and scalar compliance, never
obtained by running the implementation under test.
"""
import numpy as np


def axial_acceptance_reference():
    """L=2, N=12: inverse skeleton e=.001+(12-10)/2000=.002.

    Four load levels 3,6,9,12 yield u=.0006,.0012,.0018,.004.
    Reaction=-N, local i=-N and j=N; all other components zero.
    """
    def response(force, u):
        zero = dict.fromkeys(('dx','dy','dz','rx','ry','rz'), 0.)
        return dict(node_displacements={'10': zero, '30': dict(zero, dx=u)},
                    reaction_forces={'10': dict(fx=-force, fy=0., fz=0., mx=0., my=0., mz=0.)},
                    element_stresses={'7': {'i_end': [-force,0.,0.,0.,0.,0.],
                                             'j_end': [force,0.,0.,0.,0.,0.]}})
    final = response(12., .004)
    final.update(analysis_type='material_nonlinear', converged=True)
    final['step_results'] = [dict(response(p, u), converged=True, step=i, **{'lambda': i/4})
                             for i, (p,u) in enumerate(zip([3.,6.,9.,12.], [.0006,.0012,.0018,.004]), 1)]
    return final


def beam001_reference(data, factor=1.):
    """Three-member y-axis cantilever, tip Fx, base at y=0 (manual 7.21).

    At x measured upward from the loaded end: Vy=F, Mz=-F*x.
    For each element, invert the monotonic skeleton at Mmid, then integrate
    tipward from the fixed end:
    theta_i=theta_j-L*kappa;
    ux_i=ux_j+L*(theta_i+theta_j)/2 + F*L/(G*k*A)+F*L**3/(12*E*Iz).
    Omit F*L/(G*k*A) when G is absent or shear_correction is false.
    Last term is the reference elastic moment-gradient flexibility retained
    by the central-section formulation. Only monotonic symmetric branches with
    positive slope are covered; no cyclic history or plateau inversion.
    """
    assert set(data['node']) == {'1', '2', '3', '4'}
    assert set(data['member']) == {'1', '2', '3'}
    load = data['load']['1']['load_node'][0]
    assert int(load['n']) == 1
    force = factor*load['tx']
    tip_y = data['node']['1']['y']
    disps = {'4': dict.fromkeys(('dx','dy','dz','rx','ry','rz'), 0.)}
    end_forces = {}
    for member_id in ('3', '2', '1'):
        member = data['member'][member_id]
        ni, nj = str(member['ni']), str(member['nj'])
        yi, yj = data['node'][ni]['y'], data['node'][nj]['y']
        length = yj-yi
        assert length > 0
        mat = data['element']['1'][str(member['e'])]
        ei = mat['E']*mat['Iz']
        g = mat.get('G', mat['E']/(2*(1+mat.get('nu', .3))))
        moment = -force*((yi+yj)/2-tip_y)
        if 'nonlinear' in mat:
            nl = mat['nonlinear']
            assert nl['symmetric']
            points = [(0., 0.)] + [(nl[f'P_{i}'], nl[f'delta_{i}']) for i in (1, 2, 3)]
            assert abs(moment) < nl['P_3'], 'No unique force-controlled inverse at capacity'
            for (pa, ea), (pb, eb) in zip(points[:-1], points[1:]):
                assert pb > pa and eb > ea
                if abs(moment) <= pb:
                    curvature = np.sign(moment)*(ea+(abs(moment)-pa)*(eb-ea)/(pb-pa))
                    break
        else:
            curvature = moment/ei
        rotation = disps[nj]['rz']-length*curvature
        shear = (force*length/(g*(5/6)*mat['A'])
                 if 'G' in mat and member.get('shear_correction', True) else 0.)
        u = (disps[nj]['dx']+length*(rotation+disps[nj]['rz'])/2
             +shear+force*length**3/(12*ei))
        disps[ni] = dict(dx=u, dy=0., dz=0., rx=0., ry=0., rz=rotation)
        end_forces[member_id] = {
            'i_end': [0., -force, 0., 0., 0., -length*force/2-moment],
            'j_end': [0., force, 0., 0., 0., -length*force/2+moment]}
    return {'node_displacements': disps, 'element_stresses': end_forces,
            'reaction_forces': {'4': dict(fx=-force, fy=0., fz=0., mx=0., my=0., mz=force*tip_y)}}


def assert_beam001(result, data):
    """All nodal, reaction and section components; every converged load step."""
    def compare(r, factor):
        expected = beam001_reference(data, factor)
        for field, entities in expected.items():
            assert r[field].keys() == entities.keys()
            for key, values in entities.items():
                assert r[field][key].keys() == values.keys()
                for component, value in values.items():
                    np.testing.assert_allclose(r[field][key][component], value,
                                               rtol=1e-6, atol=1e-9,
                                               err_msg=f'{field}/{key}/{component}')
    compare(result, 1.)
    assert result['converged'] is True
    assert result['analysis_type'] == 'material_nonlinear'
    assert len(result['step_results']) == data['load']['1']['n_load_steps']
    for step in result['step_results']:
        assert step['converged'] is True
        compare(step, step['lambda'])
        record = [c for c in result['convergence_history'] if c['step'] == step['step']][-1]
        assert record['relative_residual'] < data['load']['1']['tolerance']
