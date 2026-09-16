"""solvers / nonlinear contracts."""

import numpy as np
import pytest

from fem.nonlinear.nonlinear_solver import NonlinearSolver
from tests.support.builders.nonlinear_solver import CappedBar, CubicBar, SofteningBar, axial_bar

pytestmark = pytest.mark.unit


@pytest.mark.material_nonlinear
def test_linear_bar_through_newton_matches_hand_calculation():
    # EA/L=3000, u=P/k=0.01, fixed reaction=-30.
    result = NonlinearSolver().solve_nonlinear(*axial_bar(), n_steps=3)
    assert result["node_displacements"][2]["dx"] == pytest.approx(0.01)
    assert result["reaction_forces"][1]["fx"] == pytest.approx(-30)
    np.testing.assert_allclose(result["displacement"][:6], 0, atol=1e-14)
    assert all(step["converged"] for step in result["step_results"])


@pytest.mark.material_nonlinear
def test_prescribed_motion_without_external_force_is_applied_once_per_step():
    result = NonlinearSolver().solve_nonlinear(*axial_bar(prescribed=0.02), n_steps=4)
    np.testing.assert_allclose(
        [s["displacement"][6] for s in result["step_results"]], [0.005, 0.01, 0.015, 0.02], atol=1e-14
    )
    assert result["reaction_forces"][1]["fx"] == pytest.approx(-60)
    assert result["reaction_forces"][2]["fx"] == pytest.approx(60)


@pytest.mark.material_nonlinear
def test_spring_force_participates_in_residual_and_reactions():
    args = axial_bar(spring=2000)
    args[2].add_load(2, [50, 0, 0, 0, 0, 0])
    result = NonlinearSolver().solve_nonlinear(*args, n_steps=2)
    assert result["node_displacements"][2]["dx"] == pytest.approx(50 / (3000 + 2000))
    assert result["reaction_forces"][1]["fx"] == pytest.approx(-30)
    assert result["reaction_forces"][2]["fx"] == pytest.approx(-20)


@pytest.mark.material_nonlinear
def test_unconverged_step_rolls_back_and_stops_before_callback():
    args = axial_bar()
    element = CubicBar()
    args[3][1] = element
    solver = NonlinearSolver()
    callbacks = []
    with pytest.raises(RuntimeError, match="converg"):
        solver.solve_nonlinear(*args, n_steps=3, max_iter=1, callback=callbacks.append)
    assert callbacks == []
    assert element.trial == element.committed == 0
    assert element.rollbacks >= 1
    np.testing.assert_array_equal(solver.displacement, np.zeros(12))


@pytest.mark.material_nonlinear
def test_unrestrained_model_is_rejected_not_regularized():
    args = axial_bar()
    args[2].restraints.clear()
    with pytest.raises(RuntimeError, match="converg"):
        NonlinearSolver().solve_nonlinear(*args, n_steps=1, max_iter=3)


@pytest.mark.material_nonlinear
def test_fixed_node_load_does_not_hide_free_dof_imbalance():
    args = axial_bar()
    args[2].add_load(1, [1e12, 0, 0, 0, 0, 0])
    result = NonlinearSolver().solve_nonlinear(*args, n_steps=1)
    assert result["node_displacements"][2]["dx"] == pytest.approx(0.01)
    assert result["reaction_forces"][1]["fx"] == pytest.approx(-1e12 - 30)


@pytest.mark.material_nonlinear
def test_cubic_spring_converges_to_known_nonlinear_root():
    args = axial_bar()
    args[2].loads.clear()
    args[2].add_load(2, [2, 0, 0, 0, 0, 0])
    element = CubicBar()
    args[3][1] = element
    result = NonlinearSolver().solve_nonlinear(*args, n_steps=2)
    # N=u+u**3=2 has the unique real root u=1.
    assert result["node_displacements"][2]["dx"] == pytest.approx(1, abs=1e-7)
    assert element.committed == pytest.approx(1, abs=1e-7)
    assert result["reaction_forces"][1]["fx"] == pytest.approx(-2)


