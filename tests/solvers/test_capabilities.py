"""PQ-07 machine-readable capabilities and pre-analysis rejection contracts."""

import pytest

from fem import (
    FemModel,
    UnsupportedCapabilityError,
    canonical_element_type,
    get_capability_registry,
    get_element_capability,
)
from fem.capabilities import validate_analysis_capabilities
from fem.elements.solid_element import WedgeElement as AlternateWedgeElement
from fem.file_io import _read_json_model
from tools.validation.render_capabilities import check_rendered_documents


pytestmark = pytest.mark.unit


def public_solid_model(element_type, node_count, element_ids=(37,)):
    model = FemModel()
    for node_id in range(1, node_count + 1):
        model.add_node(node_id, node_id % 3, (node_id // 3) % 3, node_id // 9)
    model.add_material(1, "reference", E=1000.0, nu=0.25, density=2.0)
    for element_id in element_ids:
        model.add_element(element_id, element_type, list(range(1, node_count + 1)), 1)
    return model


def test_registry_is_complete_json_data_and_returned_as_a_copy():
    registry = get_capability_registry()
    assert registry["schema_version"] == 1
    assert set(registry["status_definitions"]) == {"verified", "implemented", "unsupported"}
    assert set(registry["elements"]["wedge"]["analyses"]) == set(registry["analysis_types"])
    assert set(registry["elements"]["wedge"]["loads"]) == set(registry["load_types"])
    assert set(registry["elements"]["wedge"]["results"]) == set(registry["result_types"])

    registry["elements"]["wedge"]["analyses"]["modal"]["status"] = "verified"
    assert get_capability_registry()["elements"]["wedge"]["analyses"]["modal"]["status"] == "unsupported"


@pytest.mark.parametrize(
    "public_name,canonical",
    [
        ("beam", "bar"),
        ("TriElement1", "shell"),
        ("WedgeElement1", "wedge"),
        ("HexaElement2", "hexa2"),
    ],
)
def test_public_and_legacy_names_resolve_from_registry(public_name, canonical):
    assert canonical_element_type(public_name) == canonical
    assert get_element_capability(public_name)["canonical_type"] == canonical


def test_public_wedge_modal_is_rejected_with_ids_before_solver(monkeypatch):
    model = public_solid_model("wedge", 6, element_ids=(37, 91))
    monkeypatch.setattr(
        model.solver,
        "eigenvalue_analysis",
        lambda *args, **kwargs: pytest.fail("solver must not run"),
    )

    with pytest.raises(UnsupportedCapabilityError) as caught:
        model.run("modal")

    assert "element IDs [37, 91] (wedge)" in str(caught.value)
    assert "Mass matrix is not implemented" in str(caught.value)
    assert {issue["element_id"] for issue in caught.value.issues} == {37, 91}
    assert model.results is None


def test_legacy_wedge_name_uses_actual_public_path_in_rejection(monkeypatch):
    model = public_solid_model("WedgeElement1", 6)
    monkeypatch.setattr(
        model.solver,
        "eigenvalue_analysis",
        lambda *args, **kwargs: pytest.fail("solver must not run"),
    )

    with pytest.raises(UnsupportedCapabilityError, match=r"element IDs \[37\] \(wedge\)"):
        model.run_modal_analysis(1)

    assert type(model.elements[37]).__module__ == "fem.elements.advanced_element"


def test_saved_json_public_input_rejects_unsupported_wedge_modal():
    payload = {
        "analysis_type": "modal",
        "analysis_params": {"n_modes": 1},
        "nodes": {
            "1": [0, 0, 0], "2": [1, 0, 0], "3": [0, 1, 0],
            "4": [0, 0, 1], "5": [1, 0, 1], "6": [0, 1, 1],
        },
        "elements": {
            "204": {"type": "WedgeElement1", "nodes": [1, 2, 3, 4, 5, 6], "material_id": 1}
        },
        "materials": {
            "1": {"name": "reference", "E": 1000.0, "nu": 0.25, "density": 2.0}
        },
    }
    model = FemModel()
    model.read_json_model(_read_json_model(payload))

    with pytest.raises(UnsupportedCapabilityError, match=r"element IDs \[204\] \(wedge\)"):
        model.run()


@pytest.mark.parametrize(
    "element_type,node_count,analysis_type",
    [
        ("pyramid", 5, "static"),
        ("pyramid", 5, "material_nonlinear"),
        ("hexa20", 20, "modal"),
    ],
)
def test_stub_element_paths_are_rejected_before_matrix_calls(
    monkeypatch, element_type, node_count, analysis_type
):
    model = public_solid_model(element_type, node_count)
    monkeypatch.setattr(model.solver, "solve", lambda *a, **k: pytest.fail("solver must not run"))
    monkeypatch.setattr(
        model.solver, "eigenvalue_analysis", lambda *a, **k: pytest.fail("solver must not run")
    )

    with pytest.raises(UnsupportedCapabilityError, match=element_type):
        model.run(analysis_type)


def test_solid_pressure_is_rejected_before_load_assembly(monkeypatch):
    model = public_solid_model("tetra", 4)
    model.boundary.add_pressure(37, "F1", 2.0)
    monkeypatch.setattr(model.solver, "solve", lambda *a, **k: pytest.fail("solver must not run"))

    with pytest.raises(UnsupportedCapabilityError) as caught:
        model.run("static")

    assert "element IDs [37] (tetra) load 'shell_pressure'" in str(caught.value)
    assert caught.value.issues[0]["kind"] == "load"


def test_registry_does_not_accept_the_other_same_named_wedge_implementation():
    model = public_solid_model("wedge", 6)
    alternate = AlternateWedgeElement(37, list(range(1, 7)), 1)
    alternate.set_node_coordinates(model.mesh.nodes)

    with pytest.raises(RuntimeError, match="constructed fem.elements.solid_element.WedgeElement"):
        validate_analysis_capabilities(
            model.mesh.elements, {37: alternate}, model.boundary, "static"
        )


def test_readme_and_wiki_tables_are_generated_from_registry():
    check_rendered_documents()
