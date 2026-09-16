"""integration / model contracts contracts."""

import numpy as np
import pytest

from fem.boundary_condition import BoundaryCondition
from fem.file_io import result_to_jsonable
from fem.material import BarParameter
from fem.model import FemModel
from tests.support.builders.nonlinear_solver import axial_bar

pytestmark = pytest.mark.integration


def test_public_legacy_ids_are_insertion_indices_and_modern_ids_are_retained():
    from fem.material import MaterialProperty
    from fem.model import FemModel

    m = FemModel()
    for key, xyz in zip([10, 20, 30, 40], [[0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0]]):
        m.mesh.add_node(key, xyz)
        m.boundary.add_restraint(key, [True] * 6)
    m.material.add_material(1, MaterialProperty("plate", 1000.0, 0.25))
    m.mesh.add_element(42, "shell", [10, 20, 30], 1, thickness=0.1, formulation="dkt")
    m.mesh.add_element(17, "shell", [10, 30, 40], 1, thickness=0.1, formulation="dkt")
    for mode in ["static", "material_nonlinear"]:
        result = m.run(mode)
        assert set(result["shell_results"]) == {42, 17}
        assert set(result["legacy_shell_results"]) == {0, 1}
        for step in result.get("step_results", []):
            assert set(step["legacy_shell_results"]) == {0, 1}


@pytest.mark.parametrize("analysis", ["static", "material_nonlinear"])
def test_public_shell_output_with_noncontiguous_ids(analysis):
    m = FemModel()
    for nid, xyz in {10: [0, 0, 0], 20: [2, 0, 0], 30: [2, 3, 0], 40: [0, 3, 0]}.items():
        m.add_node(nid, *xyz)
        m.add_restraint(nid, True, True, True, True, True, True)
        m.add_forced_displacement(nid, dx=0.002 * xyz[0])
    m.add_material(1, "test", 1200, 0.2)
    m.add_element(17, "shell", [10, 20, 30, 40], 1, thickness=0.2)
    m.analysis_params["load_factors"] = [0.5, 1.0]
    r = result_to_jsonable(m.run(analysis))
    assert set(r["shell_results"]) == {"17"}
    assert r["shell_results"]["17"]["raw_result"]["elemStress1"][0] == pytest.approx(2.5)
    assert np.isfinite(r["shell_results"]["17"]["strain_energy"])
    if analysis == "material_nonlinear":
        assert r["step_results"][0]["shell_results"]["17"]["raw_result"]["elemStress1"][0] == pytest.approx(
            1.25
        )
        assert r["step_results"][1]["shell_results"] == r["shell_results"]


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("analysis_type", ["static", "material_nonlinear"])
def test_nonconsecutive_node_ids_preserve_reactions_and_displacements(analysis_type):
    mesh, material, _, _ = axial_bar()
    mesh.nodes = {307: mesh.nodes[2], 10: mesh.nodes[1]}  # Also deliberately unsorted.
    mesh.elements[1]["nodes"] = [10, 307]
    boundary = BoundaryCondition()
    boundary.add_restraint(10, [True] * 6)
    boundary.add_restraint(307, [False, True, True, True, True, True])
    boundary.add_load(307, [30, 0, 0, 0, 0, 0])
    model = FemModel()
    model.mesh, model.material, model.boundary = mesh, material, boundary
    material.add_bar_parameter(1, BarParameter(3, 1, 1, 1))
    result = model.run(analysis_type)
    assert result["node_displacements"][307]["dx"] == pytest.approx(0.01)
    assert result["node_displacements"][10]["dx"] == pytest.approx(0, abs=1e-14)
    assert result["reaction_forces"][10]["fx"] == pytest.approx(-30)
