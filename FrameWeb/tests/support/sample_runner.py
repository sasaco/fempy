import copy
import json
from pathlib import Path

from fem.file_io import _read_json_model, result_to_jsonable
from fem.model import FemModel
from tests.support.assertions import (
    assert_acceptance_result,
    assert_dict_almost_equal,
    comparison_errors,
)
from tests.support.section_cut_view import section_cut_result_view


def compare_section_cut_result(result, expected, model, data):
    actual = section_cut_result_view(result, model, data)
    errors = []
    for field in actual.keys() | expected.keys():
        if field not in actual:
            errors.append(f"Missing output field: {field}")
            continue
        if field not in expected:
            errors.append(f"Missing reference field: {field}")
        else:
            errors.extend(comparison_errors(actual[field], expected[field], field))
    assert not errors, f"{len(errors)} legacy mismatches; first 12:\n" + "\n".join(errors[:12])


def displacement_control_history_view(step, model, data):
    """Select the physical fields saved by the displacement-history contract."""
    section = section_cut_result_view(step, model, data)
    return {
        "lambda": step["lambda"],
        "control_displacement": step["control_displacement"],
        "curvature": section["curvature"],
        "disg": section["disg"],
        "reac": section["reac"],
        "fsec": section["fsec"],
    }


def compare_displacement_control_history(result, expected, model, data, targets):
    """Compare one continuous solve against every requested saved target."""
    steps = result["step_results"]
    step_ids = [str(i) for i in range(1, len(targets) + 1)]
    assert list(expected) == step_ids, "Saved step IDs must be contiguous and ordered"
    assert len(steps) == len(targets) == len(expected)
    for step_id, target, step in zip(step_ids, targets, steps):
        assert step["step"] == int(step_id)
        assert_dict_almost_equal(step["control_displacement"], target, f"step/{step_id}/target")
        actual = displacement_control_history_view(step, model, data)
        assert_dict_almost_equal(actual, expected[step_id], f"step/{step_id}")


def run_sample(data_path, *, contract=None):
    data = json.loads(Path(data_path).read_text(encoding="utf-8"))
    if contract is None:
        from tests.support.samples import registered_samples

        contract = next(
            (s["contract"] for s in registered_samples() if Path(s["file"]).name == Path(data_path).name),
            None,
        )
    if contract == "displacement_control_history":
        expected = data.get("result")
        assert expected, f"Independent displacement history is missing: {data_path}"
        assert len(data.get("load", {})) == 1, "Displacement-history samples require one load case"
        case = next(iter(data["load"].values()))
        targets = case["displacement_control"]["targets"]
        m = FemModel()
        m.load_model(str(data_path))
        result = m.run()
        compare_displacement_control_history(result, expected, m, data, targets)
        return result
    if contract == "cantilever_history" and "reference" not in data:
        from tests.support.assertions import assert_cantilever_history

        m = FemModel()
        m.load_model(str(data_path))
        result = m.run()
        assert_cantilever_history(result_to_jsonable(result), data)
        if not data.get("result"):
            return result
        if "0" in data["result"]:
            # The zero key explicitly identifies load-step snapshots, not a
            # legacy load case. The independent oracle above covers ALL steps;
            # additionally compare every user-provided snapshot/component.
            snapshots = {str(step["step"]): step for step in result["step_results"]}
            m.analysis_params["load_factors"] = [0.0]
            snapshots["0"] = m.run()["step_results"][0]
            for key, reference in data["result"].items():
                assert key in snapshots, f"Unknown reference load step {key}"
                assert {"disg", "reac", "fsec"} <= reference.keys(), (
                    f"Missing physical reference field at step {key}"
                )
                actual = section_cut_result_view(snapshots[key], m, data)
                for field, values in reference.items():
                    assert field in actual, f"Missing output field: {field}"
                    assert_dict_almost_equal(actual[field], values, f"step/{key}/{field}")
            return result
    if "reference" in data:
        m = FemModel()
        m.load_model(str(data_path))
        result = m.run()
        assert_acceptance_result(result_to_jsonable(result), data["reference"], m.analysis_params)
        return result
    expected = data.get("result")
    assert expected, f"Independent reference (期待結果) is missing: {data_path}"
    if "node_displacements" in expected:
        m = FemModel()
        m.load_model(str(data_path))
        result = m.run()
        assert_dict_almost_equal(result_to_jsonable(result), expected)
        return result
    for case_id, reference in expected.items():
        if "load" not in data:
            assert case_id == "1", f"Unknown reference load case {case_id}"
            m = FemModel()
            m.read_json_model(_read_json_model(copy.deepcopy(data)))
            result = m.run()
            compare_section_cut_result(result, reference, m, data)
            continue
        assert case_id in data["load"], f"Unknown reference load case {case_id}"
        from fem.legacy_beam import select_case

        case_data = select_case(data, case_id)
        case = case_data["load"][case_id]
        case_data["load"] = {case_id: case}
        for field in ("fix_node", "fix_member", "element", "joint"):
            if field in case_data and case_data[field]:
                key = str(case.get(field, case_id))
                assert key in case_data[field], f"Missing {field} case {key}"
                case_data[field] = {key: case_data[field][key]}
        m = FemModel()
        m.read_json_model(_read_json_model(case_data))
        result = m.run()
        compare_section_cut_result(result, reference, m, case_data)
    return result


def run_sample_case(data_path, case_id):
    """Run one explicit load/reference case, including missing-reference failures."""
    from fem.legacy_beam import select_case

    data = json.loads(Path(data_path).read_text(encoding="utf8"))
    assert case_id in data.get("result", {}), f"Missing reference load case {case_id}"
    if "load" not in data:
        assert case_id == "1", f"Unknown reference load case {case_id}"
        selected = copy.deepcopy(data)
    else:
        assert case_id in data["load"], f"Unknown reference load case {case_id}"
        selected = select_case(data, case_id)
        case = selected["load"][case_id]
        selected["load"] = {case_id: case}
        for field in ("fix_node", "fix_member", "element", "joint"):
            if selected.get(field):
                key = str(case.get(field, case_id))
                assert key in selected[field], f"Missing {field} case {key}"
                selected[field] = {key: selected[field][key]}
    model = FemModel()
    model.read_json_model(_read_json_model(selected))
    result = model.run()
    compare_section_cut_result(result, data["result"][case_id], model, selected)
    return result
