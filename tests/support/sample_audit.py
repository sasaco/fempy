"""Read-only audit of the all reference and load cases of each legacy sample.

Writes diagnostics, NEVER expected results. Run from the repository root:
  .venv/Scripts/python.exe -m tools.validation.audit_samples
"""

import contextlib
import copy
import hashlib
import io
import json
from pathlib import Path

from fem.file_io import _read_json_model
from fem.legacy_beam import select_case
from fem.model import FemModel
from tests.support.assertions import comparison_errors
from tests.support.paths import ROOT
from tests.support.provenance import input_findings, source_evidence
from tests.support.section_cut_view import section_cut_result_view


def audit_case(path, case_id):
    path = Path(path)
    resolved = path.resolve()
    sample_path = (
        resolved.relative_to(ROOT).as_posix() if resolved.is_relative_to(ROOT) else resolved.as_posix()
    )
    data = json.loads(path.read_text(encoding="utf-8"))
    reference = data.get("result", {}).get(case_id, {})
    entry = dict(
        sample=sample_path,
        case=case_id,
        scope="all load and reference cases",
        input_sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
        reference=dict(
            location=f"{sample_path}#/result/{case_id}",
            provenance="Embedded result; original generating program/version not recorded",
            units="Legacy contract: kN, m, rad; fixture origin unverified",
            signs="src/app/result.py section-cut convention",
            positions="node labels; notice-point segment i/j ends",
        ),
    )
    actual = None
    support_repair = None
    support_manifest = ROOT / "docs/report/material-nonlinear-phase4-support-repairs.json"
    if support_manifest.exists():
        support_repair = next(
            (
                item
                for item in json.loads(support_manifest.read_text(encoding="utf8"))
                if item["sample"] == sample_path and item["after_sha256"] == entry["input_sha256"]
            ),
            None,
        )
    repairs = ROOT / "docs/report/material-nonlinear-phase4-source-repairs.json"
    if repairs.exists():
        for repair in json.loads(repairs.read_text(encoding="utf-8")):
            if repair["sample"] == sample_path and repair.get("after_sha256") in (
                entry["input_sha256"],
                support_repair["before_sha256"] if support_repair else None,
            ):
                entry["reference"]["field_sources"] = dict(
                    disg=dict(
                        source=repair["source"],
                        sha256=repair["source_sha256"],
                        units="Native source displacement units; no 1000x rescaling",
                        input_equivalence="nodes, E/nu, nodal loads, restraints checked by tools/migrations/solid_sources.py",
                    )
                )
    if support_repair:
        entry["reference"].setdefault("field_sources", {})["reac"] = dict(
            method=support_repair["source_reference"]["method"],
            hashes=support_repair["source_reference"]["hashes"],
            maximum_free_force_residual=support_repair["source_reference"]["maximum_free_force_residual"],
        )
    tetra_manifest = ROOT / "docs/report/material-nonlinear-phase4-tetra1-repair.json"
    if tetra_manifest.exists():
        repair = json.loads(tetra_manifest.read_text(encoding="utf8"))
        if repair["sample"] == sample_path and repair["after_sha256"] == entry["input_sha256"]:
            ref = repair["source_reference"]
            entry["reference"]["provenance"] = repair["reason"]
            entry["reference"]["field_sources"] = {
                field: dict(
                    method=ref["method"],
                    hashes=ref["hashes"],
                    maximum_free_force_residual=ref["maximum_free_force_residual"],
                )
                for field in ("disg", "reac")
            }
    for name in ("pressure-repair", "tri1-reference-repair"):
        manifest = ROOT / f"docs/report/material-nonlinear-phase4-{name}.json"
        if not manifest.exists():
            continue
        repair = json.loads(manifest.read_text(encoding="utf8"))
        if repair["sample"] != sample_path or repair["after_sha256"] != entry["input_sha256"]:
            continue
        proof = repair.get("source_reference", repair)
        entry["reference"]["provenance"] = proof["method"]
        entry["reference"]["field_sources"] = {
            field: dict(method=proof["method"], hashes=proof["hashes"])
            for field in ("disg", "reac", "shell_results")
        }
    try:
        data = select_case(data, case_id)
        entry["input_findings"] = input_findings(data)
        with contextlib.redirect_stdout(io.StringIO()):
            model = FemModel()
            model.read_json_model(_read_json_model(copy.deepcopy(data)))
            result = model.run()
        actual = section_cut_result_view(result, model, data)
        entry["fields"] = {}
        for field in dict.fromkeys([*actual, *reference]):
            if field not in actual:
                entry["fields"][field] = dict(status="missing output")
                continue
            values = actual[field]
            if field not in reference:
                entry["fields"][field] = dict(status="missing reference")
                continue
            errors = comparison_errors(values, reference[field], field)
            entry["fields"][field] = dict(mismatches=len(errors), examples=errors[:5])
        entry["status"] = (
            "mismatch"
            if any(v.get("mismatches", 0) or "status" in v for v in entry["fields"].values())
            else "match"
        )
    except Exception as error:
        entry.update(status="error", error_type=type(error).__name__, message=str(error))
    entry["source_evidence"] = source_evidence(path, data, actual["disg"] if actual else None)
    return entry


def audit(path):
    data = json.loads(path.read_text(encoding="utf-8"))
    cases = dict.fromkeys([*data.get("load", {}), *data.get("result", {})] or ["1"])
    return [audit_case(path, case_id) for case_id in cases]
