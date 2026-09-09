"""integration / plate bending contracts."""

import numpy as np
import pytest

from fem.material import MaterialProperty

pytestmark = pytest.mark.integration


@pytest.mark.parametrize("segments", [1, 4])
@pytest.mark.parametrize("thickness", [0.002, 0.2])
def test_dkt_cantilever_constant_moment(segments, thickness):
    from fem.model import FemModel

    m = FemModel()
    m.material.add_material(1, MaterialProperty("plate", 1000.0, 0.0))
    for i in range(segments + 1):
        for side in (0, 1):
            m.mesh.add_node(2 * i + side + 1, [2 * i / segments, side, 0.0])
    for i in range(segments):
        for j, nodes in enumerate(([2 * i + 1, 2 * i + 3, 2 * i + 4], [2 * i + 1, 2 * i + 4, 2 * i + 2])):
            m.mesh.add_element(2 * i + j + 1, "shell", nodes, 1, thickness=thickness, formulation="dkt")
    for n in (1, 2):
        m.boundary.add_restraint(n, [True] * 6)
    curvature = 0.003
    moment = curvature * 1000 * thickness**3 / 12
    for n in (2 * segments + 1, 2 * segments + 2):
        m.boundary.add_load(n, [0.0, 0.0, 0.0, 0.0, moment / 2, 0.0])
    result = m.run()
    for n, actual in result["node_displacements"].items():
        x = 2 * ((n - 1) // 2) / segments
        assert actual["dz"] == pytest.approx(-0.5 * curvature * x * x, rel=1e-8, abs=1e-11)
        assert actual["ry"] == pytest.approx(curvature * x, rel=1e-8, abs=1e-11)


@pytest.mark.parametrize("vertical", [False, True])
@pytest.mark.parametrize("segments", [1, 4])
@pytest.mark.parametrize("thickness", [0.02, 0.2])
def test_quad_cantilever_constant_moment_full_solver(segments, thickness, vertical):
    from fem.model import FemModel

    m = FemModel()
    m.material.add_material(1, MaterialProperty("plate", 1000.0, 0.0))
    rotation = np.array([[0.0, 0.0, 1.0], [1.0, 0.0, 0.0], [0.0, 1.0, 0.0]]) if vertical else np.eye(3)
    for i in range(segments + 1):
        for side in (0, 1):
            m.mesh.add_node(2 * i + side + 1, rotation @ np.array([2 * i / segments, side, 0.0]))
    for i in range(segments):
        m.mesh.add_element(
            i + 1, "shell", [2 * i + 1, 2 * i + 3, 2 * i + 4, 2 * i + 2], 1, thickness=thickness
        )
    for n in (1, 2):
        m.boundary.add_restraint(n, [True] * 6)
    moment = 1e-5
    for n in (2 * segments + 1, 2 * segments + 2):
        m.boundary.add_load(n, np.r_[np.zeros(3), rotation @ np.array([0.0, moment / 2, 0.0])])
    result = m.run()
    curvature = moment / (1000 * thickness**3 / 12)
    for n, actual in result["node_displacements"].items():
        x = 2 * ((n - 1) // 2) / segments
        expected = np.r_[
            rotation @ np.array([0.0, 0.0, -0.5 * curvature * x * x]),
            rotation @ np.array([0.0, curvature * x, 0.0]),
        ]
        np.testing.assert_allclose(list(actual.values()), expected, rtol=1e-8, atol=1e-11)
