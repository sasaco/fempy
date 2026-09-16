"""Independent closed-form references for the two Nd SNAP examples.

No FEM/material evaluator is used. Both examples have M_b(phi, Nd) =
(1 + Nd) m_0(phi), L = A = 1, and proportional tip forces. Shear is
integrated on the straight generalized-displacement path of EACH step.
"""

from math import log1p


def _base_moment(phi, fourth):
    if phi < .001:
        return 100 * phi, 100.
    if phi < .003:
        return .1 + 50 * (phi - .001), 50.
    if phi < .005:
        return .2 + 25 * (phi - .003), 25.
    return .25 - 25 * (phi - .005) if fourth else .25, -25. if fourth else 0.


def _mean_coefficient(k, nd0, nd1, ga):
    """Integral of C = 12*k*(1+Nd)*GA / (12*k*(1+Nd)+GA)."""
    if ga is None:
        return 12 * k * (1 + (nd0 + nd1) / 2)
    b0 = 12 * k * (1 + nd0)
    delta = 12 * k * (nd1 - nd0)
    if delta == 0:
        return b0 * ga / (b0 + ga)
    return ga * (1 - ga / delta * log1p(delta / (b0 + ga)))


def axial_force_sample_history(data):
    """Return every physical step, using statics and elementary integrals."""
    member = data['member']['7']
    assert data['node'] == {'10': dict(x=0, y=0, z=0), '30': dict(x=1, y=0, z=0)}
    material = data['element']['1']['1']
    assert material['A'] == 1
    laws = material['nonlinear']['laws']
    load = data['load']['1']
    force = load['load_node'][0]
    n_ref, v_ref = -force['tx'], force['ty']
    m_ref = force['rz'] + v_ref / 2
    young = material['E']
    ga = (material.get('G', young / (2 * (1 + material['nu']))) * 5 / 6
          if member.get('shear_correction', True) else None)
    assert ga is None or 'G' in material  # Legacy input needs explicit G to enable shear.
    controlled = 'displacement_control' in load
    fourth = 'delta_4' in laws['moment_z']['axial_force_points'][0]
    if controlled:
        phis = load['displacement_control']['targets']  # theta/L, L=1
        factors = [m / (m_ref - n_ref * m)
                   for m, _ in (_base_moment(phi, fourth) for phi in phis)]
    else:
        factors = [i / load['n_load_steps'] for i in range(1, load['n_load_steps'] + 1)]
        phis = [m_ref * factor / (100 * (1 + n_ref * factor)) for factor in factors]
        assert max(phis) < .001

    previous_nd = previous_phi = previous_compression = previous_v = shear = 0.
    history = {}
    for step, (factor, phi) in enumerate(zip(factors, phis), 1):
        nd, v, m = n_ref * factor, v_ref * factor, m_ref * factor
        if 'axial' in laws:
            assert nd <= .7 and nd >= previous_nd
            compression = nd / 1000 if nd <= .4 else .0004 + (nd - .4) / 500
            # The axial JR knee at epsilon=-.0004 splits the actual Nd path.
            cuts = [0.]
            if previous_compression < .0004 < compression:
                cuts.append((.0004 - previous_compression) / (compression - previous_compression))
            cuts.append(1.)

            def axial_at(t):
                e = previous_compression + t * (compression - previous_compression)
                return 1000 * e if e <= .0004 else .4 + 500 * (e - .0004)
        else:
            compression = nd / young
            cuts = [0.]
            if phi != previous_phi:
                cuts += [(knee - previous_phi) / (phi - previous_phi)
                         for knee in (.001, .003, .005) if previous_phi < knee < phi]
            cuts.append(1.)

            def axial_at(t):
                return previous_nd + t * (nd - previous_nd)

        mean_c = 0.
        for left, right in zip(cuts, cuts[1:]):
            probe = previous_phi + (left + right) / 2 * (phi - previous_phi)
            _, k = _base_moment(probe, fourth)
            mean_c += (right - left) * _mean_coefficient(k, axial_at(left), axial_at(right), ga)
        if v != previous_v:
            shear += (v - previous_v) / mean_c
        _, k = _base_moment(phi, fourth)
        bending = k * (1 + nd)
        terminal_c = _mean_coefficient(k, nd, nd, ga)
        zeros = dict.fromkeys(('dx', 'dy', 'dz', 'rx', 'ry', 'rz'), 0.)
        cut = dict(fxi=-nd, fyi=-v, fzi=0., mxi=0., myi=0., mzi=-m-v/2,
                   fxj=-nd, fyj=-v, fzj=0., mxj=0., myj=0., mzj=-m+v/2, L=1.)
        history[str(step)] = {
            'lambda': factor,
            'control_displacement': phi,
            'curvature': {'7': {'y': 0., 'z': phi}},
            'disg': {'10': zeros, '30': zeros | dict(dx=-compression, dy=shear+phi/2, rz=phi)},
            'reac': {'10': dict(tx=nd, ty=-v, tz=0., mx=0., my=0., mz=-m-v/2)},
            'fsec': {'7': {'P1': cut}},
            'section': dict(N=-nd, Nd=nd, curvature=phi, moment=m,
                            shear_deformation=shear, shear_force=v,
                            bending_tangent=bending, effective_inertia=bending/young,
                            shear_coefficient=terminal_c),
        }
        previous_nd, previous_phi = nd, phi
        previous_compression, previous_v = compression, v
    return history


def saved_results(history, *, displacement_control):
    """Map independent values to the existing saved-sample conventions."""
    if displacement_control:
        return {key: {field: value for field, value in step.items() if field != 'section'}
                for key, step in history.items()}
    final = next(reversed(history.values()))
    return {'1': {field: final[field] for field in ('disg', 'reac', 'fsec', 'curvature')}
            | {'size': 2, 'shell_results': {}}}
