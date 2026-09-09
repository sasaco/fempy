"""integration / input routes contracts."""

import json

import pytest

from fem.model import FemModel
from main import app
from tests.support.assertions import assert_axial, assert_dict_almost_equal
from tests.support.builders.input_routes import axial_json, json_model, python_axial
from tests.support.builders.linear_frame import cantilever
from tests.support.builders.nonlinear_reference import solve
from tests.support.paths import DATA
from tests.support.serialization import wire

pytestmark = pytest.mark.integration


@pytest.mark.material_nonlinear
def test_python_json_file_http_equivalence(tmp_path):
    data = axial_json()
    path = tmp_path / "axial.json"
    path.write_text(json.dumps(data), encoding="utf-8")
    models = [python_axial(), json_model(data), FemModel()]
    models[-1].load_model(str(path))
    results = [m.run() for m in models]
    response = app.test_client().post("/", json=data)
    assert response.status_code == 200
    results.append(json.loads(response.data))
    for r in results:
        assert_axial(r)
        assert_dict_almost_equal(wire(r), wire(results[0]))
        assert wire(r) == wire(results[0])  # same input route: exact numerical identity


@pytest.mark.material_nonlinear
def test_python_json_and_http_use_saved_jr_k4_displacement_history():
    data = json.loads((DATA / "snap/jr_k4_displacement_control.json").read_text(encoding="utf8"))
    expected = list(data["result"].values())
    results = [solve(data, route) for route in ("python", "json", "http")]
    for result in results:
        assert [step["lambda"] for step in result["step_results"]] == pytest.approx(
            [step["lambda"] for step in expected], abs=1e-9
        )
        assert [step["control_displacement"] for step in result["step_results"]] == pytest.approx(
            [step["control_displacement"] for step in expected], abs=1e-12
        )
        assert [step["curvature"]["7"]["z"] for step in result["step_results"]] == pytest.approx(
            [step["curvature"]["7"]["z"] for step in expected], abs=1e-12
        )
        assert result["lambda"] == pytest.approx(12.0)
        assert result["node_displacements"]["30"]["dy"] == pytest.approx(0.040)
        assert result["node_displacements"]["30"]["rz"] == pytest.approx(0.040)
        assert result["reaction_forces"]["10"]["mz"] == pytest.approx(-12.0)
    assert_dict_almost_equal(results[1], results[0])
    assert_dict_almost_equal(results[2], results[0])


@pytest.mark.material_nonlinear
def test_omitted_nu_has_same_nonlinear_default_in_python_json_and_http():
    d = axial_json(0)
    del d["element"]["1"]["1"]["nu"]
    # G omission now selects Bernoulli; this test isolates the nu default.
    d["element"]["1"]["1"]["G"] = 10000 / 2.4
    d["load"]["1"]["load_node"][0]["ty"] = 1
    m = python_axial(0)
    m.add_nonlinear_material(1, "reference", 10000, 0.001, 0.004, 0.010, 10, 16, 22, beta=0)
    m.add_load(30, fy=1)
    results = [wire(m.run()), wire(json_model(d).run())]
    response = app.test_client().post("/", json=d)
    assert response.status_code == 200
    results.append(json.loads(response.data))
    # Existing Python nonlinear default nu=.2 -> G=10000/2.4.
    expected = 8 / 30000 + 2 / ((10000 / 2.4) * 5 / 6)
    for r in results:
        assert r["node_displacements"]["30"]["dy"] == pytest.approx(expected, abs=1e-10)


def test_omitted_g_file_http_and_saved_model_agree(tmp_path):
    from main import app

    data = cantilever()
    del data["element"]["1"]["2"]["G"]
    data["member"]["1"]["shear_correction"] = True
    path = tmp_path / "beam.json"
    path.write_text(json.dumps(data), encoding="utf-8")
    model = FemModel()
    model.load_model(str(path))
    result = model.run()
    assert result["node_displacements"][2]["dy"] == pytest.approx(0.002, abs=1e-12)
    saved = tmp_path / "saved.json"
    model.save_model(str(saved))
    restored = FemModel()
    restored.load_model(str(saved))
    assert restored.run()["node_displacements"][2]["dy"] == pytest.approx(0.002, abs=1e-12)
    response = app.test_client().post("/", json=data)
    assert response.status_code == 200
    assert json.loads(response.data)["node_displacements"]["2"]["dy"] == pytest.approx(0.002, abs=1e-12)