@pytest.mark.material_nonlinear
def test_displacement_control_traces_peak_and_negative_stiffness_branch():
    args = axial_bar()
    args[2].loads.clear()
    args[2].add_load(2, [1, 0, 0, 0, 0, 0])
    element = SofteningBar()
    args[3][1] = element
    result = NonlinearSolver().solve_nonlinear(
        *args,
        displacement_control={"node": 2, "dof": "dx", "targets": [0.5, 1.0, 1.5, 1.8]},
    )
    assert [step["displacement"][6] for step in result["step_results"]] == pytest.approx(
        [0.5, 1.0, 1.5, 1.8]
    )
    assert [step["lambda"] for step in result["step_results"]] == pytest.approx(
        [0.5, 1.0, 0.5, 0.2]
    )
    assert result["lambda"] == pytest.approx(0.2)
    assert result["control_displacement"] == pytest.approx(1.8)
    assert element.committed == pytest.approx(1.8)


@pytest.mark.material_nonlinear
def test_displacement_control_generates_equal_targets_from_final_target():
    args = axial_bar()
    args[2].loads.clear()
    args[2].add_load(2, [1, 0, 0, 0, 0, 0])
    args[3][1] = SofteningBar()
    result = NonlinearSolver().solve_nonlinear(
        *args, n_steps=4,
        displacement_control={"node": 2, "dof": "dx", "target": 1.8},
    )
    assert [step["control_displacement"] for step in result["step_results"]] == pytest.approx(
        [0.45, 0.9, 1.35, 1.8]
    )
    assert result["step_results"][-1]["lambda"] == pytest.approx(0.2)


@pytest.mark.material_nonlinear
def test_failed_displacement_control_step_restores_displacement_lambda_and_state():
    args = axial_bar()
    args[2].loads.clear()
    args[2].add_load(2, [1, 0, 0, 0, 0, 0])
    element = SofteningBar()
    args[3][1] = element
    solver = NonlinearSolver()
    with pytest.raises(RuntimeError, match="converg"):
        solver.solve_nonlinear(
            *args, max_iter=1,
            displacement_control={"node": 2, "dof": "dx", "target": 1.5},
        )
    np.testing.assert_array_equal(solver.displacement, np.zeros(12))
    assert solver.load_factor == 0
    assert element.trial == element.committed == 0
    assert element.rollbacks >= 1


@pytest.mark.material_nonlinear
def test_displacement_control_and_prescribed_load_factors_are_exclusive():
    with pytest.raises(ValueError, match="mutually exclusive"):
        NonlinearSolver().solve_nonlinear(
            *axial_bar(), load_factors=[1],
            displacement_control={"node": 2, "dof": "dx", "target": 1},
        )


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("control", [
    {},
    {"node": 99, "dof": "dx", "target": 1},
    {"node": 1, "dof": "dx", "target": 1},
    {"node": 2, "dof": "bad", "target": 1},
    {"node": 2, "dof": "dx", "target": float("nan")},
    {"node": 2, "dof": "dx", "targets": []},
])
def test_invalid_displacement_control_is_rejected(control):
    with pytest.raises(ValueError):
        NonlinearSolver().solve_nonlinear(
            *axial_bar(), displacement_control=control,
        )


@pytest.mark.material_nonlinear
def test_failed_second_step_restores_first_converged_state():
    args = axial_bar()
    args[2].loads.clear()
    args[2].add_load(2, [3, 0, 0, 0, 0, 0])
    element = CappedBar()
    args[3][1] = element
    solver = NonlinearSolver()
    callbacks = []
    with pytest.raises(RuntimeError, match="converg") as error:
        solver.solve_nonlinear(*args, n_steps=3, callback=callbacks.append)
    assert error.value.step == 2
    assert len(callbacks) == 1
    np.testing.assert_allclose(solver.displacement, callbacks[0]["displacement"])
    np.testing.assert_allclose(error.value.displacement, callbacks[0]["displacement"])
    assert element.trial == element.committed == pytest.approx(1)
    assert {record["step"] for record in solver.convergence_history} == {1, 2}


@pytest.mark.material_nonlinear
@pytest.mark.parametrize(
    "kwargs",
    [
        {"n_steps": 0},
        {"n_steps": -1},
        {"n_steps": 1.5},
        {"max_iter": 0},
        {"tol": 0},
        {"tol": float("nan")},
        {"tol": float("inf")},
    ],
)
def test_invalid_controls_rejected_before_analysis(kwargs):
    with pytest.raises(ValueError):
        NonlinearSolver().solve_nonlinear(*axial_bar(), **kwargs)
