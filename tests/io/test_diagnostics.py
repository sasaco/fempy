"""Stable diagnostics, provenance metadata, and logging contracts."""

import json
import logging

import pytest
from scipy.sparse import csr_matrix

from fem import (
    BarParameter,
    InputValidationError,
    StructuralMechanismError,
    UnsupportedAnalysisError,
)
from fem.equilibrium import NonlinearConvergenceError, solve_direct_system
from fem.file_io import read_result
from fem.model import FemModel
from main import app
from tests.support.builders.input_routes import axial_json, json_model, python_axial

pytestmark = pytest.mark.integration


def test_static_result_metadata_and_model_result_roundtrip(tmp_path, capsys):
    model = python_axial()
    model.model_metadata["units"].update(length="mm", force="N")
    result = model.run("static")
    metadata = result["metadata"]

    assert capsys.readouterr().out == ""
    assert metadata["schema_version"] == "1.0"
    assert metadata["product"]["name"] == "FEMPython"
    assert metadata["product"]["version"]
    assert len(metadata["input_sha256"]) == 64
    int(metadata["input_sha256"], 16)
    assert metadata["analysis"]["type"] == "static"
    assert metadata["coordinate_system"]["name"] == "global_cartesian"
    assert metadata["units"]["system"] == "consistent_user_defined"
    assert metadata["units"]["length"] == "mm"
    assert metadata["units"]["force"] == "N"
    assert metadata["units"]["mass"] == "unspecified"
    assert metadata["solver"]["iterations"] == 1
    assert metadata["solver"]["step_iterations"] == [1]
    assert metadata["solver"]["relative_residual"] < 1e-12
    assert metadata["solver"]["warnings"] == []
    assert metadata["solver"]["high_precision"] is False

    model_path = tmp_path / "model.json"
    result_path = tmp_path / "result.json"
    model.save_model(str(model_path))
    model.save_results(str(result_path))

    restored = FemModel()
    restored.load_model(str(model_path))
    restored_result = restored.run("static")
    assert restored.model_metadata == model.model_metadata
    assert restored_result["metadata"]["input_sha256"] == metadata["input_sha256"]
    assert read_result(str(result_path))["metadata"] == json.loads(
        result_path.read_text(encoding="utf-8")
    )["metadata"]


def test_input_hash_changes_with_analysis_input():
    first = python_axial(12).run("static")["metadata"]["input_sha256"]
    second = python_axial(13).run("static")["metadata"]["input_sha256"]
    assert first != second


def test_static_metadata_residual_includes_spring_equilibrium():
    model = python_axial()
    model.add_spring_support(30, "x", 1000)

    solver_metadata = model.run("static")["metadata"]["solver"]

    assert solver_metadata["residual_norm"] < 1e-12
    assert solver_metadata["residual_scale"] == pytest.approx(12)
    assert solver_metadata["relative_residual"] < 1e-12


def test_model_metadata_cannot_override_provenance_fields():
    model = python_axial()
    model.model_metadata["product"] = {"version": "forged"}
    with pytest.raises(InputValidationError, match="Unknown model_metadata"):
        model.run("static")
    assert model.results is None


def test_modal_metadata_uses_eigenpair_residual():
    model = FemModel()
    model.add_node(1, 0, 0, 0)
    model.add_node(2, 2, 0, 0)
    model.add_material(1, "Steel", E=200e9, nu=0.3, density=7850)
    model.material.add_bar_parameter(
        1, BarParameter(area=0.01, Iy=1e-5, Iz=1e-5, J=2e-5)
    )
    model.add_element(1, "bar", [1, 2], 1, section_id=1, shear_correction=False)
    for node_id in (1, 2):
        for direction, stiffness in (
            ("x", 1e5), ("y", 1e6), ("z", 1e6),
            ("rx", 1e4), ("ry", 1e4), ("rz", 1e4),
        ):
            model.add_spring_support(node_id, direction, stiffness)

    result = model.run_modal_analysis(1)
    metadata = result["metadata"]
    assert metadata["analysis"]["type"] == "modal"
    assert metadata["analysis"]["parameters"]["n_modes"] == 1
    assert metadata["solver"]["iterations"] == 0
    assert metadata["solver"]["relative_residual"] == pytest.approx(
        max(result["eigenpair_residuals"])
    )


