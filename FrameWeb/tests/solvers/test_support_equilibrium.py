"""Physical contracts for support assembly, independent of control strategy."""

import numpy as np
import pytest
from scipy.sparse import csr_matrix
from scipy.sparse.linalg import spsolve

from fem.boundary_condition import BoundaryCondition
from fem.solver import Solver
from tests.support.builders.nonlinear_solver import SofteningBar, axial_bar

pytestmark = pytest.mark.unit


@pytest.mark.parametrize("legacy", [False, True])
@pytest.mark.parametrize("control_dof, component", [("dx", 0), ("rz", 2)])
def test_load_and_displacement_control_balance_translation_and_rotation_springs(legacy, control_dof, component):
    args = axial_bar()
    boundary = args[2]
    boundary.loads.clear()
    args[3][1].shear_correction = False
    if legacy:
        boundary.add_restraint(2, [True]*6, [2000, 2500, 0, 0, 0, 3000])
    else:
        boundary.add_restraint(2, [False, False, True, True, True, False])
        boundary.spring_supports = {2: {"x": 2000., "y": 2500., "rz": 3000.}}
    boundary.add_load(2, [50, 35, 0, 0, 0, 25])
    # A large fixed-node load must not hide errors on the spring-supported DOFs.
    boundary.add_load(1, [1e9, 0, 0, 0, 0, 0])
    # EA/L=3000; the tip bending block is [[3000,-3000],[-3000,4000]].
    # With springs it is [[5500,-3000],[-3000,7000]], determinant 29500000.
    expected = np.array([50/5000, (7000*35+3000*25)/29500000,
                         (3000*35+5500*25)/29500000])
    factors = np.array([0., .4, 1., -.2, 0.])
    loaded = Solver().solve(*args, analysis_type="material_nonlinear", load_factors=factors)
    controlled = Solver().solve(
        *args, analysis_type="material_nonlinear",
        displacement_control={"node": 2, "dof": control_dof,
                              "targets": factors*expected[component]},
    )
    for factor, load_step, control_step in zip(factors, loaded["step_results"], controlled["step_results"]):
        for step in (load_step, control_step):
            assert step["lambda"] == pytest.approx(factor, abs=1e-12)
            np.testing.assert_allclose(step["displacement"][[6, 7, 11]], factor*expected, atol=1e-12)
            assert step["reaction_forces"][2] == pytest.approx({
                "fx": -2000*factor*expected[0], "fy": -2500*factor*expected[1],
                "fz": 0., "mx": 0., "my": 0., "mz": -3000*factor*expected[2],
            }, abs=1e-9)
        np.testing.assert_allclose(control_step["displacement"], load_step["displacement"], atol=1e-12)


def test_displacement_control_keeps_spring_balance_past_the_load_peak():
    args = axial_bar()
    args[2].loads.clear()
    args[2].add_load(2, [1, 0, 0, 0, 0, 0])
    args[2].spring_supports = {2: {"x": .25}}
    args[3][1] = SofteningBar()
    targets = [0., .5, 1., 1.5, 1.8]
    result = Solver().solve(
        *args, analysis_type="material_nonlinear",
        displacement_control={"node": 2, "dof": "dx", "targets": targets},
    )
    assert [s["lambda"] for s in result["step_results"]] == pytest.approx([0., .625, 1.25, .875, .65])
    for target, step in zip(targets, result["step_results"]):
        assert step["reaction_forces"][2]["fx"] == pytest.approx(-.25*target)


