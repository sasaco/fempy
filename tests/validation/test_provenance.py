"""validation / provenance contracts."""

import json

import pytest

from fem.legacy_beam import select_case
from tests.support.paths import ROOT
from tests.support.provenance import (
    input_findings,
    read_source_records,
    source_evidence,
)

pytestmark = pytest.mark.oracle


@pytest.mark.parametrize(
    "name", ["sampleBendHexa1", "sampleBendWedge1", "sampleBendHexa2", "sampleBendWedge2", "sampleBendTetra2"]
)
def test_complete_external_solid_input_and_repaired_displacement(name):
    path = ROOT / "tests/data/bend" / f"{name}.json"
    original = path.read_bytes()
    data = select_case(json.loads(original))
    out = source_evidence(path, data)[1]
    assert set(out["input_comparison"]) == {
        "nodes",
        "topology",
        "static_material",
        "nodal_loads",
        "restraints",
    }
    assert all(v["mismatches"] == 0 for v in out["input_comparison"].values())
    assert out["reference_coverage"]["missing_source_nodes"] == []
    scales = out["embedded_displacement_divided_by"]
    assert scales["1"]["mismatches"] == 0
    assert scales["1000"]["mismatches"] > 1000
    # Reintroducing the old transfer defect must still be diagnosed.
    for values in data["result"]["1"]["disg"].values():
        for key in values:
            values[key] *= 1000
    changed = source_evidence(path, data)[1]["embedded_displacement_divided_by"]
    assert changed["1"]["mismatches"] > 1000
    assert changed["1000"]["mismatches"] == 0
    assert path.read_bytes() == original


@pytest.mark.parametrize(
    "name", ["sampleBendHexa2", "sampleBendTetra2", "sampleBendWedge2", "sampleBendTri1"]
)
def test_missing_json_connectivity_is_proved_against_original_fem(name):
    path = ROOT / "tests/data/bend" / f"{name}.json"
    data = select_case(json.loads(path.read_text(encoding="utf-8")))
    field = "shell" if name == "sampleBendTri1" else "solid"
    assert data[field]  # Restored topology is now present in the fixture.
    data.pop(field)  # Recreate the historical migration defect.
    assert any(v["code"] == "missing_element_topology" for v in input_findings(data))
    source = read_source_records(ROOT / "docs/v0/testdata/bend" / f"{name}.fem")
    assert source["elements"]
    assert len(source["nodes"]) == len(data["node"])


def test_tetra_printed_reference_is_incomplete_and_not_same_model_proof():
    path = ROOT / "tests/data/bend/sampleBendTetra1.json"
    out = source_evidence(path, select_case(json.loads(path.read_text(encoding="utf-8"))))[1]
    assert out["counts"]["displacements"] == 10
    assert len(out["reference_coverage"]["missing_source_nodes"]) == 610
    assert out["input_comparison"] == {}


def test_source_audit_detects_input_change_instead_of_trusting_same_file_name():
    path = ROOT / "tests/data/bend/sampleBendHexa1.json"
    data = select_case(json.loads(path.read_text(encoding="utf-8")))
    data["element"]["1"]["1"]["E"] *= 2
    out = source_evidence(path, data)[1]
    assert out["input_comparison"]["static_material"]["mismatches"] == 1


def test_stale_notice_record_does_not_stop_the_numerical_audit():
    data = json.loads((ROOT / "tests/data/bar/2D_Sample06.json").read_text(encoding="utf-8"))
    findings = input_findings(select_case(data, "1"))
    assert any(v["code"] == "unknown_notice_member" and v["member"] == "39" for v in findings)


def test_selected_material_case_and_rounded_geometry_are_reported():
    data = json.loads((ROOT / "tests/data/bar/2D_Sample13.json").read_text(encoding="utf-8"))
    findings = input_findings(select_case(data, "1"))
    zero = next(v for v in findings if v["code"] == "zero_member_properties")
    assert any(v["material"] == "9" and "Iz" in v["fields"] for v in zero["members"])
    # The display/default material case has a nonzero inertia; audit the selected case.
    assert data["element"]["1"]["9"]["Iz"] > 0
    data = json.loads((ROOT / "tests/data/bar/3D_Sample10.json").read_text(encoding="utf-8"))
    findings = input_findings(select_case(data, "6"))
    rounding = next(v for v in findings if v["code"] == "legacy_millimetre_rounding")
    assert rounding["count"] == 4
    assert rounding["max_position_shift"] > 1e-4
