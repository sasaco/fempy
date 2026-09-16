"""
メッシュデータを管理するモジュール
JavaScript版のMeshModel機能に対応
"""
from typing import List, Dict, Optional, Tuple
import numpy as np


class MeshModel:
    """メッシュデータを管理するクラス"""
    
    def __init__(self):
        self.nodes: Dict[int, np.ndarray] = {}  # ノード座標 {node_id: [x, y, z]}
        self.elements: Dict[int, Dict] = {}  # 要素情報 {elem_id: {type, nodes, material_id, ...}}
        self.node_count = 0
        self.element_count = 0
        
    def add_node(self, node_id: int, coordinates: List[float]) -> None:
        """ノードを追加
        
        Args:
            node_id: ノードID
            coordinates: 座標 [x, y, z]
        """
        self.nodes[node_id] = np.array(coordinates)
        self.node_count = len(self.nodes)
        
    def add_element(self, elem_id: int, elem_type: str, node_ids: List[int], 
                   material_id: int, **kwargs) -> None:
        """要素を追加
        
        Args:
            elem_id: 要素ID
            elem_type: 要素タイプ ('bar', 'shell', 'solid')
            node_ids: 構成ノードIDリスト
            material_id: 材料ID
            **kwargs: その他の要素パラメータ
        """
        self.elements[elem_id] = {
            'type': elem_type,
            'nodes': node_ids,
            'material_id': material_id,
            **kwargs
        }
        self.element_count = len(self.elements)
        
    def get_node(self, node_id: int) -> Optional[np.ndarray]:
        """ノード座標を取得"""
        return self.nodes.get(node_id)
        
    def get_element(self, elem_id: int) -> Optional[Dict]:
        """要素情報を取得"""
        return self.elements.get(elem_id)
        
    def get_free_nodes(self) -> List[int]:
        """自由ノード（要素に属さないノード）のリストを取得"""
        used_nodes = set()
        for elem in self.elements.values():
            used_nodes.update(elem['nodes'])
        return [node_id for node_id in self.nodes.keys() if node_id not in used_nodes]
        
    def check_chirality(self, elem_id: int) -> bool:
        """要素の向き（カイラリティ）をチェック
        
        Args:
            elem_id: 要素ID
            
        Returns:
            正しい向きの場合True
        """
        elem = self.elements.get(elem_id)
        if not elem or elem['type'] != 'shell':
            return True
            
        # シェル要素の法線ベクトル計算
        nodes = [self.nodes[node_id] for node_id in elem['nodes'][:3]]
        v1 = nodes[1] - nodes[0]
        v2 = nodes[2] - nodes[0]
        normal = np.cross(v1, v2)
        
        # 正の向きかどうかをチェック（仮実装）
        return normal[2] > 0
        
    def get_face_edges(self, face_nodes: List[int]) -> List[Tuple[int, int]]:
        """面を構成するエッジのリストを取得
        
        Args:
            face_nodes: 面を構成するノードIDリスト
            
        Returns:
            エッジのリスト [(node1, node2), ...]
        """
        edges = []
        n = len(face_nodes)
        for i in range(n):
            edges.append((face_nodes[i], face_nodes[(i + 1) % n]))
        return edges
        
    def get_bounding_box(self) -> Tuple[np.ndarray, np.ndarray]:
        """メッシュの境界ボックスを取得
        
        Returns:
            (最小座標, 最大座標)
        """
        if not self.nodes:
            return np.zeros(3), np.zeros(3)
            
        coords = np.array(list(self.nodes.values()))
        return coords.min(axis=0), coords.max(axis=0) 