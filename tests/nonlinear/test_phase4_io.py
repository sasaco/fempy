"""Phase four acceptance: public input routes and independent axial solution."""
import copy
import json

import numpy as np
import pytest

from src.fem.file_io import _read_json_model
from src.fem.model import FemModel
from src.fem.material import BarParameter
from main import app


def axial_json(force=12, **controls):
    # L=2; N(e): (0,0),(.001,10),(.004,16),(.010,22).
    # N=12 gives e=.002, u=.004, R=-12 independently.
    return {
        'node': {'10': {'x': 0, 'y': 0, 'z': 0},
                 '30': {'x': 2, 'y': 0, 'z': 0}},
        'member': {'7': {'ni': 10, 'nj': 30, 'e': 1, 'cg': 0}},
        'element': {'1': {'1': {
            'E': 10000, 'nu': .25, 'A': 1, 'Iy': 1, 'Iz': 1, 'J': 1,
            'nonlinear': {'type': 'jr_stiffness_reduction', 'delta_1': .001,
                          'delta_2': .004, 'delta_3': .010,
                          'P_1': 10, 'P_2': 16, 'P_3': 22, 'beta': 0,
                          'hysteresis_dofs': ['axial']}}}},
        'fix_node': {'1': [dict(n=10, tx=1, ty=1, tz=1, rx=1, ry=1, rz=1)]},
        'load': {'1': dict(load_node=[dict(n=30, tx=force)],
                          n_load_steps=4, max_iterations=50, tolerance=1e-10,
                          **controls)},
    }


def python_axial(force=12):
    m = FemModel()
    m.add_node(10, 0, 0, 0)
    m.add_node(30, 2, 0, 0)
    m.add_nonlinear_material(1, 'reference', 10000, .001, .004, .010,
                             10, 16, 22, beta=0, nu=.25)
    m.material.add_bar_parameter(1, BarParameter(1, 1, 1, 1))
    m.add_nonlinear_bar_element(7, [10, 30], 1, 1, ['axial'])
    m.add_restraint(10, True, True, True, True, True, True)
    m.add_load(30, fx=force)
    m.analysis_params.update(n_load_steps=4, max_iterations=50, tolerance=1e-10)
    return m


def json_model(data):
    m = FemModel()
    m.read_json_model(_read_json_model(copy.deepcopy(data)))
    return m


def wire(value):
    if isinstance(value, dict):
        return {str(k): wire(v) for k, v in value.items()}
    if isinstance(value, (list, tuple, np.ndarray)):
        return [wire(v) for v in value]
    if isinstance(value, np.generic):
        return value.item()
    return value


def assert_axial(result, force=12, displacement=.004):
    r = wire(result)
    assert r['converged'] is True
    assert r['analysis_type'] == 'material_nonlinear'
    assert r['node_displacements']['30']['dx'] == pytest.approx(displacement, abs=1e-10)
    np.testing.assert_allclose(list(r['node_displacements']['10'].values()), 0, atol=1e-12)
    assert r['reaction_forces']['10']['fx'] == pytest.approx(-force, abs=1e-9)
    np.testing.assert_allclose(r['element_stresses']['7']['i_end'], [-force, 0, 0, 0, 0, 0], atol=1e-9)
    np.testing.assert_allclose(r['element_stresses']['7']['j_end'], [force, 0, 0, 0, 0, 0], atol=1e-9)
    assert len(r['step_results']) == 4
    assert all(s['converged'] for s in r['step_results'])
    for step in r['step_results']:
        last = [c for c in r['convergence_history'] if c['step'] == step['step']][-1]
        assert last['relative_residual'] < 1e-10


def test_python_json_file_http_equivalence(tmp_path):
    data = axial_json()
    path = tmp_path / 'axial.json'
    path.write_text(json.dumps(data), encoding='utf-8')
    models = [python_axial(), json_model(data), FemModel()]
    models[-1].load_model(str(path))
    results = [m.run() for m in models]
    response = app.test_client().post('/', json=data)
    assert response.status_code == 200
    results.append(json.loads(response.data))
    from run_sample import assert_dict_almost_equal
    for r in results:
        assert_axial(r)
        assert_dict_almost_equal(wire(r), wire(results[0]))
        assert wire(r) == wire(results[0])  # same input route: exact numerical identity


@pytest.mark.parametrize('analysis_type', ['material_nonlinear', 'static', 'unknown'])
def test_http_explicit_analysis_type(analysis_type):
    data = axial_json()
    data['analysis_type'] = analysis_type
    response = app.test_client().post('/', json=data)
    body = json.loads(response.data)
    if analysis_type == 'unknown':
        assert response.status_code == 400
        assert body['converged'] is False
    else:
        assert response.status_code == 200
        assert body['analysis_type'] == analysis_type
        assert body['node_displacements']['30']['dx'] == pytest.approx(
            .004 if analysis_type == 'material_nonlinear' else .0024)
        assert body['element_stresses']['7']['j_end'][0] == pytest.approx(12)
        assert body['element_stresses']['7']['i_end'][0] == pytest.approx(-12)
        assert body['reaction_forces']['10']['fx'] == pytest.approx(-12)


