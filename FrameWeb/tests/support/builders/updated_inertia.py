"""Small independent displacement fields for the updated-inertia beam tests."""
import numpy as np

from fem.nonlinear.hysteresis import JRStiffnessReductionParams
from tests.support.builders.nonlinear_beam import beam


def specimen(axis="z", *, length=1., shear=False, rotated=False):
    element = beam(length=length, shear=shear, rotated=rotated)
    element.set_hysteresis_model("moment_"+axis, JRStiffnessReductionParams.symmetric(
        .001, .003, .005, .1, .12, .13, beta=0.))
    return element


def motion(element, axis, curvature, shear):
    local = np.zeros(12)
    rotation, translation = (5, 1) if axis == "z" else (4, 2)
    local[[rotation, rotation+6]] = [-curvature*element.length/2,
                                    curvature*element.length/2]
    local[translation+6] = shear*element.length
    return element.get_transformation_matrix(12).T @ local


def section_force(element, axis, displacement):
    local = element.get_transformation_matrix(12) @ element.get_internal_force(displacement)
    rotation, translation = (5, 1) if axis == "z" else (4, 2)
    return np.array([(local[rotation+6]-local[rotation])/2, local[translation+6]])
