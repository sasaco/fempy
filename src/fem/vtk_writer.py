"""
VTKファイル出力モジュール
"""
import numpy as np
from typing import Dict, Any

class VTKWriter:
    def __init__(self, file_path: str):
        # 出力ファイルを開く
        self.file = open(file_path, 'w', encoding='utf-8')
        self.node_ids = []
        self.node_id_to_index: Dict[int, int] = {}
        self.n_points = 0
        self.cell_count = 0

    def write_header(self):
        # VTKファイルヘッダ
        self.file.write("# vtk DataFile Version 3.0\n")
        self.file.write("FrameWeb3 VTK Output\n")
        self.file.write("ASCII\n")
        self.file.write("DATASET UNSTRUCTURED_GRID\n")

    def write_points(self, nodes: Dict[int, np.ndarray]):
        # ノード座標の書き込み
        self.node_ids = sorted(nodes.keys())
        self.n_points = len(self.node_ids)
        self.node_id_to_index = {nid: idx for idx, nid in enumerate(self.node_ids)}
        self.file.write(f"POINTS {self.n_points} float\n")
        for nid in self.node_ids:
            coords = nodes[nid]
            x, y, z = float(coords[0]), float(coords[1]), float(coords[2])
            self.file.write(f"{x} {y} {z}\n")

    def write_cells(self, elements: Dict[int, Any]):
        # 要素の書き込み
        self.cell_count = len(elements)
        # 各セル行: (ノード数 + ノードインデックス...)
        total_size = sum(1 + len(elem['nodes']) for elem in elements.values())
        self.file.write(f"CELLS {self.cell_count} {total_size}\n")
        for elem_id, elem in sorted(elements.items()):
            indices = [self.node_id_to_index[nid] for nid in elem['nodes']]
            count = len(indices)
            idx_str = ' '.join(str(i) for i in indices)
            self.file.write(f"{count} {idx_str}\n")
        # セルタイプ
        self.file.write(f"CELL_TYPES {self.cell_count}\n")
        for elem_id, elem in sorted(elements.items()):
            vtk_type = self._get_vtk_cell_type(elem)
            self.file.write(f"{vtk_type}\n")

    def write_point_data(self, point_data: Dict[str, Dict[int, Any]]):
        # 節点データの書き込み
        if not point_data:
            return
        self.file.write(f"POINT_DATA {self.n_points}\n")
        for name, values in point_data.items():
            first = next(iter(values.values())) if values else None
            if isinstance(first, dict):
                # Check for tensor components to output as TENSORS
                tensor_keys = {'xx', 'yy', 'zz', 'xy', 'yz', 'zx'}
                if tensor_keys.issubset(first.keys()):
                    self.file.write(f"TENSORS {name} float\n")
                    for nid in self.node_ids:
                        comp = values.get(nid, {})
                        xx = float(comp.get('xx', 0.0))
                        xy = float(comp.get('xy', 0.0))
                        xz = float(comp.get('xz', comp.get('zx', 0.0)))
                        yx = float(comp.get('yx', comp.get('xy', 0.0)))
                        yy = float(comp.get('yy', 0.0))
                        yz = float(comp.get('yz', 0.0))
                        zx = float(comp.get('zx', comp.get('xz', 0.0)))
                        zy = float(comp.get('zy', comp.get('yz', 0.0)))
                        zz = float(comp.get('zz', 0.0))
                        self.file.write(f"{xx} {xy} {xz} {yx} {yy} {yz} {zx} {zy} {zz}\n")
                else:
                    # Vector data
                    self.file.write(f"VECTORS {name} float\n")
                    for nid in self.node_ids:
                        comp = values.get(nid, {})
                        x = float(comp.get('dx', 0.0))
                        y = float(comp.get('dy', 0.0))
                        z = float(comp.get('dz', 0.0))
                        self.file.write(f"{x} {y} {z}\n")
            else:
                # スカラー
                self.file.write(f"SCALARS {name} float 1\n")
                self.file.write("LOOKUP_TABLE default\n")
                for nid in self.node_ids:
                    val = float(values.get(nid, 0.0))
                    self.file.write(f"{val}\n")

    def write_cell_data(self, cell_data: Dict[str, Dict[int, Any]]):
        # セルデータの書き込み
        if not cell_data:
            return
        self.file.write(f"CELL_DATA {self.cell_count}\n")
        for name, values in cell_data.items():
            first = next(iter(values.values())) if values else None
            if isinstance(first, dict):
                # Check for tensor components to output as TENSORS
                tensor_keys = {'xx', 'yy', 'zz', 'xy', 'yz', 'zx'}
                if tensor_keys.issubset(first.keys()):
                    self.file.write(f"TENSORS {name} float\n")
                    for elem_id in sorted(values.keys()):
                        comp = values.get(elem_id, {})
                        xx = float(comp.get('xx', 0.0))
                        xy = float(comp.get('xy', 0.0))
                        xz = float(comp.get('xz', comp.get('zx', 0.0)))
                        yx = float(comp.get('yx', comp.get('xy', 0.0)))
                        yy = float(comp.get('yy', 0.0))
                        yz = float(comp.get('yz', 0.0))
                        zx = float(comp.get('zx', comp.get('xz', 0.0)))
                        zy = float(comp.get('zy', comp.get('yz', 0.0)))
                        zz = float(comp.get('zz', 0.0))
                        self.file.write(f"{xx} {xy} {xz} {yx} {yy} {yz} {zx} {zy} {zz}\n")
                else:
                    # Output as separate scalars per component
                    for comp in first:
                        field_name = f"{name}_{comp}"
                        self.file.write(f"SCALARS {field_name} float 1\n")
                        self.file.write("LOOKUP_TABLE default\n")
                        for elem_id in sorted(values.keys()):
                            comp_dict = values.get(elem_id, {})
                            val = float(comp_dict.get(comp, 0.0))
                            self.file.write(f"{val}\n")
            else:
                # 単一スカラー
                self.file.write(f"SCALARS {name} float 1\n")
                self.file.write("LOOKUP_TABLE default\n")
                for elem_id in sorted(values.keys()):
                    val = float(values.get(elem_id, 0.0))
                    self.file.write(f"{val}\n")

    def write_footer(self):
        # ファイルを閉じる
        self.file.close()

    def _get_vtk_cell_type(self, elem: Dict[str, Any]) -> int:
        # 要素タイプ文字列からVTKセルタイプを返す
        et = elem.get('type', '').lower()
        if et in ('bar', 'beam', 'line'):
            return 3  # VTK_LINE
        if et in ('triangle', 'trielement1'):
            return 5  # VTK_TRIANGLE
        if et in ('quadrilateral', 'quadelement1', 'quad'):
            return 9  # VTK_QUAD
        if et in ('tetra', 'tetraelement'):
            return 10  # VTK_TETRA
        if et in ('hexa', 'hexahedron'):
            return 12  # VTK_HEXAHEDRON
        if et in ('wedge', 'wedgeelement'):
            return 13  # VTK_WEDGE
        # 未対応の場合は0として返却
        return 0 