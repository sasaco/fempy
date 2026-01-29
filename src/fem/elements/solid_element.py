"""
ソリッド要素クラス
JavaScript版のSolidElement.jsに対応
"""
from typing import Dict, List, Tuple, Optional, Any
import numpy as np
from .base_element import BaseElement
from ..material import Material


class SolidElementBase(BaseElement):
    """ソリッド要素の基底クラス"""
    
    def get_dof_per_node(self) -> int:
        """節点あたりの自由度数を取得"""
        return 3  # 3並進のみ（回転自由度なし）
        
    def get_stress_strain_matrix(self) -> np.ndarray:
        """3D応力-ひずみマトリックスを取得"""
        if self.material is None:
            raise ValueError("Material properties not set")
            
        return self.material.get_elastic_matrix_3d(self.material_id)


class TetraElement(SolidElementBase):
    """4節点四面体要素クラス"""
    
    def __init__(self, element_id: int, node_ids: List[int], material_id: int):
        """
        Args:
            element_id: 要素ID
            node_ids: 構成節点ID（4節点）
            material_id: 材料ID
        """
        if len(node_ids) != 4:
            raise ValueError("Tetrahedron element must have exactly 4 nodes")
            
        super().__init__(element_id, node_ids, material_id)
        self.material: Optional[Material] = None
        
    def get_name(self) -> str:
        """要素タイプ名を取得"""
        return "tetra"
        
    def set_material_properties(self, material: Material) -> None:
        """材料特性を設定"""
        self.material = material
        
    def get_shape_functions(self, xi: np.ndarray) -> np.ndarray:
        """形状関数を取得（4節点四面体）
        
        Args:
            xi: 自然座標 [xi, eta, zeta]
            
        Returns:
            形状関数の値 (4,)
        """
        xi_val, eta_val, zeta_val = xi[0], xi[1], xi[2]
        
        N = np.array([
            1 - xi_val - eta_val - zeta_val,
            xi_val,
            eta_val,
            zeta_val
        ])
        
        return N
        
    def get_shape_derivatives(self, xi: np.ndarray) -> np.ndarray:
        """形状関数の微分を取得（定数）
        
        Args:
            xi: 自然座標（四面体では使用しない）
            
        Returns:
            形状関数の微分値 (3, 4)
        """
        # 線形要素なので微分は定数
        dN_dxi = np.array([
            [-1, 1, 0, 0],
            [-1, 0, 1, 0],
            [-1, 0, 0, 1]
        ])
        
        return dN_dxi
        
    def get_volume(self) -> float:
        """四面体の体積を計算"""
        coords = self.get_element_coordinates()
        
        # 頂点座標から体積を計算
        v1 = coords[1] - coords[0]
        v2 = coords[2] - coords[0]
        v3 = coords[3] - coords[0]
        
        volume = abs(np.dot(v1, np.cross(v2, v3))) / 6.0
        return volume
        
    def get_stiffness_matrix(self) -> np.ndarray:
        """四面体要素の剛性行列を取得"""
        if self.material is None:
            raise ValueError("Material properties not set")
            
        coords = self.get_element_coordinates()
        D = self.get_stress_strain_matrix()
        
        # ヤコビアン行列（定数）
        dN_dxi = self.get_shape_derivatives(np.zeros(3))
        J = dN_dxi @ coords
        det_J = np.linalg.det(J)
        
        if det_J <= 0:
            raise ValueError("Negative Jacobian determinant")
            
        J_inv = np.linalg.inv(J)
        
        # Bマトリックス（ひずみ-変位）
        dN_dx = J_inv @ dN_dxi
        B = np.zeros((6, 12))
        
        for i in range(4):
            B[0, i*3] = dN_dx[0, i]      # ∂u/∂x
            B[1, i*3+1] = dN_dx[1, i]    # ∂v/∂y
            B[2, i*3+2] = dN_dx[2, i]    # ∂w/∂z
            B[3, i*3] = dN_dx[1, i]      # ∂u/∂y
            B[3, i*3+1] = dN_dx[0, i]    # ∂v/∂x
            B[4, i*3+1] = dN_dx[2, i]    # ∂v/∂z
            B[4, i*3+2] = dN_dx[1, i]    # ∂w/∂y
            B[5, i*3] = dN_dx[2, i]      # ∂u/∂z
            B[5, i*3+2] = dN_dx[0, i]    # ∂w/∂x
            
        # 剛性行列
        volume = self.get_volume()
        Ke = volume * B.T @ D @ B
        
        return Ke
        
    def get_mass_matrix(self) -> np.ndarray:
        """四面体要素の質量行列を取得"""
        if self.material is None:
            raise ValueError("Material properties not set")
            
        rho = self.material.materials[self.material_id].density
        volume = self.get_volume()
        
        # 集中質量行列
        mass_per_node = rho * volume / 4
        Me = np.zeros((12, 12))
        
        for i in range(12):
            Me[i, i] = mass_per_node
            
        return Me


