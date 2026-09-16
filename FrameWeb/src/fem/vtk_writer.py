"""Legacy ASCII VTK writer for supported FEMPython mesh and result data."""

from pathlib import Path
from typing import Any, Dict, Iterable, Mapping

import numpy as np


class VTKWriter:
    """Write an ``UNSTRUCTURED_GRID`` while preserving model ID ordering."""

    _FIXED_CELL_TYPES = {
        "bar": (2, 3),
        "beam": (2, 3),
        "line": (2, 3),
        "barelement": (2, 3),
        "beamelement": (2, 3),
        "trusselement": (2, 3),
        "nonlinear_bar": (2, 3),
        "triangle": (3, 5),
        "trielement1": (3, 5),
        "quadrilateral": (4, 9),
        "quadelement1": (4, 9),
        "quad": (4, 9),
        "tetra": (4, 10),
        "tet": (4, 10),
        "tetraelement1": (4, 10),
        "tetraelement": (4, 10),
        "hexa": (8, 12),
        "hex": (8, 12),
        "hexahedron": (8, 12),
        "hexaelement1": (8, 12),
        "hexaelement": (8, 12),
        "wedge": (6, 13),
        "wedgeelement1": (6, 13),
        "wedgeelement": (6, 13),
        "tetra2": (10, 24),
        "tetraelement2": (10, 24),
        "hexa2": (20, 25),
        "hexaelement2": (20, 25),
        "wedge2": (15, 26),
        "wedgeelement2": (15, 26),
    }
    def __init__(self, file_path: str | Path):
        self.file = open(file_path, "w", encoding="utf-8", newline="\n")
        self.node_ids: list[int] = []
        self.node_id_to_index: Dict[int, int] = {}
        self.element_ids: list[int] = []
        self.n_points = 0
        self.cell_count = 0

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc_value, traceback):
        self.close()

    def close(self):
        if not self.file.closed:
            self.file.close()

    def write_header(self):
        self.file.write("# vtk DataFile Version 3.0\n")
        self.file.write("FEMPython VTK Output\n")
        self.file.write("ASCII\n")
        self.file.write("DATASET UNSTRUCTURED_GRID\n")

    def write_points(self, nodes: Mapping[int, Any]):
        self.node_ids = sorted(nodes)
        self.n_points = len(self.node_ids)
        self.node_id_to_index = {node_id: index for index, node_id in enumerate(self.node_ids)}
        coordinates = {}
        for node_id in self.node_ids:
            value = np.asarray(nodes[node_id], dtype=float)
            if value.shape != (3,) or not np.isfinite(value).all():
                raise ValueError(f"VTK point {node_id} requires three finite coordinates")
            coordinates[node_id] = value
        self.file.write(f"POINTS {self.n_points} double\n")
        for node_id in self.node_ids:
            self.file.write(" ".join(self._format_float(value) for value in coordinates[node_id]) + "\n")

    def write_cells(self, elements: Mapping[int, Any]):
        self.element_ids = sorted(elements)
        cells = []
        for element_id in self.element_ids:
            element = elements[element_id]
            expected_count, vtk_type = self._get_vtk_cell_spec(element_id, element)
            node_ids = list(element.get("nodes", []))
            if len(node_ids) != expected_count:
                raise ValueError(
                    f"VTK cell {element_id} ({element.get('type')}) requires "
                    f"{expected_count} nodes, got {len(node_ids)}"
                )
            missing = [node_id for node_id in node_ids if node_id not in self.node_id_to_index]
            if missing:
                raise ValueError(f"VTK cell {element_id} references unknown nodes {missing}")
            cells.append(([self.node_id_to_index[node_id] for node_id in node_ids], vtk_type))

        self.cell_count = len(cells)
        total_size = sum(1 + len(indices) for indices, _ in cells)
        self.file.write(f"CELLS {self.cell_count} {total_size}\n")
        for indices, _ in cells:
            self.file.write(f"{len(indices)} {' '.join(map(str, indices))}\n")
        self.file.write(f"CELL_TYPES {self.cell_count}\n")
        for _, vtk_type in cells:
            self.file.write(f"{vtk_type}\n")

    def write_point_data(self, point_data: Mapping[str, Mapping[int, Any]]):
        self._write_associated_data("POINT_DATA", self.node_ids, point_data)

    def write_cell_data(self, cell_data: Mapping[str, Mapping[int, Any]]):
        self._write_associated_data("CELL_DATA", self.element_ids, cell_data)

    def write_footer(self):
        self.close()

    def _get_vtk_cell_spec(self, element_id: int, element: Mapping[str, Any]):
        element_type = str(element.get("type", "")).lower()
        if element_type in ("shell", "shellelement"):
            count = len(element.get("nodes", []))
            if count == 3:
                return 3, 5
            if count == 4:
                return 4, 9
            raise ValueError(f"VTK cell {element_id} (shell) requires 3 or 4 nodes, got {count}")
        try:
            return self._FIXED_CELL_TYPES[element_type]
        except KeyError as error:
            raise ValueError(
                f"VTK cell {element_id} has unsupported element type {element.get('type')!r}"
            ) from error

    def _write_associated_data(
        self,
        header: str,
        ids: Iterable[int],
        data: Mapping[str, Mapping[int, Any]],
    ):
        ids = list(ids)
        datasets = [(name, values) for name, values in data.items() if values]
        if not datasets:
            return
        self.file.write(f"{header} {len(ids)}\n")
        for name, values in datasets:
            ordered = [self._required_value(values, item_id, name) for item_id in ids]
            arrays = [np.asarray(value) for value in ordered]
            shapes = {array.shape for array in arrays}
            if shapes <= {(), (1,)}:
                integer = all(
                    isinstance(value, (int, np.integer)) and not isinstance(value, (bool, np.bool_))
                    for value in ordered
                )
                dtype = "int" if integer else "double"
                self.file.write(f"SCALARS {name} {dtype} 1\n")
                self.file.write("LOOKUP_TABLE default\n")
                for value in ordered:
                    scalar = np.asarray(value).reshape(-1)[0]
                    self.file.write(f"{int(scalar) if integer else self._format_float(scalar)}\n")
            elif shapes == {(3,)}:
                self.file.write(f"VECTORS {name} double\n")
                for value in arrays:
                    self._check_numeric(value, name)
                    self.file.write(" ".join(self._format_float(component) for component in value) + "\n")
            elif shapes == {(3, 3)}:
                self.file.write(f"TENSORS {name} double\n")
                for value in arrays:
                    self._check_numeric(value, name)
                    for row in value:
                        self.file.write(" ".join(self._format_float(component) for component in row) + "\n")
            else:
                raise ValueError(f"VTK data {name!r} must contain scalar, 3-vector, or 3x3 tensor values")

    @staticmethod
    def _required_value(values: Mapping[int, Any], item_id: int, name: str):
        if item_id in values:
            return values[item_id]
        if str(item_id) in values:
            return values[str(item_id)]
        raise ValueError(f"VTK data {name!r} is missing ID {item_id}")

    @staticmethod
    def _check_numeric(value: np.ndarray, name: str):
        if not np.issubdtype(value.dtype, np.number) or np.isinf(value.astype(float)).any():
            raise ValueError(f"VTK data {name!r} must be numeric and cannot contain infinity")

    @staticmethod
    def _format_float(value: Any) -> str:
        number = float(value)
        if np.isinf(number):
            raise ValueError("VTK data cannot contain infinity")
        return "nan" if np.isnan(number) else format(number, ".17g")
