"""
高度な要素クラス
JavaScript版のAdvancedElement.jsに対応
"""
from typing import Dict, List, Tuple, Optional, Any
import numpy as np
from .base_element import BaseElement
from .solid_element import TetraElement, HexaElement


class WedgeElement(BaseElement):
    """6節点ウェッジ（三角柱）要素クラス"""
    
    def __init__(self, element_id: int, node_ids: List[int], material_id: int):
        """
        Args:
            element_id: 要素ID
            node_ids: 構成節点ID（6節点）
            material_id: 材料ID
        """
        if len(node_ids) != 6:
            raise ValueError("Wedge element must have exactly 6 nodes")
            
        super().__init__(element_id, node_ids, material_id)
        self.material = None
        
    def get_name(self) -> str:
        """要素タイプ名を取得"""
        return "wedge"
        
    def get_dof_per_node(self) -> int:
        """節点あたりの自由度数を取得"""
        return 3  # 3並進のみ
        
    def set_material_properties(self, material) -> None:
        """材料特性を設定"""
        self.material = material
        
    def get_shape_functions(self, xi: np.ndarray) -> np.ndarray:
        """形状関数を取得（6節点ウェッジ）
        
        Args:
            xi: 自然座標 [xi, eta, zeta]
            
        Returns:
            形状関数の値 (6,)
        """
        xi_val, eta_val, zeta_val = xi[0], xi[1], xi[2]
        
        # 三角形の面内形状関数
        L1 = 1 - xi_val - eta_val
        L2 = xi_val
        L3 = eta_val
        
        # z方向の線形補間
        N = np.array([
            L1 * (1 - zeta_val) / 2,
            L2 * (1 - zeta_val) / 2,
            L3 * (1 - zeta_val) / 2,
            L1 * (1 + zeta_val) / 2,
            L2 * (1 + zeta_val) / 2,
            L3 * (1 + zeta_val) / 2
        ])
        
        return N
        
    def get_shape_derivatives(self, xi: np.ndarray) -> np.ndarray:
        """形状関数の微分を取得
        
        Args:
            xi: 自然座標 [xi, eta, zeta]
            
        Returns:
            形状関数の微分値 (3, 6)
        """
        xi_val, eta_val, zeta_val = xi[0], xi[1], xi[2]
        
        dN_dxi = np.array([
            # ∂N/∂xi
            [-(1 - zeta_val) / 2, (1 - zeta_val) / 2, 0,
             -(1 + zeta_val) / 2, (1 + zeta_val) / 2, 0],
            # ∂N/∂eta
            [-(1 - zeta_val) / 2, 0, (1 - zeta_val) / 2,
             -(1 + zeta_val) / 2, 0, (1 + zeta_val) / 2],
            # ∂N/∂zeta
            [-(1 - xi_val - eta_val) / 2, -xi_val / 2, -eta_val / 2,
              (1 - xi_val - eta_val) / 2,  xi_val / 2,  eta_val / 2]
        ])
        
        return dN_dxi
        
    def get_gauss_points(self) -> Tuple[np.ndarray, np.ndarray]:
        """ウェッジ要素用ガウス積分点と重みを取得
        
        三角形面（2点）× z方向（2点）= 4点の積分
        
        Returns:
            (積分点座標, 重み)
        """
        # 三角形面の積分点（C1_3 = 1/3）
        tri_xi = 1.0 / 3.0
        tri_eta = 1.0 / 3.0
        
        # z方向の積分点（GX2 = [-1/√3, 1/√3]）
        z_gp = 1.0 / np.sqrt(3)
        
        xi = []
        w = []
        
        # V0のWEDGE1_INTパターン: [[C1_3,C1_3,GX2[0],0.5],[C1_3,C1_3,GX2[1],0.5]]
        for z in [-z_gp, z_gp]:
            xi.append([tri_xi, tri_eta, z])
            w.append(0.5)  # 三角形面積(0.5) × z方向重み(1.0)
            
        return np.array(xi), np.array(w)
        
    def get_stiffness_matrix(self) -> np.ndarray:
        """ウェッジ要素の剛性行列を取得"""
        if self.material is None:
            raise ValueError("Material properties not set")
            
        coords = self.get_element_coordinates()
        D = self.material.get_elastic_matrix_3d(self.material_id)
        
        # 18x18行列（6節点×3自由度）
        Ke = np.zeros((18, 18))
        
        # ガウス積分
        xi_gp, w_gp = self.get_gauss_points()
        
        for i, (xi, w) in enumerate(zip(xi_gp, w_gp)):
            # 形状関数の微分
            dN_dxi = self.get_shape_derivatives(xi)
            
            # ヤコビアン
            J = dN_dxi @ coords
            det_J = np.linalg.det(J)
            
            if det_J <= 0:
                raise ValueError("Negative Jacobian determinant")
                
            J_inv = np.linalg.inv(J)
            
            # グローバル座標での形状関数微分
            dN_dx = J_inv @ dN_dxi
            
            # Bマトリックス（ひずみ-変位）
            B = np.zeros((6, 18))
            for j in range(6):
                B[0, j*3] = dN_dx[0, j]      # ∂u/∂x
                B[1, j*3+1] = dN_dx[1, j]    # ∂v/∂y
                B[2, j*3+2] = dN_dx[2, j]    # ∂w/∂z
                B[3, j*3] = dN_dx[1, j]      # ∂u/∂y
                B[3, j*3+1] = dN_dx[0, j]    # ∂v/∂x
                B[4, j*3+1] = dN_dx[2, j]    # ∂v/∂z
                B[4, j*3+2] = dN_dx[1, j]    # ∂w/∂y
                B[5, j*3] = dN_dx[2, j]      # ∂u/∂z
                B[5, j*3+2] = dN_dx[0, j]    # ∂w/∂x
                
            # 剛性行列への寄与
            Ke += B.T @ D @ B * det_J * w
            
        return Ke
        
    def get_mass_matrix(self) -> np.ndarray:
        """ウェッジ要素の質量行列を取得"""
        raise NotImplementedError("Wedge element mass matrix not implemented")


