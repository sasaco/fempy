"""Public static spatial loading: output, conservation and failure recovery."""
from copy import deepcopy
from dataclasses import replace

import numpy as np
import pytest

from fem.model import FemModel
from fem.spatial_loads import SpatialLoadDefinitions, SpatialLoadPath
from fem import solver as solver_module
from fem import equilibrium
from tests.support.assertions import assert_dict_almost_equal
from tests.support.builders.spatial_loads import solver_panel, solve_spatial_internal, square_definitions
from tests.support.oracles.spatial_loads import square_strip_loads
from tests.support.serialization import wire

pytestmark = pytest.mark.integration


@pytest.fixture(params=['internal', 'public'], autouse=True)
def spatial_entry(request):
    """Keep the internal contracts and exercise them through public preflight."""
    with pytest.MonkeyPatch.context() as patch:
        if request.param == 'public':
            patch.setattr(__import__(__name__, fromlist=['']), 'solve_spatial_internal',
                          lambda model: model.run())
        yield


@pytest.mark.parametrize('shell', [False, True])
@pytest.mark.parametrize('area', [False, True])
@pytest.mark.parametrize('fixed', [False, True])
def test_internal_spatial_solve_matches_independent_nodal_loads_and_all_outputs(shell, area, fixed):
    spatial = solver_panel(shell=shell, area=area, fixed=fixed)
    direct = solver_panel(shell=shell, area=area, fixed=fixed)
    if not fixed:
        for model in (spatial, direct):
            model.add_restraint(1, True, True, True, True, True, True)
    direct.set_spatial_loads(SpatialLoadDefinitions())
    for node, force in enumerate(square_strip_loads(shell=shell, area=area), 1):
        direct.add_load(node, fz=force)
    expected = direct.run()
    result = solve_spatial_internal(spatial)
    for field in ('node_displacements', 'reaction_forces', 'element_stresses', 'shell_results', 'legacy_shell_results'):
        if field in expected:
            assert_dict_almost_equal(wire(result[field]), wire(expected[field]))
    np.testing.assert_allclose(spatial.solver.load_vector, direct.solver.load_vector, atol=2e-14)
    if not shell:
        assert result['spatial_load_contribution']['shell_element_loads'] == {}
        assert result['element_nodal_equilibrium_forces'] == {}
    else:
        vector = np.zeros_like(spatial.solver.load_vector)
        for record in result['element_nodal_equilibrium_forces'].values():
            for node, values in zip(record['node_ids'], record['forces']):
                start = spatial.solver.layout.node_offsets[node]
                vector[start:start+6] += values
        # Shell element equilibrium excludes the ordinary point load at node 2.
        vector[8] -= -3
        for node, start in spatial.solver.layout.node_offsets.items():
            reaction = result['reaction_forces'].get(node, {})
            np.testing.assert_allclose(vector[start:start+6], [reaction.get(k, 0.) for k in
                                       ('fx', 'fy', 'fz', 'mx', 'my', 'mz')], atol=2e-9)


@pytest.mark.parametrize('shell', [False, True])
def test_one_compilation_per_solve_and_no_accumulation_on_assembly_rerun_reload(monkeypatch, tmp_path, shell):
    model = solver_panel(shell=shell)
    original_input = deepcopy(model.boundary.loads[2].forces)
    original_definitions = model.boundary.spatial_loads
    compiler = solver_module.compile_spatial_loads
    calls = []

    def counted(*args, **kwargs):
        calls.append(1)
        return compiler(*args, **kwargs)

    monkeypatch.setattr(solver_module, 'compile_spatial_loads', counted)
    result = solve_spatial_internal(model)
    assert len(calls) == 1
    baseline = model.solver.load_vector.copy()
    snapshot = deepcopy(result['spatial_load_contribution'])
    for _ in range(2):
        np.testing.assert_array_equal(model.solver.assemble_load_vector(model.mesh, model.boundary, model.elements), baseline)
    result['spatial_load_contribution']['node_loads'][1][2] = 999
    assert model.solver.spatial_load_contribution.to_dict() == snapshot
    result = solve_spatial_internal(model)
    assert result['spatial_load_contribution'] == snapshot
    assert model.boundary.spatial_loads is original_definitions
    np.testing.assert_array_equal(model.boundary.loads[2].forces, original_input)
    saved = tmp_path / 'spatial.json'
    model.save_model(str(saved))
    restored = FemModel()
    restored.load_model(str(saved))
    assert solve_spatial_internal(restored)['spatial_load_contribution'] == snapshot
    np.testing.assert_array_equal(restored.solver.load_vector, baseline)


