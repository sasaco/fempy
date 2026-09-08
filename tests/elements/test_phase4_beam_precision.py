"""Rigid motion and consistent-load work checks for the shared linear path."""
import numpy as np
import pytest

from src.fem.elements.loaded_bar_element import LoadedBarElement, boundary_map
from src.fem.material import Material, MaterialProperty, BarParameter


def beam():
    e = LoadedBarElement(1, [1, 2], 1, 1)
    e.set_node_coordinates({1: np.zeros(3), 2: np.array([4.8, 0., 0.])})
    m = Material()
    m.add_material(1, MaterialProperty('stiff', 2.65e10, .25))
    e.set_material_properties(m, BarParameter(1., 1., 1., 1.))
    return e


@pytest.mark.parametrize('axis', range(3))
def test_stiff_beam_rigid_rotation_force_does_not_cancel_large_products(axis):
    e = beam()
    rotation = np.eye(3)[axis]*.01
    u = np.r_[[.2, .3, .4], rotation, [.2, .3, .4]+np.cross(rotation, [4.8, 0., 0.]), rotation]
    np.testing.assert_allclose(e.get_internal_force(u), 0., atol=1e-10)
    forces = e.calculate_forces(u)
    np.testing.assert_allclose(np.r_[forces['i_end'], forces['j_end']], 0., atol=1e-10)


def test_loaded_beam_internal_force_and_section_force_use_same_kinematics():
    e = beam()
    e.set_line_load('Ly', [3., 7.])
    e.load_factor = .25
    u = np.arange(12)*1e-4
    section = e.calculate_forces(u)
    np.testing.assert_allclose(e.get_internal_force(u),
        np.r_[section['i_end'], section['j_end']]+.25*e.get_member_load_vector(), atol=1e-10)


@pytest.mark.parametrize('length,rigidity', [(.001, 2.65e10), (4.8, 2.65e10), (100., .001)])
def test_polynomial_beam_virtual_work_and_force_resultants(length, rigidity):
    k, f = boundary_map(length, rigidity, bending=True, q=(3., 7.))
    # w=x^3, theta=3x^2: integral EI*(6x)^2 dx, and integral (3+4x/L)*x^3 dx.
    u = np.array([0., 0., length**3, 3*length**2])
    assert u@k@u == pytest.approx(12*rigidity*length**3, rel=1e-13)
    assert u@f == pytest.approx((3/4+4/5)*length**4, rel=1e-13)
    assert f[0]+f[2] == pytest.approx(5*length, rel=1e-13)
    assert f[1]+length*f[2]+f[3] == pytest.approx(17*length**2/6, rel=1e-13)
