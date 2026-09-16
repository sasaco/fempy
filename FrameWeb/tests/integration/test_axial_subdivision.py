"""Generated points of axial members must not introduce transverse mechanisms."""

import numpy as np
import pytest

from tests.support.builders.linear_frame import cantilever, run

pytestmark = pytest.mark.integration


@pytest.mark.parametrize("angle", [0, 0.37, 1.5707963267948966])
@pytest.mark.parametrize("points", [[0.5], [0.3, 0.7, 1.2, 1.7]])
def test_axial_solution_is_invariant_under_subdivision_and_rotation(angle, points):
    d = cantilever()
    axis = np.array([np.cos(angle), np.sin(angle), 0.0])
    d["node"]["2"] = dict(zip(("x", "y", "z"), 2 * axis))
    d["element"]["1"]["2"].update(Iy=0, Iz=0, J=0)
    d["fix_node"]["1"].append(dict(n=2, tx=1, ty=1, tz=1, rx=1, ry=1, rz=1))
    d["load"]["1"] = dict(load_member=[dict(m=1, mark=2, direction="x", P1=3, P2=3)])
    d["notice_points"] = [dict(m=1, Points=points)]
    m, r = run(d)
    for node, xyz in m.mesh.nodes.items():
        x = np.dot(xyz, axis)
        expected = axis * 3 * x * (2 - x) / (2 * 2000)
        np.testing.assert_allclose(
            list(r["node_displacements"][node].values())[:3], expected, atol=1e-12
        )
    for node in (1, 2):
        np.testing.assert_allclose(
            list(r["reaction_forces"][node].values())[:3], -3 * axis, atol=1e-10
        )
    assert len(r["interpolated_displacements"]) == len(points)


def test_axial_subdivision_does_not_hide_a_transverse_point_load():
    d = cantilever()
    d["element"]["1"]["2"].update(Iy=0, Iz=0, J=0)
    d["fix_node"]["1"].append(dict(n=2, tx=1, ty=1, tz=1, rx=1, ry=1, rz=1))
    d["load"]["1"] = dict(load_member=[dict(m=1, mark=1, direction="y", P1=3, L1=1)])
    with pytest.raises(ValueError, match="unsupported transverse"):
        run(d)


def test_original_axial_endpoint_mechanism_is_still_rejected():
    d = cantilever()
    d["element"]["1"]["2"].update(Iy=0, Iz=0, J=0)
    d["notice_points"] = [dict(m=1, Points=[1])]
    with pytest.raises(ValueError, match="Singular"):
        run(d)


def test_transverse_interpolation_is_explicit_and_does_not_create_reaction():
    d = cantilever()
    d["element"]["1"]["2"].update(Iy=0, Iz=0, J=0)
    d["fix_node"]["1"].append(dict(n=2, tx=1, ty=1, tz=1, rx=1, ry=1, rz=1))
    d["notice_points"] = [dict(m=1, Points=[0.5])]
    d["load"]["1"] = {}
    model, _ = run(d)
    model.add_forced_displacement(1, dy=0.1)
    model.add_forced_displacement(2, dy=0.3)
    result = model.run()
    generated = next(n for n in model.mesh.nodes if n not in (1, 2))
    assert result["node_displacements"][generated]["dy"] == pytest.approx(0.15)
    assert generated in result["interpolated_displacements"]
    for node in (1, 2):
        assert result["reaction_forces"][node]["fy"] == pytest.approx(0, abs=1e-12)
