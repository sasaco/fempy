"""Accepted midpoint total curvature: physical history and public output contracts."""

import json

import pytest

from fem.file_io import read_result
from tests.support.builders.input_routes import axial_json, json_model
from tests.support.builders.nonlinear_reference import configuration, solve
from tests.support.oracles.cantilever import cantilever_reference
from tests.support.paths import DATA
from tests.support.section_cut_view import section_cut_result_view
from tests.integration._canonical_results import (
    assert_step_diagnostics,
    assert_uniform_result,
    canonical_end_forces,
    load_step_results,
    member_results,
    node_components,
    reaction_components,
)

pytestmark = [pytest.mark.integration, pytest.mark.material_nonlinear]


@pytest.mark.parametrize("route", ["python", "json"])
@pytest.mark.parametrize("axis", ["y", "z"])
@pytest.mark.parametrize("n", [1, 4])
@pytest.mark.parametrize("asymmetric", [False, True])
def test_curvature_output_tracks_total_cyclic_response(route, axis, n, asymmetric):
    # Independent JR polygon, including residual curvature at zero moment.
    forces = [0, 10, 12, 0, -12 if asymmetric else -10, -16 if asymmetric else -12, 0, 12]
    expected = [
        0,
        0.001,
        0.002,
        0.0008,
        -0.002 if asymmetric else -0.001,
        -0.004 if asymmetric else -0.002,
        -0.004 / 3 if asymmetric else -0.0008,
        0.002,
    ]
    data = configuration("moment_" + axis, n=n, force=1, asymmetric=asymmetric)
    data["analysis_params"] = {"load_factors": forces}
    result = solve(data, route)
    assert len(result["step_results"]) == len(expected)
    for step, value in zip(result["step_results"], expected):
        assert set(step["curvature"]) == {str(7 + i) for i in range(n)}
        for components in step["curvature"].values():
            assert components == pytest.approx(
                {axis: value, "z" if axis == "y" else "y": 0}, rel=1e-8, abs=1e-12
            )
    assert result["curvature"] == result["step_results"][-1]["curvature"]
    result["curvature"]["7"][axis] = 99
    assert result["step_results"][-1]["curvature"]["7"][axis] == pytest.approx(0.002)
    assert result["step_results"][0]["curvature"]["7"][axis] == 0


@pytest.mark.parametrize("axis", ["y", "z"])
@pytest.mark.parametrize("n", [1, 4])
@pytest.mark.parametrize("asymmetric", [False, True])
def test_http_curvature_path_exposes_sibling_physical_load_steps(axis, n, asymmetric):
    forces = [0, 10, 12, 0, -12 if asymmetric else -10, -16 if asymmetric else -12, 0, 12]
    expected = [
        0,
        0.001,
        0.002,
        0.0008,
        -0.002 if asymmetric else -0.001,
        -0.004 if asymmetric else -0.002,
        -0.004 / 3 if asymmetric else -0.0008,
        0.002,
    ]
    data = configuration("moment_" + axis, n=n, force=1, asymmetric=asymmetric)
    data["analysis_params"] = {"load_factors": forces}

    steps = load_step_results(solve(data, "http"))

    assert len(steps) == len(expected)
    for step, force, deformation in zip(steps, forces, expected, strict=True):
        assert step["state"]["load_factor"] == pytest.approx(force)
        assert_uniform_result(step, "moment_" + axis, n, force, deformation)
        assert_step_diagnostics(step)


def test_curvature_output_survives_result_save_and_section_cut_view(tmp_path):
    data = configuration("moment_z", force=1)
    data["analysis_params"] = {"load_factors": [0, 12, 0]}
    model = json_model(data)
    result = model._run_solver_snapshot()
    assert result["curvature"][7] == pytest.approx(dict(y=0, z=0.0008), abs=1e-12)
    assert [s["curvature"][7]["z"] for s in result["step_results"]] == pytest.approx(
        [0, 0.002, 0.0008], abs=1e-12
    )
    public_result = model.run()
    path = tmp_path / "result.json"
    model.save_results(str(path))
    saved = read_result(str(path))
    assert saved == public_result
    assert "curvature" not in saved
    assert "step_results" not in saved
    view = section_cut_result_view(result, model, data)
    assert {"disg", "reac", "fsec", "curvature"} <= view.keys()
    assert view["curvature"]["7"] == pytest.approx(result["curvature"][7], abs=1e-12)


@pytest.mark.parametrize("mode", ["axial", "torsion"])
def test_curvature_output_does_not_report_strain_or_twist_as_bending(mode):
    result = solve(configuration(mode), "json")
    assert result["curvature"]["7"] == pytest.approx(dict(y=0, z=0), abs=1e-12)


def test_static_output_does_not_publish_nonlinear_curvature():
    data = axial_json()
    data["analysis_type"] = "static"
    assert "curvature" not in json_model(data)._run_solver_snapshot()


def test_curvature_includes_elastic_bending_axis_of_nonlinear_element():
    data = configuration("axial", force=12)
    data["load"]["1"]["load_node"][0].update(ry=-3, rz=5)
    result = solve(data, "json")
    # E*Iy = E*Iz = 10000; axial plasticity does not remove elastic bending.
    assert result["curvature"]["7"] == pytest.approx(dict(y=-0.0003, z=0.0005), abs=1e-12)


def test_nonlinear_analysis_with_only_elastic_elements_has_empty_curvature():
    data = axial_json()
    del data["element"]["1"]["1"]["nonlinear"]
    data["analysis_type"] = "material_nonlinear"
    result = json_model(data)._run_solver_snapshot()
    assert result["curvature"] == {}
    assert all(step["curvature"] == {} for step in result["step_results"])


def test_cantilever_curvature_output_matches_independent_all_step_reference():
    data = json.loads((DATA / "snap/beam001.json").read_text(encoding="utf8"))
    data["analysis_params"] = {"load_factors": [i / 100 for i in range(101)]}
    steps = load_step_results(solve(data, "http"))
    for step in steps:
        factor = step["state"]["load_factor"]
        expected = cantilever_reference(data, factor)
        nodes = node_components(step)
        reactions = reaction_components(step)
        members = member_results(step)
        for node_id, components in expected["node_displacements"].items():
            assert nodes[node_id] == pytest.approx(components, rel=1e-9, abs=1e-14)
        for node_id, components in expected["reaction_forces"].items():
            assert reactions[node_id] == pytest.approx(components, rel=1e-9, abs=1e-12)
        for member_id, raw in expected["element_stresses"].items():
            segments = members[member_id]["segments"]
            assert segments[0]["i_end"] == pytest.approx(
                canonical_end_forces(raw["i_end"], "i_end"), rel=1e-9, abs=1e-12
            )
            assert segments[-1]["j_end"] == pytest.approx(
                canonical_end_forces(raw["j_end"], "j_end"), rel=1e-9, abs=1e-12
            )
        assert_step_diagnostics(step)
    assert steps[-1]["state"]["load_factor"] == pytest.approx(1.0)
