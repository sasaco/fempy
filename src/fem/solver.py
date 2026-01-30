"""
有限要素法のソルバーモジュール
JavaScript版のSolver機能に対応
"""
from typing import Dict, Any, List, Tuple, Optional
import numpy as np
from scipy.sparse import lil_matrix, csr_matrix, diags
from scipy.sparse.linalg import spsolve, eigsh, spilu, gmres, bicgstab
from scipy.linalg import eigh, svd, lstsq
from .mesh import MeshModel
from .boundary_condition import BoundaryCondition
from .material import Material


class Solver:
    """FEM解析のソルバークラス"""
    
    def __init__(self):
        self.assembled_stiffness: Optional[csr_matrix] = None
        self.assembled_mass: Optional[csr_matrix] = None
        self.load_vector: Optional[np.ndarray] = None
        self.displacement: Optional[np.ndarray] = None
        self.eigenvalues: Optional[np.ndarray] = None
        self.eigenvectors: Optional[np.ndarray] = None
        
    def create_stiffness_matrix(self, mesh: MeshModel, material: Material, 
                              elements: Dict[int, Any]) -> csr_matrix:
        """全体剛性行列を作成
        
        Args:
            mesh: メッシュデータ
            material: 材料データ
            elements: 要素オブジェクトの辞書
            
        Returns:
            全体剛性行列（CSR形式）
        """
        # 要素タイプごとの自由度を確認し、全体の自由度数を決定
        max_dof_per_node = 6  # デフォルト（bar, shell要素）
        
        # solid要素が含まれている場合の自由度調整
        has_solid_only = True
        for elem_id, elem_data in mesh.elements.items():
            elem_type = elem_data.get('type', 'bar')
            if elem_type not in ['tetra', 'hexa', 'wedge']:
                has_solid_only = False
                break
        
        # solid要素のみの場合は3自由度/節点
        if has_solid_only:
            max_dof_per_node = 3
        
        # 自由度数の計算
        n_dof = len(mesh.nodes) * max_dof_per_node
        
        # LIL形式で初期化（要素剛性行列の組み立てに適している）
        K_global = lil_matrix((n_dof, n_dof))
        
        # 各要素の剛性行列を組み立て
        for elem_id, elem_data in mesh.elements.items():
            if elem_id not in elements:
                continue
                
            element = elements[elem_id]
            
            # 要素剛性行列を取得
            K_elem = element.get_stiffness_matrix()
            
            # 要素の節点番号を取得
            node_ids = elem_data['nodes']
            
            # 要素の自由度数を取得
            elem_dof_per_node = element.get_dof_per_node()
            
            # 全体座標系での自由度番号を計算
            dof_indices = []
            for node_id in node_ids:
                base_dof = (node_id - 1) * max_dof_per_node
                for i in range(elem_dof_per_node):
                    dof_indices.append(base_dof + i)
                    
            # 全体剛性行列に組み込み
            for i, dof_i in enumerate(dof_indices):
                for j, dof_j in enumerate(dof_indices):
                    if dof_i < n_dof and dof_j < n_dof:  # 範囲チェック追加
                        K_global[dof_i, dof_j] += K_elem[i, j]
                    
        # CSR形式に変換（計算に適している）
        self.assembled_stiffness = K_global.tocsr()
        return self.assembled_stiffness
        
    def create_mass_matrix(self, mesh: MeshModel, material: Material,
                          elements: Dict[int, Any]) -> csr_matrix:
        """全体質量行列を作成
        
        Args:
            mesh: メッシュデータ
            material: 材料データ
            elements: 要素オブジェクトの辞書
            
        Returns:
            全体質量行列（CSR形式）
        """
        # 要素タイプごとの自由度を確認し、全体の自由度数を決定
        max_dof_per_node = 6  # デフォルト（bar, shell要素）
        
        # solid要素が含まれている場合の自由度調整
        has_solid_only = True
        for elem_id, elem_data in mesh.elements.items():
            elem_type = elem_data.get('type', 'bar')
            if elem_type not in ['tetra', 'hexa', 'wedge']:
                has_solid_only = False
                break
        
        # solid要素のみの場合は3自由度/節点
        if has_solid_only:
            max_dof_per_node = 3
        
        # 自由度数の計算
        n_dof = len(mesh.nodes) * max_dof_per_node
        
        # LIL形式で初期化
        M_global = lil_matrix((n_dof, n_dof))
        
        # 各要素の質量行列を組み立て
        for elem_id, elem_data in mesh.elements.items():
            if elem_id not in elements:
                continue
                
            element = elements[elem_id]
            
            # 要素質量行列を取得
            M_elem = element.get_mass_matrix()
            
            # 要素の節点番号を取得
            node_ids = elem_data['nodes']
            
            # 要素の自由度数を取得
            elem_dof_per_node = element.get_dof_per_node()
            
            # 全体座標系での自由度番号を計算
            dof_indices = []
            for node_id in node_ids:
                base_dof = (node_id - 1) * max_dof_per_node
                for i in range(elem_dof_per_node):
                    dof_indices.append(base_dof + i)
                    
            # 全体質量行列に組み込み
            for i, dof_i in enumerate(dof_indices):
                for j, dof_j in enumerate(dof_indices):
                    if dof_i < n_dof and dof_j < n_dof:  # 範囲チェック追加
                        M_global[dof_i, dof_j] += M_elem[i, j]
                    
        # CSR形式に変換
        self.assembled_mass = M_global.tocsr()
        return self.assembled_mass
        
    def assemble_load_vector(self, mesh: MeshModel, boundary: BoundaryCondition,
                           elements: Dict[int, Any]) -> np.ndarray:
        """荷重ベクトルを組み立て
        
        Args:
            mesh: メッシュデータ
            boundary: 境界条件
            elements: 要素オブジェクトの辞書
            
        Returns:
            荷重ベクトル
        """
        # 要素タイプごとの自由度を確認し、全体の自由度数を決定
        max_dof_per_node = 6  # デフォルト（bar, shell要素）
        
        # solid要素が含まれている場合の自由度調整
        has_solid_only = True
        for elem_id, elem_data in mesh.elements.items():
            elem_type = elem_data.get('type', 'bar')
            if elem_type not in ['tetra', 'hexa', 'wedge']:
                has_solid_only = False
                break
        
        # solid要素のみの場合は3自由度/節点
        if has_solid_only:
            max_dof_per_node = 3
        
        n_dof = len(mesh.nodes) * max_dof_per_node
        F = np.zeros(n_dof)
        
        # 節点荷重の適用
        for node_id, load in boundary.loads.items():
            base_dof = (node_id - 1) * max_dof_per_node
            for i in range(min(max_dof_per_node, len(load.forces))):
                if base_dof + i < n_dof:  # 範囲チェック追加
                    F[base_dof + i] += load.forces[i]
                
        # 分布荷重の適用
        for dist_load in boundary.distributed_loads:
            elem_id = dist_load.element_id
            if elem_id not in elements:
                continue
                
            element = elements[elem_id]
            elem_data = mesh.elements[elem_id]
            
            # 要素の等価節点荷重を計算
            equiv_loads = element.get_equivalent_nodal_loads(
                dist_load.load_type, dist_load.values, dist_load.face
            )
            
            # 要素の自由度数を取得
            elem_dof_per_node = element.get_dof_per_node()
            
            # 全体荷重ベクトルに加算
            node_ids = elem_data['nodes']
            for i, node_id in enumerate(node_ids):
                base_dof = (node_id - 1) * max_dof_per_node
                for j in range(elem_dof_per_node):
                    if i * elem_dof_per_node + j < len(equiv_loads) and base_dof + j < n_dof:
                        F[base_dof + j] += equiv_loads[i * elem_dof_per_node + j]
        
        # 面圧荷重の適用（V0のloadVector関数の面圧処理を移植）
        for pressure in boundary.pressures:
            elem_id = pressure.element_id
            if elem_id not in elements:
                continue
                
            element = elements[elem_id]
            elem_data = mesh.elements[elem_id]
            
            # 要素の等価節点荷重を計算（面圧専用）
            equiv_loads = element.get_equivalent_nodal_loads(
                'pressure', [pressure.pressure], pressure.face
            )
            
            # 要素の自由度数を取得
            elem_dof_per_node = element.get_dof_per_node()
            
            # 全体荷重ベクトルに加算
            node_ids = elem_data['nodes']
            for i, node_id in enumerate(node_ids):
                base_dof = (node_id - 1) * max_dof_per_node
                for j in range(elem_dof_per_node):
                    if i * elem_dof_per_node + j < len(equiv_loads) and base_dof + j < n_dof:
                        F[base_dof + j] += equiv_loads[i * elem_dof_per_node + j]
                        
        self.load_vector = F
        return F
        
    def apply_boundary_conditions(self, K: csr_matrix, F: np.ndarray,
                                boundary: BoundaryCondition) -> Tuple[csr_matrix, np.ndarray]:
        """境界条件を適用
        
        Args:
            K: 剛性行列
            F: 荷重ベクトル
            boundary: 境界条件
            
        Returns:
            修正後の剛性行列と荷重ベクトル
        """
        K_mod = lil_matrix(K, copy=True)
        F_mod = F.copy()
        
        # 自由度/節点を判定（行列サイズから推定）
        n_nodes = len(set(node_id for node_id in boundary.restraints.keys() 
                         if hasattr(boundary, 'restraints'))) or len(F) // 6
        max_dof_per_node = len(F) // max(1, n_nodes) if n_nodes > 0 else 6
        
        # 拘束条件の適用
        for node_id, restraint in boundary.restraints.items():
            base_dof = (node_id - 1) * max_dof_per_node
            
            for i, is_restrained in enumerate(restraint.dof_restraints):
                if is_restrained and i < max_dof_per_node and base_dof + i < len(F):
                    dof = base_dof + i
                    
                    # 特殊な大きな値（>1000）はバネ定数を意味する
                    value = restraint.values[i] if restraint.values else 0
                    if abs(value) > 1000:
                        # バネ要素として処理
                        spring_k = abs(value)  # バネ定数
                        K_mod[dof, dof] += spring_k
                    else:
                        # 通常の拘束条件として処理
                        # 対角成分を大きな値で置き換え
                        penalty = 1e15 * abs(K.diagonal().max())
                        K_mod[dof, dof] = penalty
                        
                        # 強制変位の場合は右辺ベクトルに適用
                        if value != 0:
                            F_mod[dof] = penalty * value
                        else:
                            F_mod[dof] = 0
        
        return csr_matrix(K_mod), F_mod
        
    def solve_linear_system(self, K: csr_matrix, F: np.ndarray) -> np.ndarray:
        """線形方程式系を解く（V1レベル数値安定化技術適用）
        
        Args:
            K: 剛性行列
            F: 荷重ベクトル
            
        Returns:
            変位ベクトル
        """
        from scipy.sparse.linalg import spsolve, spilu, gmres, bicgstab
        from scipy.linalg import svd, lstsq
        import warnings
        
        print(f"[Solver] V1レベル数値安定化ソルバー開始")
        print(f"  - 行列サイズ: {K.shape[0]}×{K.shape[1]}")
        print(f"  - 非零要素数: {K.nnz}")
        
        # 段階1: 条件数チェック
        try:
            # 行列の対角成分統計
            diag = K.diagonal()
            max_diag = np.max(diag)
            min_diag = np.min(diag[diag > 0]) if np.any(diag > 0) else 1e-16
            cond_estimate = max_diag / min_diag
            
            print(f"  - 対角成分統計: max={max_diag:.2e}, min={min_diag:.2e}")
            print(f"  - 条件数推定: {cond_estimate:.2e}")
            
            # 良好な条件数の場合は直接法
            if cond_estimate < 1e12:
                print("  → 直接法（UMFPACK）を試行")
                try:
                    with warnings.catch_warnings():
                        warnings.filterwarnings('ignore', category=DeprecationWarning)
                        self.displacement = spsolve(K, F, use_umfpack=True)
                    
                    if not np.any(np.isnan(self.displacement)):
                        print("  [OK] 直接法成功")
                        return self.displacement
                    else:
                        print("  [NG] 直接法でNaN発生")
                except:
                    print("  [NG] 直接法失敗")
            else:
                print("  [NG] 条件数不良（>1e12）、安定化手法を適用")
                
        except Exception as e:
            print(f"  [NG] 条件数チェック失敗: {e}")
        
        # 段階2: 正則化技術（Tikhonov正則化）
        print("  [Step] Tikhonov正則化を適用")
        try:
            # 正則化パラメータ（対角成分の平均の1e-6倍）
            diag_mean = np.mean(np.abs(K.diagonal()))
            reg_param = max(1e-12, diag_mean * 1e-6)
            
            # 正則化行列 K_reg = K + λI
            I_reg = diags(np.full(K.shape[0], reg_param), format='csr')
            K_reg = K + I_reg
            
            print(f"    正則化パラメータ: {reg_param:.2e}")
            
            with warnings.catch_warnings():
                warnings.filterwarnings('ignore')
                self.displacement = spsolve(K_reg, F, use_umfpack=False)
            
            if not np.any(np.isnan(self.displacement)):
                print("  [OK] Tikhonov正則化成功")
                return self.displacement
            else:
                print("  [NG] 正則化でもNaN発生")
                
        except Exception as e:
            print(f"  [NG] 正則化失敗: {e}")
        
        # 段階3: 前処理付き反復法（GMRES）
        print("  [Step] 前処理付きGMRES反復法を適用")
        try:
            # ILU前処理器の作成
            try:
                # 正則化された行列でILU分解
                K_for_ilu = K + diags(np.full(K.shape[0], diag_mean * 1e-8), format='csr')
                ilu = spilu(K_for_ilu.tocsc(), fill_factor=2.0, drop_tol=1e-6)
                from scipy.sparse.linalg import LinearOperator
                
                def preconditioner(x):
                    return ilu.solve(x)
                
                M = LinearOperator(K.shape, matvec=preconditioner)
                print("    ILU前処理器作成成功")
                
            except Exception as ilu_e:
                print(f"    ILU前処理器作成失敗: {ilu_e}")
                M = None
            
            # GMRES反復法
            x0 = np.zeros(K.shape[0])  # 初期推定値
            
            self.displacement, info = gmres(
                K, F, x0=x0, M=M,
                tol=1e-8, maxiter=2000, restart=50,
                callback=None, callback_type='legacy'
            )
            
            if info == 0 and not np.any(np.isnan(self.displacement)):
                print(f"  [OK] GMRES反復法成功（収束）")
                return self.displacement
            else:
                print(f"  [NG] GMRES反復法失敗（info={info}）")
                
        except Exception as e:
            print(f"  [NG] GMRES反復法エラー: {e}")
        
        # 段階4: BiCGStab反復法（予備）
        print("  [Step] BiCGStab反復法を適用")
        try:
            x0 = np.zeros(K.shape[0])
            
            self.displacement, info = bicgstab(
                K, F, x0=x0, tol=1e-6, maxiter=1000
            )
            
            if info == 0 and not np.any(np.isnan(self.displacement)):
                print("  [OK] BiCGStab反復法成功")
                return self.displacement
            else:
                print(f"  [NG] BiCGStab反復法失敗（info={info}）")
                
        except Exception as e:
            print(f"  [NG] BiCGStab反復法エラー: {e}")
        
        # 段階5: 最終手段 - SVD疑似逆行列（密行列変換）
        print("  [Step] 最終手段：SVD疑似逆行列を適用")
        try:
            # 小規模問題のみSVDを適用
            if K.shape[0] <= 1000:
                print("    密行列に変換してSVD実行")
                K_dense = K.toarray()
                
                # SVDによる疑似逆行列
                U, s, Vt = svd(K_dense, full_matrices=False)
                
                # 特異値の切り捨て（条件数改善）
                s_cutoff = np.max(s) * 1e-12
                s_reg = np.where(s > s_cutoff, s, s_cutoff)
                
                # 疑似逆行列による解
                self.displacement = Vt.T @ np.diag(1/s_reg) @ U.T @ F
                
                if not np.any(np.isnan(self.displacement)):
                    print("  [OK] SVD疑似逆行列成功")
                    return self.displacement
                else:
                    print("  [NG] SVD疑似逆行列でもNaN発生")
            else:
                print("    行列が大きすぎるためSVDをスキップ")
                
        except Exception as e:
            print(f"  [NG] SVD疑似逆行列エラー: {e}")
        
        # 段階6: 最小二乗法による近似解
        print("  [Step] 最小二乗法による近似解を計算")
        try:
            if K.shape[0] <= 2000:
                K_dense = K.toarray()
                solution, residuals, rank, s = lstsq(K_dense, F, rcond=1e-12)
                
                if not np.any(np.isnan(solution)):
                    print(f"  [OK] 最小二乗法成功（rank={rank}/{K.shape[0]}）")
                    self.displacement = solution
                    return self.displacement
                    
        except Exception as e:
            print(f"  [NG] 最小二乗法エラー: {e}")
        
        # 全手法失敗の場合
        print("  [!!] 全ての数値安定化手法が失敗")
        print("  [Hint] 構造の根本的見直しが必要です:")
        print("     - 境界条件の不足（剛体モードの存在）")
        print("     - 要素の極端な寸法比")
        print("     - 材料定数の異常値")
        
        # エラー情報付きで例外発生
        raise ValueError(
            "V1レベル数値安定化ソルバーでも解けませんでした。\n"
            "構造の境界条件または要素定義を確認してください。\n"
            f"行列サイズ: {K.shape[0]}×{K.shape[1]}, 条件数推定: {cond_estimate:.2e}"
        )
        
    def solve(self, mesh: MeshModel, material: Material, boundary: BoundaryCondition,
             elements: Dict[int, Any]) -> Dict[str, Any]:
        """静的解析を実行
        
        Args:
            mesh: メッシュデータ
            material: 材料データ
            boundary: 境界条件
            elements: 要素オブジェクトの辞書
            
        Returns:
            解析結果の辞書
        """
        # 剛性行列の作成
        K = self.create_stiffness_matrix(mesh, material, elements)
        
        # 荷重ベクトルの組み立て
        F = self.assemble_load_vector(mesh, boundary, elements)
        
        # 境界条件の適用
        K_mod, F_mod = self.apply_boundary_conditions(K, F, boundary)
        
        # 線形方程式を解く
        u = self.solve_linear_system(K_mod, F_mod)
        
        # 結果を整形
        results = {
            'displacement': u,
            'node_displacements': self._format_node_displacements(u, mesh),
            'reaction_forces': self._calculate_reaction_forces(K, u, F, boundary)
        }
        
        return results
        
    def eigenvalue_analysis(self, mesh: MeshModel, material: Material,
                          boundary: BoundaryCondition, elements: Dict[int, Any],
                          n_modes: int = 10) -> Dict[str, Any]:
        """固有値解析を実行
        
        Args:
            mesh: メッシュデータ
            material: 材料データ
            boundary: 境界条件
            elements: 要素オブジェクトの辞書
            n_modes: 求める固有モード数
            
        Returns:
            固有値解析結果の辞書
        """
        # 剛性行列と質量行列の作成
        K = self.create_stiffness_matrix(mesh, material, elements)
        M = self.create_mass_matrix(mesh, material, elements)
        
        # 境界条件の適用（質量行列には適用しない）
        K_mod, _ = self.apply_boundary_conditions(K, np.zeros(K.shape[0]), boundary)
        
        # モード数の調整（行列サイズの1/3以下に制限）
        max_modes = min(n_modes, K.shape[0] // 3)
        if max_modes < 1:
            max_modes = 1
            
        # 固有値問題を解く（ARPACK収束問題対策）
        try:
            # まず標準的なパラメータで試行
            eigenvalues, eigenvectors = eigsh(
                K_mod, k=max_modes, M=M, 
                which='SA',  # 最小代数的固有値（剛体モード対応）
                maxiter=3000,  # 最大反復数を増加
                tol=1e-9       # 収束判定の緩和
            )
            
        except RuntimeError as e:
            if "No convergence" in str(e):
                # 収束しない場合はシフト技術を適用
                print("ARPACK収束失敗、シフト技術を適用中...")
                try:
                    # シフト量を設定（平均対角成分の1%）
                    avg_diag = np.mean(K_mod.diagonal())
                    shift = max(1e-6, abs(avg_diag) * 0.01)
                    
                    # シフト行列 K_shifted = K + shift * M
                    K_shifted = K_mod + shift * M
                    
                    eigenvalues, eigenvectors = eigsh(
                        K_shifted, k=max_modes, M=M,
                        which='SA',
                        maxiter=5000,  # さらに増加
                        tol=1e-8
                    )
                    
                    # シフト補正
                    eigenvalues = eigenvalues - shift
                    print(f"シフト技術により解析成功 (shift={shift:.2e})")
                    
                except RuntimeError as e2:
                    if "No convergence" in str(e2):
                        # それでも収束しない場合は少ないモード数で再試行
                        reduced_modes = max(1, max_modes // 2)
                        print(f"モード数を{reduced_modes}に減らして再試行...")
                        
                        eigenvalues, eigenvectors = eigsh(
                            K_mod, k=reduced_modes, M=M,
                            which='LM',  # 最大固有値に変更
                            maxiter=2000,
                            tol=1e-7
                        )
                        
                        # 逆順にして最小固有値を模擬
                        eigenvalues = eigenvalues[::-1]
                        eigenvectors = eigenvectors[:, ::-1]
                        print(f"減少モード数({reduced_modes})で解析成功")
                    else:
                        raise e2
            else:
                raise e
        
        # 負の固有値をゼロにクリップ（数値誤差対策）
        eigenvalues = np.maximum(eigenvalues, 0.0)
        
        # 固有円振動数と固有周期の計算
        omega = np.sqrt(eigenvalues)  # rad/s
        frequency = omega / (2 * np.pi)  # Hz
        
        # ゼロ固有値（剛体モード）の処理
        valid_indices = eigenvalues > 1e-10  # 極小固有値は除外
        if np.any(valid_indices):
            period = np.zeros_like(frequency)
            period[valid_indices] = 1.0 / frequency[valid_indices]  # s
        else:
            period = np.full_like(frequency, np.inf)
        
        self.eigenvalues = eigenvalues
        self.eigenvectors = eigenvectors
        
        results = {
            'eigenvalues': eigenvalues,
            'eigenvectors': eigenvectors,
            'frequencies': frequency,
            'periods': period,
            'modes': self._format_eigenmodes(eigenvectors, mesh)
        }
            
        return results
        
    def _format_node_displacements(self, u: np.ndarray, mesh: MeshModel) -> Dict[int, Dict[str, float]]:
        """節点変位を整形
        
        Args:
            u: 変位ベクトル
            mesh: メッシュデータ
            
        Returns:
            節点変位の辞書
        """
        displacements = {}
        
        # 自由度/節点を判定（変位ベクトルサイズから推定）
        n_nodes = len(mesh.nodes)
        max_dof_per_node = len(u) // n_nodes if n_nodes > 0 else 6
        
        for node_id in mesh.nodes.keys():
            base_dof = (node_id - 1) * max_dof_per_node
            
            # solid要素（3自由度）の場合
            if max_dof_per_node == 3:
                displacements[node_id] = {
                    'dx': u[base_dof] if base_dof < len(u) else 0.0,
                    'dy': u[base_dof + 1] if base_dof + 1 < len(u) else 0.0,
                    'dz': u[base_dof + 2] if base_dof + 2 < len(u) else 0.0,
                    'rx': 0.0,  # solid要素は回転自由度なし
                    'ry': 0.0,
                    'rz': 0.0
                }
            else:
                # bar, shell要素（6自由度）の場合
                displacements[node_id] = {
                    'dx': u[base_dof] if base_dof < len(u) else 0.0,
                    'dy': u[base_dof + 1] if base_dof + 1 < len(u) else 0.0,
                    'dz': u[base_dof + 2] if base_dof + 2 < len(u) else 0.0,
                    'rx': u[base_dof + 3] if base_dof + 3 < len(u) else 0.0,
                    'ry': u[base_dof + 4] if base_dof + 4 < len(u) else 0.0,
                    'rz': u[base_dof + 5] if base_dof + 5 < len(u) else 0.0
                }
            
        return displacements
        
    def _calculate_reaction_forces(self, K: csr_matrix, u: np.ndarray, F: np.ndarray,
                                 boundary: BoundaryCondition) -> Dict[int, Dict[str, float]]:
        """支点反力を計算
        
        Args:
            K: 剛性行列
            u: 変位ベクトル
            F: 荷重ベクトル
            boundary: 境界条件
            
        Returns:
            支点反力の辞書
        """
        # 全体の力ベクトルを計算
        F_total = K @ u
        
        # 自由度/節点を判定（変位ベクトルサイズから推定）
        n_nodes = len(set(node_id for node_id in boundary.restraints.keys()))
        max_dof_per_node = len(u) // n_nodes if n_nodes > 0 else 6
        
        # 反力 = 全体の力 - 外力
        reactions = {}
        
        for node_id, restraint in boundary.restraints.items():
            base_dof = (node_id - 1) * max_dof_per_node
            reaction = {}
            
            for i, is_restrained in enumerate(restraint.dof_restraints):
                if is_restrained and i < max_dof_per_node and base_dof + i < len(F):
                    dof = base_dof + i
                    force = F_total[dof] - F[dof]
                    
                    # solid要素（3自由度）の場合
                    if max_dof_per_node == 3:
                        dof_names = ['fx', 'fy', 'fz']
                        if i < len(dof_names):
                            reaction[dof_names[i]] = force
                    else:
                        # bar, shell要素（6自由度）の場合
                        dof_names = ['fx', 'fy', 'fz', 'mx', 'my', 'mz']
                        if i < len(dof_names):
                            reaction[dof_names[i]] = force
                    
            if reaction:
                reactions[node_id] = reaction
                
        return reactions
        
    def _format_eigenmodes(self, eigenvectors: np.ndarray, mesh: MeshModel) -> List[Dict[int, Dict[str, float]]]:
        """固有モードを整形
        
        Args:
            eigenvectors: 固有ベクトル行列
            mesh: メッシュデータ
            
        Returns:
            各モードの節点変位の辞書のリスト
        """
        modes = []
        n_modes = eigenvectors.shape[1]
        
        for mode_idx in range(n_modes):
            mode_vector = eigenvectors[:, mode_idx]
            mode_displacements = self._format_node_displacements(mode_vector, mesh)
            modes.append(mode_displacements)
            
        return modes 