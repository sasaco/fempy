"""harness / sample runner contracts."""

import copy
import json

import pytest

from tests.support.builders.input_routes import axial_json
from tests.support.builders.linear_frame import cantilever
from tests.support.oracles.cantilever_decimal import SOURCE
from tests.support.paths import DATA

pytestmark = pytest.mark.unit


@pytest.mark.material_nonlinear
def test_missing_sample_reference_never_writes(tmp_path):
    from tests.support.sample_runner import run_sample

    path = tmp_path / "missing.json"
    path.write_text(json.dumps(axial_json()), encoding="utf-8")
    before = path.read_bytes()
    with pytest.raises(AssertionError, match="reference|期待"):
        run_sample(str(path))
    assert path.read_bytes() == before


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("field", ["u", "reaction", "section", "missing", "convergence"])
def test_sample_comparison_really_rejects_corrupted_reference(tmp_path, field):
    from tests.support.oracles.axial import axial_acceptance_reference
    from tests.support.sample_runner import run_sample

    d = axial_json()
    d["reference"] = axial_acceptance_reference()
    path = tmp_path / "acceptance.json"
    path.write_text(json.dumps(d), encoding="utf-8")
    run_sample(str(path))  # correct independent reference must pass first
    reference = d["reference"]
    if field == "u":
        reference["node_displacements"]["30"]["dx"] *= 2
    elif field == "reaction":
        reference["reaction_forces"]["10"]["fx"] *= -1
    elif field == "section":
        reference["element_stresses"]["7"]["j_end"][0] *= -1
    elif field == "missing":
        del reference["node_displacements"]["30"]["dx"]
    else:
        reference["converged"] = False
    path.write_text(json.dumps(d), encoding="utf-8")
    before = path.read_bytes()
    with pytest.raises(AssertionError):
        run_sample(str(path))
    assert path.read_bytes() == before


@pytest.mark.parametrize("field", ["disg", "reac", "fsec"])
def test_late_step_reference_mutation_is_detected(tmp_path, field):
    from tests.support.sample_runner import run_sample

    data = json.loads(SOURCE.read_text(encoding="utf-8"))
    data = copy.deepcopy(data)
    step = data["result"]["62"]
    if field == "disg":
        step[field]["1"]["dx"] += 0.01
    elif field == "reac":
        step[field]["4"]["tx"] += 1
    else:
        step[field]["2"]["P1"]["mzj"] += 1
    path = tmp_path / "beam001.json"
    path.write_text(json.dumps(data), encoding="utf-8")
    before = path.read_bytes()
    with pytest.raises(AssertionError, match="step/62"):
        run_sample(path)
    assert path.read_bytes() == before


@pytest.mark.material_nonlinear
def test_audit_visits_later_case_after_first_error_and_never_writes_reference(tmp_path):
    from tests.support.sample_audit import audit

    d = cantilever()
    d["load"]["2"] = copy.deepcopy(d["load"]["1"])
    d["load"]["1"] = dict(load_member=[dict(m=1, mark=1, L1=9, P1=3, direction="y")])
    d["result"] = {"1": {}, "2": {}}
    path = tmp_path / "audit.json"
    path.write_text(json.dumps(d), encoding="utf-8")
    before = path.read_bytes()
    entries = audit(path)
    assert [entry["case"] for entry in entries] == ["1", "2"]
    assert entries[0]["status"] == "error"
    assert entries[1]["fields"]["disg"]["status"] == "missing reference"
    assert path.read_bytes() == before


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("mutation", [None, "displacement", "missing_node", "unknown_step"])
def test_cantilever_step_zero_reference_is_compared_in_addition_to_all_step_oracle(tmp_path, mutation):
    from tests.support.sample_runner import run_sample

    source = DATA / "snap/beam001.json"
    data = json.loads(source.read_text(encoding="utf-8"))
    zero = dict(
        disg={k: dict.fromkeys(("dx", "dy", "dz", "rx", "ry", "rz"), 0.0) for k in data["node"]},
        reac={"4": dict.fromkeys(("tx", "ty", "tz", "mx", "my", "mz"), 0.0)},
        fsec={},
    )
    for key, member in data["member"].items():
        length = data["node"][str(member["nj"])]["y"] - data["node"][str(member["ni"])]["y"]
        values = dict.fromkeys(
            (mode + end for mode in ("fx", "fy", "fz", "mx", "my", "mz") for end in ("i", "j")), 0.0
        )
        zero["fsec"][key] = {"P1": dict(values, L=length)}
    data["result"] = {"0": zero}
    if mutation == "displacement":
        zero["disg"]["1"]["dx"] = 1.0
    elif mutation == "missing_node":
        del zero["disg"]["1"]
    elif mutation == "unknown_step":
        data["result"]["999999"] = copy.deepcopy(zero)
    path = tmp_path / "beam001.json"
    path.write_text(json.dumps(data), encoding="utf-8")
    before = path.read_bytes()
    if mutation:
        with pytest.raises(AssertionError):
            run_sample(path)
    else:
        assert run_sample(path)["converged"]
    assert path.read_bytes() == before
