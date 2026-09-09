"""Rigid motion and consistent-load work checks for the shared linear path."""

import numpy as np

from fem.elements.loaded_bar_element import LoadedBarElement
from fem.material import BarParameter, Material, MaterialProperty


def beam():
    e = LoadedBarElement(1, [1, 2], 1, 1)
    e.set_node_coordinates({1: np.zeros(3), 2: np.array([4.8, 0.0, 0.0])})
    m = Material()
    m.add_material(1, MaterialProperty("stiff", 2.65e10, 0.25))
    e.set_material_properties(m, BarParameter(1.0, 1.0, 1.0, 1.0))
    return e