@pytest.mark.parametrize('change', ['iterations', 'tolerance', 'material', 'law', 'dof', 'load', 'empty'])
def test_http_invalid_input_is_not_success(change):
    data = axial_json()
    if change == 'iterations':
        data['load']['1']['max_iterations'] = 0
    elif change == 'tolerance':
        data['load']['1']['tolerance'] = float('nan')
    elif change == 'material':
        data['element']['1']['1']['nonlinear']['delta_2'] = .0001
    elif change == 'law':
        data['element']['1']['1']['nonlinear']['type'] = 'typo'
    elif change == 'dof':
        data['element']['1']['1']['nonlinear']['hysteresis_dofs'] = ['typo']
    elif change == 'load':
        data['load']['1']['load_node'][0]['tx'] = float('inf')
    else:
        data = {}
    response = app.test_client().post('/', json=data)
    assert response.status_code == 400
    body = json.loads(response.data)
    assert body['converged'] is False
    assert 'node_displacements' not in body


@pytest.mark.parametrize('mode', ['capacity', 'singular', 'iterations', 'zero_singular'])
def test_http_failed_analysis_and_reanalysis(mode):
    data = axial_json(30 if mode == 'capacity' else 12)
    if 'singular' in mode:
        data['fix_node'] = {}
    if mode == 'zero_singular':
        data['load']['1']['load_node'][0]['tx'] = 0
    if mode == 'iterations':
        data['load']['1']['max_iterations'] = 1
    client = app.test_client()
    response = client.post('/', json=data)
    assert response.status_code == 422
    body = json.loads(response.data)
    assert body['converged'] is False
    assert body['error_code'] == 'nonlinear_nonconvergence'
    assert 'node_displacements' not in body
    good = client.post('/', json=axial_json())
    assert good.status_code == 200
    assert_axial(json.loads(good.data))


def test_comparator_rejects_missing_extra_wrong_sign_and_number():
    from run_sample import assert_dict_almost_equal
    expected = {'u': {'dx': .004}, 'reaction': [-12., 0.], 'converged': True}
    for actual in [dict(expected, u={}), dict(expected, extra=0),
                   dict(expected, u={'dx': .005}), dict(expected, reaction=[12., 0.]),
                   dict(expected, converged=False), dict(expected, reaction=[-12.])]:
        with pytest.raises(AssertionError):
            assert_dict_almost_equal(actual, expected)


def test_missing_sample_reference_never_writes(tmp_path):
    from run_sample import run_sample
    path = tmp_path / 'missing.json'
    path.write_text(json.dumps(axial_json()), encoding='utf-8')
    before = path.read_bytes()
    with pytest.raises(AssertionError, match='reference|期待'):
        run_sample(str(path))
    assert path.read_bytes() == before


def test_model_save_load_preserves_nonlinear_input_and_result(tmp_path):
    # A saved model must retain the law, section, springs and load sequence.
    m = python_axial(20)
    m.add_spring_support(30, 'x', 2000)
    m.analysis_params['load_factors'] = [0., .5, 1., 0.]
    expected = wire(m.run())
    model_path, result_path = tmp_path/'model.json', tmp_path/'result.json'
    m.save_model(str(model_path))
    m.save_results(str(result_path))
    new = FemModel()
    new.load_model(str(model_path))
    actual = wire(new.run())
    from run_sample import assert_dict_almost_equal
    assert_dict_almost_equal(actual, expected)
    assert_dict_almost_equal(json.loads(result_path.read_text(encoding='utf-8')), expected)


@pytest.mark.parametrize('factors', [[], [0, float('nan')], [[1,2]], '123'])
def test_invalid_load_factors_rejected(factors):
    d = axial_json()
    d['analysis_params'] = {'load_factors': factors}
    r = app.test_client().post('/', json=d)
    assert r.status_code == 400


def test_real_jr_failure_restores_last_commit_and_fresh_run():
    m = python_axial(30)
    m.analysis_params['load_factors'] = [.4, 1]
    with pytest.raises(RuntimeError, match='converge') as failure:
        m.run()
    assert failure.value.step == 2
    assert m.results is None


    assert m.nonlinear_solver.displacement[6] == pytest.approx(.004, abs=1e-12)
    element = m.elements[7]
    committed = element.committed_states['axial']['center']
    assert committed.current_delta == pytest.approx(.002, abs=1e-12)
    assert committed.current_P == pytest.approx(12, abs=1e-10)
    assert element.current_states == element.committed_states
    np.testing.assert_allclose(failure.value.displacement, m.nonlinear_solver.displacement)
    m.boundary.loads.clear()
    m.add_load(30, fx=12)
    m.analysis_params.pop('load_factors')
    assert_axial(m.run())
    m.analysis_params['max_iterations'] = 1
    with pytest.raises(RuntimeError):
        m.run()
    assert m.results is None


