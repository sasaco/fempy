"""
梁要素クラス
JavaScript版のBarElement.jsに対応し、既存のFA_Beam機能を統合
"""
from typing import Dict, List, Tuple, Optional, Any
import numpy as np
import math
from .base_element import BaseElement
from ..material import Material, BarParameter
from ..section import BaseSection


class BarElement(BaseElement):
    """梁要素の基本インターフェースクラス（JavaScript版互換）"""
    
    def __init__(self, element_id: int, node_ids: List[int], material_id: int,
                 section_id: int, angle: float = 0.0):
        """
        Args:
            element_id: 要素ID
            node_ids: 構成節点ID（2節点）
            material_id: 材料ID
            section_id: 断面ID
            angle: 要素座標軸の回転角（度）
        """
        if len(node_ids) != 2:
            raise ValueError("Bar element must have exactly 2 nodes")
            
        super().__init__(element_id, node_ids, material_id)
        self.section_id = section_id
        self.angle = angle
        self.transformation_matrix: Optional[np.ndarray] = None
        self.length: Optional[float] = None
        
    def get_name(self) -> str:
        """要素タイプ名を取得"""
        return "bar"
        
    def get_dof_per_node(self) -> int:
        """節点あたりの自由度数を取得"""
        return 6  # 3並進 + 3回転
        
    def set_node_coordinates(self, coordinates: Dict[int, np.ndarray]) -> None:
        """節点座標を設定し、変換行列を計算"""
        super().set_node_coordinates(coordinates)
        self._calculate_transformation_matrix()
        self.length = self.get_element_length()
        
    def _calculate_transformation_matrix(self) -> None:
        """要素座標系への変換行列を計算"""
        coords = self.get_element_coordinates()
        xi, yi, zi = coords[0]
        xj, yj, zj = coords[1]
        
        # cal_eMatrix関数の実装（FA_Beamから移植）
        dx = xj - xi
        dy = yj - yi
        dz = zj - zi
        rad = math.radians(self.angle)
        leng = math.sqrt(dx**2 + dy**2 + dz**2)
        
        # デフォルトの基底変換行列の計算
        if (dx == 0) and (dy == 0):  # 要素x軸が全体Z軸と平行な場合
            bMatDefault = np.array([
                [0.0, 0.0, np.sign(dz)],
                [np.sign(dz), 0.0, 0.0],
                [0.0, 1.0, 0.0]
            ], dtype=float)
        else:  # 要素x軸と全体Z軸が平行でない場合
            dxn = dx / leng
            dyn = dy / leng
            dzn = dz / leng
            hLen = math.sqrt(dxn**2 + dyn**2)
            bMatDefault = np.array([
                [dxn, dyn, dzn],
                [-dyn/hLen, dxn/hLen, 0.0],
                [-dxn*dzn/hLen, -dyn*dzn/hLen, hLen]
            ], dtype=float)
            
        # 要素x軸まわりの回転による基底変換行列の計算
        bMatAngle = np.array([
            [1.0, 0.0, 0.0],
            [0.0, math.cos(rad), math.sin(rad)],
            [0.0, -math.sin(rad), math.cos(rad)]
        ], dtype=float)
        
        # 基底変換行列の計算
        self.transformation_matrix = np.matmul(bMatAngle, bMatDefault)
        
    def get_transformation_matrix(self, size: int = 12) -> np.ndarray:
        """拡張変換行列を取得
        
        Args:
            size: 行列サイズ（3の倍数）
            
        Returns:
            size x size の変換行列
        """
        if self.transformation_matrix is None:
            raise ValueError("Transformation matrix not calculated")
            
        if size <= 0:
            size = 3
        if size % 3 != 0:
            size = ((size // 3) + 1) * 3
            
        T = np.zeros((size, size), dtype=float)
        for i in range(0, size, 3):
            T[i:i+3, i:i+3] = self.transformation_matrix
            
        return T
        
    def get_stiffness_matrix(self) -> np.ndarray:
        """要素剛性行列を取得（サブクラスで実装）"""
        raise NotImplementedError("Use BEBarElement or TBarElement")
        
    def get_mass_matrix(self) -> np.ndarray:
        """要素質量行列を取得（サブクラスで実装）"""
        raise NotImplementedError("Use BEBarElement or TBarElement")


class BEBarElement(BarElement):
    """Bernoulli-Euler梁要素クラス"""
    
    def __init__(self, element_id: int, node_ids: List[int], material_id: int,
                 section_id: int, angle: float = 0.0):
        """
        Args:
            element_id: 要素ID
            node_ids: 構成節点ID（2節点）
            material_id: 材料ID
            section_id: 断面ID
            angle: 要素座標軸の回転角（度）
        """
        super().__init__(element_id, node_ids, material_id, section_id, angle)
        self.material: Optional[Material] = None
        self.bar_param: Optional[BarParameter] = None
        
    def set_material_properties(self, material: Material, bar_param: BarParameter) -> None:
        """材料特性を設定"""
        self.material = material
        self.bar_param = bar_param
        
    def get_stiffness_matrix(self) -> np.ndarray:
        """Bernoulli-Euler梁の要素剛性行列を取得"""
        if self.material is None or self.bar_param is None:
            raise ValueError("Material properties not set")
        if self.length is None:
            raise ValueError("Element length not calculated")
            
        L = self.length
        E = self.material.materials[self.material_id].E
        
        A = self.bar_param.area
        Iy = self.bar_param.Iy
        Iz = self.bar_param.Iz
        J = self.bar_param.J
        
        # 要素座標系での剛性行列（12x12）
        Ke = np.zeros((12, 12))
        
        # 軸剛性
        Ke[0, 0] = Ke[6, 6] = E * A / L
        Ke[0, 6] = Ke[6, 0] = -E * A / L
        
        # ねじり剛性
        G = self.material.materials[self.material_id].G
        Ke[3, 3] = Ke[9, 9] = G * J / L
        Ke[3, 9] = Ke[9, 3] = -G * J / L
        
        # y方向曲げ剛性
        Ke[2, 2] = Ke[8, 8] = 12 * E * Iy / L**3
        Ke[2, 4] = Ke[4, 2] = 6 * E * Iy / L**2
        Ke[2, 8] = Ke[8, 2] = -12 * E * Iy / L**3
        Ke[2, 10] = Ke[10, 2] = 6 * E * Iy / L**2
        Ke[4, 4] = Ke[10, 10] = 4 * E * Iy / L
        Ke[4, 8] = Ke[8, 4] = -6 * E * Iy / L**2
        Ke[4, 10] = Ke[10, 4] = 2 * E * Iy / L
        Ke[8, 10] = Ke[10, 8] = -6 * E * Iy / L**2
        
        # z方向曲げ剛性
        Ke[1, 1] = Ke[7, 7] = 12 * E * Iz / L**3
        Ke[1, 5] = Ke[5, 1] = -6 * E * Iz / L**2
        Ke[1, 7] = Ke[7, 1] = -12 * E * Iz / L**3
        Ke[1, 11] = Ke[11, 1] = -6 * E * Iz / L**2
        Ke[5, 5] = Ke[11, 11] = 4 * E * Iz / L
        Ke[5, 7] = Ke[7, 5] = 6 * E * Iz / L**2
        Ke[5, 11] = Ke[11, 5] = 2 * E * Iz / L
        Ke[7, 11] = Ke[11, 7] = 6 * E * Iz / L**2
        
        # 全体座標系への変換
        T = self.get_transformation_matrix(12)
        K = T.T @ Ke @ T
        
        return K
        
    def get_mass_matrix(self) -> np.ndarray:
        """Bernoulli-Euler梁の要素質量行列を取得"""
        if self.material is None or self.bar_param is None:
            raise ValueError("Material properties not set")
        if self.length is None:
            raise ValueError("Element length not calculated")
            
        L = self.length
        rho = self.material.materials[self.material_id].density
        A = self.bar_param.area
        
        # 密度がNoneの場合はデフォルト値を使用
        if rho is None:
            # 材料IDに基づいてデフォルト密度を推定
            material_name = self.material.materials[self.material_id].name
            if "Steel" in material_name or "steel" in material_name:
                rho = 7850.0  # 鋼材
            elif "Aluminum" in material_name or "aluminum" in material_name:
                rho = 2700.0  # アルミ
            elif "Concrete" in material_name or "concrete" in material_name:
                rho = 2400.0  # コンクリート
            else:
                rho = 7850.0  # 一般的な金属材料として鋼材を使用
            print(f"警告: 材料ID{self.material_id}の密度が設定されていません。デフォルト値{rho}を使用します。")
        
        # 集中質量行列（簡略化）
        m = rho * A * L / 2  # 各節点への質量配分
        Me = np.zeros((12, 12))
        
        # 並進質量
        for i in range(3):
            Me[i, i] = m
            Me[i+6, i+6] = m
            
        # 回転慣性（簡略化）
        Iy = self.bar_param.Iy
        Iz = self.bar_param.Iz
        J = self.bar_param.J
        
        Me[3, 3] = Me[9, 9] = rho * J * L / 2
        Me[4, 4] = Me[10, 10] = rho * Iz * L / 2
        Me[5, 5] = Me[11, 11] = rho * Iy * L / 2
        
        # 全体座標系への変換
        T = self.get_transformation_matrix(12)
        M = T.T @ Me @ T
        
        return M


class TBarElement(BarElement):
    """Timoshenko梁要素クラス（既存のFA_Beam機能を移植）"""
    
    def __init__(self, element_id: int, node_ids: List[int], material_id: int,
                 section_id: int, angle: float = 0.0, shear_correction: bool = True):
        """
        Args:
            element_id: 要素ID
            node_ids: 構成節点ID（2節点）
            material_id: 材料ID
            section_id: 断面ID
            angle: 要素座標軸の回転角（度）
            shear_correction: せん断変形を考慮するか
        """
        super().__init__(element_id, node_ids, material_id, section_id, angle)
        self.shear_correction = shear_correction
        self.material: Optional[Material] = None
        self.bar_param: Optional[BarParameter] = None
        
    def set_material_properties(self, material: Material, bar_param: BarParameter) -> None:
        """材料特性を設定"""
        self.material = material
        self.bar_param = bar_param
        
    def get_stiffness_matrix(self) -> np.ndarray:
        """Timoshenko梁の要素剛性行列を取得（せん断変形を考慮）"""
        if self.material is None or self.bar_param is None:
            raise ValueError("Material properties not set")
        if self.length is None:
            raise ValueError("Element length not calculated")
            
        L = self.length
        
        # V1レベルの詳細診断情報
        if L <= 0:
            coords = self.get_element_coordinates()
            print(f"🚨 ゼロ長要素詳細診断:")
            print(f"  - 要素ID: {self.element_id}")
            print(f"  - 節点ID: {self.node_ids}")
            print(f"  - 計算された長さ: {L}")
            print(f"  - 節点座標:")
            for i, node_id in enumerate(self.node_ids):
                print(f"    節点{node_id}: {coords[i]}")
            if len(self.node_ids) == 2:
                vector = coords[1] - coords[0]
                print(f"  - ベクトル: {vector}")
                print(f"  - ベクトルノルム: {np.linalg.norm(vector)}")
                
            # 節点重複チェック
            if len(self.node_ids) == 2 and self.node_ids[0] == self.node_ids[1]:
                raise ValueError(f"要素{self.element_id}: 同一節点({self.node_ids[0]})で要素が構成されています。要素分割処理で節点重複が発生した可能性があります。")
            elif np.allclose(coords[0], coords[1], atol=1e-12):
                raise ValueError(f"要素{self.element_id}: 節点{self.node_ids[0]}と節点{self.node_ids[1]}が同一座標です。座標: {coords[0]} ≈ {coords[1]}")
            else:
                raise ValueError(f"要素{self.element_id}: 要素長さが無効です: L={L}。節点座標または距離計算に問題があります。")
        
        E = self.material.materials[self.material_id].E
        G = self.material.materials[self.material_id].G
        
        A = self.bar_param.area
        Iy = self.bar_param.Iy
        Iz = self.bar_param.Iz
        J = self.bar_param.J
        
        # V1レベルの数値安定性チェック
        if E <= 0:
            raise ValueError(f"ヤング係数が無効です: E={E}")
        if G <= 0:
            raise ValueError(f"せん断弾性係数が無効です: G={G}")
        if A <= 0:
            raise ValueError(f"断面積が無効です: A={A}")
            
        # せん断補正係数の安全性チェック
        ky = self.bar_param.kappa_y if self.shear_correction else float('inf')
        kz = self.bar_param.kappa_z if self.shear_correction else float('inf')
        
        # ゼロ除算防止: せん断補正係数の検証
        if self.shear_correction:
            if ky <= 0:
                print(f"警告: せん断補正係数ky={ky}が無効です。デフォルト値5/6を使用します。")
                ky = 5.0/6.0
            if kz <= 0:
                print(f"警告: せん断補正係数kz={kz}が無効です。デフォルト値5/6を使用します。")
                kz = 5.0/6.0
        
        # せん断変形パラメータの安全な計算
        if self.shear_correction:
            # ゼロ除算防止: 分母の安全性チェック
            denom_y_calc = ky * G * A * L**2
            denom_z_calc = kz * G * A * L**2
            
            if abs(denom_y_calc) < 1e-12:
                print(f"警告: せん断変形計算でゼロ除算検出 (Y方向)。Bernoulli-Euler梁として処理します。")
                phi_y = 0
            else:
                phi_y = 12 * E * Iy / denom_y_calc
                
            if abs(denom_z_calc) < 1e-12:
                print(f"警告: せん断変形計算でゼロ除算検出 (Z方向)。Bernoulli-Euler梁として処理します。")
                phi_z = 0
            else:
                phi_z = 12 * E * Iz / denom_z_calc
                
            # 無限大・NaN値の検証
            if not np.isfinite(phi_y):
                print(f"警告: phi_y={phi_y}が無効値です。ゼロに設定します。")
                phi_y = 0
            if not np.isfinite(phi_z):
                print(f"警告: phi_z={phi_z}が無効値です。ゼロに設定します。")
                phi_z = 0
        else:
            phi_y = phi_z = 0
            
        # 要素座標系での剛性行列（12x12）
        Ke = np.zeros((12, 12))
        
        # 軸剛性（Bernoulli-Eulerと同じ）
        Ke[0, 0] = Ke[6, 6] = E * A / L
        Ke[0, 6] = Ke[6, 0] = -E * A / L
        
        # ねじり剛性（Bernoulli-Eulerと同じ）
        Ke[3, 3] = Ke[9, 9] = G * J / L
        Ke[3, 9] = Ke[9, 3] = -G * J / L
        
        # y方向曲げ剛性（せん断変形を考慮）
        denom_y = 1 + phi_y
        # 分母の安全性再確認
        if abs(denom_y) < 1e-12:
            print(f"警告: denom_y={denom_y}がゼロに近い値です。デフォルト値1.0を使用します。")
            denom_y = 1.0
            
        Ke[2, 2] = Ke[8, 8] = 12 * E * Iy / (L**3 * denom_y)
        Ke[2, 4] = Ke[4, 2] = 6 * E * Iy / (L**2 * denom_y)
        Ke[2, 8] = Ke[8, 2] = -12 * E * Iy / (L**3 * denom_y)
        Ke[2, 10] = Ke[10, 2] = 6 * E * Iy / (L**2 * denom_y)
        Ke[4, 4] = (4 + phi_y) * E * Iy / (L * denom_y)
        Ke[10, 10] = (4 + phi_y) * E * Iy / (L * denom_y)
        Ke[4, 8] = Ke[8, 4] = -6 * E * Iy / (L**2 * denom_y)
        Ke[4, 10] = Ke[10, 4] = (2 - phi_y) * E * Iy / (L * denom_y)
        Ke[8, 10] = Ke[10, 8] = -6 * E * Iy / (L**2 * denom_y)
        
        # z方向曲げ剛性（せん断変形を考慮）
        denom_z = 1 + phi_z
        # 分母の安全性再確認
        if abs(denom_z) < 1e-12:
            print(f"警告: denom_z={denom_z}がゼロに近い値です。デフォルト値1.0を使用します。")
            denom_z = 1.0
            
        Ke[1, 1] = Ke[7, 7] = 12 * E * Iz / (L**3 * denom_z)
        Ke[1, 5] = Ke[5, 1] = -6 * E * Iz / (L**2 * denom_z)
        Ke[1, 7] = Ke[7, 1] = -12 * E * Iz / (L**3 * denom_z)
        Ke[1, 11] = Ke[11, 1] = -6 * E * Iz / (L**2 * denom_z)
        Ke[5, 5] = (4 + phi_z) * E * Iz / (L * denom_z)
        Ke[11, 11] = (4 + phi_z) * E * Iz / (L * denom_z)
        Ke[5, 7] = Ke[7, 5] = 6 * E * Iz / (L**2 * denom_z)
        Ke[5, 11] = Ke[11, 5] = (2 - phi_z) * E * Iz / (L * denom_z)
        Ke[7, 11] = Ke[11, 7] = 6 * E * Iz / (L**2 * denom_z)
        
        # V1レベルの剛性行列検証
        if np.any(np.isnan(Ke)) or np.any(np.isinf(Ke)):
            raise ValueError("剛性行列にNaNまたは無限大が含まれています。材料定数または形状定数を確認してください。")
        
        # 全体座標系への変換
        T = self.get_transformation_matrix(12)
        K = T.T @ Ke @ T
        
        # 最終検証
        if np.any(np.isnan(K)) or np.any(np.isinf(K)):
            raise ValueError("変換後の剛性行列にNaNまたは無限大が含まれています。")
        
        return K
        
    def get_mass_matrix(self) -> np.ndarray:
        """Timoshenko梁の要素質量行列を取得"""
        # BEBarElementと同じ実装を使用（質量行列は同じ）
        be_element = BEBarElement(self.element_id, self.node_ids, 
                                 self.material_id, self.section_id, self.angle)
        be_element.material = self.material
        be_element.bar_param = self.bar_param
        be_element.transformation_matrix = self.transformation_matrix
        be_element.length = self.length
        
        return be_element.get_mass_matrix()
        
    def calculate_forces(self, displacement: np.ndarray) -> Dict[str, np.ndarray]:
        """要素力を計算
        
        Args:
            displacement: 要素節点変位ベクトル（12要素）
            
        Returns:
            断面力の辞書 {'i_end': [...], 'j_end': [...]}
        """
        # 要素座標系への変換
        T = self.get_transformation_matrix(12)
        disp_local = T @ displacement
        
        # 要素剛性行列を取得
        K_local = T @ self.get_stiffness_matrix() @ T.T
        
        # 要素力の計算
        forces_local = K_local @ disp_local
        
        # i端とj端の断面力
        i_forces = forces_local[0:6]
        j_forces = forces_local[6:12]
        
        return {
            'i_end': i_forces,
            'j_end': j_forces
        } 