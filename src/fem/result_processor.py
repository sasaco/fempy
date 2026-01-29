"""
計算結果処理モジュール
JavaScript版のFemResultクラスに相当する機能を提供
"""
import numpy as np
from typing import Dict, List, Any, Optional, Union
# レガシーモジュールのインポートは一旦コメントアウト
# from models.fa_shell import FA_Shell
# from models.fa_node import FA_Node
# from models.fa_material import FA_Material
# from models.fa_thickness import FA_Thickness
# from strain_stress import calculate_shell_results

# 新しいモジュール構成のインポート
from .strain_stress import calculate_shell_results, calculate_tetra_strain_stress, calculate_hexa_strain_stress, calculate_wedge_strain_stress


class ResultProcessor:
    """計算結果処理クラス - JavaScript版のFemResultクラスに相当"""
    
    def __init__(self, nodes: Optional[List[Any]] = None, 
                 shells: Optional[List[Any]] = None, 
                 materials: Optional[List[Any]] = None, 
                 thicknesses: Optional[List[Any]] = None,
                 displacements: Optional[Dict[int, Dict[str, float]]] = None):
        """初期化
        
        Args:
            nodes: 節点リスト
            shells: シェル要素リスト
            materials: 材料リスト
            thicknesses: 厚さリスト
            displacements: 節点変位辞書 {節点ID: {"dx": dx, "dy": dy, ...}}
        """
        self.nodes = nodes or []
        self.shells = shells or []
        self.materials = materials or []
        self.thicknesses = thicknesses or []
        self.displacements = displacements or {}
        self.shell_results: Dict[int, Dict[str, Any]] = {}
    
    def process_displacement(self, displacement: np.ndarray, mesh: Any) -> Dict[int, Dict[str, float]]:
        """変位結果を処理して節点ごとの変位辞書を作成
        
        Args:
            displacement: 変位ベクトル
            mesh: メッシュデータ
            
        Returns:
            節点ID -> 変位成分の辞書
        """
        node_displacements = {}
        
        # 節点IDのリストを取得
        node_ids = sorted(mesh.nodes.keys())
        
        for i, node_id in enumerate(node_ids):
            # 各節点は6自由度（dx, dy, dz, rx, ry, rz）
            base_idx = i * 6
            
            if base_idx + 6 <= len(displacement):
                node_displacements[node_id] = {
                    "dx": float(displacement[base_idx]),
                    "dy": float(displacement[base_idx + 1]),
                    "dz": float(displacement[base_idx + 2]),
                    "rx": float(displacement[base_idx + 3]),
                    "ry": float(displacement[base_idx + 4]),
                    "rz": float(displacement[base_idx + 5])
                }
            else:
                # 不足している場合は0で埋める
                node_displacements[node_id] = {
                    "dx": float(displacement[base_idx]) if base_idx < len(displacement) else 0.0,
                    "dy": float(displacement[base_idx + 1]) if base_idx + 1 < len(displacement) else 0.0,
                    "dz": float(displacement[base_idx + 2]) if base_idx + 2 < len(displacement) else 0.0,
                    "rx": float(displacement[base_idx + 3]) if base_idx + 3 < len(displacement) else 0.0,
                    "ry": float(displacement[base_idx + 4]) if base_idx + 4 < len(displacement) else 0.0,
                    "rz": float(displacement[base_idx + 5]) if base_idx + 5 < len(displacement) else 0.0
                }
                
        return node_displacements
    
    def process_stress(self, elements: Dict[int, Any], displacement: np.ndarray) -> Dict[int, Any]:
        """要素応力を計算
        
        Args:
            elements: 要素オブジェクトの辞書
            displacement: 変位ベクトル
            
        Returns:
            要素ID -> 応力結果の辞書
        """
        element_stresses = {}
        
        for elem_id, element in elements.items():
            try:
                # 要素タイプに応じた変位抽出
                node_ids = element.node_ids if hasattr(element, 'node_ids') else []
                elem_disp = []
                
                for node_id in node_ids:
                    base_dof = (node_id - 1) * 6
                    dof_per_node = element.get_dof_per_node() if hasattr(element, 'get_dof_per_node') else 6
                    
                    for i in range(dof_per_node):
                        if base_dof + i < len(displacement):
                            elem_disp.append(displacement[base_dof + i])
                        else:
                            elem_disp.append(0.0)
                
                # 応力計算（実装されている要素のみ）
                if hasattr(element, 'calculate_stress_strain'):
                    stress_strain = element.calculate_stress_strain(np.array(elem_disp))
                    element_stresses[elem_id] = stress_strain
                elif hasattr(element, 'calculate_forces'):
                    forces = element.calculate_forces(np.array(elem_disp))
                    element_stresses[elem_id] = forces
                else:
                    # 未実装の要素タイプはスキップ
                    element_stresses[elem_id] = {"status": "Not implemented"}
                    
            except Exception as e:
                # エラーが発生した場合はエラー情報を記録
                element_stresses[elem_id] = {"error": str(e)}
                
        return element_stresses
    
    def format_eigenvalues(self, eigenvalues: np.ndarray, eigenvectors: np.ndarray) -> Dict[str, Any]:
        """固有値解析結果を整形
        
        Args:
            eigenvalues: 固有値配列
            eigenvectors: 固有ベクトル行列
            
        Returns:
            整形された固有値解析結果
        """
        result = {
            "n_modes": len(eigenvalues),
            "eigenvalues": [],
            "frequencies": [],
            "periods": [],
            "eigenvectors": []
        }
        
        # 各モードについて処理
        for i in range(len(eigenvalues)):
            eigenvalue = float(eigenvalues[i])
            
            # 固有円振動数と周期を計算
            if eigenvalue > 0:
                angular_freq = np.sqrt(eigenvalue)
                frequency = angular_freq / (2 * np.pi)
                period = 1.0 / frequency if frequency > 0 else float('inf')
            else:
                angular_freq = 0.0
                frequency = 0.0
                period = float('inf')
            
            result["eigenvalues"].append(eigenvalue)
            result["frequencies"].append(frequency)
            result["periods"].append(period)
            
            # 固有ベクトルを追加
            if eigenvectors is not None and i < eigenvectors.shape[1]:
                eigvec = eigenvectors[:, i].tolist()
                result["eigenvectors"].append(eigvec)
        
        return result
    
    def process_shell_results(self) -> None:
        """シェル要素の計算結果を処理する"""
        # 新しいモジュール構成を使用して実装
        # fem/calculation.py の __calculate_shell_stress_strain メソッドを参考に
        # fem.strain_stress.calculate_shell_results を活用
        
        for i, shell in enumerate(self.shells):
            if hasattr(shell, 'iMat') and hasattr(shell, 'iThick'):
                # 材料と厚さの取得
                mat = self.materials[shell.iMat]
                thick = self.thicknesses[shell.iThick]
                
                # JavaScript版と完全に同じ材料マトリクス計算
                ks_rect = 5.0 / 6.0  # JavaScript: var KS_RECT=5/6
                coef = mat.e / (1.0 - mat.poi * mat.poi)  # 精度を上げるため二乗を掛け算で計算
                s2 = coef * mat.poi
                msh = np.array([
                    [coef, s2, 0.0, 0.0, 0.0],
                    [s2, coef, 0.0, 0.0, 0.0],
                    [0.0, 0.0, mat.g, 0.0, 0.0],
                    [0.0, 0.0, 0.0, ks_rect * mat.g, 0.0],
                    [0.0, 0.0, 0.0, 0.0, ks_rect * mat.g]
                ], dtype=np.float64)
                
                # JavaScript版では要素IDは1から始まるため、i+1を渡す
                result = calculate_shell_results(shell, self.displacements, msh, thick.t, i+1)
                
                # 結果の整形
                stress_points = []
                strain_points = []
                
                # nodeStress1とnodeStrain1から結果を取得
                for node_idx in range(len(result["nodeStress1"])):
                    stress = result["nodeStress1"][node_idx]
                    strain = result["nodeStrain1"][node_idx]
                    
                    stress_point = {
                        "mx": stress[0],   # σx
                        "my": stress[1],   # σy
                        "mxy": stress[3],  # τxy
                        "qx": stress[4],   # τyz (シェル要素の場合は0または小さい値)
                        "qy": stress[5]    # τzx (シェル要素の場合は0または小さい値)
                    }
                    strain_point = {
                        "ex": strain[0],   # εx
                        "ey": strain[1],   # εy
                        "exy": strain[3]   # γxy
                    }
                    stress_points.append(stress_point)
                    strain_points.append(strain_point)
                
                # ひずみエネルギーの計算
                # 要素全体のエネルギーを使用
                strain_energy = result["elemEnergy1"]
                
                self.shell_results[i] = {
                    "stress": stress_points,
                    "strain": strain_points,
                    "strain_energy": strain_energy,
                    "raw_result": result  # 生の計算結果も保存
                }
    
    def process_solid_results(self, solids: List[Any]) -> Dict[int, Dict[str, Any]]:
        """ソリッド要素の計算結果を処理する
        
        Args:
            solids: ソリッド要素リスト
            
        Returns:
            ソリッド要素インデックス -> 計算結果の辞書
        """
        # fem/calculation.py の __calculate_solid_stress_strain メソッドを参考に
        # fem.strain_stress.calculate_tetra_strain_stress, 
        # calculate_hexa_strain_stress, calculate_wedge_strain_stress を活用
        
        solid_results = {}
        
        for i, solid in enumerate(solids):
            try:
                # 材料の取得
                if hasattr(solid, 'material_num'):
                    material = self.materials[solid.material_num]
                else:
                    # 材料番号が無い場合はスキップ
                    solid_results[i] = {"error": "No material_num attribute"}
                    continue
                
                # 要素タイプに応じた処理
                if hasattr(solid, 'element_type'):
                    element_type = solid.element_type
                else:
                    # 要素タイプが不明な場合はスキップ
                    solid_results[i] = {"error": "No element_type attribute"}
                    continue
                
                # ひずみ・応力計算
                if element_type == "tetra":
                    strain, stress, energy = calculate_tetra_strain_stress(
                        solid, self.displacements, material, self.nodes
                    )
                elif element_type == "hexa":
                    strain, stress, energy = calculate_hexa_strain_stress(
                        solid, self.displacements, material, self.nodes
                    )
                elif element_type == "wedge":
                    strain, stress, energy = calculate_wedge_strain_stress(
                        solid, self.displacements, material, self.nodes
                    )
                else:
                    solid_results[i] = {"error": f"Unknown element type: {element_type}"}
                    continue
                
                # 結果を辞書形式で保存
                solid_results[i] = {
                    'strain': strain.to_list(),
                    'stress': stress.to_list(),
                    'energy': energy,
                    'strain_obj': strain,  # オブジェクトも保存
                    'stress_obj': stress
                }
                
            except Exception as e:
                # エラーが発生した場合はエラー情報を記録
                solid_results[i] = {"error": str(e)}
        
        return solid_results
    
    def get_shell_result(self, shell_index: int) -> Optional[Dict[str, Any]]:
        """シェル要素の計算結果を取得する
        
        Args:
            shell_index: シェル要素インデックス
            
        Returns:
            計算結果辞書
        """
        return self.shell_results.get(shell_index)
    
    def get_all_shell_results(self) -> Dict[int, Dict[str, Any]]:
        """全シェル要素の計算結果を取得する
        
        Returns:
            全計算結果辞書 {シェル要素インデックス: 計算結果辞書}
        """
        return self.shell_results
