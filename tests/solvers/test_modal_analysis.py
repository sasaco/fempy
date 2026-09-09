"""Modal-analysis contracts for low modes, constraints, and public entry points."""
import numpy as np
import pytest
from scipy.sparse.linalg import ArpackNoConvergence

from fem import FemModel
from fem.boundary_condition import BoundaryCondition
from fem.material import Material
from fem.mesh import MeshModel
from fem.solver import Solver


pytestmark = pytest.mark.unit


class DiagonalTranslationElement:
    """One synthetic three-DOF element with independent modal coordinates."""

    node_ids = [10]

    def __init__(self, stiffness, mass=None):
        self.stiffness = np.diag(stiffness)
        self.mass = np.eye(3) if mass is None else np.diag(mass)

    def get_dof_per_node(self):
        return 3

    def get_stiffness_matrix(self):
        return self.stiffness

    def get_mass_matrix(self):
        return self.mass


def diagonal_model(stiffness, mass=None):
    mesh = MeshModel()
    mesh.add_node(10, [0, 0, 0])
    mesh.add_element(4, "tetra", [10], 1)
    return (
        mesh,
        Material(),
        BoundaryCondition(),
        {4: DiagonalTranslationElement(stiffness, mass)},
    )


def test_public_modal_wrapper_uses_requested_count_and_restores_configuration(monkeypatch):
    model = FemModel()
    model.analysis_params["n_modes"] = 7
    monkeypatch.setattr(model, "run", lambda analysis: model.analysis_params["n_modes"])

    assert model.run_modal_analysis(2) == 2
    assert model.analysis_params["n_modes"] == 7


@pytest.mark.parametrize("invalid", [True, 0, -1, 1.5])
def test_modal_mode_count_must_be_a_positive_integer(invalid):
    with pytest.raises(ValueError, match="positive integer"):
        Solver().eigenvalue_analysis(*diagonal_model([1, 2, 3]), n_modes=invalid)


def test_modal_reduces_stiffness_and_mass_to_the_same_free_dofs():
    args = diagonal_model([2, 3, 4])
    args[2].add_restraint(10, [False, True, True])

    result = Solver().eigenvalue_analysis(*args, n_modes=1)

    np.testing.assert_allclose(result["eigenvalues"], [2])
    np.testing.assert_allclose(result["eigenvectors"][:, 0], [1, 0, 0], atol=1e-12)
    assert result["modes"][0][10] == pytest.approx(
        {"dx": 1, "dy": 0, "dz": 0, "rx": 0, "ry": 0, "rz": 0}
    )


def test_modal_does_not_silently_reduce_requested_mode_count():
    args = diagonal_model([2, 3, 4])
    args[2].add_restraint(10, [False, True, True])

    with pytest.raises(ValueError, match="requested 2.*only 1"):
        Solver().eigenvalue_analysis(*args, n_modes=2)


def test_zero_mode_has_infinite_python_period_and_json_safe_null():
    from fem.file_io import result_to_jsonable

    result = Solver().eigenvalue_analysis(*diagonal_model([0, 2, 3]), n_modes=2)

    np.testing.assert_allclose(result["eigenvalues"], [0, 2], atol=1e-12)
    assert np.isinf(result["periods"][0])
    assert result_to_jsonable(result)["periods"][0] is None
    assert result["periods"][1] == pytest.approx(2 * np.pi / np.sqrt(2))


def test_significant_negative_eigenvalue_is_reported_as_instability():
    with pytest.raises(ValueError, match="negative eigenvalue"):
        Solver().eigenvalue_analysis(*diagonal_model([-1, 2, 3]), n_modes=1)


def test_modal_retry_keeps_low_mode_selection(monkeypatch):
    calls = []

    def eigsh_stub(*args, **kwargs):
        calls.append(kwargs)
        if len(calls) == 1:
            raise ArpackNoConvergence("no convergence", np.array([]), np.empty((3, 0)))
        return np.array([2.0]), np.array([[1.0], [0.0], [0.0]])

    monkeypatch.setattr("fem.solver.eigsh", eigsh_stub)
    result = Solver().eigenvalue_analysis(*diagonal_model([2, 3, 4]), n_modes=1)

    assert result["eigenvalues"][0] == pytest.approx(2)
    assert calls[0]["which"] == "SA"
    assert calls[1]["which"] == "LM"
    assert calls[1]["sigma"] == 0.0
    assert len(result["eigenvalues"]) == 1


def test_modal_reports_residual_and_mass_orthogonality_checks():
    result = Solver().eigenvalue_analysis(*diagonal_model([2, 3, 4]), n_modes=2)

    assert max(result["eigenpair_residuals"]) < 1e-10
    assert result["mass_orthogonality_error"] < 1e-10