@pytest.mark.parametrize('sample', ['shellRibQuad1.json', 'shellRibTri1.json'])
def test_failed_postprocessing_clears_result_and_http_is_error(sample, monkeypatch):
    from pathlib import Path
    d = json.loads((Path(__file__).parents[1]/'data/shell'/sample).read_text(encoding='utf-8'))
    m = json_model(d)
    # The vertical-plane defect is fixed; retain the error-propagation contract
    # with a deterministic postprocessing failure, not a permanent defect.
    assert m.run()['element_stresses']
    from src.fem.elements.shell_element import ShellElement
    def fail(self, displacement):
        raise np.linalg.LinAlgError('injected postprocessing failure')
    monkeypatch.setattr(ShellElement, 'calculate_stress_strain', fail)
    from fem.elements.shell_element import ShellElement as HttpShellElement
    monkeypatch.setattr(HttpShellElement, 'calculate_stress_strain', fail)
    with pytest.raises(np.linalg.LinAlgError):
        m.run()
    assert m.results is None
    response = app.test_client().post('/', json=d)
    assert response.status_code == 500
    assert json.loads(response.data)['converged'] is False


def test_json_does_not_add_unrequested_rotational_restraint():
    d = axial_json(0)
    d['fix_node']['1'][0]['rz'] = 0
    m = json_model(d)
    assert m.boundary.restraints[10].dof_restraints[5] is False
    response = app.test_client().post('/', json=d)
    assert response.status_code == 422


@pytest.mark.parametrize('field', ['u', 'reaction', 'section', 'missing', 'convergence'])
def test_sample_comparison_really_rejects_corrupted_reference(tmp_path, field):
    from run_sample import run_sample
    from reference_solutions import axial_acceptance_reference
    d = axial_json()
    d['reference'] = axial_acceptance_reference()
    path = tmp_path/'acceptance.json'
    path.write_text(json.dumps(d), encoding='utf-8')
    run_sample(str(path))  # correct independent reference must pass first
    reference = d['reference']
    if field == 'u':
        reference['node_displacements']['30']['dx'] *= 2
    elif field == 'reaction':
        reference['reaction_forces']['10']['fx'] *= -1
    elif field == 'section':
        reference['element_stresses']['7']['j_end'][0] *= -1
    elif field == 'missing':
        del reference['node_displacements']['30']['dx']
    else:
        reference['converged'] = False
    path.write_text(json.dumps(d), encoding='utf-8')
    before = path.read_bytes()
    with pytest.raises(AssertionError):
        run_sample(str(path))
    assert path.read_bytes() == before


def test_explicit_static_spring_reactions_balance():
    d = axial_json(5)
    d['analysis_type'] = 'static'
    d['boundary_conditions'] = {'spring_supports': {'30': {'x': 2000}}}
    r = app.test_client().post('/', json=d)
    assert r.status_code == 200
    body = json.loads(r.data)
    assert body['node_displacements']['30']['dx'] == pytest.approx(5/7000, abs=1e-10)
    assert body['reaction_forces']['10']['fx'] == pytest.approx(-25/7)
    assert body['reaction_forces']['30']['fx'] == pytest.approx(-10/7)


def test_omitted_nu_has_same_nonlinear_default_in_python_json_and_http():
    d = axial_json(0)
    del d['element']['1']['1']['nu']
    d['load']['1']['load_node'][0]['ty'] = 1
    m = python_axial(0)
    m.add_nonlinear_material(1, 'reference', 10000, .001, .004, .010, 10, 16, 22, beta=0)
    m.add_load(30, fy=1)
    results = [wire(m.run()), wire(json_model(d).run())]
    response = app.test_client().post('/', json=d)
    assert response.status_code == 200
    results.append(json.loads(response.data))
    # Existing Python nonlinear default nu=.2 -> G=10000/2.4.
    expected = 8/30000 + 2/((10000/2.4)*5/6)
    for r in results:
        assert r['node_displacements']['30']['dy'] == pytest.approx(expected, abs=1e-10)


def test_failed_file_reload_does_not_leave_previous_success(tmp_path):
    m = python_axial()
    assert_axial(m.run())
    path = tmp_path/'invalid.json'
    path.write_text('{}', encoding='utf-8')
    with pytest.raises(ValueError):
        m.load_model(str(path))
    assert m.get_results() is None
