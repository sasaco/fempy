"""integration / pressure solution contracts."""

import copy
import json

import numpy as np
import pytest

from fem.file_io import _read_json_model
from fem.model import FemModel
from fem.v0_io import read_v0_model as read_source_model
from tests.support.paths import ROOT

pytestmark = pytest.mark.integration


def test_pressure_fix_is_identical_in_json_and_original_format_and_balances_load():
    base = ROOT / "tests/data/shell/shellPressureTest1"
    data = json.loads(base.with_suffix(".json").read_text(encoding="utf8"))
    sources = [
        _read_json_model(copy.deepcopy(data)),
        read_source_model(base.with_suffix(".fem").read_text(encoding="utf8").splitlines()),
    ]
    results = []
    for parsed in sources:
        m = FemModel()
        m.read_json_model(parsed)
        assert all(m.boundary.restraints[1].dof_restraints)
        result = m.run()
        results.append(result)
        assert list(result["node_displacements"][1].values()) == [0.0] * 6
        # Integral of -1000 ez over the unit square and moments about node 1.
        r = result["reaction_forces"][1]
        np.testing.assert_allclose(
            [r[k] for k in ("fx", "fy", "fz", "mx", "my", "mz")],
            [0, 0, 1000, 500, -500, 0],
            rtol=1e-8,
            atol=1e-10,
        )
    for node in results[0]["node_displacements"]:
        np.testing.assert_allclose(
            list(results[0]["node_displacements"][node].values()),
            list(results[1]["node_displacements"][node].values()),
            rtol=1e-8,
            atol=1e-10,
        )


def test_releasing_pressure_fixture_rotations_creates_a_loaded_rigid_mode():
    import numpy as np

    from fem.file_io import _read_json_model
    from fem.model import FemModel

    data = json.loads((ROOT / "tests/data/shell/shellPressureTest1.json").read_text(encoding="utf-8"))
    data["boundary_conditions"]["restraints"]["1"]["dof"][3:] = [False] * 3
    m = FemModel()
    m.read_json_model(_read_json_model(data))
    element = m.elements[1]
    coordinates = element.get_element_coordinates()
    rigid = np.zeros((4, 6))
    rigid[:, :3] = np.cross([0.0, 1.0, 0.0], coordinates)
    rigid[:, 4] = 1.0
    # A rotation about node 1 satisfies its three translation restraints.
    np.testing.assert_array_equal(rigid[0, :3], 0.0)
    scaled_stiffness = element.get_stiffness_matrix() / (205e9 * 0.01)
    np.testing.assert_allclose(scaled_stiffness @ rigid.ravel(), 0.0, atol=1e-12)
    pressure = element.get_equivalent_nodal_loads("pressure", [1000.0], "F1")
    assert pressure @ rigid.ravel() == pytest.approx(500.0, abs=1e-10)
    with pytest.raises(ValueError, match="Singular"):
        m.run()
