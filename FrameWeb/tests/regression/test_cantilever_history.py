"""regression / cantilever history contracts."""

import json

import pytest

from main import app
from tests.support.builders.input_routes import json_model
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
    result = m.run()
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
    else:
        r = wire(json_model(d).run())
    assert_cantilever_history(r, d)


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("data_path", history_samples(), ids=lambda p: p.stem)
def test_all_stored_history_steps(data_path):
    """
    data/snap ディレクトリ配下の材料非線形要素テストデータを実行
    """
    run_sample(data_path, contract="cantilever_history")
