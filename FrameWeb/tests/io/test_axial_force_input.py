"""Public Nd-table input, persistence and HTTP contracts."""

import copy
import base64
import gzip
import json

import pytest

from fem.file_io import _read_json_model
from fem.diagnostics import InputValidationError
from fem.material import BarParameter
from fem.model import FemModel
from main import app
from tests.support.builders.input_routes import axial_json

pytestmark = pytest.mark.integration


def law():
    return {
        "type": "jr_stiffness_reduction",
        "symmetric": True,
        "beta": 0.,
        "axial_force_points": [
            {"Nd": -2., "delta_1": .001, "P_1": .05,
             "delta_2": .003, "P_2": .10, "delta_3": .005, "P_3": .125},
            {"Nd": 0., "delta_1": .001, "P_1": .10,
             "delta_2": .003, "P_2": .20, "delta_3": .005, "P_3": .25},
            {"Nd": 2., "delta_1": .001, "P_1": .30,
             "delta_2": .003, "P_2": .60, "delta_3": .005, "P_3": .75},
        ],
    }


def axial_law():
    return {
        "type": "jr_stiffness_reduction", "symmetric": True, "beta": 0.,
        "delta_1": .0004, "P_1": .4,
        "delta_2": .0010, "P_2": .7,
        "delta_3": .0020, "P_3": .9,
    }


def python_model():
    model = FemModel()
    model.add_node(1, 0., 0., 0.)
    model.add_node(2, 1., 0., 0.)
    model.add_nonlinear_material_laws(
        1, "Nd beam", E=1000., nu=.25, laws={"moment_z": law()},
    )
    model.material.add_bar_parameter(1, BarParameter(1., 1., 1., 1.))
    # Omitting hysteresis_dofs lets the law keys define the active components.
    model.add_nonlinear_bar_element(7, [1, 2], 1, 1)
    return model


def test_python_laws_create_axis_specific_table():
    model = python_model()
    model._create_elements()
    element = model.elements[7]
    assert list(element.axial_force_tables) == ["moment_z"]
    assert element.axial_force_tables["moment_z"].to_dict() == law() | {"K_min": .5}
    assert model.mesh.elements[7]["hysteresis_dofs"] == ["moment_z"]


def test_normalized_model_roundtrip_preserves_laws(tmp_path):
    model = python_model()
    path = tmp_path / "nd-model.json"
    model.save_model(str(path))
    wire = json.loads(path.read_text(encoding="utf-8"))
    assert wire["nonlinear_materials"]["1"]["laws"]["moment_z"]["axial_force_points"][1]["Nd"] == 0.

    restored = FemModel()
    restored.load_model(str(path))
    restored._create_elements()
    assert (restored.elements[7].axial_force_tables["moment_z"].to_dict()
            == model.material.nonlinear_laws[1]["moment_z"].to_dict())


def test_component_laws_can_combine_nonlinear_axial_and_Nd_bending(tmp_path):
    model = FemModel()
    model.add_node(1, 0., 0., 0.)
    model.add_node(2, 1., 0., 0.)
    model.add_nonlinear_material_laws(
        1, "coupled", 1000., nu=.25,
        laws={"axial": axial_law(), "moment_z": law()},
    )
    model.material.add_bar_parameter(1, BarParameter(1., 1., 1., 1.))
    model.add_nonlinear_bar_element(7, [1, 2], 1, 1)
    model._create_elements()
    element = model.elements[7]
    assert list(element.hysteresis_models) == ["axial"]
    assert list(element.axial_force_tables) == ["moment_z"]
    assert model.mesh.elements[7]["hysteresis_dofs"] == ["axial", "moment_z"]
    path = tmp_path / "coupled.json"
    model.save_model(str(path))
    restored = FemModel()
    restored.load_model(str(path))
    restored._create_elements()
    assert list(restored.elements[7].hysteresis_models) == ["axial"]
    assert list(restored.elements[7].axial_force_tables) == ["moment_z"]


def test_public_solver_uses_nonlinear_axial_law_before_Nd_bending():
    model = FemModel()
    model.add_node(1, 0., 0., 0.)
    model.add_node(2, 1., 0., 0.)
    model.add_nonlinear_material_laws(
        1, "coupled", 1000., nu=.25,
        laws={"axial": axial_law(), "moment_z": law()},
    )
    model.material.add_bar_parameter(1, BarParameter(1., 1., 1., 1.))
    model.add_nonlinear_bar_element(7, [1, 2], 1, 1)
    model.add_restraint(1, True, True, True, True, True, True)
    model.add_load(2, fx=-.6, mz=.08)
    model.analysis_params.update(n_load_steps=4, max_iterations=30, tolerance=1e-10)
    result = model._run_solver_snapshot("material_nonlinear")
    section = result["section_response"][7]["center"]["z"]
    assert section["N"] == pytest.approx(-.6, abs=1e-10)
    assert section["Nd"] == pytest.approx(.6, abs=1e-10)
    assert section["moment"] == pytest.approx(.08, abs=1e-10)
    assert section["curvature"] == pytest.approx(.0005, abs=1e-9)


