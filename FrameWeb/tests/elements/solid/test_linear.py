"""Linear solid interpolation, quadrature and matrix contracts."""

import numpy as np
import pytest

from tests.support.builders.linear_elements import solid

pytestmark = pytest.mark.unit


@pytest.fixture(params=["tetra", "hexa", "wedge"])
def linear_solid(request):
    element, point = solid(request.param)
    return request.param, element, point


def test_initialization(linear_solid):
    kind, element, _ = linear_solid
    assert element.get_name() == kind


def test_shape_functions(linear_solid):
    _, element, point = linear_solid
    values = element.get_shape_functions(point)
    assert abs(values.sum() - 1.0) < 5e-8
    assert np.all(values >= 0)


def test_shape_derivatives(linear_solid):
    _, element, point = linear_solid
    derivatives = element.get_shape_derivatives(point)
    assert derivatives.shape == (3, len(element.node_ids))
    assert all(abs(row.sum()) < 5e-8 for row in derivatives)


def test_tetrahedron_volume():
    element, _ = solid("tetra")
    assert abs(element.get_volume() - 1 / 6) < 5e-8


@pytest.mark.parametrize("kind,n,weight_sum", [("hexa", 8, 8.0), ("wedge", 6, 6.0)])
def test_gauss_points(kind, n, weight_sum):
    element, _ = solid(kind)
    points, weights = element.get_gauss_points()
    assert points.shape == (n, 3) and weights.shape == (n,)
    assert abs(weights.sum() - weight_sum) < 5e-8


def test_stiffness_matrix(linear_solid):
    _, element, _ = linear_solid
    matrix = element.get_stiffness_matrix()
    n = 3 * len(element.node_ids)
    assert matrix.shape == (n, n)
    assert np.allclose(matrix, matrix.T)


def test_mass_matrix(linear_solid):
    _, element, _ = linear_solid
    matrix = element.get_mass_matrix()
    n = 3 * len(element.node_ids)
    assert matrix.shape == (n, n)
    assert np.all(np.diag(matrix) >= 0)
