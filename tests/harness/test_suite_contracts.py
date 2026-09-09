"""Prevent collection drift, reference contamination and baseline masking."""

import ast
import hashlib
import json
import xml.etree.ElementTree as ET

import pytest

from tests.support.paths import DATA, ROOT
from tests.support.samples import registered_samples
from tools.validation.check_test_results import compare_results, failure_cause

pytestmark = pytest.mark.unit


def test_sample_manifest_covers_all_original_files_cases_and_snapshots():
    samples = registered_samples()
    assert len(samples) == 45
    assert len({s["id"] for s in samples}) == len(samples)
    assert {s["file"] for s in samples} == {
        p.relative_to(DATA).as_posix()
        for folder in ("bar", "shell", "bend", "snap")
        for p in (DATA / folder).glob("*.json")
    }
    assert sum(len(s["cases"]) for s in samples) == 323
    for sample in samples:
        path = DATA / sample["file"]
        assert hashlib.sha256(path.read_bytes()).hexdigest() == sample["sha256"]
        data = json.loads(path.read_text(encoding="utf8"))
        if sample["contract"] == "cantilever_history":
            assert sample["steps"] == list(data["result"])
            assert set(sample["steps"]) == {str(i) for i in range(101)}
        else:
            assert sample["cases"] == list(
                dict.fromkeys([*data.get("load", {}), *data.get("result", {})] or ["1"])
            )
        for source in sample["sources"]:
            assert hashlib.sha256((ROOT / source["path"]).read_bytes()).hexdigest() == source["sha256"]


def test_test_modules_do_not_import_each_other_or_use_historical_names():
    for path in (ROOT / "tests").rglob("*.py"):
        assert "phase4" not in path.name and not path.name.startswith("test_v0")
        for node in ast.walk(ast.parse(path.read_text(encoding="utf8"))):
            if isinstance(node, ast.ImportFrom):
                assert not any(p.startswith("test_") for p in (node.module or "").split(".")), path
                assert not (node.module or "").startswith(("src.fem", "src.app")), path


def test_oracles_and_recursive_assertions_have_no_product_dependencies():
    visited = set()

    def visit(path):
        path = path.resolve()
        if path in visited:
            return
        visited.add(path)
        for node in ast.walk(ast.parse(path.read_text(encoding="utf8"))):
            names = (
                [node.module or ""]
                if isinstance(node, ast.ImportFrom)
                else [a.name for a in node.names]
                if isinstance(node, ast.Import)
                else []
            )
            for name in names:
                assert not name.startswith(("fem", "app", "src.fem", "src.app", "main")), (path, name)
                if name.startswith("tests."):
                    dependency = ROOT / (name.replace(".", "/") + ".py")
                    assert dependency.exists(), dependency
                    visit(dependency)

    for path in (ROOT / "tests/support/oracles").glob("*.py"):
        visit(path)
    visit(ROOT / "tests/support/assertions.py")


@pytest.mark.parametrize(
    "message,expected",
    [
        (
            "AssertionError: 12 legacy mismatches; first 12:\nvalues",
            {"kind": "reference_mismatch", "count": 12},
        ),
        ("ValueError: Singular matrix", {"kind": "ValueError", "message": "Singular matrix"}),
        (
            "AssertionError: unrelated failure",
            {"kind": "unexpected", "message": "AssertionError: unrelated failure"},
        ),
    ],
)
def test_baseline_classification_keeps_other_failure_causes_visible(message, expected):
    assert failure_cause(message) == expected


def test_public_model_and_http_import_the_same_classes():
    import main
    from fem.model import FemModel
    from tests.support.builders.input_routes import axial_json, json_model

    assert main.FemModel is FemModel
    assert isinstance(json_model(axial_json()), FemModel)


@pytest.mark.parametrize(
    "mutation", [None, "missing_case", "new_failure", "changed_cause", "resolved", "skip"]
)
def test_baseline_gate_rejects_collection_and_outcome_changes(tmp_path, mutation):
    baseline = json.loads((ROOT / "tests/regression/known_failures.json").read_text(encoding="utf8"))
    suite = ET.Element("testsuite")
    cases = {}
    for sample in registered_samples():
        for case_id in sample["cases"]:
            key = f"{sample['id']}:{case_id}"
            case = ET.SubElement(
                suite,
                "testcase",
                classname="tests.regression.test_structural_samples",
                name=f"test_saved_sample_case[{key}]",
            )
            cases[key] = case
            cause = baseline["failures"].get(key)
            if cause:
                message = (
                    f"AssertionError: {cause['count']} legacy mismatches; first 12:"
                    if cause["kind"] == "reference_mismatch"
                    else f"ValueError: {cause['message']}"
                )
                ET.SubElement(case, "failure", message=message)
    failed = cases[next(iter(baseline["failures"]))]
    passed = next(case for case in cases.values() if case.find("failure") is None)
    if mutation == "missing_case":
        suite.remove(passed)
    elif mutation == "new_failure":
        extra = ET.SubElement(
            suite, "testcase", classname="tests.materials.test_jr_hysteresis", name="test_history"
        )
        ET.SubElement(extra, "failure", message="AssertionError: broken state")
    elif mutation == "changed_cause":
        failed.find("failure").set("message", "ValueError: unrelated numerical failure")
    elif mutation == "resolved":
        failed.remove(failed.find("failure"))
    elif mutation == "skip":
        ET.SubElement(passed, "skipped")
    report = tmp_path / "results.xml"
    ET.ElementTree(suite).write(report, encoding="utf8")
    issues = compare_results(report, baseline)
    assert bool(issues) is (mutation is not None)
