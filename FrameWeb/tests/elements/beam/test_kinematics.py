"""elements/beam / kinematics contracts."""

import numpy as np
import pytest

from fem.elements.bar_element import BEBarElement, TBarElement
from fem.elements.nonlinear_bar_element import NonlinearBarElement
from tests.support.builders.nonlinear_beam import RIGIDITIES, beam, force

pytestmark = pytest.mark.unit


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("cls", [BEBarElement, TBarElement, NonlinearBarElement])
@pytest.mark.parametrize("rotation", [False, True])
@pytest.mark.parametrize("mode", ["translation", "rotation"])
def test_rigid_body_motion_has_zero_force(cls, rotation, mode):
    e = beam(cls, dofs=RIGIDITIES if cls is NonlinearBarElement else (), rotated=rotation)
    u = np.zeros(12)
    if mode == "translation":
        u[:3] = u[6:9] = [0.02, -0.03, 0.04]
    else:
        omega = np.array([0.002, -0.003, 0.004])
        u[3:6] = u[9:12] = omega
        x = e.get_element_coordinates()
        u[6:9] = np.cross(omega, x[1] - x[0])
    np.testing.assert_allclose(force(e, u), 0, atol=2e-12)


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("rotation", [False, True])
def test_endpoint_forces_and_moments_balance(rotation):
    e = beam(dofs=RIGIDITIES, rotated=rotation)
    u = np.array([1, -2, 3, -4, 5, -6, 7, 8, -9, 10, -11, 12]) * 0.001
    f = force(e, u)
    x = e.get_element_coordinates()
    np.testing.assert_allclose(f[:3] + f[6:9], 0, atol=1e-12)
    np.testing.assert_allclose(f[3:6] + f[9:12] + np.cross(x[1] - x[0], f[6:9]), 0, atol=2e-12)


@pytest.mark.material_nonlinear
def test_rotated_response_matches_independent_basis_rotation():
    plain, turned = beam(dofs=RIGIDITIES), beam(dofs=RIGIDITIES, rotated=True)
    # Construct frame from geometry, independently of element's transformation getter.
    ex = np.array([2.0, -3.0, 6.0]) / 7
    ey = np.cross([0.0, 0.0, 1.0], ex)
    ey /= np.linalg.norm(ey)
    ez = np.cross(ex, ey)
    a = np.deg2rad(23)
    r = np.array([ex, np.cos(a) * ey + np.sin(a) * ez, -np.sin(a) * ey + np.cos(a) * ez])
    t = np.kron(np.eye(4), r)
    u = np.linspace(-0.006, 0.012, 12)
    np.testing.assert_allclose(force(turned, t.T @ u), t.T @ force(plain, u), atol=2e-12)
    np.testing.assert_allclose(
        turned.get_tangent_stiffness_matrix(t.T @ u),
        t.T @ plain.get_tangent_stiffness_matrix(u) @ t,
        atol=2e-12,
    )
