"""Beam construction and prescribed kinematic fields."""

import numpy as np

from fem.elements.bar_element import BEBarElement
from fem.elements.nonlinear_bar_element import NonlinearBarElement
from fem.material import BarParameter, Material, MaterialProperty
from fem.nonlinear.hysteresis import JRStiffnessReductionParams

RIGIDITIES = {"axial": 6000.0, "torsion": 560.0, "moment_y": 800.0, "moment_z": 1800.0}


def skeleton(k):
    # Slopes k, k/4, k/10. Breakpoints deliberately separated from FD probes.
    return JRStiffnessReductionParams.symmetric(
        0.001, 0.005, 0.02, k * 0.001, k * 0.002, k * 0.0035, beta=0.4
    )


def beam(cls=NonlinearBarElement, dofs=(), length=2.0, shear=True, rotated=False):
    kwargs = {} if cls is BEBarElement else {"shear_correction": shear}
    e = cls(1, [10, 307], 1, 1, angle=23 if rotated else 0, **kwargs)
    origin = np.array([1.0, -2.0, 3.0])
    axis = np.array([2.0, -3.0, 6.0]) / 7 if rotated else np.array([1.0, 0.0, 0.0])
    e.set_node_coordinates({10: origin, 307: origin + length * axis})
    mat = Material()
    mat.add_material(1, MaterialProperty("test", 2000.0, 0.25))
    e.set_material_properties(mat, BarParameter(3.0, 0.4, 0.9, 0.7, 0.6, 0.8))
    for dof in dofs:
        e.set_hysteresis_model(dof, skeleton(RIGIDITIES[dof]))
    return e


def force(e, u):
    if isinstance(e, NonlinearBarElement):
        return e.get_internal_force(u)
    return e.get_stiffness_matrix() @ u


def axial_motion(q):
    u = np.zeros(12)
    u[6] = 2 * q
    return u
