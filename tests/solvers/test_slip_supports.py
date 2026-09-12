"""Equilibrium, zero stiffness and lifetime of grounded hysteretic supports."""
from copy import deepcopy

import numpy as np
import pytest
from scipy.sparse import csr_matrix

from fem.boundary_dofs import BoundaryDofs
from fem.equilibrium import NonlinearConvergenceError
from fem.file_io import _read_json_model
from fem.model import FemModel
from fem.solver import Solver
from tests.support.builders.slip_support import slip_model_data, slip_definition
from tests.support.builders.nonlinear_solver import axial_bar, CubicBar

pytestmark = pytest.mark.integration

FORCES = [5., 12., 6., 0., 0., 7., 0., -5., -12., -6., 0., 0., 6., 13.]


@pytest.mark.parametrize('direction, kb', [('x', 3000), ('y', 3000), ('z', 3000),
                                        ('rx', 400), ('ry', 4000), ('rz', 4000)])
def test_parallel_beam_support_balance_both_controls(direction, kb):
    raw = slip_model_data(direction)
    targets = raw['analysis_params']['displacement_control']['targets']
    expected = kb*np.array(targets)+FORCES
    model = FemModel()
    model.read_json_model(_read_json_model(raw))
    controlled = model.run()
    assert controlled['analysis_type'] == 'material_nonlinear'
    force_name = {'x': 'fx', 'y': 'fy', 'z': 'fz', 'rx': 'mx', 'ry': 'my', 'rz': 'mz'}[direction]
    index = ['x', 'y', 'z', 'rx', 'ry', 'rz'].index(direction)
    for target, force, factor, step in zip(targets, FORCES, expected, controlled['step_results']):
        assert step['lambda'] == pytest.approx(factor, abs=1e-9)
        assert step['support_response'][30][direction]['force'] == pytest.approx(force, abs=1e-10)
        assert step['reaction_forces'][30][force_name] == pytest.approx(-force, abs=1e-9)
        assert step['displacement'][6+index] == pytest.approx(target, abs=1e-12)
        assert step['element_stresses'][5]['j_end'][index] == pytest.approx(kb*target, abs=1e-9)
    raw['analysis_params'] = {'load_factors': expected.tolist(), 'tolerance': 1e-10}
    model.read_json_model(_read_json_model(raw))
    loaded = model.run()
    np.testing.assert_allclose([s['displacement'][6+index] for s in loaded['step_results']], targets, atol=1e-12)
    assert loaded['support_response'][30][direction]['force'] == pytest.approx(13.)
    assert loaded['metadata']['solver']['relative_residual'] < 1e-10


def test_boundary_tangent_residual_and_trial_order_are_consistent():
    from fem.nonlinear.support_springs import SupportSprings
    definition = _read_json_model(slip_model_data())['boundary']
    resolved = BoundaryDofs.from_boundary(definition, 12, 6, {10: 0, 30: 6}.__getitem__)
    runtime = resolved.support_state
    u = np.zeros(12)
    u[6] = .03
    runtime.commit(runtime.evaluate(u))
    committed = runtime.snapshot()
    # Large discarded candidates must not change the next unload line.
    u[6] = 10.
    resolved.residual(np.zeros(12), u)
    u[6] = .024
    k = resolved.add_spring_stiffness(csr_matrix((12, 12)), u)
    assert k[6, 6] == 1000.
    assert resolved.residual(np.zeros(12), u)[6] == pytest.approx(-6.)
    assert runtime.snapshot() == committed
    u[6] += 1e-6
    assert resolved.spring_force(u)[6] == pytest.approx(6.001)
    assert runtime.snapshot() == committed


class EmptyBar(CubicBar):
    def get_internal_force(self, u):
        return np.zeros(12)

    def get_tangent_stiffness_matrix(self, u):
        return np.zeros((12, 12))


def spring_only_system(K2=100.):
    args = axial_bar()
    args[2].loads.clear()
    args[2].add_load(2, [1, 0, 0, 0, 0, 0])
    args[2].nonlinear_spring_supports = {2: {'x': dict(slip_definition(), K2=K2)}}
    args[3][1] = EmptyBar()
    return args


def test_displacement_control_can_cross_exact_zero_stiffness():
    args = spring_only_system()
    solver = Solver()
    result = solver.solve(*args, analysis_type='material_nonlinear', displacement_control={
        'node': 2, 'dof': 'dx', 'targets': [.03, .018, .018, .01, -.03, -.018, 0.]})
    assert [s['lambda'] for s in result['step_results']] == pytest.approx([12, 0, 0, 0, -12, 0, 0], abs=1e-10)
    assert result['support_response'][2]['x']['tangent'] == 0.


def test_load_control_rejects_mechanism_on_arrival_even_with_zero_residual():
    args = spring_only_system()
    solver = Solver()
    with pytest.raises(NonlinearConvergenceError) as caught:
        solver.solve(*args, analysis_type='material_nonlinear', load_factors=[12., 0.])
    assert caught.value.step == 2
    assert solver.displacement[6] == pytest.approx(.03)
    assert solver.load_factor == 12.
    assert len(solver.step_results) == 1
    assert solver.support_springs.snapshot()[2]['x']['deformation'] == pytest.approx(.03)
    assert caught.value.details['support_response'][2]['x']['tangent'] == 0.


