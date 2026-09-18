"""Updated-inertia acceptance through the public, unified nonlinear solver."""
from copy import deepcopy

import numpy as np
import pytest

from fem.boundary_condition import BoundaryCondition
from fem.diagnostics import NumericalConditionError
from fem.equilibrium import NonlinearConvergenceError
from fem.mesh import MeshModel
from fem.nonlinear.hysteresis import JRStiffnessReductionParams
from fem.nonlinear.nonlinear_solver import NonlinearSolver
from tests.support.builders.updated_inertia import specimen
from tests.support.builders.input_routes import json_model
from tests.support.builders.nonlinear_reference import configuration, solve
from tests.integration._canonical_results import (
    assert_step_diagnostics,
    assert_uniform_result,
    load_step_results,
)

pytestmark = [pytest.mark.integration, pytest.mark.material_nonlinear]


def cantilever(axis="z", *, restrained=False, shear=False, fourth=None):
    element = specimen(axis, shear=shear)
    if fourth is not None:
        element.set_hysteresis_model("moment_"+axis, JRStiffnessReductionParams.symmetric(
            .001, .003, .005, .1, .12, .13, beta=0., delta_4=.007, P_4=fourth))
    mesh = MeshModel()
    mesh.add_node(10, [0., 0., 0.])
    mesh.add_node(307, [1., 0., 0.])
    mesh.add_element(1, "bar", [10, 307], 1)
    element.set_node_coordinates(mesh.nodes)
    boundary = BoundaryCondition()
    boundary.add_restraint(10, [True]*6)
    flags = [True]*6
    rotation, translation = (5, 1) if axis == "z" else (4, 2)
    flags[rotation] = False
    flags[translation] = restrained
    boundary.add_restraint(307, flags)
    load = np.zeros(6)
    load[rotation] = 1.
    boundary.add_load(307, load.tolist())
    return mesh, element.material, boundary, {1: element}


@pytest.mark.parametrize("axis", ["y", "z"])
def test_plateau_with_free_transverse_dof_stops_and_rolls_back_entire_state(axis):
    args = cantilever(axis)
    element = args[3][1]
    solver = NonlinearSolver()
    accepted = []
    snapshots = []

    def callback(step):
        accepted.append(step)
        snapshots.append(deepcopy(element.committed_bending_states))

    with pytest.raises(NonlinearConvergenceError) as failure:
        solver.solve_nonlinear(*args, callback=callback, displacement_control={
            "node": 307, "dof": "r"+axis, "targets": [.002, .006, .007]})
    assert failure.value.step == 3
    assert len(accepted) == 2
    assert element.current_bending_states == element.committed_bending_states == snapshots[-1]
    assert element.current_states == element.committed_states
    np.testing.assert_array_equal(element._trial_response[0], element._committed_response[0])
    np.testing.assert_array_equal(element._trial_response[1], element._committed_response[1])
    rotation, translation = (11, 7) if axis == "z" else (10, 8)
    assert solver.displacement[rotation] == pytest.approx(.006)
    assert solver.displacement[translation] == pytest.approx(.003 if axis == "z" else -.003)
    output = element.calculate_forces(solver.displacement)
    assert output["j_end"][rotation-6] == pytest.approx(.13)
    assert output["j_end"][translation-6] == pytest.approx(0., abs=1e-12)


@pytest.mark.parametrize("axis", ["y", "z"])
@pytest.mark.parametrize("sign", [-1., 1.])
def test_restrained_transverse_dof_continues_on_zero_and_negative_fourth_slope(axis, sign):
    for fourth, expected in [(None, [.44, .52, .52]), (.11, [.44, .48, -.08])]:
        args = cantilever(axis, restrained=True, fourth=fourth)
        result = NonlinearSolver().solve_nonlinear(*args, displacement_control={
            "node": 307, "dof": "r"+axis, "targets": (sign*np.array([.002, .006, .020])).tolist()})
        assert [step["lambda"] for step in result["step_results"]] == pytest.approx(sign*np.array(expected))
        assert args[3][1].committed_bending_states["moment_"+axis].moment == pytest.approx(
            sign*(.13 if fourth is None else -.02))


