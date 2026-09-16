"""Independent source-model reference for saved triangular shell samples."""

import hashlib
import tempfile
from pathlib import Path

from tests.support.oracles.source_triangle import solve_source
from tests.support.paths import ROOT
from tests.support.provenance import read_source_records

INPUT_KINDS = {"Material", "ShellParameter", "Node", "TriElement1", "Restraint", "Load"}
OUTPUT_KINDS = {
    "ResultType",
    "Displacement",
    "Strain1",
    "Stress1",
    "StrEnergy1",
    "Strain2",
    "Stress2",
    "StrEnergy2",
}


def check_input(data, source, *, element_types=("TriElement1",)):
    assert data["dimension"] == 3
    assert {n: [v[k] for k in "xyz"] for n, v in data["node"].items()} == source[
        "nodes"
    ]
    assert data["shell"] == {
        k: dict(e=int(v["material"]), nodes=list(map(int, v["nodes"])))
        for k, v in source["elements"].items()
    }
    assert len(data["element"]) == 1
    for key, material in data["element"]["1"].items():
        assert {k: material[k] for k in ("E", "nu", "G")} == source["materials"][key]
    for key, element in source["elements"].items():
        material = data["element"]["1"][element["material"]]
        assert element["type"] in element_types
        assert (
            material.get("thickness", material["A"])
            == source["shell_parameters"][element["parameter"]]
        )
    assert data["fix_node"] == {
        "1": [
            dict(
                n=n,
                **dict(
                    zip(
                        ("tx", "ty", "tz", "rx", "ry", "rz"),
                        [bool(v) for v in row[::2]],
                    )
                ),
            )
            for n, row in source["restraints"].items()
        ]
    }
    assert all(not any(row[1::2]) for row in source["restraints"].values())
    assert len(data["load"]) == 1
    case = data["load"]["1"]
    assert case.get("rate", 1) == 1
    assert {
        str(row["n"]): [row.get(k, 0) for k in ("tx", "ty", "tz", "rx", "ry", "rz")]
        for row in case["load_node"]
    } == source["loads"]
    assert not case.get("load_member") and not case.get("load_shell")
    assert all(
        not data.get(k)
        for k in ("member", "solid", "rigid", "notice_points", "boundary_conditions")
    )
    assert all(not any(data.get(k, {}).values()) for k in ("fix_member", "joint"))


def independent_sample(data, source_path, *, spin=False):
    source_path = Path(source_path)
    check_input(data, read_source_records(source_path))
    lines = []
    for line in source_path.read_text(encoding="utf8").splitlines():
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        kind = line.split()[0]
        if kind in INPUT_KINDS:
            lines.append(line)
        else:
            assert kind in OUTPUT_KINDS, f"Unknown source record {kind}"
    # Only the original INPUT echo reaches the independent solver. Printed
    # legacy displacements/stresses never seed or define the reference.
    (ROOT / "tmp").mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(
        dir=ROOT / "tmp", prefix="triangle-source-"
    ) as directory:
        model = Path(directory) / "input.fem"
        model.write_text("\n".join(lines) + "\n", encoding="utf8")
        if spin:
            from tests.support.oracles.source_triangle_spin import (
                solve_source as solve_spin,
            )

            reference, proof = solve_spin(model)
        else:
            reference, proof = solve_source(model)
        digest = proof["hashes"].pop(model.relative_to(ROOT).as_posix())
    proof["input_echo_sha256"] = digest
    proof["hashes"][source_path.resolve().relative_to(ROOT).as_posix()] = (
        hashlib.sha256(source_path.read_bytes()).hexdigest()
    )
    proof["hashes"][Path(__file__).resolve().relative_to(ROOT).as_posix()] = (
        hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
    )
    proof["input_equivalence"] = (
        "all nodes, element topology, E/nu/G, thickness, restraints and nodal loads"
    )
    return reference, proof
