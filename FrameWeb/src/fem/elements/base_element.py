"""
有限要素の基底クラス
JavaScript版のElement.jsに対応
"""
from typing import Dict, List, Tuple, Optional, Any
import numpy as np


class BaseElement:
    """すべての要素クラスの基底クラス"""
    
    def __init__(self, element_id: int, node_ids: List[int], material_id: int):
        """
        Args:
            element_id: 要素ID
            node_ids: 構成節点IDのリスト
            material_id: 材料ID
        """
        if element_id < 0:
            raise ValueError("Element ID must be non-negative")
        if not node_ids:
            raise ValueError("Node IDs list cannot be empty")
        if material_id < 0:
            raise ValueError("Material ID must be non-negative")
            
        self.element_id = element_id
        self.node_ids = node_ids
        self.material_id = material_id
        self.nodes_coordinates: Optional[np.ndarray] = None
        
    def get_stiffness_matrix(self) -> np.ndarray:
        """要素剛性行列を取得
        
        Returns:
            要素剛性行列
        """
        raise NotImplementedError("Stiffness matrix not implemented for base class")
        
    def get_mass_matrix(self) -> np.ndarray:
        """要素質量行列を取得
        
        Returns:
            要素質量行列
        """
        raise NotImplementedError("Mass matrix not implemented for base class")
        
    def get_name(self) -> str:
        """要素タイプ名を取得
        
        Returns:
            要素タイプ名
        """
        raise NotImplementedError("Element name not implemented for base class")
        
    def get_dof_per_node(self) -> int:
        """節点あたりの自由度数を取得
        
        Returns:
            節点あたりの自由度数
        """
        raise NotImplementedError("Degrees of freedom per node not implemented for base class")
        
    def set_node_coordinates(self, coordinates: Dict[int, np.ndarray]) -> None:
        """節点座標を設定
        
        Args:
            coordinates: {node_id: [x, y, z]} の辞書
        """
        coords_list = []
        for node_id in self.node_ids:
            if node_id not in coordinates:
                raise ValueError(f"Node {node_id} coordinates not found")
            coords_list.append(coordinates[node_id])
        self.nodes_coordinates = np.array(coords_list)
        
    def get_element_coordinates(self) -> np.ndarray:
        """要素の節点座標を取得
        
        Returns:
            節点座標の配列 (n_nodes x 3)
        """
        if self.nodes_coordinates is None:
            raise ValueError("Node coordinates not set")
        return self.nodes_coordinates
        
    def get_element_length(self) -> float:
        """要素の長さを取得（梁要素用）
        
        Returns:
            要素長さ
        """
        if len(self.node_ids) != 2:
            raise NotImplementedError("Length calculation only for 2-node elements")
            
        coords = self.get_element_coordinates()
        return np.linalg.norm(coords[1] - coords[0])
        
    def get_element_volume(self) -> float:
        """要素の体積を取得
        
        Returns:
            要素体積
        """
        raise NotImplementedError("Volume calculation not implemented for base class")
        
    def get_shape_functions(self, xi: np.ndarray) -> np.ndarray:
        """形状関数を取得
        
        Args:
            xi: 自然座標 (要素に応じて1D, 2D, or 3D)
            
        Returns:
            形状関数の値
        """
        raise NotImplementedError("Shape functions not implemented for base class")
        
    def get_shape_derivatives(self, xi: np.ndarray) -> np.ndarray:
        """形状関数の微分を取得
        
        Args:
            xi: 自然座標
            
        Returns:
            形状関数の微分値
        """
        raise NotImplementedError("Shape derivatives not implemented for base class")
        
    def get_jacobian(self, xi: np.ndarray) -> float:
        """ヤコビアン行列の行列式を取得
        
        Args:
            xi: 自然座標
            
        Returns:
            ヤコビアン行列の行列式
        """
        # 2節点要素（1D）の場合、ヤコビアンは要素長さの半分
        if len(self.node_ids) == 2:
            return self.get_element_length() / 2.0
        # 形状関数の微分を取得
        dN_dxi = self.get_shape_derivatives(xi)
        
        # 節点座標を取得
        coords = self.get_element_coordinates()
        
        # ヤコビアン行列を計算
        J = dN_dxi @ coords
        
        # 行列式を計算
        det_J = np.linalg.det(J)
        
        return det_J
        
    def get_strain_displacement_matrix(self, xi: np.ndarray) -> np.ndarray:
        """ひずみ-変位マトリックス（Bマトリックス）を取得
        
        Args:
            xi: 自然座標
            
        Returns:
            Bマトリックス
        """
        raise NotImplementedError("B-matrix not implemented for base class")
        
    def get_stress_strain_matrix(self) -> np.ndarray:
        """応力-ひずみマトリックス（Dマトリックス）を取得
        
        Returns:
            Dマトリックス
        """
        raise NotImplementedError("D-matrix not implemented for base class")
        
    def get_equivalent_nodal_loads(self, load_type: str, values: List[float], 
                                 face: Optional[int] = None) -> np.ndarray:
        """分布荷重の等価節点荷重を計算
        
        Args:
            load_type: 荷重タイプ
            values: 荷重値
            face: 面番号（シェル/ソリッド要素の場合）
            
        Returns:
            等価節点荷重ベクトル
        """
        raise NotImplementedError("Equivalent nodal loads not implemented for base class")
        
    def calculate_stress_strain(self, displacement: np.ndarray) -> Dict[str, Any]:
        """応力とひずみを計算
        
        Args:
            displacement: 節点変位ベクトル
            
        Returns:
            応力・ひずみの辞書
        """
        raise NotImplementedError("Stress-strain calculation not implemented for base class")
        
    def get_gauss_points(self) -> Tuple[np.ndarray, np.ndarray]:
        """ガウス積分点と重みを取得
        
        Returns:
            (積分点座標, 重み)
        """
        raise NotImplementedError("Gauss points not implemented for base class")
        
    @staticmethod
    def get_gauss_points_1d(n_points: int) -> Tuple[np.ndarray, np.ndarray]:
        """1次元ガウス積分点と重みを取得
        
        Args:
            n_points: 積分点数
            
        Returns:
            (積分点座標, 重み)
        """
        if n_points == 1:
            xi = np.array([0.0])
            w = np.array([2.0])
        elif n_points == 2:
            xi = np.array([-1/np.sqrt(3), 1/np.sqrt(3)])
            w = np.array([1.0, 1.0])
        elif n_points == 3:
            xi = np.array([-np.sqrt(3/5), 0.0, np.sqrt(3/5)])
            w = np.array([5/9, 8/9, 5/9])
        elif n_points == 4:
            xi1 = np.sqrt((3 - 2*np.sqrt(6/5))/7)
            xi2 = np.sqrt((3 + 2*np.sqrt(6/5))/7)
            xi = np.array([-xi2, -xi1, xi1, xi2])
            w1 = (18 + np.sqrt(30))/36
            w2 = (18 - np.sqrt(30))/36
            w = np.array([w2, w1, w1, w2])
        else:
            raise ValueError(f"Gauss points for {n_points} points not implemented")
            
        return xi, w 