"""validation / solid sources contracts."""

import copy
import json

import numpy as np
import pytest

from fem.file_io import _read_json_model, result_to_jsonable
from fem.model import FemModel
from tests.support.assertions import assert_dict_almost_equal
from tests.support.oracles.source_solid import solve_source
from tests.support.paths import ROOT
from tests.support.provenance import read_source_records
from tests.support.repairs.solid_sources import STEMS
from tests.support.repairs.support_reactions import completed_reference

pytestmark = [pytest.mark.oracle, pytest.mark.slow, pytest.mark.requires_node]


@pytest.mark.parametrize("stem", ["sampleBendHexa2", "sampleBendWedge2", "sampleBendTetra2"])
def test_quadratic_solid_all_outputs_against_independent_decimal_source(stem):
    from fem.file_io import _read_json_model
    from fem.model import FemModel
    from tests.support.sample_runner import compare_section_cut_result

    path = ROOT / "tests/data/bend" / (stem + ".json")
    before = path.read_bytes()
    data = json.loads(before)
    source_path = ROOT / "docs/v0/testdata/bend" / (stem + ".out")
    reference = solve_source(source_path, decimal_stiffness=True)
    fixed = completed_reference(data, read_source_records(source_path), reference)
    model = FemModel()
    model.read_json_model(_read_json_model(copy.deepcopy(data)))
    compare_section_cut_result(model.run(), fixed["result"]["1"], model, data)
    assert path.read_bytes() == before


def test_tetrahedron_all_outputs_against_source_input_solution(tetra_reference):
    from fem.file_io import _read_json_model
    from fem.model import FemModel
    from tests.support.repairs.tetrahedron_reference import completed_data
    from tests.support.sample_runner import compare_section_cut_result

    data, source, ref = tetra_reference
    fixed = completed_data(data, source, ref)
    model = FemModel()
    model.read_json_model(_read_json_model(copy.deepcopy(data)))
    result = model.run()
    compare_section_cut_result(result, fixed["result"]["1"], model, data)


@pytest.mark.parametrize("stem", STEMS)
def test_solid_displacements_and_global_equilibrium(stem):
    path = ROOT / "tests/data/bend" / (stem + ".json")
    before = path.read_bytes()
    source = read_source_records(ROOT / "docs/v0/testdata/bend" / (stem + ".out"))
    data = json.loads(before)
    model = FemModel()
    model.read_json_model(_read_json_model(copy.deepcopy(data)))
    result = result_to_jsonable(model.run())
    assert_dict_almost_equal(result["node_displacements"], source["displacements"])
    force, moment = np.zeros(3), np.zeros(3)
    for node, load in source["loads"].items():
        force += load[:3]
        moment += np.cross(source["nodes"][node], load[:3]) + load[3:]
    for node, reaction in result["reaction_forces"].items():
        reaction_force = np.array([reaction[k] for k in ("fx", "fy", "fz")])
        force += reaction_force
        moment += np.cross(source["nodes"][node], reaction_force)
    linear = stem.endswith("1")
    np.testing.assert_allclose(force, 0.0, atol=1e-8 if linear else 1e-7)
    np.testing.assert_allclose(moment, 0.0, atol=1e-7 if linear else 1e-5)
    assert path.read_bytes() == before
