"""Physical shell invariants; no reference values come from a FEM solver."""

import numpy as np

from fem.elements.shell_element import ShellElement
from fem.material import Material, MaterialProperty


def shell(n, rotation=np.eye(3)):
    coords = np.array([[0.0, 0.0, 0.0], [2.0, 0.0, 0.0], [2.0, 3.0, 0.0], [0.0, 3.0, 0.0]])[:n]
    coords = coords @ rotation.T
    e = ShellElement(1, list(range(n)), 1, 0.2)
    m = Material()
    m.add_material(1, MaterialProperty("patch", 1000.0, 0.25, density=2.0))
    e.set_material_properties(m)
    e.set_node_coordinates(dict(enumerate(coords)))
    return e, coords
