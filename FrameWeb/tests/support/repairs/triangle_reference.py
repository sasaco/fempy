"""Complete Tri1 reference from the original .fem, with input identity guards."""

from tests.support.oracles.source_triangle import solve_source
from tests.support.paths import ROOT
from tests.support.provenance import read_source_records

SAMPLE = "tests/data/bend/sampleBendTri1.json"

SOURCE = "docs/v0/testdata/bend/sampleBendTri1.fem"


def check_input(data, source):
    assert data["dimension"] == 3
    assert source["shell_parameters"] == {"1": 15.0}
    assert all(v["parameter"] == "1" for v in source["elements"].values())
    assert {n: [v[k] for k in "xyz"] for n, v in data["node"].items()} == source["nodes"]
    assert data["shell"] == {
        k: dict(e=int(v["material"]), nodes=list(map(int, v["nodes"])), formulation="dkt")
        for k, v in source["elements"].items()
    }
    assert len(data["element"]) == 1 and len(data["element"]["1"]) == 1
    material = data["element"]["1"]["1"]
    assert {k: material[k] for k in ("E", "nu", "G")} == source["materials"]["1"]
    assert material["thickness"] == material["A"] == 15
    assert data["fix_node"] == {
        "1": [
            dict(n=n, **{k: int(v[2 * i]) for i, k in enumerate(("tx", "ty", "tz", "rx", "ry", "rz"))})
            for n, v in source["restraints"].items()
        ]
    }
    assert all(all(v == 0 for v in row[1::2]) for row in source["restraints"].values())
    assert len(data["load"]) == 1
    case = data["load"]["1"]
    assert case.get("rate", 1) == 1
    assert {
        str(v["n"]): [v.get(k, 0) for k in ("tx", "ty", "tz", "mx", "my", "mz")] for v in case["load_node"]
    } == source["loads"]
    assert not case.get("load_member") and not case.get("load_shell")
    assert all(not data.get(k) for k in ("member", "solid", "rigid", "boundary_conditions", "notice_points"))
    assert all(not v for field in ("fix_member", "joint") for v in data.get(field, {}).values())


def completed_data(data):
    source = read_source_records(ROOT / SOURCE)
    check_input(data, source)
    result, proof = solve_source(ROOT / SOURCE)
    assert proof["maximum_free_force_residual"] <= 1e-10
    assert set(result["disg"]) == set(source["nodes"])
    assert len(result["shell_results"]) == len(source["elements"])
    return {"1": result}, proof
