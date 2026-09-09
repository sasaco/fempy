"""Reproducible diagnostics, never a generator of expected FEM results."""

import argparse
import contextlib
import hashlib
import io
import json
import re
from pathlib import Path

import numpy as np

from fem.file_io import result_to_jsonable
from fem.model import FemModel
from tests.support.assertions import assert_cantilever_history
from tests.support.oracles.cantilever import cantilever_reference


def maximum_error(a, b):
    if isinstance(b, dict):
        return max((maximum_error(a[k], v) for k, v in b.items()), default=0.0)
    return float(np.max(np.abs(np.asarray(a) - np.asarray(b))))


def measure(output):
    path = Path("tests/data/snap/beam001.json")
    data = json.loads(path.read_text(encoding="utf-8"))
    with contextlib.redirect_stdout(io.StringIO()):
        model = FemModel()
        model.load_model(str(path))
        result = result_to_jsonable(model.run())
    assert_cantilever_history(result, data)
    reference = cantilever_reference(data)
    errors = {field: maximum_error(result[field], values) for field, values in reference.items()}
    all_errors = dict.fromkeys(errors, 0.0)
    for step in result["step_results"]:
        for field, values in cantilever_reference(data, step["lambda"]).items():
            all_errors[field] = max(all_errors[field], maximum_error(step[field], values))
    mat = data["element"]["1"]["2"]
    force = data["load"]["1"]["load_node"][0]["tx"]
    shear_enabled = "G" in mat and data["member"]["2"].get("shear_correction", True)
    metrics = dict(
        reference="tests/support/oracles/cantilever.py: equilibrium, inverse skeleton and scalar flexibility",
        input_path=path.as_posix(),
        input_sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
        tip_force=force,
        central_moment=-force
        * ((data["node"]["2"]["y"] + data["node"]["3"]["y"]) / 2 - data["node"]["1"]["y"]),
        central_curvature=(
            reference["node_displacements"]["3"]["rz"] - reference["node_displacements"]["2"]["rz"]
        )
        / 0.1,
        shear_correction_by_member={
            key: "G" in data["element"]["1"][str(member["e"])] and member.get("shear_correction", True)
            for key, member in data["member"].items()
        },
        shear_strain=force / (mat["G"] * 5 / 6 * mat["A"]) if shear_enabled else 0.0,
        tip=result["node_displacements"]["1"],
        maximum_absolute_error_final=errors,
        maximum_absolute_error_all_steps=all_errors,
        step_count=len(result["step_results"]),
        maximum_final_step_relative_residual=max(
            [c for c in result["convergence_history"] if c["step"] == step["step"]][-1]["relative_residual"]
            for step in result["step_results"]
        ),
        free_end_moment_error_all_steps=max(
            abs(step["element_stresses"]["1"]["i_end"][5]) for step in result["step_results"]
        ),
        constitutive_free_end_moment_error_all_steps=max(
            abs(step.get("constitutive_element_stresses", step["element_stresses"])["1"]["i_end"][5])
            for step in result["step_results"]
        ),
        force_recovery=result.get("force_recovery"),
        limitation="Mathematical small-displacement central-section validation only. "
        "Omitted G disables shear deformation; external JR agreement is not established.",
    )
    external = {}
    for suffix in ("OUT", "FMT", "PDN", "PDS", "DSP", "ndt"):
        source = path.with_suffix("." + suffix)
        if not source.exists():
            external[suffix] = dict(status="source unavailable")
            continue
        raw = source.read_bytes()
        external[suffix] = dict(path=source.as_posix(), sha256=hashlib.sha256(raw).hexdigest())
        text = raw.decode("cp932", errors="replace")
        if suffix == "FMT":
            rows = [line.split() for line in text.splitlines() if len(line.split()) == 6]
            row = rows[-1]
            external[suffix].update(
                last=dict(
                    nonlinear_id=int(row[0]),
                    moment=float(row[1]),
                    phi_analysis=float(row[2]),
                    phi_limit=float(row[3]),
                )
            )
        if suffix == "PDS":
            nodes = {}
            for line in text.splitlines():
                values = line.split()
                if len(values) == 4 and re.fullmatch(r"[1-4]", values[0]):
                    try:
                        nodes[values[0]] = dict(
                            zip(("dx_m", "dy_m", "rotation_rad"), [float(v) * 0.001 for v in values[1:]])
                        )
                    except ValueError:
                        pass
            external[suffix]["last_nodes_converted_from_mm_and_permille_rad"] = nodes
    metrics["external_sources"] = external
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(metrics, ensure_ascii=False, indent=2), encoding="utf-8")
    print(
        json.dumps(
            {k: metrics[k] for k in ("step_count", "maximum_absolute_error_all_steps", "tip")}, indent=2
        )
    )


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=Path("tmp/cantilever-audit.json"))
    measure(parser.parse_args().output)
