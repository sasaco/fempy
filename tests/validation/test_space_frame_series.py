"""Independent spatial reference must first reproduce six cantilever modes."""

import json

import pytest

from tests.support.assertions import assert_dict_almost_equal
from tests.support.oracles.space_frame_series import solve_reference
from tests.support.paths import ROOT

pytestmark = pytest.mark.oracle


def frame():
    return dict(
        dimension=3,
        node={"1": dict(x=0, y=0, z=0), "2": dict(x=4, y=0, z=0)},
        member={"1": dict(ni=1, nj=2, e=1, cg=0)},
        element={"1": {"1": dict(E=2000, G=800, A=3, Iy=7, Iz=5, J=2, Xp=0.00001)}},
        fix_node={"1": [dict(n=1, tx=1, ty=1, tz=1, rx=1, ry=1, rz=1)]},
        load={"1": {}},
    )


@pytest.mark.parametrize(
    "load,expected",
    [
        ("tx", dict(dx=6 * 4 / (2000 * 3))),
        ("ty", dict(dy=6 * 4**3 / (3 * 2000 * 5), rz=6 * 4**2 / (2 * 2000 * 5))),
        ("tz", dict(dz=6 * 4**3 / (3 * 2000 * 7), ry=-6 * 4**2 / (2 * 2000 * 7))),
        ("rx", dict(rx=6 * 4 / (800 * 2))),
        ("ry", dict(dz=-6 * 4**2 / (2 * 2000 * 7), ry=6 * 4 / (2000 * 7))),
        ("rz", dict(dy=6 * 4**2 / (2 * 2000 * 5), rz=6 * 4 / (2000 * 5))),
    ],
)
def test_spatial_series_cantilever_compliance(load, expected):
    d = frame()
    d["load"]["1"] = {"load_node": [dict(n=2, **{load: 6})]}
    result = solve_reference(d, "1")
    for component, value in result["disg"]["2"].items():
        assert value == pytest.approx(expected.get(component, 0), abs=1e-14)


def test_spatial_series_interior_bending_couple():
    d = frame()
    d["load"]["1"] = {"load_member": [dict(m=1, mark=11, direction="z", L1=1.5, P1=6)]}
    r = solve_reference(d, "1")
    assert r["reac"]["1"]["mz"] == pytest.approx(-6, abs=1e-14)
    assert r["reac"]["1"]["ty"] == pytest.approx(0, abs=1e-14)
    assert r["disg"]["2"]["dy"] == pytest.approx(
        6 * 1.5 * (4 - 1.5 / 2) / (2000 * 5), abs=1e-14
    )


def test_spatial_series_uniform_temperature():
    d = frame()
    d["load"]["1"] = {"load_member": [dict(m=1, mark=9, P1=30)]}
    r = solve_reference(d, "1")
    assert r["disg"]["2"]["dx"] == pytest.approx(0.00001 * 30 * 4, abs=1e-14)
    assert r["fsec"]["1"]["P1"]["fxi"] == pytest.approx(0, abs=1e-14)


def test_spatial_reference_rejects_an_unrestrained_global_rotation():
    d = frame()
    d["fix_node"]["1"][0]["rz"] = 0
    with pytest.raises(AssertionError, match="Unsupported global rigid motion"):
        solve_reference(d, "1")


def test_spatial_load_transfer_uses_endpoint_statics():
    d = frame()
    d["element"]["1"]["2"] = {
        **d["element"]["1"]["1"],
        "A": 0,
        "Iy": 0,
        "Iz": 0,
        "J": 0,
    }
    d["member"]["2"] = dict(ni=1, nj=2, e=2, cg=0)
    d["load"]["1"] = {"load_member": [dict(m=2, mark=2, direction="y", P1=50, P2=50)]}
    r = solve_reference(d, "1")
    assert r["disg"]["2"]["dy"] == pytest.approx(100 * 4**3 / (3 * 2000 * 5), abs=1e-14)
    assert r["reac"]["1"]["ty"] == pytest.approx(-200, abs=1e-12)
    assert r["reac"]["1"]["mz"] == pytest.approx(-400, abs=1e-12)
    assert r["fsec"]["2"]["P1"]["fyi"] == pytest.approx(-100, abs=1e-12)


def test_spatial_uniform_foundation_rigid_translation():
    d = frame()
    d["fix_node"]["1"][0].update(ty=0, rz=0)
    d["fix_member"] = {"1": [dict(m=1, ty=7)]}
    d["load"]["1"] = {"load_member": [dict(m=1, mark=2, direction="y", P1=21, P2=21)]}
    r = solve_reference(d, "1")
    for node in ("1", "2"):
        assert r["disg"][node]["dy"] == pytest.approx(3, abs=1e-13)
        assert r["disg"][node]["rz"] == pytest.approx(0, abs=1e-13)


def test_spatial_released_uniform_beam_matches_simple_support_solution():
    d = frame()
    d["fix_node"]["1"].append(dict(n=2, ty=1))
    d["joint"] = {"1": [dict(m=1, zi=0, zj=0)]}
    d["notice_points"] = [dict(m=1, Points=[2.0])]
    d["load"]["1"] = {"load_member": [dict(m=1, mark=2, direction="y", P1=6, P2=6)]}
    r = solve_reference(d, "1")
    assert r["disg"]["1n1"]["dy"] == pytest.approx(
        5 * 6 * 4**4 / (384 * 2000 * 5), abs=1e-14
    )
    assert r["fsec"]["1"]["P1"]["mzi"] == pytest.approx(0, abs=1e-13)
    assert r["fsec"]["1"]["P2"]["mzj"] == pytest.approx(0, abs=1e-13)


def test_spatial_long_foundation_preserves_uniform_translation():
    d = frame()
    d["node"]["2"]["x"] = 40
    d["fix_node"]["1"][0].update(ty=0, rz=0)
    d["fix_member"] = {"1": [dict(m=1, ty=7)]}
    d["load"]["1"] = {"load_member": [dict(m=1, mark=2, direction="y", P1=21, P2=21)]}
    r = solve_reference(d, "1")
    assert r["disg"]["2"]["dy"] == pytest.approx(3, abs=1e-13)
    assert r["disg"]["2"]["rz"] == pytest.approx(0, abs=1e-13)


def test_spatial_couple_at_material_interface_matches_compliance_integral():
    d = frame()
    d["element"]["1"]["2"] = {**d["element"]["1"]["1"], "E": 8000, "G": 3200}
    d["rigid"] = [dict(m=1, Ilength=1, Jlength=0, e=2)]
    d["load"]["1"] = {"load_member": [dict(m=1, mark=11, direction="z", L1=1, P1=6)]}
    r = solve_reference(d, "1")
    assert r["disg"]["2"]["dy"] == pytest.approx(6 * (4 - 0.5) / (8000 * 5), abs=1e-14)
    assert r["reac"]["1"]["mz"] == pytest.approx(-6, abs=1e-13)


SAMPLES = [f"bar/3D_Sample{number:02d}" for number in range(1, 11)] + [
    "shell/3D_Sample01"
]
CASES = [
    (sample, case)
    for sample in SAMPLES
    for case in json.loads(
        (ROOT / f"tests/data/{sample}.json").read_text(encoding="utf8")
    )["load"]
]


@pytest.mark.parametrize("sample,case", CASES)
def test_saved_spatial_reference_matches_independent_series(sample, case):
    data = json.loads((ROOT / f"tests/data/{sample}.json").read_text(encoding="utf8"))
    assert_dict_almost_equal(data["result"][case], solve_reference(data, case))
