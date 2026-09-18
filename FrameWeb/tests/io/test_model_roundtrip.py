"""io / model roundtrip contracts."""

import json

import numpy as np
import pytest

from fem.model import FemModel
from fem.file_io import write_result
from fem.result_contracts import ResultContractError
from tests.support.builders.input_routes import python_axial
from tests.support.builders.linear_frame import cantilever, run
from tests.support.serialization import wire

pytestmark = pytest.mark.integration


@pytest.mark.material_nonlinear
def test_model_save_load_preserves_nonlinear_input_and_result(tmp_path):
    # A saved model must retain the law, section, springs and load sequence.
    m = python_axial(20)
    m.add_spring_support(30, "x", 2000)
    m.analysis_params["load_factors"] = [0.0, 0.5, 1.0, 0.0]
    expected = wire(m.run())
    model_path, result_path = tmp_path / "model.json", tmp_path / "result.json"
    m.save_model(str(model_path))
    m.save_results(str(result_path))
    new = FemModel()
    new.load_model(str(model_path))
    actual = wire(new.run())
    from tests.support.assertions import assert_dict_almost_equal

    assert_dict_almost_equal(actual, expected)
    assert_dict_almost_equal(json.loads(result_path.read_text(encoding="utf-8")), expected)
    loaded = FemModel()
    loaded.load_results(str(result_path))
    assert_dict_almost_equal(wire(loaded.get_results()), expected)


def test_public_result_lifecycle_rejects_solver_native_snapshots(tmp_path):
    model = python_axial()
    solver_snapshot = model._run_solver_snapshot("static")
    path = tmp_path / "solver-snapshot.json"
    write_result(solver_snapshot, str(path))

    with pytest.raises(ResultContractError, match="analysis_result_set|missing"):
        model.load_results(str(path))
    assert model.get_results() is None

    model.results = solver_snapshot
    with pytest.raises(ResultContractError, match="analysis_result_set|missing"):
        model.get_results()
    with pytest.raises(ResultContractError, match="analysis_result_set|missing"):
        model.save_results(str(tmp_path / "rejected.json"))


@pytest.mark.material_nonlinear
def test_saved_legacy_member_features_retain_the_same_physical_model(tmp_path):
    d = cantilever()
    d["dimension"] = 2
    d["fix_member"] = {"1": [dict(m=1, ty=500)]}
    d["load"]["1"] = dict(load_member=[dict(m=1, mark=2, direction="y", P1=2, P2=3)])
    m, expected = run(d)
    path = tmp_path / "member.json"
    m.save_model(str(path))
    restored = FemModel()
    restored.load_model(str(path))
    actual = restored._run_solver_snapshot()
    np.testing.assert_allclose(actual["displacement"], expected["displacement"], atol=1e-12)
    assert actual["reaction_forces"].keys() == expected["reaction_forces"].keys()
    for key in expected["element_stresses"]:
        for end in ("i_end", "j_end"):
            np.testing.assert_allclose(
                actual["element_stresses"][key][end], expected["element_stresses"][key][end], atol=1e-10
            )
