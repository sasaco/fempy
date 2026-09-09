"""VTK export contracts verified through the independent meshio reader."""

from pathlib import Path

import meshio
import numpy as np
import pytest
from meshio._mesh import topological_dimension

from fem.file_io import write_vtk
from fem.material import MaterialProperty
from fem.mesh import MeshModel
from fem.model import FemModel


# meshio maps VTK type 26 to wedge15 but 5.3.5 omits only its dimension
# metadata. Supplying that metadata lets its independent legacy parser finish.
topological_dimension.setdefault("wedge15", 3)
PRE_VTK_97_WEDGE_ORDER = np.array([0, 2, 1, 3, 5, 4])


VTK_CASES = {
    "bar": ("line", [[0, 0, 0], [1, 0, 0]]),
    "nonlinear_bar": ("line", [[0, 0, 0], [1, 0, 0]]),
    "shell3": ("triangle", [[0, 0, 0], [1, 0, 0], [0, 1, 0]]),
    "shell4": ("quad", [[0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0]]),
    "tetra": ("tetra", [[0, 0, 0], [1, 0, 0], [0, 1, 0], [0, 0, 1]]),
    "hexa": (
        "hexahedron",
        [[-1, -1, -1], [1, -1, -1], [1, 1, -1], [-1, 1, -1],
         [-1, -1, 1], [1, -1, 1], [1, 1, 1], [-1, 1, 1]],
    ),
    "wedge": (
        "wedge",
        [[0, 0, -1], [1, 0, -1], [0, 1, -1],
         [0, 0, 1], [1, 0, 1], [0, 1, 1]],
    ),
    "tetra2": (
        "tetra10",
        [[0, 0, 0], [1, 0, 0], [0, 1, 0], [0, 0, 1],
         [.5, 0, 0], [.5, .5, 0], [0, .5, 0], [0, 0, .5],
         [.5, 0, .5], [0, .5, .5]],
    ),
    "wedge2": (
        "wedge15",
        [[0, 0, -1], [1, 0, -1], [0, 1, -1],
         [0, 0, 1], [1, 0, 1], [0, 1, 1],
         [.5, 0, -1], [.5, .5, -1], [0, .5, -1],
         [.5, 0, 1], [.5, .5, 1], [0, .5, 1],
         [0, 0, 0], [1, 0, 0], [0, 1, 0]],
    ),
    "hexa2": (
        "hexahedron20",
        [[-1, -1, -1], [1, -1, -1], [1, 1, -1], [-1, 1, -1],
         [-1, -1, 1], [1, -1, 1], [1, 1, 1], [-1, 1, 1],
         [0, -1, -1], [1, 0, -1], [0, 1, -1], [-1, 0, -1],
         [0, -1, 1], [1, 0, 1], [0, 1, 1], [-1, 0, 1],
         [-1, -1, 0], [1, -1, 0], [1, 1, 0], [-1, 1, 0]],
    ),
}


def _mixed_mesh():
    mesh = MeshModel()
    expected = {}
    next_node = 10
    # Deliberately use neither element insertion order nor consecutive IDs.
    element_ids = [700, 20, 450, 31, 900, 62, 15, 810, 44, 305]
    for (kind, (vtk_kind, coordinates)), element_id in zip(VTK_CASES.items(), element_ids):
        node_ids = []
        for coordinate in coordinates:
            next_node += 7
            node_ids.append(next_node)
            mesh.add_node(next_node, coordinate)
        element_type = "shell" if kind.startswith("shell") else kind
        mesh.add_element(element_id, element_type, node_ids, 1)
        expected[element_id] = (vtk_kind, node_ids)
    return mesh, expected


def _cells_by_element_id(vtk):
    observed = {}
    for block, ids in zip(vtk.cells, vtk.cell_data["element_id"]):
        for connectivity, element_id in zip(block.data, np.ravel(ids)):
            # meshio 5.3.5 applies the pre-VTK-9.7 linear-wedge convention.
            # Current VTK 9.7 parametric coordinates match FEMPython order.
            if block.type == "wedge":
                connectivity = connectivity[PRE_VTK_97_WEDGE_ORDER]
            node_ids = np.ravel(vtk.point_data["node_id"])[connectivity].astype(int).tolist()
            observed[int(element_id)] = (block.type, node_ids)
    return observed


def _cell_value(vtk, field, element_id):
    for ids, values in zip(vtk.cell_data["element_id"], vtk.cell_data[field]):
        matches = np.flatnonzero(np.ravel(ids) == element_id)
        if len(matches):
            return values[matches[0]]
    raise AssertionError(f"element {element_id} is absent from {field}")


def _vtk_tensor(components):
    xx, yy, zz, xy, yz, zx = components
    return [[xx, xy, zx], [xy, yy, yz], [zx, yz, zz]]


def test_mixed_topology_ids_and_quadratic_node_order_roundtrip(tmp_path: Path):
    mesh, expected = _mixed_mesh()
    path = tmp_path / "mixed.vtk"

    write_vtk({"mesh": mesh}, {}, path)

    vtk = meshio.read(path)
    assert len(vtk.points) == len(mesh.nodes)
    assert sum(len(block.data) for block in vtk.cells) == len(mesh.elements)
    assert _cells_by_element_id(vtk) == expected


