"""Independent scalar series must satisfy closed-form physics before reuse."""

import json

import pytest

from tests.support.assertions import assert_dict_almost_equal
from tests.support.oracles.plane_frame_series import solve_reference
from tests.support.paths import ROOT

pytestmark = pytest.mark.oracle


def simple_frame():
    return dict(
        dimension=2,
        node={"1": dict(x=0, y=0, z=0), "2": dict(x=4, y=0, z=0)},
        member={"1": dict(ni=1, nj=2, e=1, cg=0)},
        element={"1": {"1": dict(E=2000, A=3, Iz=5, G=1, Xp=0.00001)}},
        fix_node={"1": [dict(n=1, tx=1, ty=1, rz=1)]},
        load={"1": {}},
    )


def test_series_cantilever_tip_force():
    d = simple_frame()
    d["load"]["1"] = {"load_node": [dict(n=2, ty=6)]}
    r = solve_reference(d, "1")
    assert r["disg"]["2"]["dy"] == pytest.approx(6 * 4**3 / (3 * 2000 * 5), abs=1e-14)
    assert r["reac"]["1"]["ty"] == pytest.approx(-6, abs=1e-14)
    assert r["reac"]["1"]["mz"] == pytest.approx(-24, abs=1e-14)


def test_series_uniform_foundation_rigid_translation():
    d = simple_frame()
    d["fix_node"]["1"] = [dict(n=1, tx=1)]
    d["fix_member"] = {"1": [dict(m=1, ty=7)]}
    d["load"]["1"] = {"load_member": [dict(m=1, mark=2, direction="y", P1=21, P2=21)]}
    r = solve_reference(d, "1")
    for node in ("1", "2"):
        assert r["disg"][node]["dy"] == pytest.approx(3, abs=1e-14)
        assert r["disg"][node]["rz"] == pytest.approx(0, abs=1e-14)
    for component in ("fyi", "fyj", "mzi", "mzj"):
        assert r["fsec"]["1"]["P1"][component] == pytest.approx(0, abs=1e-14)


def test_series_free_thermal_expansion():
    d = simple_frame()
    d["load"]["1"] = {"load_member": [dict(m=1, mark=9, direction="x", P1=30)]}
    r = solve_reference(d, "1")
    assert r["disg"]["2"]["dx"] == pytest.approx(0.00001 * 30 * 4, abs=1e-14)
    assert r["fsec"]["1"]["P1"]["fxi"] == pytest.approx(0, abs=1e-14)


def test_series_interior_point_force():
    d = simple_frame()
    d["load"]["1"] = {"load_member": [dict(m=1, mark=1, direction="y", P1=6, L1=1.5)]}
    r = solve_reference(d, "1")
    assert r["disg"]["2"]["dy"] == pytest.approx(
        6 * 1.5**2 * (12 - 1.5) / (6 * 2000 * 5), abs=1e-14
    )
    assert r["reac"]["1"]["mz"] == pytest.approx(-9, abs=1e-14)


@pytest.mark.parametrize("position", [0.0, 4.0])
def test_series_endpoint_member_force_matches_nodal_force(position):
    d = simple_frame()
    d["load"]["1"] = {
        "load_member": [dict(m=1, mark=1, direction="y", P1=6, L1=position)]
    }
    r = solve_reference(d, "1")
    assert r["reac"]["1"]["ty"] == pytest.approx(-6, abs=1e-14)
    assert r["reac"]["1"]["mz"] == pytest.approx(-6 * position, abs=1e-14)
    assert r["disg"]["2"]["dy"] == pytest.approx(
        6 * position**2 * (12 - position) / (6 * 2000 * 5), abs=1e-14
    )


@pytest.mark.parametrize("axis", ["x", "y"])
def test_series_piecewise_rigidity_matches_compliance_integral(axis):
    d = simple_frame()
    d["element"]["1"]["2"] = {**d["element"]["1"]["1"], "E": 8000}
    d["rigid"] = [dict(m=1, Ilength=1, Jlength=0, e=2)]
    d["load"]["1"] = {"load_node": [dict(n=2, **{"t" + axis: 6})]}
    r = solve_reference(d, "1")
    if axis == "x":
        expected = 6 * (1 / (8000 * 3) + 3 / (2000 * 3))
    else:
        expected = 6 * ((4**3 - 3**3) / (3 * 8000 * 5) + 3**3 / (3 * 2000 * 5))
    assert r["disg"]["2"]["d" + axis] == pytest.approx(expected, abs=1e-14)
    assert r["fsec"]["1"]["P1"]["fxj" if axis == "x" else "fyj"] == pytest.approx(
        6 if axis == "x" else -6, abs=1e-14
    )


def test_series_axial_only_member_preserves_axial_solution():
    d = simple_frame()
    d["element"]["1"]["1"]["Iz"] = 0.0
    d["fix_node"]["1"].append(dict(n=2, ty=1))
    d["notice_points"] = [dict(m=1, Points=[1.0, 2.0])]
    d["load"]["1"] = {"load_node": [dict(n=2, tx=6)]}
    r = solve_reference(d, "1")
    assert r["disg"]["2"]["dx"] == pytest.approx(6 * 4 / (2000 * 3), abs=1e-14)
    assert r["disg"]["1n1"]["dx"] == pytest.approx(6 / (2000 * 3), abs=1e-14)
    assert r["disg"]["1n2"]["dy"] == 0.0
    assert r["fsec"]["1"]["P1"]["fxi"] == pytest.approx(6, abs=1e-14)


SAMPLES = tuple(
    f"2D_Sample{number:02d}" for number in (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 13)
)
CASES = [
    (sample, case)
    for sample in SAMPLES
    for case in json.loads(
        (ROOT / f"tests/data/bar/{sample}.json").read_text(encoding="utf8")
    )["load"]
]


@pytest.mark.parametrize("sample,case", CASES)
def test_saved_plane_frame_reference_matches_independent_series(sample, case):
    data = json.loads(
        (ROOT / f"tests/data/bar/{sample}.json").read_text(encoding="utf8")
    )
    assert_dict_almost_equal(data["result"][case], solve_reference(data, case))
