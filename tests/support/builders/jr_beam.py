"""Phase 3 integration: hand-calculated JR cycles at the central section."""

import numpy as np

from fem.elements.nonlinear_bar_element import NonlinearBarElement
from fem.material import BarParameter, Material, MaterialProperty
from fem.nonlinear.hysteresis import JRStiffnessReductionParams


def beam(dof):
    e = NonlinearBarElement(1, [10, 307], 1, 1)
    e.set_node_coordinates({10: np.zeros(3), 307: np.array([2.0, 0.0, 0.0])})
    mat = Material()
    mat.add_material(1, MaterialProperty("test", 2000.0, 0.25))
    e.set_material_properties(mat, BarParameter(3.0, 0.4, 0.9, 0.7, 0.6, 0.8))
    # Small strain/curvature: x=delta/.001, slopes are 10000,2000,1000,0.
    e.set_hysteresis_model(dof, JRStiffnessReductionParams.symmetric(0.001, 0.004, 0.01, 10, 16, 22, beta=0))
    return e


def motion(dof, x):
    u = np.zeros(12)
    i, j = NonlinearBarElement.DOF_MAPPING[dof]
    u[i], u[j] = -x * 0.001, x * 0.001  # L=2; opposing rotations cancel shear.
    return u
