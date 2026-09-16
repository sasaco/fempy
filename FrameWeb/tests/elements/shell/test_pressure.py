"""Surface-pressure nodal values, face direction and force/moment balance."""

import numpy as np
import pytest

from fem.elements.shell_element import ShellElement
from fem.material import MaterialProperty
from tests.support.builders.shell_kinematics import shell

pytestmark = pytest.mark.unit

CASES = [
    (n, face, vertical, 7.0, False) for n in (3, 4) for face in ("F1", "F2") for vertical in (False, True)
]
CASES += [(3, face, False, pressure, True) for pressure in (100.0, 1000.0, 10000.0) for face in ("F1", "F2")]
CASES += [(4, "F1", False, 1000.0, True)]


@pytest.mark.parametrize("n,face,vertical,pressure,unit_geometry", CASES)
def test_surface_pressure_nodal_values_and_equilibrium(n, face, vertical, pressure, unit_geometry):
    rotation = np.array([[0.0, 0.0, 1.0], [1.0, 0.0, 0.0], [0.0, 1.0, 0.0]]) if vertical else np.eye(3)
    if unit_geometry:
        coords = (
            np.array([[0.0, 0.0, 0.0], [1.0, 0.0, 0.0], [0.5, 1.0, 0.0]])
            if n == 3
            else np.array([[0.0, 0.0, 0.0], [1.0, 0.0, 0.0], [1.0, 1.0, 0.0], [0.0, 1.0, 0.0]])
        )
        element = ShellElement(1, list(range(1, n + 1)), 1, 0.01)
        element.set_node_coordinates(dict(enumerate(coords, 1)))
        element.set_material_properties(MaterialProperty("Steel", 2.05e11, 0.3, density=7850.0))
        area = 0.5 if n == 3 else 1.0
    else:
        element, coords = shell(n, rotation)
        area = 3.0 if n == 3 else 6.0
    sign = -1 if face == "F1" else 1
    values = element.get_equivalent_nodal_loads("pressure", [pressure], face)
    assert values.shape == (6 * n,)
    loads = values.reshape(n, 6)
    resultant = rotation @ np.array([0.0, 0.0, sign * pressure * area])
    np.testing.assert_allclose(loads[:, :3], np.tile(resultant / n, (n, 1)), atol=1e-12)
    np.testing.assert_allclose(loads[:, 3:], 0.0, atol=1e-12)
    np.testing.assert_allclose(loads[:, :3].sum(axis=0), resultant, atol=1e-12)
    np.testing.assert_allclose(
        np.cross(coords, loads[:, :3]).sum(axis=0), np.cross(coords.mean(axis=0), resultant), atol=1e-12
    )
    opposite = element.get_equivalent_nodal_loads("pressure", [pressure], "F2" if face == "F1" else "F1")
    np.testing.assert_allclose(values, -opposite, atol=1e-12)
    assert abs(np.abs(values).sum() - pressure * area) < 5e-11


@pytest.mark.parametrize(
    "face,values", [("F3", [1.0]), ("bad", [1.0]), ("F1", [float("nan")]), ("F1", [1.0, 2.0])]
)
def test_invalid_surface_pressure_never_falls_back(face, values):
    element, _ = shell(4)
    with pytest.raises(ValueError):
        element.get_equivalent_nodal_loads("pressure", values, face)


@pytest.mark.parametrize(
    "kind,values,face,error",
    [
        ("invalid_type", [1000.0], "F1", NotImplementedError),
        ("pressure", [], "F1", ValueError),
        ("pressure", [1000.0], None, ValueError),
    ],
)
def test_invalid_triangle_pressure(kind, values, face, error):
    element = ShellElement(1, [1, 2, 3], 1, 0.01)
    element.set_node_coordinates(
        {1: np.array([0.0, 0.0, 0.0]), 2: np.array([1.0, 0.0, 0.0]), 3: np.array([0.5, 1.0, 0.0])}
    )
    element.set_material_properties(MaterialProperty("Steel", 2.05e11, 0.3, density=7850.0))
    with pytest.raises(error):
        element.get_equivalent_nodal_loads(kind, values, face)
