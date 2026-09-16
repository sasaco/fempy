"""Kirchhoff triangle: quadratic displacement and thin-plate work oracles."""

import numpy as np

from fem.elements.shell_element import ShellElement
from fem.material import Material, MaterialProperty


def triangle(thickness, rotation=np.eye(3)):
    xy = np.array([[0.0, 0.0, 0.0], [3.0, 0.0, 0.0], [0.7, 2.0, 0.0]])
    e = ShellElement(17, [5, 8, 20], 1, thickness, formulation="dkt")
    e.set_node_coordinates(dict(zip(e.node_ids, xy @ rotation.T)))
    m = Material()
    m.add_material(1, MaterialProperty("plate", 1000.0, 0.25))
    e.set_material_properties(m)
    return e, xy
