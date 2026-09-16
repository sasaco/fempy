"""elements/beam / foundation contracts."""

import numpy as np
import pytest

from fem.elements.loaded_bar_element import boundary_map

pytestmark = pytest.mark.unit


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("length", [1.0, 50.0, 1000.0])
def test_axial_foundation_matches_hyperbolic_dynamic_stiffness(length):
    rigidity, foundation = 7.0, 19.0
    alpha = np.sqrt(foundation / rigidity)
    z = alpha * length
    csch = 2 * np.exp(-z) / (1 - np.exp(-2 * z))
    expected = rigidity * alpha * np.array([[1 / np.tanh(z), -csch], [-csch, 1 / np.tanh(z)]])
    k, f = boundary_map(length, rigidity, foundation, q=(3.0, 3.0))
    np.testing.assert_allclose(k, expected, rtol=1e-10, atol=1e-12)
    np.testing.assert_allclose(k @ np.full(2, 3 / foundation) - f, 0.0, atol=1e-11)


@pytest.mark.material_nonlinear
def test_long_bending_foundation_matches_independent_half_infinite_solution():
    rigidity, foundation = 7.0, 19.0
    beta = (foundation / (4 * rigidity)) ** 0.25
    length = 1000 / beta
    k, f = boundary_map(length, rigidity, foundation, bending=True)
    left = rigidity * np.array([[4 * beta**3, 2 * beta**2], [2 * beta**2, 2 * beta]])
    np.testing.assert_allclose(k[:2, :2], left, rtol=1e-10, atol=1e-11)
    np.testing.assert_allclose(k[2:, 2:], left * np.array([[1, -1], [-1, 1]]), rtol=1e-10, atol=1e-11)
    np.testing.assert_allclose(k[:2, 2:], 0.0, atol=1e-11)
    np.testing.assert_array_equal(f, 0.0)


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("shear", [None, 17.0])
def test_linear_foundation_particular_solution_preserves_consistent_loads(shear):
    length, rigidity, foundation = 1000.0, 7.0, 19.0
    q0, q1 = 3.0, 11.0
    theta = (q1 - q0) / (length * foundation)
    u = np.array([q0 / foundation, theta, q1 / foundation, theta])
    k, f = boundary_map(length, rigidity, foundation, bending=True, q=(q0, q1), shear_rigidity=shear)
    np.testing.assert_allclose(k @ u - f, 0.0, atol=1e-10)
    assert np.min(np.linalg.eigvalsh(k)) > 0