def test_uncontrolled_mechanism_is_not_resolved_by_displacement_control():
    args = spring_only_system()
    args[2].add_restraint(2, [False, False, True, True, True, True])
    with pytest.raises(NonlinearConvergenceError):
        Solver().solve(*args, analysis_type='material_nonlinear', displacement_control={
            'node': 2, 'dof': 'dx', 'targets': [0., .03]})


def test_perfect_plastic_plateau_and_reversal_predictor():
    args = spring_only_system(0.)
    solver = Solver()
    result = solver.solve(*args, analysis_type='material_nonlinear', displacement_control={
        'node': 2, 'dof': 'dx', 'targets': [.03, .03, .025, .02, 0.]})
    assert [s['lambda'] for s in result['step_results']] == pytest.approx([10, 10, 5, 0, 0], abs=1e-10)
    with pytest.raises(NonlinearConvergenceError):
        solver.solve(*args, analysis_type='material_nonlinear', load_factors=[5., 10.])


def test_reanalysis_snapshot_ownership_and_failed_step_rollback():
    args = axial_bar()
    args[2].nonlinear_spring_supports = {2: {'x': slip_definition()}}
    solver = Solver()
    kwargs = dict(analysis_type='material_nonlinear', displacement_control={
        'node': 2, 'dof': 'dx', 'targets': [.03, .024]})
    result = solver.solve(*args, **kwargs)
    reference = deepcopy(result['support_response'])
    result['support_response'][2]['x']['force'] = 1e6
    result['step_results'][0]['support_response'][2]['x']['zero_pos'] = 99
    assert solver.support_springs.snapshot() == reference
    assert solver.solve(*args, **kwargs)['support_response'] == reference
    with pytest.raises(NonlinearConvergenceError):
        solver.solve(*args, analysis_type='material_nonlinear', max_iter=1, load_factors=[0., 100.])
    assert solver.support_springs.snapshot()[2]['x']['delta_max_pos'] == 0.
    assert solver.load_factor == 0.


@pytest.mark.parametrize('kind', ['static', 'modal'])
def test_low_level_solver_rejects_unsupported_analysis(kind):
    args = spring_only_system()
    solver = Solver()
    with pytest.raises(ValueError, match='[Ss]lip|nonlinear support'):
        if kind == 'static':
            solver.solve(*args)
        else:
            solver.eigenvalue_analysis(*args)


def test_multi_support_trial_failure_is_atomic_and_keeps_independent_histories():
    from fem.nonlinear.support_springs import SupportSprings
    from fem.nonlinear.hysteresis.slip import SlipSpringParams
    params = SlipSpringParams(1000, 100, .01)
    runtime = SupportSprings({0: (10, 'x', params), 1: (30, 'rz', params)})
    runtime.commit(runtime.evaluate(np.array([.03, -.03])))
    expected = runtime.snapshot()
    with pytest.raises(ValueError, match='30.*rz'):
        runtime.evaluate(np.array([.06, float('nan')]))
    runtime.rollback()
    assert runtime.snapshot() == expected
    trial = runtime.evaluate(np.array([.024, -.024]))
    assert [s.force for s in trial.values()] == pytest.approx([6, -6])
    assert trial[0].delta_max_neg == trial[1].delta_max_pos == 0.
    runtime.reset()
    assert runtime.snapshot()[10]['x']['delta_max_pos'] == 0.


def test_local_evaluation_error_has_step_support_location_and_rolls_back(monkeypatch):
    import fem.nonlinear.support_springs as module
    original = module.evaluate_slip

    def failed(params, committed, deformation, **kwargs):
        if deformation > .04:
            raise ValueError('injected local evaluation failure')
        return original(params, committed, deformation, **kwargs)

    monkeypatch.setattr(module, 'evaluate_slip', failed)
    args = spring_only_system()
    solver = Solver()
    with pytest.raises(ValueError) as caught:
        solver.solve(*args, analysis_type='material_nonlinear', displacement_control={
            'node': 2, 'dof': 'dx', 'targets': [.03, .06]})
    assert caught.value.details['step'] == 2
    assert caught.value.details['node'] == 2
    assert caught.value.details['direction'] == 'x'
    assert solver.load_factor == pytest.approx(12)
    assert solver.support_springs.snapshot()[2]['x']['deformation'] == pytest.approx(.03)
    assert solver.displacement[6] == pytest.approx(.03)


def test_support_capability_and_result_metadata_describe_the_law():
    from fem.capabilities import get_capability_registry
    capability = get_capability_registry()['support_types']['slip']
    assert capability['analysis_types'] == ['material_nonlinear']
    assert capability['status'] == 'verified'
    assert capability['directions'] == ['x', 'y', 'z', 'rx', 'ry', 'rz']
    model = FemModel()
    model.read_json_model(_read_json_model(slip_model_data()))
    result = model.run()
    assert result['metadata']['analysis']['support_models'] == ['slip_v1']
