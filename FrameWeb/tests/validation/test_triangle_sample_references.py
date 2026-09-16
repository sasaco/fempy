"""Saved triangular shell values must match an independently executed source."""

import json
import subprocess

import numpy as np
import pytest

from tests.support.assertions import assert_dict_almost_equal
from tests.support.oracles.triangle_sample import check_input, independent_sample
from tests.support.paths import ROOT
from tests.support.provenance import read_source_records

pytestmark = [pytest.mark.oracle, pytest.mark.requires_node]


@pytest.mark.parametrize(
    "name", ["shellBeamTri1", "shellThickBeamTri1", "shellRibTri1", "shellTensTorTri1"]
)
def test_saved_triangle_sample_uses_independent_source_reference(name):
    data = json.loads(
        (ROOT / f"tests/data/shell/{name}.json").read_text(encoding="utf8")
    )
    reference, proof = independent_sample(
        data,
        ROOT / f"docs/v0/testdata/shell/{name}.out",
        spin=name in ("shellRibTri1", "shellTensTorTri1"),
    )
    assert proof["maximum_free_force_residual"] < 1e-10
    assert_dict_almost_equal(data["result"]["1"], reference)


@pytest.mark.parametrize(
    "mutation", ["node", "thickness", "material", "load", "restraint"]
)
def test_triangle_source_identity_rejects_changed_input(mutation):
    name = "shellBeamTri1"
    data = json.loads(
        (ROOT / f"tests/data/shell/{name}.json").read_text(encoding="utf8")
    )
    if mutation == "node":
        data["node"]["1"]["x"] += 0.1
    elif mutation == "thickness":
        data["element"]["1"]["1"]["thickness"] *= 2
    elif mutation == "material":
        data["element"]["1"]["1"]["E"] *= 2
    elif mutation == "load":
        data["load"]["1"]["load_node"][0]["tz"] *= 2
    else:
        data["fix_node"]["1"][0]["tx"] = False
    with pytest.raises(AssertionError):
        check_input(
            data, read_source_records(ROOT / f"docs/v0/testdata/shell/{name}.out")
        )


@pytest.fixture(scope="module")
def independent_spin_operator(tmp_path_factory):
    # A rotated triangle tests the coordinate-free curl/rotation constraint.
    basis = np.array([[1, 2, 2], [2, 1, -2], [-2, 2, -1]]) / 3
    points = np.array([[0.0, 0.0, 0.0], [2.0, 0.0, 0.0], [0.0, 3.0, 0.0]]) @ basis.T
    source = tmp_path_factory.mktemp("spin-oracle") / "triangle.fem"
    lines = [
        "Material 1 1000 .25 400 0 0 0",
        "ShellParameter 1 .2",
        "TriElement1 1 1 1 1 2 3",
    ]
    lines += [
        f"Node {i + 1} " + " ".join(map(repr, row))
        for i, row in enumerate(points.tolist())
    ]
    source.write_text("\n".join(lines) + "\n", encoding="utf8")
    script = ROOT / "tests/support/oracles/source_triangle_spin.cjs"
    raw = json.loads(
        subprocess.check_output(["node", str(script), str(source)], encoding="utf8")
    )
    k = np.zeros((18, 18))
    for i, row in enumerate(raw["stiffness_rows"]):
        for j, value in row:
            k[i, j] = value
    return k, points, basis


def test_independent_spin_operator_has_six_physical_rigid_modes(
    independent_spin_operator,
):
    k, points, _ = independent_spin_operator
    for direction in np.eye(3):
        for rotation in (False, True):
            u = np.zeros((3, 6))
            u[:, :3] = np.cross(direction, points) if rotation else direction
            if rotation:
                u[:, 3:] = direction
            np.testing.assert_allclose(k @ u.ravel(), 0.0, atol=1e-11)
    assert np.count_nonzero(np.linalg.eigvalsh(k) < 1e-10) == 6


def test_independent_spin_operator_constant_membrane_energy(independent_spin_operator):
    k, points, basis = independent_spin_operator
    xy = points @ basis
    u = np.zeros((3, 6))
    u[:, 0] = 0.01 * xy[:, 0] + 0.03 * xy[:, 1]
    u[:, 1] = 0.02 * xy[:, 1]
    u[:, 5] = -0.03 / 2
    u = (u.reshape(-1, 3) @ basis.T).ravel()
    expected = (
        3
        * 0.2
        / 2
        * 1000
        / (1 - 0.25**2)
        * (0.01**2 + 0.02**2 + 2 * 0.25 * 0.01 * 0.02 + (1 - 0.25) / 2 * 0.03**2)
    )
    assert u @ k @ u / 2 == pytest.approx(expected, abs=1e-12)


def test_independent_spin_operator_relative_rotation_energy(independent_spin_operator):
    k, points, basis = independent_spin_operator
    normal = basis[:, 2]
    u = np.zeros((3, 6))
    u[:, :3] = np.cross(0.02 * normal, points)
    u[:, 3:] = 0.05 * normal
    expected = 0.5 * 0.001 * 400 * 0.2 * 3 * (0.05 - 0.02) ** 2
    assert u.ravel() @ k @ u.ravel() / 2 == pytest.approx(expected, abs=1e-12)
