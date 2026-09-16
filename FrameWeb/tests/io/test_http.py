"""io / http contracts."""

import json

import numpy as np
import pytest

from main import app
from tests.support.assertions import assert_axial
from tests.support.builders.input_routes import axial_json, json_model
from tests.support.paths import DATA

pytestmark = pytest.mark.integration


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("analysis_type", ["material_nonlinear", "static", "unknown"])
def test_http_explicit_analysis_type(analysis_type):
    data = axial_json()
    data["analysis_type"] = analysis_type
    response = app.test_client().post("/", json=data)
    body = json.loads(response.data)
    if analysis_type == "unknown":
        assert response.status_code == 400
        assert body["converged"] is False
    else:
        assert response.status_code == 200
        assert body["analysis_type"] == analysis_type
        assert body["node_displacements"]["30"]["dx"] == pytest.approx(
            0.004 if analysis_type == "material_nonlinear" else 0.0024
        )
        assert body["element_stresses"]["7"]["j_end"][0] == pytest.approx(12)
        assert body["element_stresses"]["7"]["i_end"][0] == pytest.approx(-12)
        assert body["reaction_forces"]["10"]["fx"] == pytest.approx(-12)


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("change", ["iterations", "tolerance", "material", "law", "dof", "load", "empty"])
def test_http_invalid_input_is_not_success(change):
    data = axial_json()
    if change == "iterations":
        data["load"]["1"]["max_iterations"] = 0
    elif change == "tolerance":
        data["load"]["1"]["tolerance"] = float("nan")
    elif change == "material":
        data["element"]["1"]["1"]["nonlinear"]["delta_2"] = 0.0001
    elif change == "law":
        data["element"]["1"]["1"]["nonlinear"]["type"] = "typo"
    elif change == "dof":
        data["element"]["1"]["1"]["nonlinear"]["hysteresis_dofs"] = ["typo"]
    elif change == "load":
        data["load"]["1"]["load_node"][0]["tx"] = float("inf")
    else:
        data = {}
    response = app.test_client().post("/", json=data)
    assert response.status_code == 400
    body = json.loads(response.data)
    assert body["converged"] is False
    assert "node_displacements" not in body


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("mode", ["capacity", "singular", "iterations", "zero_singular"])
def test_http_failed_analysis_and_reanalysis(mode):
    data = axial_json(30 if mode == "capacity" else 12)
    if "singular" in mode:
        data["fix_node"] = {}
    if mode == "zero_singular":
        data["load"]["1"]["load_node"][0]["tx"] = 0
    if mode == "iterations":
        data["load"]["1"]["max_iterations"] = 1
    client = app.test_client()
    response = client.post("/", json=data)
    assert response.status_code == 422
    body = json.loads(response.data)
    assert body["converged"] is False
    assert body["error_code"] == "nonlinear_nonconvergence"
    assert "node_displacements" not in body
    good = client.post("/", json=axial_json())
    assert good.status_code == 200
    assert_axial(json.loads(good.data))


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("factors", [[], [0, float("nan")], [[1, 2]], "123"])
def test_invalid_load_factors_rejected(factors):
    d = axial_json()
    d["analysis_params"] = {"load_factors": factors}
    r = app.test_client().post("/", json=d)
    assert r.status_code == 400


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("sample", ["shellRibQuad1.json", "shellRibTri1.json"])
def test_failed_postprocessing_clears_result_and_http_is_error(sample, monkeypatch):
    d = json.loads((DATA / "shell" / sample).read_text(encoding="utf-8"))
    m = json_model(d)
    # The vertical-plane defect is fixed; retain the error-propagation contract
    # with a deterministic postprocessing failure, not a permanent defect.
    assert m.run()["element_stresses"]
    from fem.elements.shell_element import ShellElement

    def fail(self, displacement):
        raise np.linalg.LinAlgError("injected postprocessing failure")

    monkeypatch.setattr(ShellElement, "calculate_stress_strain", fail)
    with pytest.raises(np.linalg.LinAlgError):
        m.run()
    assert m.results is None
    response = app.test_client().post("/", json=d)
    assert response.status_code == 500
    assert json.loads(response.data)["converged"] is False


@pytest.mark.material_nonlinear
def test_explicit_static_spring_reactions_balance():
    d = axial_json(5)
    d["analysis_type"] = "static"
    d["boundary_conditions"] = {"spring_supports": {"30": {"x": 2000}}}
    r = app.test_client().post("/", json=d)
    assert r.status_code == 200
    body = json.loads(r.data)
    assert body["node_displacements"]["30"]["dx"] == pytest.approx(5 / 7000, abs=1e-10)
    assert body["reaction_forces"]["10"]["fx"] == pytest.approx(-25 / 7)
    assert body["reaction_forces"]["30"]["fx"] == pytest.approx(-10 / 7)