class HexaElement(SolidElementBase):
    """8節点六面体要素クラス"""
    
    def __init__(self, element_id: int, node_ids: List[int], material_id: int):
        """
        Args:
            element_id: 要素ID
            node_ids: 構成節点ID（8節点）
            material_id: 材料ID
        """
        if len(node_ids) != 8:
            raise ValueError("Hexahedron element must have exactly 8 nodes")
            
        super().__init__(element_id, node_ids, material_id)
        self.material: Optional[Material] = None
        
    def get_name(self) -> str:
        """要素タイプ名を取得"""
        return "hexa"
        
    def set_material_properties(self, material: Material) -> None:
        """材料特性を設定"""
        self.material = material
        
    def get_shape_functions(self, xi: np.ndarray) -> np.ndarray:
        """形状関数を取得（8節点六面体）
        
        Args:
            xi: 自然座標 [xi, eta, zeta]
            
        Returns:
            形状関数の値 (8,)
        """
        xi_val, eta_val, zeta_val = xi[0], xi[1], xi[2]
        
        N = np.array([
            0.125 * (1 - xi_val) * (1 - eta_val) * (1 - zeta_val),
            0.125 * (1 + xi_val) * (1 - eta_val) * (1 - zeta_val),
            0.125 * (1 + xi_val) * (1 + eta_val) * (1 - zeta_val),
            0.125 * (1 - xi_val) * (1 + eta_val) * (1 - zeta_val),
            0.125 * (1 - xi_val) * (1 - eta_val) * (1 + zeta_val),
            0.125 * (1 + xi_val) * (1 - eta_val) * (1 + zeta_val),
            0.125 * (1 + xi_val) * (1 + eta_val) * (1 + zeta_val),
            0.125 * (1 - xi_val) * (1 + eta_val) * (1 + zeta_val)
        ])
        
        return N
        
    def get_shape_derivatives(self, xi: np.ndarray) -> np.ndarray:
        """形状関数の微分を取得
        
        Args:
            xi: 自然座標 [xi, eta, zeta]
            
        Returns:
            形状関数の微分値 (3, 8)
        """
        xi_val, eta_val, zeta_val = xi[0], xi[1], xi[2]
        
        dN_dxi = np.array([
            # ∂N/∂xi
            [-0.125 * (1 - eta_val) * (1 - zeta_val),
              0.125 * (1 - eta_val) * (1 - zeta_val),
              0.125 * (1 + eta_val) * (1 - zeta_val),
             -0.125 * (1 + eta_val) * (1 - zeta_val),
             -0.125 * (1 - eta_val) * (1 + zeta_val),
              0.125 * (1 - eta_val) * (1 + zeta_val),
              0.125 * (1 + eta_val) * (1 + zeta_val),
             -0.125 * (1 + eta_val) * (1 + zeta_val)],
            # ∂N/∂eta
            [-0.125 * (1 - xi_val) * (1 - zeta_val),
             -0.125 * (1 + xi_val) * (1 - zeta_val),
              0.125 * (1 + xi_val) * (1 - zeta_val),
              0.125 * (1 - xi_val) * (1 - zeta_val),
             -0.125 * (1 - xi_val) * (1 + zeta_val),
             -0.125 * (1 + xi_val) * (1 + zeta_val),
              0.125 * (1 + xi_val) * (1 + zeta_val),
              0.125 * (1 - xi_val) * (1 + zeta_val)],
            # ∂N/∂zeta
            [-0.125 * (1 - xi_val) * (1 - eta_val),
             -0.125 * (1 + xi_val) * (1 - eta_val),
             -0.125 * (1 + xi_val) * (1 + eta_val),
             -0.125 * (1 - xi_val) * (1 + eta_val),
              0.125 * (1 - xi_val) * (1 - eta_val),
              0.125 * (1 + xi_val) * (1 - eta_val),
              0.125 * (1 + xi_val) * (1 + eta_val),
              0.125 * (1 - xi_val) * (1 + eta_val)]
        ])
        
        return dN_dxi
        
    def get_gauss_points(self) -> Tuple[np.ndarray, np.ndarray]:
        """3D ガウス積分点と重みを取得（2x2x2）
        
        Returns:
            (積分点座標, 重み)
        """
        # 2x2x2 ガウス積分
        gp_1d = 1.0 / np.sqrt(3)
        xi = []
        w = []
        
        for i in [-gp_1d, gp_1d]:
            for j in [-gp_1d, gp_1d]:
                for k in [-gp_1d, gp_1d]:
                    xi.append([i, j, k])
                    w.append(1.0)
                    
        return np.array(xi), np.array(w)
        
    def get_stiffness_matrix(self) -> np.ndarray:
        """六面体要素の剛性行列を取得"""
        if self.material is None:
            raise ValueError("Material properties not set")
            
        coords = self.get_element_coordinates()
        D = self.get_stress_strain_matrix()
        
        # 24x24行列（8節点×3自由度）
        Ke = np.zeros((24, 24))
        
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
            B = np.zeros((6, 24))
            for j in range(8):
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
        """六面体要素の質量行列を取得"""
        if self.material is None:
            raise ValueError("Material properties not set")
            
        coords = self.get_element_coordinates()
        rho = self.material.materials[self.material_id].density
        
        # 24x24行列（8節点×3自由度）
        Me = np.zeros((24, 24))
        
        # ガウス積分
        xi_gp, w_gp = self.get_gauss_points()
        
        for i, (xi, w) in enumerate(zip(xi_gp, w_gp)):
            # 形状関数
            N = self.get_shape_functions(xi)
            
            # ヤコビアン
            dN_dxi = self.get_shape_derivatives(xi)
            J = dN_dxi @ coords
            det_J = np.linalg.det(J)
            
            # 質量行列への寄与
            for j in range(8):
                for k in range(8):
                    mass_factor = rho * N[j] * N[k] * det_J * w
                    for d in range(3):
                        Me[j*3+d, k*3+d] += mass_factor
                        
        return Me
        
    def calculate_stress_strain(self, displacement: np.ndarray) -> Dict[str, Any]:
        """応力とひずみを計算
        
        Args:
            displacement: 節点変位ベクトル（24要素）
            
        Returns:
            応力・ひずみの辞書
        """
        coords = self.get_element_coordinates()
        D = self.get_stress_strain_matrix()
        
        # ガウス点での応力・ひずみ
        xi_gp, _ = self.get_gauss_points()
        stress_gp = []
        strain_gp = []
        
        for xi in xi_gp:
            # 形状関数微分
            dN_dxi = self.get_shape_derivatives(xi)
            J = dN_dxi @ coords
            J_inv = np.linalg.inv(J)
            dN_dx = J_inv @ dN_dxi
            
            # Bマトリックス
            B = np.zeros((6, 24))
            for j in range(8):
                B[0, j*3] = dN_dx[0, j]
                B[1, j*3+1] = dN_dx[1, j]
                B[2, j*3+2] = dN_dx[2, j]
                B[3, j*3] = dN_dx[1, j]
                B[3, j*3+1] = dN_dx[0, j]
                B[4, j*3+1] = dN_dx[2, j]
                B[4, j*3+2] = dN_dx[1, j]
                B[5, j*3] = dN_dx[2, j]
                B[5, j*3+2] = dN_dx[0, j]
                
            # ひずみと応力
            strain = B @ displacement
            stress = D @ strain
            
            strain_gp.append(strain)
            stress_gp.append(stress)
            
        return {
            'gauss_points': xi_gp,
            'strain': np.array(strain_gp),
            'stress': np.array(stress_gp)
        }