def test_normalized_boundary_keeps_sparse_ids_legacy_springs_and_free_rotations():
    from fem.boundary_dofs import BoundaryDofs

    boundary = BoundaryCondition()
    boundary.add_restraint(10, [True]*6, [.2, 0, 0, 0, 0, 0])
    boundary.add_restraint(30, [True, False, False, False, False, False], [-2000, 0, 0, 0, 0, 0])
    boundary.spring_supports = {30: {"rz": 3.}}
    offsets = {10: 0, 30: 6}
    resolved = BoundaryDofs.from_boundary(boundary, 12, 6, offsets.__getitem__)
    assert resolved.prescribed == {0: .2, 1: 0, 2: 0, 3: 0, 4: 0, 5: 0}
    assert resolved.springs == {6: 2000, 11: 3}
    np.testing.assert_array_equal(resolved.free, np.arange(6, 12))
    boundary.spring_supports[30]["rz"] = 99
    assert resolved.springs[11] == 3  # Normalization owns its input snapshot.


def test_support_tangent_is_the_negative_derivative_of_equilibrium_residual():
    from fem.boundary_dofs import BoundaryDofs

    resolved = BoundaryDofs(3, 3, {}, {0: 2., 2: 5.})
    k = csr_matrix([[10., -3., 0.], [-3., 8., -1.], [0., -1., 4.]])
    u, du, external = np.array([.2, -.1, .3]), np.array([.1, -.2, .4]), np.array([2., 3., 4.])
    tangent = resolved.add_spring_stiffness(k)
    residual = resolved.residual(external-k@u, u)
    changed = resolved.residual(external-k@(u+du), u+du)
    np.testing.assert_allclose(changed-residual, -tangent@du, atol=1e-14)
    np.testing.assert_array_equal(k.diagonal(), [10., 8., 4.])


def test_constraint_elimination_uses_already_assembled_support_equilibrium_once():
    from fem.boundary_dofs import BoundaryDofs

    resolved = BoundaryDofs(3, 3, {0: .2, 2: 0.}, {1: 20.})
    k = csr_matrix([[10., -10., 0.], [-10., 10., 0.], [0., 0., 1.]])
    u = np.array([.05, .03, 0.])
    external = np.array([0., 4., 0.])
    residual = resolved.residual(external-k@u, u)
    supported = resolved.add_spring_stiffness(k)
    constrained, rhs = resolved.constrain(supported, residual, u, load_factor=.5)
    np.testing.assert_allclose(u+spsolve(constrained, rhs), [.1, 1/6, 0.], atol=1e-14)
    np.testing.assert_array_equal(constrained.toarray(), constrained.toarray().T)
    np.testing.assert_array_equal(supported.diagonal(), [10., 30., 1.])
    np.testing.assert_allclose(residual, [-.2, 3.6, 0.], atol=1e-14)


def test_compensated_support_residual_retains_sub_float_displacement():
    from fem.boundary_dofs import BoundaryDofs

    resolved = BoundaryDofs(3, 3, {}, {0: 1.})
    residual = np.array([1e16, 0., 0.])
    u, low = np.array([1e16, 0., 0.]), np.array([1., 0., 0.])
    np.testing.assert_array_equal(resolved.residual(residual, u, correction=low), [-1., 0., 0.])
    np.testing.assert_array_equal(residual, [1e16, 0., 0.])


@pytest.mark.parametrize("reduced", [False, True])
def test_equilibrium_measure_excludes_fixed_loads_and_scales_rotational_springs(reduced):
    from fem.boundary_dofs import BoundaryDofs
    from fem.convergence import evaluate_equilibrium

    resolved = BoundaryDofs(6, 6, {0: 0.}, {1: 2., 5: 4.})
    state = evaluate_equilibrium(
        resolved, np.array([1e12, 8., 0., 0., 0., 11.]),
        np.array([0., 3., 0., 0., 0., 5.]), np.array([0., 1., 0., 0., 0., .5]),
        length=2., reference_load=np.array([0., 0., 0., 0., 0., 20.]),
        dof_indices=resolved.free if reduced else None,
    )
    np.testing.assert_array_equal(state.residual, [1e12, 3., 0., 0., 0., 4.])
    assert state.residual_norm == pytest.approx(np.sqrt(13))
    assert state.residual_scale == pytest.approx(10.)
    assert state.relative_residual == pytest.approx(np.sqrt(13)/10)
