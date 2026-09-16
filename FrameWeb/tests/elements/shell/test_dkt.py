"""elements/shell / dkt contracts."""

import numpy as np
import pytest

from fem.elements.shell_element import ShellElement
from tests.support.builders.dkt_triangle import triangle

pytestmark = pytest.mark.unit


@pytest.mark.parametrize("t", [0.002, 0.02, 0.2])
@pytest.mark.parametrize(
    "curvature", [[0.003, 0.0, 0.0], [0.0, 0.002, 0.0], [0.0, 0.0, 0.004], [0.003, -0.002, 0.004]]
)
def test_dkt_quadratic_bending_has_exact_thickness_cubed_energy(t, curvature):
    e, xyz = triangle(t)
    x, y = xyz[:, 0], xyz[:, 1]
    kx, ky, kxy = curvature
    u = np.zeros((3, 6))
    u[:, 2] = -0.5 * (kx * x * x + ky * y * y + kxy * x * y)
    u[:, 3] = -ky * y - kxy * x / 2
    u[:, 4] = kx * x + kxy * y / 2
    elastic = (
        1000 / (1 - 0.25**2) * np.array([[1.0, 0.25, 0.0], [0.25, 1.0, 0.0], [0.0, 0.0, (1 - 0.25) / 2]])
    )
    expected = 3 * t**3 / 24 * np.array(curvature) @ elastic @ curvature
    assert u.ravel() @ e.get_stiffness_matrix() @ u.ravel() / 2 == pytest.approx(expected, rel=1e-10)
    result = e.calculate_shell_results(u.ravel())
    assert result["strain_energy"] == pytest.approx(expected, rel=1e-10)
    np.testing.assert_allclose(
        result["resultants"]["moment"], t**3 / 12 * elastic @ curvature, rtol=1e-10, atol=1e-15
    )


def test_dkt_covariance_and_exactly_six_null_modes():
    rotation = np.array([[1.0, 2.0, 2.0], [2.0, 1.0, -2.0], [-2.0, 2.0, -1.0]]) / 3
    e, _ = triangle(0.2)
    rotated, _ = triangle(0.2, rotation)
    transform = np.kron(np.eye(6), rotation)
    np.testing.assert_allclose(
        rotated.get_stiffness_matrix(), transform @ e.get_stiffness_matrix() @ transform.T, atol=1e-11
    )
    eigenvalues = np.linalg.eigvalsh(e.get_stiffness_matrix())
    assert np.min(eigenvalues) > -1e-10
    assert np.count_nonzero(eigenvalues < 1e-10) == 6


def test_dkt_requires_a_triangle():
    with pytest.raises(ValueError, match="dkt"):
        ShellElement(1, [1, 2, 3, 4], 1, 0.1, formulation="dkt")
