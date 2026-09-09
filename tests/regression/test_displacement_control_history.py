"""Regression contracts for saved displacement-control histories."""

import json
from copy import deepcopy

import pytest

from tests.support.paths import DATA
from tests.support.oracles.jr_k4_bending import jr_k4_bending_history
from tests.support.sample_runner import compare_displacement_control_history, run_sample
from tests.support.samples import displacement_control_history_samples

pytestmark = [pytest.mark.regression, pytest.mark.material_nonlinear]

SAMPLE = DATA / "snap/jr_k4_displacement_control.json"
TARGETS = [0.0, 0.001, 0.002, 0.004, 0.008, 0.014, 0.020, 0.028, 0.036, 0.040]
LAMBDAS = [0.0, 5.0, 10.0, 12.0, 16.0, 19.0, 22.0, 18.0, 14.0, 12.0]
CURVATURES = [0.0, 0.0005, 0.001, 0.002, 0.004, 0.007, 0.010, 0.014, 0.018, 0.020]


def test_jr_k4_fixture_stores_every_requested_displacement():
    data = json.loads(SAMPLE.read_text(encoding="utf8"))
    nonlinear = data["element"]["1"]["1"]["nonlinear"]
    load = data["load"]["1"]

    assert nonlinear["hysteresis_dofs"] == ["moment_z"]
    assert load["load_node"] == [{"n": 30, "rz": 1}]
    assert load["displacement_control"]["dof"] == "rz"
    assert load["displacement_control"]["targets"] == TARGETS
    assert list(data["result"]) == [str(step) for step in range(1, 11)]
    assert [step["control_displacement"] for step in data["result"].values()] == TARGETS
    assert [step["lambda"] for step in data["result"].values()] == LAMBDAS
    assert [step["curvature"]["7"]["z"] for step in data["result"].values()] == CURVATURES
    assert all(step["curvature"]["7"]["y"] == 0.0 for step in data["result"].values())
    assert any(value != 0.0 for value in CURVATURES[1:])
    assert data["result"] == jr_k4_bending_history(data)


@pytest.mark.parametrize(
    "data_path",
    displacement_control_history_samples(),
    ids=lambda path: path.stem,
)
def test_all_stored_displacement_control_steps(data_path):
    run_sample(data_path, contract="displacement_control_history")


def test_jr_k4_history_comparison_rejects_old_all_zero_curvature_fixture():
    from fem.model import FemModel

    data = json.loads(SAMPLE.read_text(encoding="utf8"))
    model = FemModel()
    model.load_model(str(SAMPLE))
    result = model.run()
    all_zero = deepcopy(data["result"])
    for step in all_zero.values():
        step["curvature"]["7"]["z"] = 0.0

    with pytest.raises(AssertionError):
        compare_displacement_control_history(result, all_zero, model, data, TARGETS)


@pytest.mark.parametrize(
    "step,path",
    [
        ("8", ("lambda",)),
        ("9", ("reac", "10", "mz")),
        ("10", ("fsec", "7", "P1", "mzi")),
        ("10", ("curvature", "7", "z")),
    ],
)
def test_jr_k4_history_comparison_rejects_changed_softening_values(step, path):
    from fem.model import FemModel

    data = json.loads(SAMPLE.read_text(encoding="utf8"))
    model = FemModel()
    model.load_model(str(SAMPLE))
    result = model.run()
    changed = deepcopy(data["result"])
    value = changed[step]
    for key in path[:-1]:
        value = value[key]
    value[path[-1]] += 1.0

    with pytest.raises(AssertionError):
        compare_displacement_control_history(result, changed, model, data, TARGETS)
