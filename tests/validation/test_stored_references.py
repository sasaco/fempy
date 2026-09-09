"""validation / stored references contracts."""

import json

import pytest

from tests.support.assertions import assert_dict_almost_equal
from tests.support.oracles.cantilever import cantilever_reference
from tests.support.oracles.cantilever_decimal import SOURCE, reference_values
from tests.support.paths import DATA, ROOT
from tests.support.provenance import read_source_records
from tests.support.repairs.triangle_reference import SAMPLE, completed_data

pytestmark = pytest.mark.oracle


@pytest.mark.material_nonlinear
@pytest.mark.parametrize(
    "moment,curvature",
    [
        (0.0, 0.0),
        (1000.0, -1e-5),
        (2000.0, -5.5e-5),
        (3000.0, -0.0001),
        (4850.0, -0.0009420275),
    ],
)
def test_cantilever_reference_inverse_at_hand_calculated_ordinates(moment, curvature):
    from tests.support.oracles.cantilever import cantilever_reference

    d = json.loads((DATA / "snap/beam001.json").read_text(encoding="utf-8"))
    # Current delta3=.0010103: kappa=-(.0001+(4850-3000)*.0009103/2000).
    factor = moment / (4.85 * d["load"]["1"]["load_node"][0]["tx"])
    nodes = cantilever_reference(d, factor)["node_displacements"]
    assert (nodes["3"]["rz"] - nodes["2"]["rz"]) / 0.1 == pytest.approx(curvature, abs=1e-14)


def test_stored_cantilever_all_steps_against_independent_scalar_reference():
    data = json.loads(SOURCE.read_text(encoding="utf-8"))
    saved = data["result"]
    n = data["load"]["1"]["n_load_steps"]
    assert set(saved) == {str(i) for i in range(n + 1)}
    assert saved == reference_values(data)
    for i in range(n + 1):
        expected = cantilever_reference(data, i / n)
        snapshot = saved[str(i)]
        for node, displacement in expected["node_displacements"].items():
            assert snapshot["disg"][node] == pytest.approx(displacement, abs=1e-12)
        reaction = expected["reaction_forces"]["4"]
        assert snapshot["reac"]["4"] == pytest.approx(
            dict(
                tx=reaction["fx"],
                ty=reaction["fy"],
                tz=reaction["fz"],
                mx=reaction["mx"],
                my=reaction["my"],
                mz=reaction["mz"],
            ),
            abs=1e-12,
        )
        force = i / n * data["load"]["1"]["load_node"][0]["tx"]
        for member, values in snapshot["fsec"].items():
            ni, nj = (str(data["member"][member][key]) for key in ("ni", "nj"))
            section = values["P1"]
            assert section["fyi"] == pytest.approx(-force, abs=1e-12)
            assert section["fyj"] == pytest.approx(-force, abs=1e-12)
            assert section["mzi"] == pytest.approx(force * (5 + data["node"][ni]["y"]), abs=1e-12)
            assert section["mzj"] == pytest.approx(force * (5 + data["node"][nj]["y"]), abs=1e-12)


def test_cantilever_hand_calculated_branch_crossings_and_tip_motion():
    data = json.loads(SOURCE.read_text(encoding="utf-8"))
    # F=10*s, |Mmid|=48.5*s: cross P1 between 20/21, P2 between 61/62.
    assert data["load"]["1"]["n_load_steps"] == 100
    assert data["load"]["1"]["load_node"][0]["tx"] == 1000
    for step, curvature in [
        (20, 0.0000097),
        (21, 0.0000108325),
        (61, 0.0000981325),
        (62, 0.00010318605),
        (100, 0.0009420275),
    ]:
        nodes = data["result"][str(step)]["disg"]
        assert (nodes["2"]["rz"] - nodes["3"]["rz"]) / 0.1 == pytest.approx(curvature, abs=1e-14)
    assert all("G" not in mat for mat in data["element"]["1"].values())
    assert data["result"]["2"]["disg"]["1"]["dx"] == pytest.approx(0.0000005001212578616352, abs=1e-15)
    assert data["result"]["100"]["disg"]["1"]["dx"] == pytest.approx(0.00045836690039308176, abs=1e-15)


def test_pressure_saved_reference_is_independent_exact_polynomial_solution():
    from tests.support.assertions import assert_dict_almost_equal
    from tests.support.repairs.pressure_reference import completed_data

    data = json.loads((ROOT / "tests/data/shell/shellPressureTest1.json").read_text(encoding="utf8"))
    assert_dict_almost_equal(data["result"], completed_data(data))


def test_tri1_conditions_match_the_original_fem():
    from tests.support.provenance import read_source_records

    source = read_source_records(ROOT / "docs/v0/testdata/bend/sampleBendTri1.fem")
    data = json.loads((ROOT / "tests/data/bend/sampleBendTri1.json").read_text(encoding="utf8"))
    assert data["shell"] == {
        k: dict(e=int(v["material"]), nodes=list(map(int, v["nodes"])), formulation="dkt")
        for k, v in source["elements"].items()
    }
    assert data["element"]["1"]["1"]["thickness"] == 15
    assert data["element"]["1"]["1"]["G"] == source["materials"]["1"]["G"]
    assert data["fix_node"]["1"] == [
        dict(n=n, **{k: int(v[2 * i]) for i, k in enumerate(("tx", "ty", "tz", "rx", "ry", "rz"))})
        for n, v in source["restraints"].items()
    ]


@pytest.mark.slow
@pytest.mark.requires_node
def test_all_tri1_outputs_match_original_operators_without_production_imports():
    data = json.loads((ROOT / SAMPLE).read_text(encoding="utf8"))
    reference, proof = completed_data(data)
    assert proof["maximum_free_force_residual"] < 1e-10
    assert_dict_almost_equal(data["result"], reference)
    source = read_source_records(ROOT / "docs/v0/testdata/bend/sampleBendTri1.out")
    assert_dict_almost_equal(reference["1"]["disg"], source["displacements"])
    assert len(reference["1"]["shell_results"]) == 240
