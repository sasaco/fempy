"""Zero-section members transfer loads to endpoints without elastic stiffness."""

import copy

import numpy as np
import pytest

from tests.support.builders.linear_frame import cantilever, run

pytestmark = pytest.mark.integration


@pytest.mark.parametrize(
    "q0,q1,a,b", [(3, 3, 0, 2), (0, 6, 0, 2), (6, 0, 0.3, 1.7), (2, -3, 0.2, 1.3)]
)
@pytest.mark.parametrize("direction", ["x", "y", "z", "r", "gy"])
def test_transfer_conserves_force_and_first_moment(q0, q1, a, b, direction):
    data = cantilever()
    data["element"]["1"]["2"].update(A=0, Iy=0, Iz=0, J=0)
    data["fix_node"]["1"].append(dict(n=2, tx=1, ty=1, tz=1, rx=1, ry=1, rz=1))
    data["load"]["1"] = dict(
        load_member=[
            dict(m=1, mark=2, direction=direction, P1=q0, P2=q1, L1=a, L2=2 - b)
        ]
    )
    original = copy.deepcopy(data)
    model, result = run(data)
    # Integral of q and x*q; independent statics, including a partial trapezoid.
    total = (b - a) * (q0 + q1) / 2
    first = a * total + (b - a) ** 2 * (q0 + 2 * q1) / 6
    expected = np.zeros(12)
    component = {"x": 0, "y": 1, "z": 2, "r": 3, "gy": 1}[direction]
    expected[component], expected[6 + component] = total - first / 2, first / 2
    assert len(model.mesh.nodes) == 2
    np.testing.assert_array_equal(
        model.elements[1].get_stiffness_matrix(), np.zeros((12, 12))
    )
    np.testing.assert_allclose(model.solver.load_vector, expected, atol=1e-12)
    np.testing.assert_allclose(
        list(result["reaction_forces"][1].values()), -expected[:6], atol=1e-12
    )
    np.testing.assert_allclose(
        list(result["reaction_forces"][2].values()), -expected[6:], atol=1e-12
    )
    np.testing.assert_allclose(
        result["element_stresses"][1]["i_end"], -expected[:6], atol=1e-12
    )
    assert data == original


def test_transfer_rotates_local_load_and_preserves_point_couple():
    data = cantilever()
    data["node"]["2"] = dict(x=0, y=2, z=0)
    data["element"]["1"]["2"].update(A=0, Iy=0, Iz=0, J=0)
    data["fix_node"]["1"].append(dict(n=2, tx=1, ty=1, tz=1, rx=1, ry=1, rz=1))
    data["load"]["1"] = dict(
        load_member=[
            dict(m=1, mark=1, direction="x", P1=8, L1=0.5),
            dict(m=1, mark=11, direction="gz", P1=4, L1=1.5),
        ]
    )
    model, _ = run(data)
    np.testing.assert_allclose(
        model.solver.load_vector, [0, 6, 0, 0, 0, 1, 0, 2, 0, 0, 0, 3], atol=1e-12
    )


def test_transfer_does_not_support_an_unrestrained_structure():
    data = cantilever()
    data["element"]["1"]["2"].update(A=0, Iy=0, Iz=0, J=0)
    with pytest.raises(ValueError, match="Singular"):
        run(data)
