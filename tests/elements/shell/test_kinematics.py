"""elements/shell / kinematics contracts."""

import numpy as np
import pytest

from tests.support.builders.shell_kinematics import shell

pytestmark = pytest.mark.unit


@pytest.mark.parametrize("n", [3, 4])
@pytest.mark.parametrize("axis", range(3))
@pytest.mark.parametrize("rotation", [False, True])
def test_shell_six_rigid_body_modes_have_no_internal_force(n, axis, rotation):
    e, coords = shell(n)
    u = np.zeros((n, 6))
    direction = np.eye(3)[axis]
    if rotation:
        u[:, :3] = np.cross(direction, coords)
        u[:, 3:] = direction
    else:
        u[:, :3] = direction
    np.testing.assert_allclose(e.get_stiffness_matrix() @ u.ravel(), 0.0, atol=2e-12)


@pytest.mark.parametrize("n", [3, 4])
def test_shell_stiffness_and_mass_rotate_with_the_plane(n):
    # Orthogonal rotation has no aligned global shell normal.
    r = np.array([[2.0, -2.0, 1.0], [1.0, 2.0, 2.0], [-2.0, -1.0, 2.0]]) / 3
    e, _ = shell(n)
    rotated, _ = shell(n, r)
    t = np.kron(np.eye(2 * n), r)
    np.testing.assert_allclose(
        rotated.get_stiffness_matrix(), t @ e.get_stiffness_matrix() @ t.T, rtol=1e-12, atol=2e-12
    )
    np.testing.assert_allclose(
        rotated.get_mass_matrix(), t @ e.get_mass_matrix() @ t.T, rtol=1e-12, atol=2e-12
    )


@pytest.mark.parametrize("n", [3, 4])
def test_shell_affine_membrane_energy(n):
    e, coords = shell(n)
    strain = np.array([0.001, 0.002, 0.003])
    u = np.zeros((n, 6))
    u[:, 0] = strain[0] * coords[:, 0] + strain[2] * coords[:, 1]
    u[:, 1] = strain[1] * coords[:, 1]
    u[:, 5] = -strain[2] / 2  # Consistent in-plane spin, (dv/dx-du/dy)/2.
    stress = 1000 / (1 - 0.25**2) * np.array([0.001 + 0.25 * 0.002, 0.002 + 0.25 * 0.001, 0.375 * 0.003])
    area = 3.0 if n == 3 else 6.0
    expected = area * 0.2 * np.dot(strain, stress) / 2
    assert u.ravel() @ e.get_stiffness_matrix() @ u.ravel() / 2 == pytest.approx(expected, rel=1e-12)


@pytest.mark.parametrize("n", [3, 4])
def test_degenerate_shell_is_rejected_without_fallback(n):
    e, coords = shell(n)
    coords[:, 1:] = 0.0
    e.set_node_coordinates(dict(enumerate(coords)))
    with pytest.raises(ValueError, match="[Dd]egenerate"):
        e.get_stiffness_matrix()


@pytest.mark.parametrize("n", [3, 4])
def test_shell_has_exactly_six_physical_rigid_zero_modes(n):
    e, _ = shell(n)
    eigenvalues = np.linalg.eigvalsh(e.get_stiffness_matrix())
    assert np.min(eigenvalues) > -1e-10
    assert np.count_nonzero(eigenvalues < 1e-10) == 6


@pytest.mark.parametrize("n", [3, 4])
def test_drill_spin_constraint_has_independent_constant_field_energy(n):
    e, coords = shell(n)
    u = np.zeros((n, 6))
    spin, rotation = 0.02, 0.05
    u[:, :3] = np.cross([0.0, 0.0, spin], coords)
    u[:, 5] = rotation
    area = 3.0 if n == 3 else 6.0
    expected = 0.5 * 0.001 * 400 * 0.2 * area * (rotation - spin) ** 2
    assert 0.5 * u.ravel() @ e.get_stiffness_matrix() @ u.ravel() == pytest.approx(expected, abs=1e-12)
    output = e.calculate_shell_results(u.ravel())
    assert output["strain_energy"] == pytest.approx(0.0, abs=1e-12)
    assert output["drilling_energy"] == pytest.approx(expected, abs=1e-12)


@pytest.mark.parametrize("axis", [0, 1])
@pytest.mark.parametrize("thickness", [0.02, 0.2, 2.0])
def test_quad_constant_bending_energy_has_no_shear_locking(axis, thickness):
    e, coords = shell(4)
    e.thickness = thickness
    curvature = 0.003
    u = np.zeros((4, 6))
    u[:, 2] = -0.5 * curvature * coords[:, axis] ** 2
    u[:, 4 if axis == 0 else 3] = curvature * coords[:, axis] * (1 if axis == 0 else -1)
    # Unit-width plate bending stiffness E*t^3/[12(1-nu^2)].
    energy = 6.0 * 1000 * thickness**3 / (12 * (1 - 0.25**2)) * curvature**2 / 2
    assert u.ravel() @ e.get_stiffness_matrix() @ u.ravel() / 2 == pytest.approx(energy, rel=1e-10)


from tests.support.builders.linear_elements import shell as linear_shell


@pytest.fixture(params=[3, 4], ids=["triangle", "quadrilateral"])
def linear_shell_element(request):
    element, point = linear_shell(request.param)
    return request.param, element, point


def test_initialization(linear_shell_element):
    n, element, _ = linear_shell_element
    assert element.get_node_count() == n
    assert element.get_name() == ("TriElement1" if n == 3 else "QuadElement1")
    assert element.get_matrix_size() == 6 * n


def test_shape_functions(linear_shell_element):
    _, element, point = linear_shell_element
    values = element.get_shape_functions(point)
    assert abs(values.sum() - 1.0) < 5e-8
    assert np.all(values >= 0)


def test_shape_derivatives(linear_shell_element):
    n, element, point = linear_shell_element
    derivatives = element.get_shape_derivatives(point)
    assert derivatives.shape == (2, n)
    assert all(abs(row.sum()) < 5e-8 for row in derivatives)


def test_gauss_points(linear_shell_element):
    n, element, _ = linear_shell_element
    points, weights = element.get_gauss_points()
    count, total = (1, 0.5) if n == 3 else (4, 4.0)
    assert points.shape == (count, 2) and weights.shape == (count,)
    assert abs(weights.sum() - total) < 5e-8


def test_jacobian_determinant(linear_shell_element):
    _, element, point = linear_shell_element
    assert element.get_jacobian_determinant(point) > 0


def test_stiffness_matrix(linear_shell_element):
    n, element, _ = linear_shell_element
    matrix = element.get_stiffness_matrix()
    assert matrix.shape == (6 * n, 6 * n)
    assert np.allclose(matrix, matrix.T)


def test_mass_matrix(linear_shell_element):
    n, element, _ = linear_shell_element
    matrix = element.get_mass_matrix()
    assert matrix.shape == (6 * n, 6 * n)
    assert np.all(np.diag(matrix) >= 0)
