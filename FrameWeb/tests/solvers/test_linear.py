"""solvers / linear contracts."""

import numpy as np
import pytest
from scipy.sparse import csr_matrix

from fem.solver import Solver

pytestmark = pytest.mark.unit


@pytest.mark.material_nonlinear
def test_sparse_product_retains_real_sub_ulp_force_and_python311_fallback(monkeypatch):
    import math

    from scipy.sparse import csr_matrix

    from fem.precision import sparse_product

    high = np.array([1.0, 1.0, 1.0])
    low = np.array([0.0, 1e-18, 0.0])
    matrix = csr_matrix([[1e10, -1e10, 3e-13]])
    assert sparse_product(matrix, high, low)[0] == pytest.approx(-1e-8 + 3e-13, abs=1e-23)
    monkeypatch.delattr(math, "fma", raising=False)
    assert sparse_product(matrix, high, low)[0] == pytest.approx(-1e-8 + 3e-13, abs=1e-23)


@pytest.mark.material_nonlinear
def test_linear_solver_scales_without_artificial_stiffness():
    s = Solver()
    np.testing.assert_allclose(
        s.solve_linear_system(csr_matrix(np.diag([1e-15, 1e15])), np.ones(2)),
        [1e15, 1e-15],
        rtol=1e-12,
        atol=0,
    )


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("force", [[0, 0], [1, -1], [1, 0]])
def test_linear_solver_rejects_rigid_mode(force):
    with pytest.raises((ValueError, np.linalg.LinAlgError), match="[Ss]ingular|rank"):
        Solver().solve_linear_system(csr_matrix([[1.0, -1.0], [-1.0, 1.0]]), np.array(force))
