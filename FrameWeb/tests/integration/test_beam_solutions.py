"""integration / beam solutions contracts."""

import copy

import numpy as np
import pytest

from fem.material import BarParameter, MaterialProperty
from fem.model import FemModel
from tests.support.builders.nonlinear_beam import RIGIDITIES, beam

pytestmark = pytest.mark.integration


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("shear", [False, True])
@pytest.mark.parametrize("count", [1, 4])
@pytest.mark.parametrize("axis,rotation,ei,ga,sign", [(1, 5, 1800, 1440, 1), (2, 4, 800, 1920, -1)])
def test_cantilever_tip_load_matches_hand_solution_and_mesh_refinement(
    shear, count, axis, rotation, ei, ga, sign
):
    length, load = 2.0, 0.01
    k = np.zeros((6 * (count + 1), 6 * (count + 1)))
    for n in range(count):
        e = beam(dofs=RIGIDITIES, length=length / count, shear=shear)
        k[6 * n : 6 * n + 12, 6 * n : 6 * n + 12] += e.get_tangent_stiffness_matrix(np.zeros(12))
    f = np.zeros(6 * (count + 1))
    f[-6 + axis] = load
    u = np.zeros_like(f)
    u[6:] = np.linalg.solve(k[6:, 6:], f[6:])
    expected = load * length**3 / (3 * ei) + (load * length / ga if shear else 0)
    assert u[-6 + axis] == pytest.approx(expected, rel=1e-11)
    assert u[-6 + rotation] == pytest.approx(sign * load * length**2 / (2 * ei), rel=1e-11)


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("analysis", ["static", "material_nonlinear"])
def test_fem_model_publishes_section_forces_with_nonconsecutive_nodes(analysis):
    model = FemModel()
    model.mesh.add_node(10, [0, 0, 0])
    model.mesh.add_node(307, [2, 0, 0])
    model.material.add_bar_parameter(1, BarParameter(3, 0.4, 0.9, 0.7))
    model.boundary.add_restraint(10, [True] * 6)
    model.boundary.add_restraint(307, [False, True, True, True, True, True])
    model.boundary.add_load(307, [9, 0, 0, 0, 0, 0])
    if analysis == "material_nonlinear":
        model.add_nonlinear_material(1, "test", 2000, 0.001, 0.005, 0.02, 6, 12, 21)
        model.add_nonlinear_bar_element(1, [10, 307], 1, 1, ["axial"])
    else:
        model.material.add_material(1, MaterialProperty("test", 2000, 0.25))
        model.mesh.add_element(1, "bar", [10, 307], 1, section_id=1)
    model.analysis_params.update(n_load_steps=3, tolerance=1e-10)
    result = model.run(analysis)
    expected_u = 0.006 if analysis == "material_nonlinear" else 0.003
    assert result["node_displacements"][307]["dx"] == pytest.approx(expected_u, abs=1e-12)
    output = result["element_stresses"][1]
    np.testing.assert_allclose(output["i_end"], [-9, 0, 0, 0, 0, 0], atol=1e-10)
    np.testing.assert_allclose(output["j_end"], [9, 0, 0, 0, 0, 0], atol=1e-10)
    assert result["reaction_forces"][10]["fx"] == pytest.approx(-9)


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("axis,rotation,ei,sign", [("moment_y", 4, 800, -1), ("moment_z", 5, 1800, 1)])
@pytest.mark.parametrize("count", [1, 4])
def test_nonlinear_pure_bending_solution_and_output_under_refinement(axis, rotation, ei, sign, count):
    model = FemModel()
    model.material.add_bar_parameter(1, BarParameter(3, 0.4, 0.9, 0.7, 0.6, 0.8))
    model.add_nonlinear_material(
        1, "test", 2000, 0.001, 0.005, 0.02, ei * 0.001, ei * 0.002, ei * 0.0035, nu=0.25
    )
    for n in range(count + 1):
        model.mesh.add_node(10 + n * 100, [2 * n / count, 0, 0])
    for n in range(count):
        model.add_nonlinear_bar_element(n + 1, [10 + n * 100, 110 + n * 100], 1, 1, [axis])
    model.boundary.add_restraint(10, [True] * 6)
    load = np.zeros(6)
    # M/EI=.0015 -> kappa=.001 + (.0015-.001)/.25 = .003.
    load[rotation] = ei * 0.0015
    model.boundary.add_load(10 + count * 100, load)
    model.analysis_params.update(n_load_steps=3, tolerance=1e-10)
    result = model.run("material_nonlinear")
    displacement = result["displacement"]
    transverse = 2 if rotation == 4 else 1
    assert displacement[-6 + rotation] == pytest.approx(0.006, abs=1e-12)
    assert displacement[-6 + transverse] == pytest.approx(sign * 0.006, abs=1e-12)
    for elem_id, output in result["element_stresses"].items():
        expected = np.zeros(6)
        expected[rotation] = ei * 0.0015
        np.testing.assert_allclose(output["i_end"], -expected, atol=2e-10)
        np.testing.assert_allclose(output["j_end"], expected, atol=2e-10)
        assert model.elements[elem_id].committed_states[axis]["center"].current_delta == pytest.approx(0.003)
    before = copy.deepcopy([e.committed_states for e in model.elements.values()])
    model._post_process_results()
    assert [e.committed_states for e in model.elements.values()] == before
