"""postprocess / shell results contracts."""

import copy

import numpy as np
import pytest

from fem.elements.shell_element import ShellElement
from fem.elements.shell_postprocess import legacy_shell_view
from fem.material import Material, MaterialProperty, ShellParameter
from tests.support.builders.dkt_triangle import triangle
from tests.support.builders.shell_output import shell

pytestmark = pytest.mark.unit


def test_dkt_transverse_resultants_follow_moment_equilibrium():
    e, coords = triangle(0.2)
    result = e.calculate_shell_results(np.arange(18) * 0.001)
    force, moment = np.zeros(3), np.zeros(3)
    nodes = dict(zip(e.node_ids, coords))
    for edge in result["edge_resultants"].values():
        for node, end in zip(edge["node_ids"], ("i_end", "j_end")):
            values = np.asarray(edge[end])
            force += values[:3]
            moment += values[3:] + np.cross(nodes[node], values[:3])
    np.testing.assert_allclose(force, 0.0, atol=1e-12)
    np.testing.assert_allclose(moment, 0.0, atol=1e-12)
    assert np.linalg.norm(result["resultants"]["shear"]) > 1e-6


@pytest.mark.parametrize("n", [3, 4])
@pytest.mark.parametrize("vertical", [False, True])
def test_shell_rotated_membrane_patch(n, vertical):
    coords = np.array([[0, 0, 0], [2, 0, 0], [2, 3, 0], [0, 3, 0]], dtype=float)[:n]
    rotation = np.array([[0, 0, 1], [1, 0, 0], [0, 1, 0]]) if vertical else np.eye(3)
    u = np.zeros((n, 6))
    u[:, 0], u[:, 1] = 0.001 * coords[:, 0] + 0.003 * coords[:, 1], 0.002 * coords[:, 1]
    u[:, :3] = u[:, :3] @ rotation
    m = Material()
    m.add_material(1, MaterialProperty("patch", 1000, 0.25))
    e = ShellElement(1, list(range(n)), 1, 0.1)
    e.set_material_properties(m, ShellParameter(0.1))
    e.set_node_coordinates(dict(enumerate(coords @ rotation)))
    r = e.calculate_stress_strain(u.ravel())
    expected = [0.001, 0.002, 0.003]
    np.testing.assert_allclose(r["strain"], np.tile(expected, (len(r["strain"]), 1)), atol=1e-14)


def test_legacy_engineering_shear_energy_density_and_separate_surfaces():
    e, coords = triangle(0.2)
    u = np.zeros((3, 6))
    u[:, 0] = 0.003 * coords[:, 1]
    u[:, 4] = 0.002 * coords[:, 0]
    u[:, 2] = -0.001 * coords[:, 0] ** 2
    modern = e.calculate_shell_results(u.ravel())
    saved = copy.deepcopy(modern)
    legacy = legacy_shell_view(e, u.ravel())
    assert set(legacy) == {"stress", "strain", "strain_energy", "raw_result"}
    raw = legacy["raw_result"]
    assert raw["nodeStrain1"][0][3] == pytest.approx(0.003)
    assert raw["nodeStrain2"][0][3] == pytest.approx(0.003)
    assert raw["elemStrain1"][0] == pytest.approx(0.0002)
    assert raw["elemStrain2"][0] == pytest.approx(-0.0002)
    assert legacy["strain_energy"] == pytest.approx(raw["elemEnergy1"])
    assert modern == saved


def test_legacy_view_uses_point_mean_not_area_weighted_mean():
    # Use direct construction here because this test exercises a distorted quad.
    from fem.elements.shell_element import ShellElement
    from fem.material import Material, MaterialProperty

    e = ShellElement(99, [10, 20, 30, 40], 1, 0.2)
    e.set_node_coordinates(
        dict(zip(e.node_ids, [[0.0, 0.0, 0.0], [3.0, 0.0, 0.0], [2.0, 2.0, 0.0], [0.0, 1.0, 0.0]]))
    )
    m = Material()
    m.add_material(1, MaterialProperty("plate", 1000.0, 0.25))
    e.set_material_properties(m)
    u = np.arange(24) * 0.001
    legacy = legacy_shell_view(e, u)
    modern = e.calculate_shell_results(u)
    assert legacy["strain_energy"] != pytest.approx(modern["raw_result"]["elemEnergy1"], rel=1e-6)