def test_two_element_frame_balances_with_shared_definition_and_owned_states():
    model = FemModel()
    for node, x in ((1, 0.), (2, 1.), (3, 2.)):
        model.add_node(node, x, 0., 0.)
    model.add_nonlinear_material_laws(
        1, "shared", 1000., nu=.25, laws={"moment_z": law()},
    )
    model.material.add_bar_parameter(1, BarParameter(1., 1., 1., 1.))
    model.add_nonlinear_bar_element(7, [1, 2], 1, 1)
    model.add_nonlinear_bar_element(8, [2, 3], 1, 1)
    model.add_restraint(1, True, True, True, True, True, True)
    model.add_load(3, fx=-.5, mz=.075)
    model.analysis_params.update(n_load_steps=4, max_iterations=30, tolerance=1e-10)
    result = model._run_solver_snapshot("material_nonlinear")
    for element_id in (7, 8):
        section = result["section_response"][element_id]["center"]["z"]
        assert section["Nd"] == pytest.approx(.5, abs=1e-10)
        assert section["moment"] == pytest.approx(.075, abs=1e-10)
        assert section["curvature"] == pytest.approx(.0005, abs=1e-9)
    assert (model.elements[7].committed_bending_states["moment_z"]
            is not model.elements[8].committed_bending_states["moment_z"])


def legacy_data():
    data = axial_json(force=-.5)
    data["element"]["1"]["1"]["nonlinear"] = {
        "laws": {"moment_z": copy.deepcopy(law())},
        "hysteresis_dofs": ["moment_z"],
    }
    return data


def test_legacy_laws_and_http_use_the_same_definition():
    parsed = _read_json_model(legacy_data())
    model = FemModel()
    model.read_json_model(parsed)
    assert model.material.nonlinear_laws[1]["moment_z"].to_dict()["axial_force_points"] == law()["axial_force_points"]

    response = app.test_client().post("/", json=legacy_data())
    assert response.status_code == 200
    body = json.loads(response.data)
    assert body["kind"] == "analysis_result_set"
    final = body["results"][-1]
    assert final["state"]["is_final"] is True
    segment = final["member_section_forces"][0]["segments"][0]
    assert segment["i_end"]["fx"] == pytest.approx(-.5)
    assert segment["j_end"]["fx"] == pytest.approx(-.5)


def test_compressed_http_preserves_Nd_definition_and_result():
    compressed = gzip.compress(json.dumps(legacy_data()).encode("utf-8"))
    request = base64.b64encode(json.dumps(list(compressed)).encode("utf-8"))
    response = app.test_client().post(
        "/", data=request,
        headers={"Content-Type": "application/json", "Content-Encoding": "gzip"},
    )
    assert response.status_code == 200
    body = json.loads(gzip.decompress(base64.b64decode(response.data)).decode("utf-8"))
    assert body["kind"] == "analysis_result_set"
    segment = body["results"][-1]["member_section_forces"][0]["segments"][0]
    assert segment["i_end"]["fx"] == pytest.approx(-.5)


@pytest.mark.parametrize("steps", [1, 2, 4])
def test_public_solver_balances_simultaneous_compression_and_bending(steps):
    model = python_model()
    model.add_restraint(1, True, True, True, True, True, True)
    model.add_load(2, fx=-.5, mz=.075)
    model.analysis_params.update(n_load_steps=steps, max_iterations=30, tolerance=1e-10)
    result = model._run_solver_snapshot("material_nonlinear")

    section = result["section_response"][7]["center"]["z"]
    assert section["N"] == pytest.approx(-.5, abs=1e-10)
    assert section["Nd"] == pytest.approx(.5, abs=1e-10)
    assert section["moment"] == pytest.approx(.075, abs=1e-10)
    assert section["curvature"] == pytest.approx(.0005, abs=1e-10)
    assert section["shear_force"] == pytest.approx(0., abs=1e-10)
    assert result["reaction_forces"][1]["fx"] == pytest.approx(.5, abs=1e-10)
    assert result["reaction_forces"][1]["mz"] == pytest.approx(-.075, abs=1e-10)
    assert result["metadata"]["analysis"]["beam_formulation"] == "jr_axial_force_updated_inertia_v1"


def test_out_of_range_trial_reports_context_and_rolls_back_all_section_state():
    model = python_model()
    model.add_restraint(1, True, True, True, True, True, True)
    model.add_load(2, fx=-3.)
    model.analysis_params.update(n_load_steps=2, max_iterations=30, tolerance=1e-10)
    with pytest.raises(InputValidationError) as failure:
        model._run_solver_snapshot("material_nonlinear")
    assert failure.value.details == {
        "reason": "axial_force_out_of_range", "Nd": 3., "range": [-2., 2.],
        "element_id": 7, "axis": "z", "step": 2, "load_factor": 1.,
    }
    element = model.elements[7]
    assert element.current_bending_states == element.committed_bending_states
    assert element.committed_bending_states["moment_z"].axial_history.Nd == pytest.approx(1.5)
    assert model.results is None


@pytest.mark.parametrize("bad", [
    {"axial": law()},
    {"moment_z": {"type": "typo", "axial_force_points": law()["axial_force_points"]}},
])
def test_invalid_component_or_law_is_rejected(bad):
    with pytest.raises(ValueError):
        FemModel().add_nonlinear_material_laws(1, "bad", 1000., laws=bad)
