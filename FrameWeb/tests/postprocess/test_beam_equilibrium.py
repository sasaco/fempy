"""postprocess / beam equilibrium contracts."""

import numpy as np
import pytest

from fem.beam_equilibrium import recover_free_branches
from tests.support.builders.beam_precision import beam

pytestmark = pytest.mark.unit


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("tip", [1, 2])
@pytest.mark.parametrize("moment", [0.0, 3e-13, -7.0])
def test_free_end_recovery_preserves_applied_moments_and_force_balance(tip, moment):
    e = beam()
    other = 3 - tip
    coords = {1: np.zeros(3), 2: np.array([4.8, 0.0, 0.0])}
    load = np.array([2.0, 3.0, -5.0, 7.0, 11.0, moment])
    end = load.copy()
    far = -load.copy()
    far[3:] -= np.cross(coords[tip] - coords[other], load[:3])
    expected = {"i_end": end if tip == 1 else far, "j_end": far if tip == 1 else end}
    raw = {1: {k: v + np.full(6, 1e-9) for k, v in expected.items()}}
    recovered, _ = recover_free_branches(coords, {1: e}, raw, {tip: load}, {other}, 1.0, 1e-8)
    for key, values in expected.items():
        np.testing.assert_allclose(recovered[1][key], values, rtol=0, atol=1e-14)
    assert recovered[1]["i_end" if tip == 1 else "j_end"][5] == moment
    assert raw[1]["i_end"][0] != recovered[1]["i_end"][0]


@pytest.mark.material_nonlinear
def test_recovery_does_not_hide_a_constitutive_or_solver_error():
    e = beam()
    with pytest.raises(ValueError, match="equilibrium recovery"):
        recover_free_branches(
            {1: np.zeros(3), 2: np.array([4.8, 0.0, 0.0])},
            {1: e},
            {1: dict(i_end=np.zeros(6), j_end=np.zeros(6))},
            {2: np.ones(6)},
            {1},
            1.0,
            1e-8,
        )


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("foundation", [False, True])
def test_supported_or_foundation_member_is_not_statically_recovered(foundation):
    e = beam()
    if foundation:
        e.foundation[1] = 1.0
    raw = {1: dict(i_end=np.arange(6.0), j_end=np.arange(6.0))}
    recovered, ids = recover_free_branches(
        {1: np.zeros(3), 2: np.array([4.8, 0.0, 0.0])},
        {1: e},
        raw,
        {},
        {1} if foundation else {1, 2},
        1.0,
        1e-8,
    )
    assert ids == []
    np.testing.assert_array_equal(recovered[1]["i_end"], raw[1]["i_end"])


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("factor", [1.0, -0.3])
def test_recovery_with_consistent_distributed_load_and_reversed_loading(factor):
    e = beam()
    e.set_line_load("Ly", [3.0, 7.0])
    # Uniformly supported at node 1, free at node 2. Physical end reactions
    # integrate q: total=5L, moment=17L^2/6.
    total = 5 * e.length * factor
    moment = 17 * e.length**2 / 6 * factor
    expected = {1: dict(i_end=np.array([0.0, -total, 0.0, 0.0, 0.0, -moment]), j_end=np.zeros(6))}
    consistent = factor * e.get_member_load_vector()
    recovered, ids = recover_free_branches(
        {1: np.zeros(3), 2: np.array([e.length, 0.0, 0.0])},
        {1: e},
        expected,
        {1: consistent[:6], 2: consistent[6:]},
        {1},
        factor,
        1e-8,
    )
    assert ids == [1]
    for end in ("i_end", "j_end"):
        np.testing.assert_allclose(recovered[1][end], expected[1][end], atol=1e-12)
