"""Independent surface tensor, resultant and energy acceptance tests."""

import numpy as np

from fem.elements.shell_element import ShellElement
from fem.material import Material, MaterialProperty


def shell(n=4, rotation=None):
    xy = np.array([[0, 0, 0], [2, 0, 0], [2, 3, 0], [0, 3, 0.0]])[:n]
    rotation = np.eye(3) if rotation is None else rotation
    e = ShellElement(17, list(range(10, 10 + n)), 1, 0.2)
    m = Material()
    m.add_material(1, MaterialProperty("test", 1200.0, 0.2))
    e.set_material_properties(m)
    e.set_node_coordinates(dict(zip(e.node_ids, xy @ rotation.T)))
    return e, xy