def test_result_quantities_remain_distinct_and_cell_aligned(tmp_path: Path):
    mesh = MeshModel()
    for node_id, coordinate in {
        10: [0, 0, 0], 20: [1, 0, 0], 30: [0, 1, 0], 40: [0, 0, 1]
    }.items():
        mesh.add_node(node_id, coordinate)
    # Sorted cell order is shell, beam, solid; result insertion order differs.
    mesh.add_element(70, "bar", [10, 20], 1)
    mesh.add_element(5, "shell", [10, 20, 30], 1)
    mesh.add_element(400, "tetra", [10, 20, 30, 40], 1)
    result = {
        "node_displacements": {
            40: {"dx": 4, "dy": 5, "dz": 6},
            10: {"dx": 1, "dy": 2, "dz": 3},
            20: {"dx": 2, "dy": 3, "dz": 4},
            30: {"dx": 3, "dy": 4, "dz": 5},
        },
        "reaction_forces": {10: {"fx": -1, "fy": -2, "fz": -3}},
        "element_stresses": {
            400: {
                "stress": np.array([[1, 2, 3, 4, 5, 6], [3, 4, 5, 6, 7, 8]]),
                "strain": np.array([[.1, .2, .3, .4, .5, .6], [.3, .4, .5, .6, .7, .8]]),
            },
            70: {"i_end": np.arange(1, 7), "j_end": np.arange(7, 13)},
        },
        "shell_results": {
            5: {
                "raw_result": {
                    "elemStress1": [11, 12, 13, 14, 15, 16],
                    "elemStress2": [21, 22, 23, 24, 25, 26],
                    "elemStrain1": [.1, .2, .3, .4, .5, .6],
                    "elemStrain2": [.7, .8, .9, 1.0, 1.1, 1.2],
                },
                "resultants": {
                    "membrane": [31, 32, 33],
                    "moment": [41, 42, 43],
                    "shear": [51, 52],
                },
            }
        },
    }
    path = tmp_path / "results.vtk"

    write_vtk({"mesh": mesh}, result, path)

    vtk = meshio.read(path)
    point_ids = np.ravel(vtk.point_data["node_id"])
    node_index = int(np.flatnonzero(point_ids == 10)[0])
    np.testing.assert_allclose(vtk.point_data["displacement"][node_index], [1, 2, 3])
    np.testing.assert_allclose(vtk.point_data["reaction_force"][node_index], [-1, -2, -3])
    assert np.isnan(vtk.point_data["reaction_force"][point_ids == 20]).all()

    assert _cell_value(vtk, "section_force_i_fx", 70) == pytest.approx(1)
    assert np.isnan(_cell_value(vtk, "section_force_i_fx", 5))
    assert _cell_value(vtk, "shell_membrane_nx", 5) == pytest.approx(31)
    assert _cell_value(vtk, "shell_moment_mxy", 5) == pytest.approx(43)
    assert _cell_value(vtk, "shell_shear_qy", 5) == pytest.approx(52)
    np.testing.assert_allclose(
        _cell_value(vtk, "solid_stress_gauss_point_mean", 400),
        [[2, 5, 7], [5, 3, 6], [7, 6, 4]],
    )
    np.testing.assert_allclose(
        _cell_value(vtk, "shell_stress_surface_1", 5),
        [[11, 14, 16], [14, 12, 15], [16, 15, 13]],
    )


def test_public_shell_analysis_results_roundtrip(tmp_path: Path):
    model = FemModel()
    model.material.add_material(1, MaterialProperty("plate", 1000.0, 0.0))
    for node_id, coordinate in {
        10: [0, 0, 0], 20: [0, 1, 0], 30: [2, 1, 0], 40: [2, 0, 0]
    }.items():
        model.mesh.add_node(node_id, coordinate)
    model.mesh.add_element(75, "shell", [10, 40, 30, 20], 1, thickness=0.2)
    for node_id in (10, 20):
        model.boundary.add_restraint(node_id, [True] * 6)
    for node_id in (30, 40):
        model.boundary.add_load(node_id, [0, 0, 0, 0, 5e-6, 0])

    result = model.run()
    path = tmp_path / "shell.vtk"
    write_vtk({"mesh": model.mesh}, result, path)

    vtk = meshio.read(path)
    assert _cells_by_element_id(vtk) == {75: ("quad", [10, 40, 30, 20])}
    assert _cell_value(vtk, "shell_moment_mx", 75) == pytest.approx(
        result["shell_results"][75]["resultants"]["moment"][0]
    )
    np.testing.assert_allclose(
        _cell_value(vtk, "shell_stress_surface_2", 75),
        _vtk_tensor(result["shell_results"][75]["raw_result"]["elemStress2"]),
    )


def test_missing_generic_cell_component_is_nan_not_zero(tmp_path: Path):
    mesh = MeshModel()
    mesh.add_node(10, [0, 0, 0])
    mesh.add_node(20, [1, 0, 0])
    mesh.add_node(30, [2, 0, 0])
    mesh.add_element(50, "bar", [10, 20], 1)
    mesh.add_element(90, "bar", [20, 30], 1)
    path = tmp_path / "missing.vtk"

    write_vtk({"mesh": mesh}, {"element_stresses": {50: {"axial": 12.5}}}, path)

    vtk = meshio.read(path)
    assert _cell_value(vtk, "stress_axial", 50) == pytest.approx(12.5)
    assert np.isnan(_cell_value(vtk, "stress_axial", 90))


@pytest.mark.parametrize(
    ("element_type", "nodes"),
    [("unknown", [1, 2]), ("shell", [1, 2, 3, 4, 5]), ("tetra2", list(range(1, 10)))],
)
def test_unsupported_or_malformed_cell_fails_explicitly(tmp_path: Path, element_type, nodes):
    mesh = MeshModel()
    for node_id in range(1, max(nodes) + 1):
        mesh.add_node(node_id, [node_id, 0, 0])
    mesh.add_element(1, element_type, nodes, 1)

    with pytest.raises(ValueError, match="VTK cell"):
        write_vtk({"mesh": mesh}, {}, tmp_path / "bad.vtk")