class WedgeElement(SolidElementBase):
    """6節点くさび要素クラス"""
    def __init__(self, element_id: int, node_ids: list, material_id: int):
        if len(node_ids) != 6:
            raise ValueError("Wedge element must have exactly 6 nodes")
        super().__init__(element_id, node_ids, material_id)
        self.material: Optional[Material] = None

    def get_name(self) -> str:
        return "wedge"

    def set_material_properties(self, material: Material) -> None:
        self.material = material

    def get_shape_functions(self, xi: np.ndarray) -> np.ndarray:
        # xi = [xi, eta, zeta]
        xsi, eta, zeta = xi[0], xi[1], xi[2]
        N = np.zeros(6)
        N[0] = 0.5 * (1 - xsi - eta) * (1 - zeta)
        N[1] = 0.5 * xsi * (1 - zeta)
        N[2] = 0.5 * eta * (1 - zeta)
        N[3] = 0.5 * (1 - xsi - eta) * (1 + zeta)
        N[4] = 0.5 * xsi * (1 + zeta)
        N[5] = 0.5 * eta * (1 + zeta)
        return N

    def get_shape_derivatives(self, xi: np.ndarray) -> np.ndarray:
        # xi = [xi, eta, zeta]
        xsi, eta, zeta = xi[0], xi[1], xi[2]
        dN = np.zeros((3, 6))
        # dN/dxsi
        dN[0, 0] = -0.5 * (1 - zeta)
        dN[0, 1] =  0.5 * (1 - zeta)
        dN[0, 2] =  0.0
        dN[0, 3] = -0.5 * (1 + zeta)
        dN[0, 4] =  0.5 * (1 + zeta)
        dN[0, 5] =  0.0
        # dN/deta
        dN[1, 0] = -0.5 * (1 - zeta)
        dN[1, 1] =  0.0
        dN[1, 2] =  0.5 * (1 - zeta)
        dN[1, 3] = -0.5 * (1 + zeta)
        dN[1, 4] =  0.0
        dN[1, 5] =  0.5 * (1 + zeta)
        # dN/dzeta
        dN[2, 0] = -0.5 * (1 - xsi - eta)
        dN[2, 1] = -0.5 * xsi
        dN[2, 2] = -0.5 * eta
        dN[2, 3] =  0.5 * (1 - xsi - eta)
        dN[2, 4] =  0.5 * xsi
        dN[2, 5] =  0.5 * eta
        return dN

    def get_gauss_points(self) -> Tuple[np.ndarray, np.ndarray]:
        """ガウス積分点と重みを取得（くさび要素）"""
        # 6点ガウス積分: 3角形内積分点 × 2点 zeta 方向
        gauss_points = [
            [1/6, 1/6, -1/3],
            [2/3, 1/6, -1/3],
            [1/6, 2/3, -1/3],
            [1/6, 1/6,  1/3],
            [2/3, 1/6,  1/3],
            [1/6, 2/3,  1/3],
        ]
        weights = [1.0] * 6
        return np.array(gauss_points), np.array(weights)

    def get_mass_matrix(self) -> np.ndarray:
        if self.material is None:
            raise ValueError("Material properties not set")
        rho = self.material.materials[self.material_id].density
        coords = self.get_element_coordinates()
        # 2点ガウス積分点（参考: 旧実装 WEDGE1_INT）
        gauss_points = [
            [1/6, 1/6, -1/3],
            [2/3, 1/6, -1/3],
            [1/6, 2/3, -1/3],
            [1/6, 1/6,  1/3],
            [2/3, 1/6,  1/3],
            [1/6, 2/3,  1/3],
        ]
        weights = [1/6]*6
        M = np.zeros((18, 18))
        for i, xi in enumerate(gauss_points):
            N = self.get_shape_functions(np.array(xi))
            dN = self.get_shape_derivatives(np.array(xi))
            J = dN @ coords
            detJ = np.linalg.det(J)
            Ni = np.zeros((3, 18))
            for a in range(6):
                Ni[0, a*3+0] = N[a]
                Ni[1, a*3+1] = N[a]
                Ni[2, a*3+2] = N[a]
            M += rho * detJ * weights[i] * (Ni.T @ Ni)
        return M

    def get_stiffness_matrix(self) -> np.ndarray:
        if self.material is None:
            raise ValueError("Material properties not set")
        coords = self.get_element_coordinates()
        D = self.get_stress_strain_matrix()
        gauss_points = [
            [1/6, 1/6, -1/3],
            [2/3, 1/6, -1/3],
            [1/6, 2/3, -1/3],
            [1/6, 1/6,  1/3],
            [2/3, 1/6,  1/3],
            [1/6, 2/3,  1/3],
        ]
        weights = [1/6]*6
        K = np.zeros((18, 18))
        for i, xi in enumerate(gauss_points):
            dN = self.get_shape_derivatives(np.array(xi))
            J = dN @ coords
            detJ = np.linalg.det(J)
            J_inv = np.linalg.inv(J)
            dN_dx = J_inv @ dN
            B = np.zeros((6, 18))
            for a in range(6):
                B[0, a*3+0] = dN_dx[0, a]
                B[1, a*3+1] = dN_dx[1, a]
                B[2, a*3+2] = dN_dx[2, a]
                B[3, a*3+0] = dN_dx[1, a]
                B[3, a*3+1] = dN_dx[0, a]
                B[4, a*3+1] = dN_dx[2, a]
                B[4, a*3+2] = dN_dx[1, a]
                B[5, a*3+0] = dN_dx[2, a]
                B[5, a*3+2] = dN_dx[0, a]
            K += detJ * weights[i] * (B.T @ D @ B)
        return K


class SolidElement:
    """ソリッド要素のファクトリクラス"""
    
    @staticmethod
    def create_element(element_type: str, element_id: int, node_ids: List[int], 
                      material_id: int) -> BaseElement:
        """要素タイプに応じたソリッド要素インスタンスを作成
        
        Args:
            element_type: 要素タイプ名（'tetra', 'hexa'）
            element_id: 要素ID
            node_ids: 構成節点ID
            material_id: 材料ID
            
        Returns:
            要素インスタンス
        """
        if element_type == 'tetra' or element_type == 'tet':
            return TetraElement(element_id, node_ids, material_id)
        elif element_type == 'hexa' or element_type == 'hex':
            return HexaElement(element_id, node_ids, material_id)
        elif element_type == 'wedge' or element_type == 'wed':
            return WedgeElement(element_id, node_ids, material_id)
        else:
            raise ValueError(f"Unknown solid element type: {element_type}")
            
    @staticmethod
    def get_name() -> str:
        """要素タイプ名を取得"""
        return "solid" 