@pytest.mark.parametrize("axis", ["y", "z"])
def test_condensation_failure_has_element_axis_step_and_restores_all_state(axis):
    # GA=1440 (z) / 1920 (y), hence singular B=-120 / -160 for L=1.
    slope = -160. if axis == "y" else -120.
    args = cantilever(axis, restrained=True, shear=True)
    element = args[3][1]
    element.set_hysteresis_model("moment_"+axis, JRStiffnessReductionParams.symmetric(
        .001, .003, .005, .1, .12, .13, beta=0., delta_4=.0055, P_4=.13+slope*.0005))
    solver = NonlinearSolver()
    with pytest.raises(NumericalConditionError) as failure:
        solver.solve_nonlinear(*args, displacement_control={
            "node": 307, "dof": "r"+axis, "targets": [.002, .006]})
    assert failure.value.details["reason"] == "singular_shear_condensation"
    assert failure.value.details["element_id"] == 1
    assert failure.value.details["axis"] == axis
    assert failure.value.details["step"] == 2
    assert element.current_bending_states == element.committed_bending_states
    assert element.current_states == element.committed_states
    assert element.committed_bending_states["moment_"+axis].curvature == pytest.approx(.002)


@pytest.mark.parametrize("route", ["python", "json"])
def test_accepted_section_output_records_updated_inertia_and_is_an_owned_snapshot(route):
    data = configuration("moment_z", force=1.)
    data["analysis_params"] = {"load_factors": [12., 10.]}
    result = solve(data, route)
    first, last = [step["section_response"]["7"]["center"]["z"] for step in result["step_results"]]
    assert first["Nd"] == pytest.approx(0., abs=1e-12)
    assert first["curvature"] == pytest.approx(.002)
    assert first["moment"] == pytest.approx(12.)
    assert first["bending_tangent"] == pytest.approx(2000.)
    assert first["effective_inertia"] == pytest.approx(.2)
    assert last["bending_tangent"] == pytest.approx(10000.)
    assert last["branch"] == "unloading"
    assert result["section_response"]["7"]["center"]["z"] == last
    assert result["metadata"]["analysis"]["beam_formulation"] == "jr_updated_inertia_v1"
    result["section_response"]["7"]["center"]["z"]["moment"] = 999.
    assert last["moment"] == pytest.approx(10.)


def test_http_updated_inertia_path_exposes_complete_sibling_snapshots():
    data = configuration("moment_z", force=1.0)
    data["analysis_params"] = {"load_factors": [12.0, 10.0]}

    steps = load_step_results(solve(data, "http"))

    assert len(steps) == 2
    assert_uniform_result(steps[0], "moment_z", 1, 12.0, 0.002)
    assert_uniform_result(steps[1], "moment_z", 1, 10.0, 0.0018)
    for step in steps:
        assert_step_diagnostics(step)


def test_reference_static_and_modal_analyses_do_not_inherit_updated_inertia(tmp_path):
    data = configuration("moment_z", force=12.)
    model = json_model(data)
    model.analysis_params['n_modes'] = 2
    reference = model._run_solver_snapshot("static")
    reference_modal = model._run_solver_snapshot("modal")
    nonlinear = model._run_solver_snapshot("material_nonlinear")
    assert nonlinear["section_response"][7]["center"]["z"]["moment"] == pytest.approx(12.)
    public_result = model.run("material_nonlinear")
    saved = tmp_path/"response.json"
    model.save_results(str(saved))
    from fem.file_io import read_result
    assert read_result(str(saved)) == public_result
    repeated = model._run_solver_snapshot("material_nonlinear")
    np.testing.assert_allclose(repeated["displacement"], nonlinear["displacement"], atol=1e-12)
    elastic = model._run_solver_snapshot("static")
    np.testing.assert_allclose(elastic["displacement"], reference["displacement"], atol=1e-12)
    assert "section_response" not in elastic
    modal = model._run_solver_snapshot("modal")
    np.testing.assert_allclose(modal["frequencies"], reference_modal["frequencies"], rtol=1e-12)
