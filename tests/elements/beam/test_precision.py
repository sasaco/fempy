"""elements/beam / precision contracts."""

import numpy as np
import pytest

from fem.elements.loaded_bar_element import boundary_map
from tests.support.builders.beam_precision import beam

pytestmark = pytest.mark.unit


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("axis", range(3))
def test_stiff_beam_rigid_rotation_force_does_not_cancel_large_products(axis):
    e = beam()
    rotation = np.eye(3)[axis] * 0.01
    u = np.r_[[0.2, 0.3, 0.4], rotation, [0.2, 0.3, 0.4] + np.cross(rotation, [4.8, 0.0, 0.0]), rotation]
    np.testing.assert_allclose(e.get_internal_force(u), 0.0, atol=1e-10)
    forces = e.calculate_forces(u)
    np.testing.assert_allclose(np.r_[forces["i_end"], forces["j_end"]], 0.0, atol=1e-10)


@pytest.mark.material_nonlinear
def test_loaded_beam_internal_force_and_section_force_use_same_kinematics():
    e = beam()
    e.set_line_load("Ly", [3.0, 7.0])
    e.load_factor = 0.25
    u = np.arange(12) * 1e-4
    section = e.calculate_forces(u)
    np.testing.assert_allclose(
        e.get_internal_force(u),
        np.r_[section["i_end"], section["j_end"]] + 0.25 * e.get_member_load_vector(),
        atol=1e-10,
    )


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("length,rigidity", [(0.001, 2.65e10), (4.8, 2.65e10), (100.0, 0.001)])
def test_polynomial_beam_virtual_work_and_force_resultants(length, rigidity):
    k, f = boundary_map(length, rigidity, bending=True, q=(3.0, 7.0))
    # w=x^3, theta=3x^2: integral EI*(6x)^2 dx, and integral (3+4x/L)*x^3 dx.
    u = np.array([0.0, 0.0, length**3, 3 * length**2])
    assert u @ k @ u == pytest.approx(12 * rigidity * length**3, rel=1e-13)
    assert u @ f == pytest.approx((3 / 4 + 4 / 5) * length**4, rel=1e-13)
    assert f[0] + f[2] == pytest.approx(5 * length, rel=1e-13)
    assert f[1] + length * f[2] + f[3] == pytest.approx(17 * length**2 / 6, rel=1e-13)


@pytest.mark.material_nonlinear
def test_beam_keeps_displacement_below_the_rigid_translation_ulp():
    e = beam()
    high = np.zeros(12)
    high[[0, 6]] = 1.0
    low = np.zeros(12)
    low[6] = 1e-18
    expected = e.material.materials[1].E / e.length * 1e-18
    force = e.calculate_forces(high, displacement_correction=low)
    assert force["j_end"][0] == pytest.approx(expected, rel=1e-13, abs=1e-20)
    assert force["i_end"][0] == -force["j_end"][0]