def test_high_precision_path_is_declared_in_metadata():
    from tests.support.builders.linear_frame import cantilever, run

    data = cantilever()
    data["notice_points"] = [dict(m=1, Points=[1.9999999])]
    _, result = run(data)
    assert "precise_end_forces" in result
    assert result["metadata"]["solver"]["high_precision"] is True


@pytest.mark.material_nonlinear
def test_nonlinear_metadata_summarizes_accepted_history(caplog, capsys):
    model = json_model(axial_json())
    with caplog.at_level(logging.DEBUG, logger="fem.equilibrium"):
        result = model.run("material_nonlinear")

    solver_metadata = result["metadata"]["solver"]
    assert capsys.readouterr().out == ""
    assert any("|R|/|F|" in record.message for record in caplog.records)
    assert solver_metadata["iterations"] == sum(
        step["iterations"] for step in result["step_results"]
    )
    assert solver_metadata["residual_norm"] == pytest.approx(
        result["convergence_history"][-1]["residual_norm"]
    )
    assert solver_metadata["relative_residual"] == pytest.approx(
        result["convergence_history"][-1]["relative_residual"]
    )


def test_unknown_analysis_has_same_python_and_http_code():
    model = python_axial()
    with pytest.raises(UnsupportedAnalysisError) as caught:
        model.run("not-an-analysis")
    assert caught.value.error_code == "unsupported_analysis"
    assert caught.value.details["analysis_type"] == "not-an-analysis"
    assert model.results is None

    data = axial_json()
    data["analysis_type"] = "not-an-analysis"
    response = app.test_client().post("/", json=data)
    body = response.get_json()
    assert response.status_code == 400
    assert body["error_code"] == caught.value.error_code
    assert body["error_category"] == "unsupported"
    assert body["details"]["analysis_type"] == "not-an-analysis"


def test_mechanism_has_same_python_and_http_code_and_dof_details():
    model = python_axial()
    model.boundary.restraints.clear()
    with pytest.raises(StructuralMechanismError) as caught:
        model.run("static")
    assert caught.value.error_code == "structural_mechanism"
    assert model.results is None

    with pytest.raises(StructuralMechanismError) as zero_diagonal:
        solve_direct_system(csr_matrix([[1.0, 0.0], [0.0, 0.0]]), [1.0, 0.0])
    assert zero_diagonal.value.details["matrix_dofs"] == [1]

    data = axial_json()
    data["analysis_type"] = "static"
    data["fix_node"] = {}
    response = app.test_client().post("/", json=data)
    body = response.get_json()
    assert response.status_code == 422
    assert body["error_code"] == caught.value.error_code
    assert body["error_category"] == "structural"


def test_numerical_rank_failure_is_distinct_from_input_and_mechanism():
    matrix = csr_matrix([[1.0, 1.0], [1.0, 1.0 + 5e-16]])
    with pytest.raises(ValueError) as caught:
        solve_direct_system(matrix, [1.0, 1.0])
    assert caught.value.error_code == "numerical_ill_conditioning"

    with pytest.raises(InputValidationError) as invalid:
        FemModel().run("static")
    assert invalid.value.error_code == "invalid_input"


@pytest.mark.material_nonlinear
def test_nonconvergence_exposes_stable_python_details():
    model = python_axial(30)
    model.analysis_params.update(load_factors=[1.0], max_iterations=2)
    with pytest.raises(NonlinearConvergenceError) as caught:
        model.run("material_nonlinear")
    assert caught.value.error_code == "nonlinear_nonconvergence"
    assert caught.value.details == {"step": 1, "load_factor": 1.0}
    assert model.results is None