@pytest.mark.parametrize('failure', ['geometry', 'stiffness', 'solve', 'postprocess', 'public_preflight'])
def test_failed_analysis_discards_compilation_and_retry_is_clean(monkeypatch, failure):
    model = solver_panel()
    baseline = solve_spatial_internal(model)['spatial_load_contribution']
    definitions = model.boundary.spatial_loads
    if failure == 'geometry':
        model.boundary.spatial_loads = replace(definitions, paths=(
            SpatialLoadPath(1, [(-1, .5, 0), (2, .5, 0)]), *definitions.paths[1:]))
        monkeypatch.setattr(model.solver, 'create_stiffness_matrix', lambda *a: pytest.fail('geometry must fail before K'))
        operation = lambda: solve_spatial_internal(model)
    elif failure in ('stiffness', 'solve'):
        def fail(*args, **kwargs):
            raise ValueError('deliberate analysis failure')
        if failure == 'stiffness':
            monkeypatch.setattr(model.solver, 'create_stiffness_matrix', fail)
        else:
            monkeypatch.setattr(solver_module, 'direct_step', fail)
        operation = lambda: solve_spatial_internal(model)
    elif failure == 'postprocess':
        # Exercise the real public cleanup after a compiled solver result.
        def fail_run(*args):
            raise ValueError('deliberate postprocess failure')
        monkeypatch.setattr(model, '_post_process_results', fail_run)
        operation = model.run
    else:
        operation = lambda: model.run('modal')
    with pytest.raises(ValueError):
        operation()
    assert model.solver.spatial_load_contribution is None
    assert model.solver._shell_direct_loads == {}
    assert model.solver.load_vector is None
    assert all('spatial_load_contribution' not in step for step in model.solver.step_results)
    monkeypatch.undo()
    model.boundary.spatial_loads = definitions
    assert solve_spatial_internal(model)['spatial_load_contribution'] == baseline


@pytest.mark.parametrize('analysis', ['modal', 'material_nonlinear'])
def test_internal_nonstatic_analysis_also_rejects_before_matrices(monkeypatch, analysis):
    model = solver_panel()
    solve_spatial_internal(model)
    monkeypatch.setattr(model.solver, 'create_stiffness_matrix', lambda *a: pytest.fail('no K assembly'))
    with pytest.raises(ValueError, match='static') as caught:
        if analysis == 'modal':
            model.solver.eigenvalue_analysis(model.mesh, model.material, model.boundary, model.elements)
        else:
            model.solver.solve(model.mesh, model.material, model.boundary, model.elements, analysis_type=analysis)
    assert caught.value.details['panel_ids'] == [7]
    assert caught.value.details['load_ids'] == [1]
    assert model.solver.spatial_load_contribution is None


def test_removing_spatial_input_clears_solver_and_leaves_original_result_schema():
    model = solver_panel()
    solve_spatial_internal(model)
    model.set_spatial_loads(SpatialLoadDefinitions())
    result = model.run()
    assert 'spatial_load_contribution' not in result
    assert 'element_nodal_equilibrium_forces' not in result
    assert model.solver.spatial_load_contribution is None
    assert model.solver.load_vector[8] == -3
    assert np.count_nonzero(model.solver.load_vector) == 1


@pytest.mark.parametrize('pressure_route', ['pressure', 'distributed'])
def test_mixed_shell_pressure_and_spatial_loads_retain_both_direct_terms(pressure_route):
    model = solver_panel(shell=True)
    if pressure_route == 'pressure':
        model.boundary.add_pressure(1, 'F2', 3.)
    else:
        model.boundary.add_distributed_load(1, 'pressure', [3.], 'F2')
    result = solve_spatial_internal(model)
    # Every node is fixed: Ke*u=0; output is the negative direct element load.
    forces = np.array(result['element_nodal_equilibrium_forces'][1]['forces'])
    expected = square_strip_loads(shell=True, area=True) + 3.
    np.testing.assert_allclose(forces[:, 2], -expected, atol=1e-13)
    np.testing.assert_array_equal(forces[:, [0, 1, 3, 4, 5]], 0)
    assert result['spatial_load_contribution']['resultant'][2] == pytest.approx(50.)


