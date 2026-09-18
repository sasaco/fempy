"""regression / cantilever history contracts."""

import json

import numpy as np
import pytest

from main import app
from tests.integration._canonical_results import (
    assert_step_diagnostics,
    canonical_end_forces,
    load_step_results,
    member_results,
    node_components,
    reaction_components,
)
from tests.support.builders.input_routes import json_model
from tests.support.oracles.cantilever import cantilever_reference
from tests.support.paths import DATA
from tests.support.sample_runner import run_sample
from tests.support.samples import history_samples
from tests.support.serialization import wire

pytestmark = pytest.mark.regression


@pytest.mark.material_nonlinear
def test_cantilever_all_stored_steps_keep_strict_zero_moment_reference():
    from tests.support.sample_runner import run_sample

    result = run_sample("tests/data/snap/beam001.json")
    for step in result["step_results"]:
        assert abs(step["element_stresses"][1]["i_end"][5]) < 1e-10
        assert 1 in step["force_recovery"]["elements"]


@pytest.mark.material_nonlinear
def test_public_cantilever_keeps_a_real_sub_tolerance_end_moment():
    import json
    from pathlib import Path

    from fem.file_io import _read_json_model
    from fem.model import FemModel

    data = json.loads(Path("tests/data/snap/beam001.json").read_text(encoding="utf8"))
    data["load"]["1"]["load_node"][0]["rz"] = 3e-13
    m = FemModel()
    m.read_json_model(_read_json_model(data))
    result = m._run_solver_snapshot()
    assert result["element_stresses"][1]["i_end"][5] == 3e-13
    assert "constitutive_element_stresses" in result


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("route", ["json", "http"])
def test_cantilever_independent_flexibility_reference(route):
    from tests.support.assertions import assert_cantilever_history

    d = json.loads((DATA / "snap/beam001.json").read_text(encoding="utf-8"))
    if route == "http":
        response = app.test_client().post("/", json=d)
        assert response.status_code == 200, response.data
        r = json.loads(response.data)
        steps = load_step_results(r)
        assert len(steps) == d["load"]["1"]["n_load_steps"]
        for step in steps:
            expected = cantilever_reference(d, step["state"]["load_factor"])
            nodes = node_components(step)
            reactions = reaction_components(step)
            members = member_results(step)
            for node_id, components in expected["node_displacements"].items():
                assert nodes[node_id] == pytest.approx(components, rel=1e-6, abs=1e-9)
            for node_id, components in expected["reaction_forces"].items():
                assert reactions[node_id] == pytest.approx(components, rel=1e-6, abs=1e-9)
            for member_id, end_forces in expected["element_stresses"].items():
                segment = members[member_id]["segments"][0]
                for end, values in end_forces.items():
                    np.testing.assert_allclose(
                        list(segment[end].values()),
                        list(canonical_end_forces(values, end).values()),
                        rtol=1e-6,
                        atol=1e-9,
                    )
            assert_step_diagnostics(step)
        return
    else:
        r = wire(json_model(d)._run_solver_snapshot())
    assert_cantilever_history(r, d)


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("data_path", history_samples(), ids=lambda p: p.stem)
def test_all_stored_history_steps(data_path):
    """
    data/snap ディレクトリ配下の材料非線形要素テストデータを実行
    """
    run_sample(data_path, contract="cantilever_history")
