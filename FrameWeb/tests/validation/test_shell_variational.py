"""Independent shell energy checks and complete original-input references."""

import json

import mpmath as mp
import numpy as np
import pytest

from tests.support.assertions import assert_dict_almost_equal
from tests.support.oracles.shell_variational import operator, solve_reference
from tests.support.oracles.triangle_sample import check_input
from tests.support.paths import ROOT
from tests.support.provenance import read_source_records

pytestmark = pytest.mark.oracle


@pytest.fixture(params=[3, 4])
def independent_shell(request):
    n = request.param
    xy = [(0, 0), (2, 0), (0, 3)] if n == 3 else [(0, 0), (2, 0), (2, 3), (0, 3)]
    points = [mp.matrix([x, y, 0]) for x, y in xy]
    with mp.workdps(65):
        k, response = operator(points, dict(E=1000, nu=0.25, G=400), 0.2)
    return n, np.array(xy, dtype=float), np.array(k.tolist(), dtype=float), response


def test_independent_shell_six_rigid_modes(independent_shell):
    n, xy, k, _ = independent_shell
    xyz = np.c_[xy, np.zeros(n)]
    for direction in np.eye(3):
        for rotation in (False, True):
            u = np.zeros((n, 6))
            u[:, :3] = np.cross(direction, xyz) if rotation else direction
            if rotation:
                u[:, 3:] = direction
            np.testing.assert_allclose(k @ u.ravel(), 0, atol=1e-12)
    assert np.count_nonzero(np.linalg.eigvalsh(k) < 1e-10) == 6


def test_independent_shell_constant_membrane_energy(independent_shell):
    n, xy, k, _ = independent_shell
    u = np.zeros((n, 6))
    u[:, 0] = 0.01 * xy[:, 0] + 0.03 * xy[:, 1]
    u[:, 1] = 0.02 * xy[:, 1]
    u[:, 5] = -0.015
    expected = (
        (3 if n == 3 else 6)
        * 0.2
        / 2
        * 1000
        / (1 - 0.25**2)
        * (0.01**2 + 0.02**2 + 2 * 0.25 * 0.01 * 0.02 + (1 - 0.25) / 2 * 0.03**2)
    )
    assert u.ravel() @ k @ u.ravel() / 2 == pytest.approx(expected, abs=1e-12)


def test_independent_shell_constant_curvature_and_surface_signs(independent_shell):
    n, xy, k, response = independent_shell
    u = np.zeros((n, 6))
    curvature = 0.003
    u[:, 2] = -0.5 * curvature * xy[:, 0] ** 2
    u[:, 4] = curvature * xy[:, 0]
    expected = (
        (3 if n == 3 else 6) * 1000 * 0.2**3 / (24 * (1 - 0.25**2)) * curvature**2
    )
    assert u.ravel() @ k @ u.ravel() / 2 == pytest.approx(expected, abs=1e-12)
    with mp.workdps(65):
        raw = response(list(u.ravel()))["raw_result"]
    for side, sign in [(1, 1), (2, -1)]:
        for values in raw[f"nodeStress{side}"]:
            assert values[0] == pytest.approx(
                sign * 0.1 * curvature * 1000 / (1 - 0.25**2), abs=1e-12
            )
            assert values[4] == pytest.approx(0, abs=1e-12)
            assert values[5] == pytest.approx(0, abs=1e-12)


def test_independent_shell_drilling_energy_is_separate(independent_shell):
    n, xy, k, response = independent_shell
    u = np.zeros((n, 6))
    u[:, 0] = -0.02 * xy[:, 1]
    u[:, 1] = 0.02 * xy[:, 0]
    u[:, 5] = 0.05
    expected = 0.5 * 0.001 * 400 * 0.2 * (3 if n == 3 else 6) * (0.05 - 0.02) ** 2
    assert u.ravel() @ k @ u.ravel() / 2 == pytest.approx(expected, abs=1e-12)
    with mp.workdps(65):
        output = response(list(u.ravel()))
    assert output["strain_energy"] == pytest.approx(0, abs=1e-12)


SAMPLES = (
    "shellTri1",
    "shellBeamQuad1",
    "shellQuad1",
    "shellQuad1_t1",
    "shellQuad1_t2",
    "shellRibQuad1",
    "shellTensTorQuad1",
    "shellThickBeamQuad1",
)


@pytest.mark.parametrize("name", SAMPLES)
def test_saved_shell_reference_matches_independent_variational_solution(name):
    data = json.loads(
        (ROOT / f"tests/data/shell/{name}.json").read_text(encoding="utf8")
    )
    source = read_source_records(ROOT / f"docs/v0/testdata/shell/{name}.out")
    check_input(data, source, element_types=("TriElement1", "QuadElement1"))
    assert_dict_almost_equal(data["result"]["1"], solve_reference(data))
