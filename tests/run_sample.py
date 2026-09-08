"""Read-only numerical verification. Never manufacture or overwrite references."""
import copy
import json
import math
from numbers import Real
from pathlib import Path
import sys
import pytest
import numpy as np

project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))
sys.path.insert(0, str(project_root / 'src'))
from src.fem.model import FemModel
from src.fem.file_io import _read_json_model, result_to_jsonable


def assert_dict_almost_equal(actual, expected, path=''):
    """Compare every key and sequence entry, including finite values and signs."""
    if isinstance(expected, dict):
        assert isinstance(actual, dict), f'Type mismatch at {path}'
        assert actual.keys() == expected.keys(), f'Keys differ at {path}: {actual.keys()} != {expected.keys()}'
        for key in expected:
            assert_dict_almost_equal(actual[key], expected[key], f'{path}/{key}')
    elif isinstance(expected, (list, tuple)):
        assert isinstance(actual, (list, tuple)), f'Type mismatch at {path}'
        assert len(actual) == len(expected), f'Length differs at {path}'
        for i, (a, e) in enumerate(zip(actual, expected)):
            assert_dict_almost_equal(a, e, f'{path}/{i}')
    elif isinstance(expected, bool) or expected is None:
        assert actual is expected, f'Value differs at {path}: {actual} != {expected}'
    elif isinstance(expected, Real):
        assert isinstance(actual, Real) and not isinstance(actual, bool), f'Type mismatch at {path}'
        assert math.isfinite(actual) and math.isfinite(expected), f'Non-finite at {path}'
        assert actual == pytest.approx(expected, rel=1e-8, abs=1e-10), f'Value differs at {path}: {actual} != {expected}'
    else:
        assert actual == expected, f'Value differs at {path}: {actual} != {expected}'


def legacy_result_view(result, model, data):
    """Map IDs and section-cut signs using geometry and src/app/result.py.

    This maps actual output only. It neither computes nor alters reference values.
    Unmapped nodes/fields stay visible as mismatches.
    """
    result = result_to_jsonable(result)
    nodes = data.get('node', {k: dict(zip(('x','y','z'), v)) for k,v in data.get('nodes', {}).items()})
    labels = {int(k): k for k in nodes}
    labels.update(getattr(model, 'node_labels', {}))
    member_points = {str(p['m']): sorted(set(p.get('Points', [])))
                     for p in data.get('notice_points', [])}
    sections = {}
    for member_id, member in data.get('member', {}).items():
        start = np.array([nodes[str(member['ni'])][k] for k in ('x','y','z')])
        end = np.array([nodes[str(member['nj'])][k] for k in ('x','y','z')])
        length = np.linalg.norm(end-start)
        axis = (end-start)/length
        rigid_points = [p for r in data.get('rigid', []) if str(r['m']) == member_id
                        for p in (float(r.get('Ilength', 0)), length-float(r.get('Jlength', 0)))
                        if 0 < p < length]
        points = []
        for point in sorted([0.,length]+rigid_points+[p for p in member_points.get(member_id, []) if 0 < p < length]):
            if not points or point-points[-1] > 1e-10*max(1.,length):
                points.append(point)
        candidates = []
        for node, coord in model.mesh.nodes.items():
            t = np.dot(coord-start, axis)
            if (node not in labels and 0 < t < length and
                    np.linalg.norm(coord-start-t*axis) < 1e-8*max(1.,length)):
                candidates.append((t,node))
        n_count = l_count = 0
        for t, node in sorted(candidates):
            if any(abs(t-p) < 1e-8*max(1.,length) for p in points[1:-1]):
                n_count += 1
                labels[node] = f'{member_id}n{n_count}'
            else:
                l_count += 1
                labels[node] = f'{member_id}l{l_count}'
        elements = []
        for key, element in model.elements.items():
            mesh_data = model.mesh.elements[key]
            if str(mesh_data.get('original_id', key)) != member_id or not hasattr(element, 'calculate_forces'):
                continue
            ti = np.dot(model.mesh.nodes[element.node_ids[0]]-start, axis)
            tj = np.dot(model.mesh.nodes[element.node_ids[1]]-start, axis)
            elements.append((ti,tj,str(key)))
        segments = {}
        for i, (a,b) in enumerate(zip(points[:-1], points[1:]), 1):
            inside = sorted(e for e in elements if e[0] >= a-1e-8 and e[1] <= b+1e-8)
            if not inside:
                continue  # missing segment remains a key mismatch, never a pass
            fi = result['element_stresses'][inside[0][2]]['i_end']
            fj = result['element_stresses'][inside[-1][2]]['j_end']
            signs = [-1,1,1,-1,-1,1]
            values = {k+'i': v*s for k,v,s in zip(('fx','fy','fz','mx','my','mz'),fi,signs)}
            values.update({k+'j': -v*s for k,v,s in zip(('fx','fy','fz','mx','my','mz'),fj,signs)})
            values['L'] = b-a
            segments[f'P{i}'] = values
        sections[member_id] = segments
    displacement = {labels.get(int(k), f'unmapped:{k}'): v for k,v in result['node_displacements'].items()
                    if labels.get(int(k), f'unmapped:{k}') is not None}
    reactions = {k: {out: result['reaction_forces'].get(k, {}).get(src, 0.)
                     for out,src in zip(('tx','ty','tz','mx','my','mz'),('fx','fy','fz','mx','my','mz'))}
                 for k in nodes if int(k) in model.boundary.restraints
                 and int(k) not in getattr(model.boundary, 'auxiliary_restraint_nodes', set())
                 or int(k) in getattr(model.boundary, 'spring_supports', {})}
    if data.get('dimension') == 2:
        generated = sorted(set(model.mesh.nodes)-{int(k) for k in nodes})
        if generated:
            reaction = result['reaction_forces'].get(str(generated[-1]), {})
            reactions['0'] = dict(tx=0., ty=0., tz=0., mx=0., my=0., mz=reaction.get('mz', 0.))
    return dict(disg=displacement, reac=reactions, fsec=sections, size=len(model.mesh.nodes),
                shell_results=result.get('legacy_shell_results', result.get('shell_results', {})))


