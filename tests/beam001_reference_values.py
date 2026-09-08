"""Independent decimal reference for the beam001 monotonic cantilever.

No FEM/model/solver imports. Running with --write explicitly replaces only the
dummy/reference result field; ordinary tests and --check never write fixtures.
Derivation and scope: docs/report/beam001-reference-values.md.
"""
import argparse
from decimal import Decimal, localcontext
import json
from pathlib import Path


SOURCE = Path(__file__).parent / 'data/snap/beam001.json'
D = lambda value: Decimal(str(value))


def reference_values(data):
    """Return steps 0..n, in the disg/reac/fsec schema, from scalar equations."""
    # Fail closed for changes outside this reference's derived model.
    assert data['node'] == {
        '1': dict(x=0, y=-5, z=0), '2': dict(x=0, y=-.2, z=0),
        '3': dict(x=0, y=-.1, z=0), '4': dict(x=0, y=0, z=0)}
    assert data['member'] == {
        '1': dict(ni=1, nj=2, e=1, cg=0),
        '2': dict(ni=2, nj=3, e=2, cg=0),
        '3': dict(ni=3, nj=4, e=1, cg=0)}
    assert set(data['load']) == {'1'}
    case = data['load']['1']
    assert len(case['load_node']) == 1
    load = case['load_node'][0]
    assert str(load['n']) == '1' and load['tx'] > 0
    assert all(load.get(k, 0) == 0 for k in ('ty', 'tz', 'rx', 'ry', 'rz'))
    assert case.get('rate', 1) == 1 and not case.get('load_member')
    assert not data.get('analysis_params')
    assert all(not data.get(k) for k in
               ('rigid', 'notice_points', 'fix_member', 'joint', 'shell', 'solid', 'boundary_conditions'))
    assert len(data['fix_node']['1']) == 1
    support = data['fix_node']['1'][0]
    assert str(support['n']) == '4'
    assert all(support[k] == 1 for k in ('tx', 'ty', 'tz', 'rx', 'ry', 'rz'))
    assert all(str(case.get(k, 1)) == '1' for k in ('element', 'fix_node', 'fix_member', 'joint'))
    mats = data['element']['1']
    assert 'nonlinear' not in mats['1']
    nl = mats['2']['nonlinear']
    assert nl['type'] == 'jr_stiffness_reduction' and nl['symmetric']
    assert nl['hysteresis_dofs'] == ['moment_z']
    n = case['n_load_steps']
    assert isinstance(n, int) and n > 0
    with localcontext() as context:
        context.prec = 50
        points = [(D(0), D(0))] + [(D(nl[f'P_{i}']), D(nl[f'delta_{i}'])) for i in (1, 2, 3)]
        assert all(pb > pa and kb > ka for (pa, ka), (pb, kb) in zip(points, points[1:]))
        assert D(load['tx'])*D('4.85') < points[-1][0]
        result = {}
        for step in range(n+1):
            force = D(load['tx'])*D(step)/D(n)
            zero_disp = dict.fromkeys(('dx', 'dy', 'dz', 'rx', 'ry', 'rz'), 0.)
            disps = {'4': zero_disp}
            sections = {}
            u_j = theta_j = D(0)
            for member_id in ('3', '2', '1'):
                member = data['member'][member_id]
                ni, nj = str(member['ni']), str(member['nj'])
                yi, yj = D(data['node'][ni]['y']), D(data['node'][nj]['y'])
                length = yj-yi
                mat = mats[str(member['e'])]
                ei, ga = D(mat['E'])*D(mat['Iz']), D(mat['G'])*D(mat['A'])*D(5)/D(6)
                assert ei > 0 and ga > 0
                # Positive magnitude; physical Mz and curvature are negative.
                m = force*((yi+yj)/2+D(5))
                if member_id == '2':
                    for (pa, ka), (pb, kb) in zip(points, points[1:]):
                        if m <= pb:
                            kappa = -(ka+(m-pa)*(kb-ka)/(pb-pa))
                            break
                else:
                    kappa = -m/ei
                theta_i = theta_j-length*kappa
                u_i = u_j+length*(theta_i+theta_j)/2+force*length/ga+force*length**3/(12*ei)
                disps[ni] = dict(zero_disp, dx=float(u_i), rz=float(theta_i))
                # Local cut-force convention, independently from equilibrium.
                values = dict.fromkeys((mode+end for mode in ('fx', 'fy', 'fz', 'mx', 'my', 'mz')
                                        for end in ('i', 'j')), 0.)
                values.update(fyi=float(-force), fyj=float(-force),
                              mzi=float(force*(yi+D(5))), mzj=float(force*(yj+D(5))), L=float(length))
                sections[member_id] = {'P1': values}
                u_j, theta_j = u_i, theta_i
            result[str(step)] = dict(
                disg={key: disps[key] for key in ('1', '2', '3', '4')},
                reac={'4': dict(tx=float(-force), ty=0., tz=0., mx=0., my=0., mz=float(-5*force))},
                fsec={key: sections[key] for key in ('1', '2', '3')})
        return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', action='store_true', help='Explicitly replace beam001 result only')
    parser.add_argument('--check', action='store_true', help='Check stored values without writing (default)')
    args = parser.parse_args()
    assert not (args.write and args.check)
    raw = SOURCE.read_bytes()
    data = json.loads(raw)
    expected = reference_values(data)
    if args.write:
        # Preserve the entire user-authored analysis input byte for byte.
        marker = b'    "result": '
        prefix, _, tail = raw.partition(marker)
        assert tail
        tail_text = tail.decode('utf-8')
        old_result, end = json.JSONDecoder().raw_decode(tail_text)
        assert old_result == data['result']
        suffix = tail_text[end:].encode('utf-8')
        newline = b'\r\n' if b'\r\n' in raw else b'\n'
        rendered = json.dumps(expected, ensure_ascii=False, indent=4, allow_nan=False)
        rendered = rendered.replace('\n', '\n    ').encode('utf-8').replace(b'\n', newline)
        updated = prefix+marker+rendered+suffix
        assert {k: v for k, v in json.loads(updated).items() if k != 'result'} == {
            k: v for k, v in data.items() if k != 'result'}
        assert SOURCE.read_bytes() == raw, 'Input changed during generation'
        SOURCE.write_bytes(updated)
    else:
        assert data['result'] == expected, 'Stored result differs from the independent decimal reference'
    print(f'{len(expected)} snapshots (0..{len(expected)-1}): ' + ('written' if args.write else 'verified'))


if __name__ == '__main__':
    main()