class PyramidElement(BaseElement):
    """5節点ピラミッド要素クラス"""
    
    def __init__(self, element_id: int, node_ids: List[int], material_id: int):
        """
        Args:
            element_id: 要素ID
            node_ids: 構成節点ID（5節点）
            material_id: 材料ID
        """
        if len(node_ids) != 5:
            raise ValueError("Pyramid element must have exactly 5 nodes")
            
        super().__init__(element_id, node_ids, material_id)
        self.material = None
        
    def get_name(self) -> str:
        """要素タイプ名を取得"""
        return "pyramid"
        
    def get_dof_per_node(self) -> int:
        """節点あたりの自由度数を取得"""
        return 3  # 3並進のみ
        
    def get_shape_functions(self, xi: np.ndarray) -> np.ndarray:
        """形状関数を取得（5節点ピラミッド）
        
        Args:
            xi: 自然座標 [xi, eta, zeta]
            
        Returns:
            形状関数の値 (5,)
        """
        xi_val, eta_val, zeta_val = xi[0], xi[1], xi[2]
        
        # 特殊な形状関数（ピラミッド用）
        if abs(1 - zeta_val) < 1e-10:  # 頂点での特異性を回避
            N = np.array([0, 0, 0, 0, 1])
        else:
            r = xi_val / (1 - zeta_val)
            s = eta_val / (1 - zeta_val)
            N = np.array([
                0.25 * (1 - r) * (1 - s) * (1 - zeta_val),
                0.25 * (1 + r) * (1 - s) * (1 - zeta_val),
                0.25 * (1 + r) * (1 + s) * (1 - zeta_val),
                0.25 * (1 - r) * (1 + s) * (1 - zeta_val),
                zeta_val
            ])
            
        return N
        
    def get_shape_derivatives(self, xi: np.ndarray) -> np.ndarray:
        """形状関数の微分を取得"""
        raise NotImplementedError("Pyramid element shape derivatives not implemented")
        
    def get_stiffness_matrix(self) -> np.ndarray:
        """ピラミッド要素の剛性行列を取得"""
        raise NotImplementedError("Pyramid element stiffness matrix not implemented")
        
    def get_mass_matrix(self) -> np.ndarray:
        """ピラミッド要素の質量行列を取得"""
        raise NotImplementedError("Pyramid element mass matrix not implemented")


