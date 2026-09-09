"""Explicit, source-checked repair of migrated solid connectivity/displacements.

No FEM imports or computed solver values. Refuse to repair a different model.
--write repairs only topology and disg; missing reaction references stay missing.
"""

import copy

STEMS = ("sampleBendHexa1", "sampleBendWedge1", "sampleBendHexa2", "sampleBendWedge2", "sampleBendTetra2")

TYPES = {
    "TetraElement2": "tetra2",
    "WedgeElement2": "wedge2",
    "HexaElement2": "hexa2",
    "HexaElement1": "hexa",
    "WedgeElement1": "wedge",
    "TetraElement1": "tetra",
}


def assert_source_input(data, source):
    """Input equivalence is an assertion, never a best-effort name match."""
    assert data.get("dimension", 3) == 3
    assert len(data["load"]) == len(data["element"]) == len(data["fix_node"]) == 1
    assert all(
        not data.get(k)
        for k in ("member", "shell", "rigid", "fix_member", "joint", "notice_points", "boundary_conditions")
    )
    case = next(iter(data["load"].values()))
    assert str(case.get("element", 1)) == next(iter(data["element"]))
    assert str(case.get("fix_node", 1)) == next(iter(data["fix_node"]))
    assert not case.get("load_member") and case.get("rate", 1) == 1
    assert {k: [v[a] for a in ("x", "y", "z")] for k, v in data["node"].items()} == source["nodes"]
    mats = next(iter(data["element"].values()))
    assert all(not mat.get("nonlinear") for mat in mats.values())
    assert {k: {f: v[f] for f in ("E", "nu")} for k, v in mats.items()} == {
        k: {f: v[f] for f in ("E", "nu")} for k, v in source["materials"].items()
    }
    loads = {}
    for row in case["load_node"]:
        key = str(row["n"])
        loads[key] = [
            a + row.get(k, 0) for a, k in zip(loads.get(key, [0.0] * 6), ("tx", "ty", "tz", "rx", "ry", "rz"))
        ]
    assert loads == {k: v + [0.0] * (6 - len(v)) for k, v in source["loads"].items()}
    rests = {
        str(row["n"]): [row.get(k, 0) for k in ("tx", "ty", "tz", "rx", "ry", "rz")]
        for row in next(iter(data["fix_node"].values()))
    }
    assert rests == {k: v[::2] + [0.0] * (6 - len(v) // 2) for k, v in source["restraints"].items()}
    assert all(all(x == 0 for x in v[1::2]) for v in source["restraints"].values())
    topology = {
        k: dict(nodes=list(map(int, v["nodes"])), e=int(v["material"]), type=TYPES[v["type"]])
        for k, v in source["elements"].items()
    }
    if data.get("solid"):
        actual = {k: dict(nodes=list(map(int, v["nodes"])), e=int(v["e"])) for k, v in data["solid"].items()}
        assert actual == {k: {f: v[f] for f in ("nodes", "e")} for k, v in topology.items()}
        assert all(
            v.get("type", topology[k]["type"]) in (topology[k]["type"], source["elements"][k]["type"])
            for k, v in data["solid"].items()
        )
    return topology


def repaired_data(data, source):
    topology = assert_source_input(data, source)
    assert set(source["displacements"]) == set(data["node"])
    assert set(data["result"]) == {"1"}
    old = data["result"]["1"]["disg"]
    assert old.keys() == source["displacements"].keys()
    for nid, values in old.items():
        assert values.keys() == source["displacements"][nid].keys()
    # Allow a checked, already-repaired file. Never accept arbitrary numbers.
    assert any(
        all(
            abs(v / factor - source["displacements"][nid][key])
            <= 1e-12 * max(1.0, abs(source["displacements"][nid][key]))
            for nid, values in old.items()
            for key, v in values.items()
        )
        for factor in (1.0, 1000.0)
    )
    repaired = copy.deepcopy(data)
    if not repaired.get("solid"):
        repaired["solid"] = topology
    repaired["result"]["1"]["disg"] = copy.deepcopy(source["displacements"])
    return repaired
