"""Modal-analysis contracts for low modes, constraints, and public entry points."""
import logging

import numpy as np
import pytest
from scipy.sparse.linalg import ArpackNoConvergence
from scipy.sparse.linalg import eigsh as scipy_eigsh

from fem import BarParameter, FemModel
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


def one_free_x_dof_model(element_type, coordinates, **element_options):
    """Build a public model whose sole modal coordinate has a closed-form root."""
    model = FemModel()
    node_ids = []
    for index, coordinates_i in enumerate(coordinates):
        node_id = 10 + 3 * index
        node_ids.append(node_id)
        model.add_node(node_id, *coordinates_i)
    model.add_material(1, "reference", E=1000.0, nu=0.25, density=2.0)
    model.add_element(8, element_type, node_ids, 1, **element_options)
    for index, node_id in enumerate(node_ids):
        model.add_restraint(
            node_id,
            dx=index != 1,
            dy=True,
            dz=True,
            rx=True,
            ry=True,
            rz=True,
        )
    return model


def shell_grid_model(size=8):
    """Cantilevered triangular-shell grid large enough to require sparse ARPACK."""
    model = FemModel()
    node = lambda x, y: 1 + y * (size + 1) + x
    for y in range(size + 1):
        for x in range(size + 1):
            model.add_node(node(x, y), float(x), float(y), 0.0)
    model.add_material(1, "plate", E=1000.0, nu=0.25, density=2.0)
    element_id = 1
    for y in range(size):
        for x in range(size):
            lower_left = node(x, y)
            lower_right = node(x + 1, y)
            upper_left = node(x, y + 1)
            upper_right = node(x + 1, y + 1)
            model.add_element(
                element_id,
                "shell",
                [lower_left, lower_right, upper_right],
                1,
                thickness=0.2,
                formulation="mindlin",
            )
            model.add_element(
                element_id + 1,
                "shell",
                [lower_left, upper_right, upper_left],
                1,
                thickness=0.2,
                formulation="mindlin",
            )
            element_id += 2
    for y in range(size + 1):
        model.add_restraint(node(0, y), True, True, True, True, True, True)
    return model


def test_public_modal_wrapper_uses_requested_count_and_restores_configuration(monkeypatch):
    model = FemModel()
    model.analysis_params["n_modes"] = 7
    monkeypatch.setattr(model, "run", lambda analysis: model.analysis_params["n_modes"])

    assert model.run_modal_analysis(2) == 2
    assert model.analysis_params["n_modes"] == 7


def test_v0_modal_element_construction_uses_controllable_logging(caplog, capsys):
    with caplog.at_level(logging.DEBUG, logger="fem.model"):
        model = one_free_x_dof_model(
            "TriElement1",
            [[0, 0, 0], [2, 0, 0], [0, 3, 0]],
            thickness=0.2,
            formulation="dkt",
        )
        model.run_modal_analysis(1)
    assert capsys.readouterr().out == ""
    assert any("V0互換" in record.message for record in caplog.records)


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


def test_wiki_spring_supported_beam_modal_example_has_closed_form_frequency():
    model = FemModel()
    model.add_node(1, 0, 0, 0)
    model.add_node(2, 2, 0, 0)
    model.add_material(1, "Steel", E=200e9, nu=0.3, density=7850)
    model.material.add_bar_parameter(
        1, BarParameter(area=0.01, Iy=1e-5, Iz=1e-5, J=2e-5)
    )
    model.add_element(1, "bar", [1, 2], 1, section_id=1, shear_correction=False)
    for node_id in (1, 2):
        for direction, stiffness in (
            ("x", 1e5),
            ("y", 1e6),
            ("z", 1e6),
            ("rx", 1e4),
            ("ry", 1e4),
            ("rz", 1e4),
        ):
            model.add_spring_support(node_id, direction, stiffness)

    result = model.run_modal_analysis(1)

    end_mass = 7850 * 0.01 * 2 / 2
    expected_frequency = np.sqrt(1e5 / end_mass) / (2 * np.pi)
    assert result["frequencies"] == pytest.approx([expected_frequency], rel=1e-5)


@pytest.mark.parametrize(
    "element_type,coordinates,options,expected_eigenvalue,expected_dof",
    [
        (
            "TriElement1",
            [[0, 0, 0], [2, 0, 0], [0, 3, 0]],
            {"thickness": 0.2, "formulation": "dkt"},
            800.0,
            6,
        ),
        (
            "TetraElement1",
            [[0, 0, 0], [1, 0, 0], [0, 1, 0], [0, 0, 1]],
            {},
            2400.0,
            3,
        ),
        (
            "TetraElement2",
            [
                [0, 0, 0],
                [1, 0, 0],
                [0, 1, 0],
                [0, 0, 1],
                [0.5, 0, 0],
                [0.5, 0.5, 0],
                [0, 0.5, 0],
                [0, 0, 0.5],
                [0.5, 0, 0.5],
                [0, 0.5, 0.5],
            ],
            {},
            25200.0,
            3,
        ),
    ],
)
def test_public_modal_path_matches_independent_one_dof_element_roots(
    element_type, coordinates, options, expected_eigenvalue, expected_dof
):
    # These roots follow directly from the element interpolation integrals with
    # E=1000, nu=.25 and rho=2. They are not generated by the product solver.
    result = one_free_x_dof_model(element_type, coordinates, **options).run_modal_analysis(1)

    assert result["eigenvalues"] == pytest.approx([expected_eigenvalue], rel=1e-12)
    assert result["frequencies"] == pytest.approx(
        [np.sqrt(expected_eigenvalue) / (2 * np.pi)], rel=1e-12
    )
    assert result["eigenpair_residuals"][0] < 1e-12
    assert result["mass_orthogonality_error"] < 1e-12
    nonzero_dofs = np.flatnonzero(np.abs(result["eigenvectors"][:, 0]) > 1e-12)
    np.testing.assert_array_equal(nonzero_dofs, [expected_dof])


def test_large_public_shell_model_retry_returns_same_low_modes(monkeypatch):
    model = shell_grid_model()
    regular = model.run_modal_analysis(4)
    calls = []

    def fail_once_then_solve(*args, **kwargs):
        calls.append(kwargs.copy())
        if len(calls) == 1:
            size = args[0].shape[0]
            raise ArpackNoConvergence("injected", np.array([]), np.empty((size, 0)))
        return scipy_eigsh(*args, **kwargs)

    monkeypatch.setattr("fem.solver.eigsh", fail_once_then_solve)
    retried = model.run_modal_analysis(4)

    assert calls[0]["which"] == "SA"
    assert calls[1]["which"] == "LM"
    assert calls[1]["sigma"] == 0.0
    np.testing.assert_allclose(retried["eigenvalues"], regular["eigenvalues"], rtol=2e-8)
    assert np.all(np.diff(retried["eigenvalues"]) >= 0)
    assert max(retried["eigenpair_residuals"]) < 1e-8
    assert retried["mass_orthogonality_error"] < 1e-8
    fixed_dofs = [y * 9 * 6 for y in range(9)]
    for start in fixed_dofs:
        np.testing.assert_allclose(retried["eigenvectors"][start : start + 6], 0.0)