class Hexa20Element(BaseElement):
    """20節点六面体要素クラス（2次要素）"""
    
    def __init__(self, element_id: int, node_ids: List[int], material_id: int):
        """
        Args:
            element_id: 要素ID
            node_ids: 構成節点ID（20節点）
            material_id: 材料ID
        """
        if len(node_ids) != 20:
            raise ValueError("20-node hexahedron element must have exactly 20 nodes")
            
        super().__init__(element_id, node_ids, material_id)
        self.material = None
        
    def get_name(self) -> str:
        """要素タイプ名を取得"""
        return "hexa20"
        
    def get_dof_per_node(self) -> int:
        """節点あたりの自由度数を取得"""
        return 3  # 3並進のみ
        
    def get_shape_functions(self, xi: np.ndarray) -> np.ndarray:
        """形状関数を取得（20節点六面体、2次要素）"""
        xi_val, eta_val, zeta_val = xi[0], xi[1], xi[2]
        
        # 頂点節点（8個）
        N_corner = []
        for i in [-1, 1]:
            for j in [-1, 1]:
                for k in [-1, 1]:
                    N_corner.append(
                        0.125 * (1 + i*xi_val) * (1 + j*eta_val) * (1 + k*zeta_val) *
                        (i*xi_val + j*eta_val + k*zeta_val - 2)
                    )
                    
        # 中間節点（12個）
        N_mid = []
        # xi = 0の面上
        for j in [-1, 1]:
            for k in [-1, 1]:
                N_mid.append(
                    0.25 * (1 - xi_val**2) * (1 + j*eta_val) * (1 + k*zeta_val)
                )
        # eta = 0の面上
        for i in [-1, 1]:
            for k in [-1, 1]:
                N_mid.append(
                    0.25 * (1 + i*xi_val) * (1 - eta_val**2) * (1 + k*zeta_val)
                )
        # zeta = 0の面上
        for i in [-1, 1]:
            for j in [-1, 1]:
                N_mid.append(
                    0.25 * (1 + i*xi_val) * (1 + j*eta_val) * (1 - zeta_val**2)
                )
                
        N = np.concatenate([N_corner, N_mid])
        return N
        
    def get_shape_derivatives(self, xi: np.ndarray) -> np.ndarray:
        """形状関数の微分を取得（20節点）"""
        raise NotImplementedError("20-node hexahedron shape derivatives not implemented")
        
    def get_stiffness_matrix(self) -> np.ndarray:
        """20節点六面体要素の剛性行列を取得"""
        raise NotImplementedError("20-node hexahedron stiffness matrix not implemented")
        
    def get_mass_matrix(self) -> np.ndarray:
        """20節点六面体要素の質量行列を取得"""
        raise NotImplementedError("20-node hexahedron mass matrix not implemented")


class AdvancedElement:
    """高度な要素タイプのファクトリクラス"""
    
    @staticmethod
    def create_element(element_type: str, element_id: int, node_ids: List[int], 
                      material_id: int) -> BaseElement:
        """要素タイプに応じた要素インスタンスを作成
        
        Args:
            element_type: 要素タイプ名
            element_id: 要素ID
            node_ids: 構成節点ID
            material_id: 材料ID
            
        Returns:
            要素インスタンス
        """
        element_types = {
            'wedge': WedgeElement,
            'pyramid': PyramidElement,
            'hexa20': Hexa20Element
        }
        
        if element_type not in element_types:
            raise ValueError(f"Unknown advanced element type: {element_type}")
            
        return element_types[element_type](element_id, node_ids, material_id)
        
    @staticmethod
    def is_advanced_element(element_type: str) -> bool:
        """高度な要素タイプかどうかを判定
        
        Args:
            element_type: 要素タイプ名
            
        Returns:
            高度な要素タイプの場合True
        """
        advanced_types = ['wedge', 'pyramid', 'hexa20']
        return element_type in advanced_types 