def test_loading_triangles_on_shell_nodes_are_not_direct_shell_pressure():
    model = solver_panel(shell=True)
    definitions = model.boundary.spatial_loads
    panel = replace(definitions.panels[0], elements=(), triangles=((1, 2, 3), (1, 3, 4)))
    model.set_spatial_loads(replace(definitions, panels=(panel,)))
    result = solve_spatial_internal(model)
    assert result['spatial_load_contribution']['shell_element_loads'] == {}
    np.testing.assert_array_equal(result['element_nodal_equilibrium_forces'][1]['forces'], 0)
    assert sum(r['fz'] for r in result['reaction_forces'].values()) == pytest.approx(-47.)


def test_uniform_shell_spatial_area_matches_existing_pressure_displacements_and_reactions():
    spatial = solver_panel(shell=True, fixed=False)
    pressure = solver_panel(shell=True, fixed=False)
    for model in (spatial, pressure):
        model.add_restraint(1, True, True, True, True, True, True)
    panel = spatial.boundary.spatial_loads.panels[0]
    spatial.set_spatial_loads(square_definitions(panel, coefficients=(3, 0, 0, 0)))
    pressure.set_spatial_loads(SpatialLoadDefinitions())
    pressure.boundary.add_pressure(1, 'F2', 3.)
    result, expected = solve_spatial_internal(spatial), pressure.run()
    for field in ('node_displacements', 'reaction_forces', 'shell_results', 'legacy_shell_results'):
        assert_dict_almost_equal(wire(result[field]), wire(expected[field]))


def test_high_precision_beam_fallback_retains_spatial_external_forces(monkeypatch):
    spatial = solver_panel(fixed=False)
    direct = solver_panel(fixed=False)
    for model in (spatial, direct):
        model.add_restraint(1, True, True, True, True, True, True)
    direct.set_spatial_loads(SpatialLoadDefinitions())
    for node, force in enumerate(square_strip_loads(shell=False, area=True), 1):
        direct.add_load(node, fz=force)
    expected = direct.run()

    def require_precision(*args):
        raise ValueError('Linear frame requires high precision')

    monkeypatch.setattr(equilibrium, '_direct_step', require_precision)
    result = solve_spatial_internal(spatial)
    assert spatial.solver.precise_end_forces is not None
    for field in ('node_displacements', 'reaction_forces', 'element_stresses'):
        assert_dict_almost_equal(wire(result[field]), wire(expected[field]))
    assert result['spatial_load_contribution']['resultant'][2] == pytest.approx(50.)


@pytest.mark.parametrize('shell', [False, True])
def test_result_json_preserves_spatial_snapshot_and_equilibrium_output(tmp_path, shell):
    model = solver_panel(shell=shell)
    result = solve_spatial_internal(model)
    saved = tmp_path / 'results.json'
    model.save_results(str(saved))
    restored = FemModel()
    restored.load_results(str(saved))
    for field in ('spatial_load_contribution', 'element_nodal_equilibrium_forces'):
        assert_dict_almost_equal(wire(restored.results[field]), wire(result[field]))


def test_line_on_two_cantilever_tips_matches_point_force_closed_form():
    from fem.file_io import _read_json_model
    from tests.support.builders.spatial_loads import legacy_panel

    data = legacy_panel()
    data['member'] = {'1': dict(ni=1, nj=2, e=1, shear_correction=False),
                      '2': dict(ni=4, nj=3, e=1, shear_correction=False)}
    data['line']['1']['position'] = [dict(x=2, y=0), dict(x=2, y=2)]
    data['load']['1'].update(load_inf=[dict(L1=1, P11=3, P12=3)], load_node=[])
    model = FemModel()
    model.read_json_model(_read_json_model(data))
    for node in (1, 4):
        model.add_restraint(node, True, True, True, True, True, True)
    result = solve_spatial_internal(model)
    for node in (2, 3):
        assert result['node_displacements'][node]['dz'] == pytest.approx(3*2**3/(3*1000), abs=1e-14)
        assert result['node_displacements'][node]['ry'] == pytest.approx(-3*2**2/(2*1000), abs=1e-14)
    for node in (1, 4):
        assert result['reaction_forces'][node]['fz'] == pytest.approx(-3., abs=1e-13)
        assert result['reaction_forces'][node]['my'] == pytest.approx(6., abs=1e-13)
