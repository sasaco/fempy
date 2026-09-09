"""Original linear element fixtures with explicit geometry and material."""

from types import SimpleNamespace

import numpy as np

from fem.elements.shell_element import ShellElement
from fem.elements.solid_element import HexaElement, TetraElement, WedgeElement
from fem.material import Material, ShellParameter

SOLIDS = {
    "tetra": (TetraElement, [[0, 0, 0], [1, 0, 0], [0, 1, 0], [0, 0, 1]], [0.25, 0.25, 0.25]),
    "hexa": (
        HexaElement,
        [[0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0], [0, 0, 1], [1, 0, 1], [1, 1, 1], [0, 1, 1]],
        [0, 0, 0],
    ),
    "wedge": (WedgeElement, [[0, 0, 0], [1, 0, 0], [0, 1, 0], [0, 0, 1], [1, 0, 1], [0, 1, 1]], [0, 0, 0]),
}


def material():
    value = Material()
    value.materials = {1: SimpleNamespace(E=210e9, G=80e9, density=7850, nu=0.3)}
    return value


def solid(kind):
    cls, coords, point = SOLIDS[kind]
    element = cls(1, list(range(1, len(coords) + 1)), 1)
    element.set_node_coordinates(dict(enumerate(np.array(coords, dtype=float), 1)))
    element.set_material_properties(material())
    return element, np.array(point, dtype=float)


def shell(n):
    coords = [[0, 0, 0], [1, 0, 0], [0, 1, 0]] if n == 3 else [[0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0]]
    element = ShellElement(1, list(range(1, n + 1)), 1, 0.1)
    element.set_node_coordinates(dict(enumerate(np.array(coords, dtype=float), 1)))
    element.set_material_properties(material(), ShellParameter(0.1, 1))
    return element, np.array([1 / 3, 1 / 3] if n == 3 else [0.0, 0.0])
