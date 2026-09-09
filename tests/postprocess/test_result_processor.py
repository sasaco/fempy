"""Public result processing must use the same DOF layout as assembly."""

import numpy as np
import pytest

from fem.mesh import MeshModel
from fem.result_processor import ResultProcessor


pytestmark = pytest.mark.unit


def mesh_with_nodes(element_type):
    mesh = MeshModel()
    mesh.add_node(30, [1, 0, 0])
    mesh.add_node(10, [0, 0, 0])
    mesh.add_element(7, element_type, [10, 30], 1)
    return mesh


def test_three_dof_displacements_follow_sorted_noncontiguous_node_layout():
    result = ResultProcessor().process_displacement(
        np.array([1, 2, 3, 4, 5, 6]), mesh_with_nodes("tetra")
    )

    assert result[10] == pytest.approx(
        {"dx": 1, "dy": 2, "dz": 3, "rx": 0, "ry": 0, "rz": 0}
    )
    assert result[30] == pytest.approx(
        {"dx": 4, "dy": 5, "dz": 6, "rx": 0, "ry": 0, "rz": 0}
    )


def test_six_dof_displacements_preserve_existing_schema():
    result = ResultProcessor().process_displacement(
        np.arange(1, 13), mesh_with_nodes("bar")
    )

    assert result[10] == pytest.approx(
        {"dx": 1, "dy": 2, "dz": 3, "rx": 4, "ry": 5, "rz": 6}
    )
    assert result[30] == pytest.approx(
        {"dx": 7, "dy": 8, "dz": 9, "rx": 10, "ry": 11, "rz": 12}
    )


def test_mixed_three_and_six_dof_elements_use_one_six_dof_layout():
    mesh = mesh_with_nodes("tetra")
    mesh.add_node(50, [2, 0, 0])
    mesh.add_element(8, "bar", [30, 50], 1)

    result = ResultProcessor().process_displacement(np.arange(1, 19), mesh)

    assert result[10]["rx"] == 4
    assert result[30]["dx"] == 7
    assert result[50]["rz"] == 18


def test_displacement_length_mismatch_is_not_hidden_by_zero_fill():
    with pytest.raises(ValueError, match="expected 6.*received 5"):
        ResultProcessor().process_displacement(
            np.arange(5), mesh_with_nodes("tetra")
        )
