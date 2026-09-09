"""Run the complete suite and reject any change to the known failure baseline.

The raw pytest exit status, log and JUnit report are always retained. A passing
baseline comparison does not mean the complete FEM suite passes.
"""

import argparse
import hashlib
import importlib.metadata
import json
import platform
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

from tests.support.paths import ROOT


def failure_cause(message):
    mismatch = re.match(r"AssertionError: (\d+) (?:legacy|section-cut) mismatches;", message)
    if mismatch:
        return {"kind": "reference_mismatch", "count": int(mismatch[1])}
    if message.startswith("ValueError: "):
        return {"kind": "ValueError", "message": message.removeprefix("ValueError: ")}
    return {"kind": "unexpected", "message": message}


def compare_results(report, baseline):
    """Compare failure IDs AND causes; errors/skips/missing cases never pass."""
    expected = baseline["failures"]
    seen, failures, issues = set(), {}, []
    cases = list(ET.parse(report).getroot().iter("testcase"))
    if not cases:
        return ["JUnit report contains no test cases"]
    for case in cases:
        name, owner = case.get("name", ""), case.get("classname", "")
        if case.find("error") is not None or case.find("skipped") is not None:
            issues.append(f"Unexpected error/skip: {owner}::{name}")
        match = re.fullmatch(r"test_saved_sample_case\[(.+)\]", name)
        sample = match[1] if owner == "tests.regression.test_structural_samples" and match else None
        if sample:
            if sample in seen:
                issues.append(f"Duplicate sample case: {sample}")
            seen.add(sample)
        failure = case.find("failure")
        if failure is None:
            continue
        if sample is None:
            issues.append(f"Unexpected failure: {owner}::{name}")
            continue
        failures[sample] = failure_cause(failure.get("message", ""))
    manifest = json.loads((ROOT / "tests/data/manifest.json").read_text(encoding="utf8"))
    registered = {f"{s['id']}:{c}" for s in manifest["samples"] for c in s["cases"]}
    if seen != registered:
        issues.append(
            f"Sample collection changed: missing={sorted(registered - seen)}, extra={sorted(seen - registered)}"
        )
    for key in sorted(expected.keys() | failures.keys()):
        if key not in failures:
            issues.append(f"Resolved/missing failure; review baseline: {key}")
        elif key not in expected:
            issues.append(f"New failure: {key}: {failures[key]}")
        else:
            cause = {k: v for k, v in expected[key].items() if k != "fields"}
            if failures[key] != cause:
                issues.append(f"Failure cause changed: {key}: {cause} -> {failures[key]}")
    return issues


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--report", type=Path, help="Validate an existing complete JUnit report")
    parser.add_argument("--output-dir", type=Path, default=ROOT / "tmp/test-results")
    args = parser.parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    report = args.report or args.output_dir / "complete.xml"
    exit_code = None
    if not args.report:
        with (args.output_dir / "pytest.log").open("w", encoding="utf8") as log:
            run = subprocess.run(
                [sys.executable, "-m", "pytest", "tests", "-q", "--tb=short", f"--junitxml={report}"],
                cwd=ROOT,
                stdout=log,
                stderr=subprocess.STDOUT,
            )
        exit_code = run.returncode
    baseline = json.loads((ROOT / "tests/regression/known_failures.json").read_text(encoding="utf8"))
    issues = compare_results(report, baseline) if report.exists() else ["JUnit report missing"]
    if exit_code is not None and exit_code != 1:
        issues.append(f"Unexpected raw pytest exit code: {exit_code}; current baseline requires 1")
    record = dict(
        raw_pytest_exit_code=exit_code,
        report=str(report),
        baseline_matches=not issues,
        known_failures=len(baseline["failures"]),
        issues=issues,
        python=sys.version,
        platform=platform.platform(),
        commit=subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
        dependencies={
            name: importlib.metadata.version(name) for name in ("numpy", "scipy", "pytest", "sympy")
        },
        node=subprocess.check_output(["node", "--version"], text=True).strip(),
        input_hashes={
            sample["file"]: hashlib.sha256((ROOT / "tests/data" / sample["file"]).read_bytes()).hexdigest()
            for sample in json.loads((ROOT / "tests/data/manifest.json").read_text(encoding="utf8"))[
                "samples"
            ]
        },
    )
    (args.output_dir / "baseline-comparison.json").write_text(
        json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf8"
    )
    print(
        "Known failure baseline matches (complete suite still has 308 failures)"
        if not issues
        else "\n".join(issues)
    )
    return 1 if issues else 0


if __name__ == "__main__":
    raise SystemExit(main())