@pytest.mark.parametrize("n", [3, 4])
@pytest.mark.parametrize("rotated", [False, True])
@pytest.mark.parametrize("mode", ["membrane", "bending", "shear", "rigid"])
def test_surface_tensors(n, rotated, mode):
    rotation = np.array([[0.0, 0, 1], [1, 0, 0], [0, 1, 0]]) if rotated else np.eye(3)
    e, xy = shell(n, rotation)
    u = np.zeros((n, 6))
    eps = np.zeros((3, 3))
    sig = np.zeros((3, 3))
    if mode == "membrane":
        u[:, 0] = 0.002 * xy[:, 0] + 0.006 * xy[:, 1]
        u[:, 1] = -0.001 * xy[:, 1]
        eps = np.array([[0.002, 0.003, 0], [0.003, -0.001, 0], [0, 0, 0]])
        sig = np.array([[2.25, 3.0, 0], [3.0, -0.75, 0], [0, 0, 0]])
    elif mode == "bending":
        u[:, 4] = 0.02 * xy[:, 0]
        u[:, 2] = -0.01 * xy[:, 0] ** 2
        eps[0, 0] = 0.002
        sig[0, 0] = 2.5
        sig[1, 1] = 0.5
    elif mode == "shear":
        u[:, 2] = 0.003 * xy[:, 0] + 0.004 * xy[:, 1]
        eps[0, 2] = eps[2, 0] = 0.0015
        eps[1, 2] = eps[2, 1] = 0.002
        sig[0, 2] = sig[2, 0] = 1.25
        sig[1, 2] = sig[2, 1] = 5 / 3
    else:
        omega = np.array([0.02, -0.01, 0.03])
        u[:, :3] = np.cross(omega, xy) + [1.0, 2.0, 3.0]
        u[:, 3:] = omega
    u[:, :3] = u[:, :3] @ rotation.T
    u[:, 3:] = u[:, 3:] @ rotation.T
    r = e.calculate_shell_results(u.ravel())["raw_result"]
    for side, sign in [(1, 1), (2, -1)]:
        # Tri Mindlin interpolation cannot reproduce zero bending shear at
        # every vertex. Verify its in-plane tensor separately in that case.
        a, b = eps.copy(), sig.copy()
        if mode == "bending":
            a *= sign
            b *= sign
        a, b = rotation @ a @ rotation.T, rotation @ b @ rotation.T
        av = a[[0, 1, 2, 0, 1, 2], [0, 1, 2, 1, 2, 0]]
        bv = b[[0, 1, 2, 0, 1, 2], [0, 1, 2, 1, 2, 0]]
        indices = [1, 2, 4] if rotated else [0, 1, 3]
        if mode != "bending" or n == 4:
            indices = list(range(6))
        for prefix in ["node", "elem"]:
            actual_a = np.atleast_2d(r[f"{prefix}Strain{side}"])
            actual_b = np.atleast_2d(r[f"{prefix}Stress{side}"])
            np.testing.assert_allclose(
                actual_a[:, indices], np.tile(av[indices], (len(actual_a), 1)), atol=2e-14
            )
            np.testing.assert_allclose(
                actual_b[:, indices], np.tile(bv[indices], (len(actual_b), 1)), atol=2e-11
            )


def test_shell_integrated_energy_and_resultants():
    e, xy = shell()
    u = np.zeros((4, 6))
    u[:, 4] = 0.02 * xy[:, 0]
    u[:, 2] = -0.01 * xy[:, 0] ** 2
    r = e.calculate_shell_results(u.ravel())
    # Dxx=1250, area=6, kappa=.02, t=.2.
    expected = 0.5 * 1250 * 0.2**3 / 12 * 0.02**2 * 6
    assert r["strain_energy"] == pytest.approx(expected, abs=1e-12)
    assert r["strain_energy"] == pytest.approx(
        0.5 * u.ravel() @ e.get_stiffness_matrix() @ u.ravel(), abs=1e-12
    )
    np.testing.assert_allclose(r["resultants"]["moment"], [1 / 60, 1 / 300, 0], atol=1e-13)
    assert r["raw_result"]["elemStrain1"][0] == pytest.approx(0.002)
    assert r["raw_result"]["elemStrain2"][0] == pytest.approx(-0.002)


def test_drilling_energy_is_separate_from_physical_energy():
    e, _ = shell()
    u = np.zeros((4, 6))
    u[:, 5] = [0, 0.01, 0.02, 0.03]
    r = e.calculate_shell_results(u.ravel())
    assert r["strain_energy"] == 0
    assert r["drilling_energy"] > 0
    assert r["drilling_energy"] == pytest.approx(0.5 * u.ravel() @ e.get_stiffness_matrix() @ u.ravel())


@pytest.mark.parametrize("u", [np.zeros(23), np.full(24, np.nan), np.full(24, np.inf)])
def test_shell_output_rejects_incomplete_or_nonfinite_displacement(u):
    e, _ = shell()
    with pytest.raises(ValueError, match="finite displacement"):
        e.calculate_shell_results(u)


@pytest.mark.parametrize("n", [3, 4])
@pytest.mark.parametrize("rotated", [False, True])
def test_edge_tractions_from_uniform_membrane_stress(n, rotated):
    rotation = np.array([[0.0, 0, 1], [1, 0, 0], [0, 1, 0]]) if rotated else np.eye(3)
    e, xyz = shell(n, rotation)
    u = np.zeros((n, 6))
    u[:, 0] = 0.002 * xyz[:, 0]
    u[:, :3] = u[:, :3] @ rotation.T
    result = e.calculate_shell_results(u.ravel())
    # sigma_x=2.5, sigma_y=.5; thickness=.2.
    tensor = rotation @ np.diag([2.5, 0.5, 0.0]) @ rotation.T
    forces, moments = np.zeros(3), np.zeros(3)
    for edge in result["edge_resultants"].values():
        i, j = [e.node_ids.index(nid) for nid in edge["node_ids"]]
        length = np.linalg.norm(xyz[j] - xyz[i])
        expected = tensor @ edge["outward_normal"] * 0.2 * length / 2
        for node, end in zip([i, j], ["i_end", "j_end"]):
            np.testing.assert_allclose(edge[end][:3], expected, atol=1e-13)
            np.testing.assert_allclose(edge[end][3:], 0, atol=1e-13)
            forces += edge[end][:3]
            moments += np.cross(rotation @ xyz[node], edge[end][:3])
    np.testing.assert_allclose(forces, 0, atol=1e-13)
    np.testing.assert_allclose(moments, 0, atol=1e-13)


def test_edge_bending_couple_has_physical_rotation_sign():
    e, xy = shell()
    u = np.zeros((4, 6))
    u[:, 4] = 0.02 * xy[:, 0]
    u[:, 2] = -0.01 * xy[:, 0] ** 2
    edges = e.calculate_shell_results(u.ravel())["edge_resultants"]
    # Right edge outward normal +x: couple +y, Mxx=1/60, length=3.
    for end in ["i_end", "j_end"]:
        np.testing.assert_allclose(edges["11-12"][end], [0, 0, 0, 0, 0.025, 0], atol=1e-13)
