"""Public beam references for accumulated shear and the full section tangent."""
from copy import deepcopy

import numpy as np
import pytest

from tests.support.builders.updated_inertia import specimen, motion, section_force

pytestmark = [pytest.mark.unit, pytest.mark.material_nonlinear]


@pytest.mark.parametrize("axis", ["y", "z"])
@pytest.mark.parametrize("shear", [False, True])
@pytest.mark.parametrize("rotated", [False, True])
@pytest.mark.parametrize("sign", [-1., 1.])
def test_public_force_crosses_breakpoint_and_preserves_nonsymmetric_tangent(axis, shear, rotated, sign):
    element = specimen(axis, shear=shear, rotated=rotated)
    u = motion(element, axis, sign*.002, sign*.001)
    ga = (1920. if axis == "y" else 1440.) if shear else None
    c1 = 1200. if ga is None else 1200.*ga/(1200.+ga)
    c2 = 120. if ga is None else 120.*ga/(120.+ga)
    average = (c1+c2)/2
    np.testing.assert_allclose(section_force(element, axis, u),
                               sign*np.array([.11, average*.001]), rtol=1e-12, atol=1e-13)
    # Independent generalized motion columns; virtual work: q.T f = L e.T p.
    q = np.column_stack([motion(element, axis, 1., 0.), motion(element, axis, 0., 1.)])
    k = element.get_tangent_stiffness_matrix(u)
    expected = np.array([[10., 0.], [.5*(c2-average), average]])
    np.testing.assert_allclose(q.T @ k @ q / element.length, expected, rtol=1e-12, atol=1e-12)
    for h in [1e-7, 1e-8]:
        fd = np.column_stack([(element.get_internal_force(u+h*d)-
                               element.get_internal_force(u-h*d))/(2*h) for d in np.eye(12)])
        np.testing.assert_allclose(k, fd, rtol=1e-6, atol=1e-7)


@pytest.mark.parametrize("axis", ["y", "z"])
def test_committed_shear_survives_reversal_read_calls_and_rollback(axis):
    element = specimen(axis)
    first = motion(element, axis, .002, .001)
    section_force(element, axis, first)
    element.commit_state()
    held = motion(element, axis, .0018, .001)
    np.testing.assert_allclose(section_force(element, axis, held), [.09, .66], atol=1e-13)
    accepted = element.get_internal_force(held).copy()
    candidate = deepcopy(element.current_states)
    other = motion(element, axis, -.004, -.002)
    element.get_tangent_stiffness_matrix(other)
    element.calculate_forces(other)
    assert element.current_states == candidate
    element.commit_state()
    element.get_internal_force(other)
    element.rollback_state()
    np.testing.assert_allclose(section_force(element, axis, held), [.09, .66], atol=1e-13)
    ends = element.calculate_forces(held)
    np.testing.assert_allclose(np.r_[ends["i_end"], ends["j_end"]], accepted, atol=1e-13)
    element.reset_states()
    np.testing.assert_allclose(section_force(element, axis, first), [.11, .66], atol=1e-13)


@pytest.mark.parametrize("axis", ["y", "z"])
def test_commits_on_same_straight_path_preserve_integrated_force(axis):
    element = specimen(axis)
    section_force(element, axis, motion(element, axis, .001, .0005))
    element.commit_state()
    np.testing.assert_allclose(section_force(element, axis, motion(element, axis, .002, .001)),
                               [.11, .66], atol=1e-13)


@pytest.mark.parametrize("axis", ["y", "z"])
@pytest.mark.parametrize("length", [.5, 1., 3.])
@pytest.mark.parametrize("shear", [False, True])
def test_public_constant_branch_tip_compliance(axis, length, shear):
    element = specimen(axis, length=length, shear=shear)
    element.get_internal_force(motion(element, axis, .0015, .0003))
    element.commit_state()
    k = element.get_tangent_stiffness_matrix(motion(element, axis, .002, .0007))
    indices = [7, 11] if axis == "z" else [8, 10]
    dv, rotation = np.linalg.solve(k[np.ix_(indices, indices)], [1., 0.])
    ga = 1920. if axis == "y" else 1440.
    assert dv == pytest.approx(length**3/30.+(length/ga if shear else 0.), rel=1e-12)
    assert rotation == pytest.approx((1. if axis == "z" else -1.)*length**2/20., rel=1e-12)


def test_shared_properties_and_two_axes_keep_independent_shear_history():
    element = specimen("z")
    element.set_hysteresis_model("moment_y", element.hysteresis_models["moment_z"].params)
    other = specimen("z")
    other.set_material_properties(element.material, element.bar_param)
    geometry = deepcopy(vars(element.bar_param))
    u = motion(element, "z", .002, .001)+motion(element, "y", -.0018, -.0004)
    element.get_internal_force(u)
    element.commit_state()
    np.testing.assert_allclose(section_force(element, "z", u), [.11, .66], atol=1e-13)
    np.testing.assert_allclose(section_force(other, "z", motion(other, "z", .002, .001)),
                               [.11, .66], atol=1e-13)
    assert vars(element.bar_param) == geometry