def comparison_errors(actual, expected, path=''):
    """Visit all common numeric leaves even when another key/quantity fails."""
    errors = []
    if isinstance(actual, dict) and isinstance(expected, dict):
        missing, extra = expected.keys()-actual.keys(), actual.keys()-expected.keys()
        if missing or extra:
            errors.append(f'{path}: missing={sorted(missing)}, extra={sorted(extra)}')
        for key in sorted(actual.keys() & expected.keys(), key=str):
            errors.extend(comparison_errors(actual[key], expected[key], f'{path}/{key}'))
    else:
        try:
            assert_dict_almost_equal(actual, expected, path)
        except AssertionError as error:
            errors.append(str(error))
    return errors


def compare_legacy_result(result, expected, model, data):
    actual = legacy_result_view(result, model, data)
    errors = []
    for field in actual.keys() | expected.keys():
        if field not in actual:
            errors.append(f'Missing output field: {field}')
            continue
        if field not in expected:
            errors.append(f'Missing reference field: {field}')
        else:
            errors.extend(comparison_errors(actual[field], expected[field], field))
    assert not errors, f'{len(errors)} legacy mismatches; first 12:\n' + '\n'.join(errors[:12])


def run_sample(data_path):
    data = json.loads(Path(data_path).read_text(encoding='utf-8'))
    if Path(data_path).name == 'beam001.json' and 'reference' not in data:
        from reference_solutions import assert_beam001
        m = FemModel()
        m.load_model(str(data_path))
        result = m.run()
        assert_beam001(result_to_jsonable(result), data)
        if not data.get('result'):
            return result
        if '0' in data['result']:
            # The zero key explicitly identifies load-step snapshots, not a
            # legacy load case. The independent oracle above covers ALL steps;
            # additionally compare every user-provided snapshot/component.
            snapshots = {str(step['step']): step for step in result['step_results']}
            m.analysis_params['load_factors'] = [0.]
            snapshots['0'] = m.run()['step_results'][0]
            for key, reference in data['result'].items():
                assert key in snapshots, f'Unknown reference load step {key}'
                assert {'disg','reac','fsec'} <= reference.keys(), f'Missing physical reference field at step {key}'
                actual = legacy_result_view(snapshots[key], m, data)
                for field, values in reference.items():
                    assert field in actual, f'Missing output field: {field}'
                    assert_dict_almost_equal(actual[field], values, f'step/{key}/{field}')
            return result
    if 'reference' in data:
        m = FemModel()
        m.load_model(str(data_path))
        result = m.run()
        assert_acceptance_result(result_to_jsonable(result), data['reference'], m.analysis_params)
        return result
    expected = data.get('result')
    assert expected, f'Independent reference (期待結果) is missing: {data_path}'
    if 'node_displacements' in expected:
        m = FemModel()
        m.load_model(str(data_path))
        result = m.run()
        assert_dict_almost_equal(result_to_jsonable(result), expected)
        return result
    for case_id, reference in expected.items():
        if 'load' not in data:
            assert case_id == '1', f'Unknown reference load case {case_id}'
            m = FemModel()
            m.read_json_model(_read_json_model(copy.deepcopy(data)))
            result = m.run()
            compare_legacy_result(result, reference, m, data)
            continue
        assert case_id in data['load'], f'Unknown reference load case {case_id}'
        from src.fem.legacy_beam import select_case
        case_data = select_case(data, case_id)
        case = case_data['load'][case_id]
        case_data['load'] = {case_id: case}
        for field in ('fix_node', 'fix_member', 'element', 'joint'):
            if field in case_data and case_data[field]:
                key = str(case.get(field, case_id))
                assert key in case_data[field], f'Missing {field} case {key}'
                case_data[field] = {key: case_data[field][key]}
        m = FemModel()
        m.read_json_model(_read_json_model(case_data))
        result = m.run()
        compare_legacy_result(result, reference, m, case_data)
    return result


def assert_acceptance_result(result, reference, controls):
    """Independent physical reference schema; iteration counts use criteria.

    Every displacement, reaction and end force is compared at every step.
    Raw displacement is checked against the nodal view, and convergence records
    must terminate within the requested tolerance and iteration budget.
    """
    fields = ('node_displacements', 'reaction_forces', 'element_stresses', 'converged')
    assert set(reference) == set(fields) | {'analysis_type', 'step_results'}
    actual = {k: result[k] for k in fields}
    actual['analysis_type'] = result['analysis_type']
    actual['step_results'] = [{k: s[k] for k in fields + ('step', 'lambda')}
                              for s in result['step_results']]
    assert_dict_almost_equal(actual, reference)
    for s in result['step_results']:
        flattened = [v for node in sorted(s['node_displacements'], key=int)
                     for v in s['node_displacements'][node].values()]
        assert_dict_almost_equal(s['displacement'], flattened, 'raw displacement')
        history = [c for c in result['convergence_history'] if c['step'] == s['step']]
        assert len(history) == s['iterations']
        assert 1 <= s['iterations'] <= controls.get('max_iterations', 50)
        assert history[-1]['relative_residual'] < controls.get('tolerance', 1e-6)
        if history[-1]['relative_du'] is not None:
            assert history[-1]['relative_du'] < controls.get('tolerance', 1e-6)
