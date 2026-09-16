"""harness / source repairs contracts."""

import copy
import json

import pytest

from tests.support.paths import ROOT
from tests.support.provenance import read_source_records
from tests.support.repairs.solid_sources import repaired_data
from tests.support.repairs.support_reactions import completed_reference
from tests.support.repairs.triangle_reference import SAMPLE, SOURCE, check_input

pytestmark = [pytest.mark.unit, pytest.mark.requires_node]


@pytest.mark.parametrize(
    "field", ["node", "load", "fix_node", "element", "result", "type", "dimension", "case"]
)
def test_source_repair_rejects_changed_model_or_unknown_reference(field):
    data = json.loads((ROOT / "tests/data/bend/sampleBendHexa2.json").read_text(encoding="utf-8"))
    source = read_source_records(ROOT / "docs/v0/testdata/bend/sampleBendHexa2.out")
    if field == "node":
        data[field]["1"]["x"] += 1
    elif field == "load":
        data[field]["1"]["load_node"][0]["tz"] += 1
    elif field == "fix_node":
        data[field]["1"][0]["tx"] = 0
    elif field == "element":
        data[field]["1"]["1"]["E"] += 1
    elif field == "type":
        next(iter(data["solid"].values()))["type"] = "tetra2"
    elif field == "dimension":
        data[field] = 2
    elif field == "case":
        data["load"]["1"]["element"] = "999"
    else:
        data["result"]["1"]["disg"]["1"]["dx"] += 1
    with pytest.raises(AssertionError):
        repaired_data(data, source)


@pytest.mark.parametrize("change", ["load", "hash", "residual", "reaction", "node", "existing"])
def test_support_reference_repair_rejects_unverified_evidence(source_reference, change):
    source, reference = copy.deepcopy(source_reference)
    data = json.loads((ROOT / "tests/data/bend/sampleBendHexa1.json").read_text(encoding="utf8"))
    if change == "load":
        data["load"]["1"]["load_node"][0]["tz"] += 1
    elif change == "hash":
        reference["hashes"][source["path"]] = "unknown"
    elif change == "residual":
        reference["maximum_free_force_residual"] = 1e-5
    elif change == "reaction":
        reference["reac"].pop(next(iter(reference["reac"])))
    elif change == "node":
        reference["node_ids"].pop()
    else:
        data["result"]["1"]["reac"] = {"unknown": {}}
    with pytest.raises(AssertionError):
        completed_reference(data, source, reference)


def test_support_reference_completion_preserves_inputs_and_is_idempotent(source_reference):
    source, reference = source_reference
    data = json.loads((ROOT / "tests/data/bend/sampleBendHexa1.json").read_text(encoding="utf8"))
    original = copy.deepcopy(data)
    fixed = completed_reference(data, source, reference)
    assert data == original
    assert {k: v for k, v in fixed.items() if k != "result"} == {
        k: v for k, v in data.items() if k != "result"
    }
    assert fixed["result"]["1"]["disg"] == data["result"]["1"]["disg"]
    assert completed_reference(fixed, source, reference) == fixed
    assert set(fixed["result"]["1"]) == {"disg", "reac", "size", "fsec", "shell_results"}


@pytest.mark.parametrize(
    "change", ["material", "thickness", "support", "load", "node", "topology", "member_load"]
)
def test_tri1_reference_rejects_changed_input_conditions(change):
    data = json.loads((ROOT / SAMPLE).read_text(encoding="utf8"))
    if change == "material":
        data["element"]["1"]["1"]["E"] *= 2
    elif change == "thickness":
        data["element"]["1"]["1"]["thickness"] = 2
    elif change == "support":
        data["fix_node"]["1"][0]["rx"] = 0
    elif change == "load":
        data["load"]["1"]["load_node"][0]["tz"] *= 2
    elif change == "node":
        data["node"]["1"]["x"] += 0.1
    elif change == "topology":
        next(iter(data["shell"].values()))["nodes"][0] = 999
    else:
        data["load"]["1"]["load_member"] = [dict(m=1, mark=2, P1=3)]
    with pytest.raises(AssertionError):
        check_input(data, read_source_records(ROOT / SOURCE))


@pytest.mark.parametrize(
    "change", ["load", "support", "material", "node", "topology", "source", "hash", "coverage", "residual"]
)
def test_tetra_input_repair_rejects_unverified_conditions(tetra_reference, change):
    from tests.support.repairs.tetrahedron_reference import completed_data

    data, source, ref = copy.deepcopy(tetra_reference)
    if change == "load":
        data["load"]["1"]["load_node"][0]["tz"] += 1
    elif change == "support":
        data["fix_node"]["1"][0]["tx"] = 0
    elif change == "material":
        data["element"]["1"]["1"]["E"] += 1
    elif change == "node":
        data["node"]["1"]["x"] += 1
    elif change == "topology":
        next(iter(data["solid"].values()))["nodes"][0] = 99999
    elif change == "source":
        source["sha256"] = "unknown"
    elif change == "hash":
        ref["hashes"][source["path"]] = "unknown"
    elif change == "coverage":
        ref["disg"].pop("1")
    else:
        ref["maximum_free_force_residual"] = 1e-5
    with pytest.raises(AssertionError):
        completed_data(data, source, ref)


from tests.support.repairs.solid_sources import STEMS


@pytest.mark.parametrize("stem", STEMS)
def test_solid_source_repair_is_pure_and_idempotent(stem):
    source = read_source_records(ROOT / "docs/v0/testdata/bend" / (stem + ".out"))
    data = json.loads((ROOT / "tests/data/bend" / (stem + ".json")).read_text(encoding="utf-8"))
    original = copy.deepcopy(data)
    fixed = repaired_data(data, source)
    assert data == original  # Ordinary validation never writes reference files.
    assert repaired_data(fixed, source) == fixed
    assert fixed["result"]["1"].keys() == original["result"]["1"].keys()


def test_tetrahedron_reference_repair_preserves_input_and_is_idempotent(tetra_reference):
    from tests.support.repairs.tetrahedron_reference import completed_data

    data, source, ref = tetra_reference
    original = copy.deepcopy(data)
    fixed = completed_data(data, source, ref)
    assert data == original
    assert {k: v for k, v in fixed.items() if k != "result"} == {
        k: v for k, v in data.items() if k != "result"
    }
    assert completed_data(fixed, source, ref) == fixed
