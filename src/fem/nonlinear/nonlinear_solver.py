"""
Newton-Raphson非線形ソルバー

材料非線形解析のための反復ソルバー
既存のSolverクラスを継承し、V1レベル数値安定化技術を活用
"""
from typing import Dict, Any, List, Optional, Callable
import numpy as np
from scipy.sparse import lil_matrix, csr_matrix
from scipy.sparse.linalg import spsolve

from ..solver import Solver
from ..mesh import MeshModel
from ..material import Material
from ..boundary_condition import BoundaryCondition


class NonlinearSolver(Solver):
    """Newton-Raphson法による非線形ソルバー

    線形ソルバー(Solver)を継承し、非線形解析機能を追加

    Attributes:
        convergence_history: 収束履歴のリスト
    """

    # デフォルトパラメータ
    DEFAULT_N_STEPS = 10
    DEFAULT_MAX_ITER = 50
    DEFAULT_TOL = 1e-6
    DEFAULT_MIN_STEP_SIZE = 0.01

    def __init__(self):
        super().__init__()
        self.convergence_history: List[Dict[str, Any]] = []

    def solve_nonlinear(
        self,
        mesh: MeshModel,
        material: Material,
        boundary: BoundaryCondition,
        elements: Dict[int, Any],
        n_steps: int = DEFAULT_N_STEPS,
        max_iter: int = DEFAULT_MAX_ITER,
        tol: float = DEFAULT_TOL,
        callback: Optional[Callable] = None
    ) -> Dict[str, Any]:
        """材料非線形解析を実行

        Newton-Raphson法による荷重増分解析

        Args:
            mesh: メッシュデータ
            material: 材料データ
            boundary: 境界条件
            elements: 要素オブジェクトの辞書
            n_steps: 荷重増分ステップ数
            max_iter: 各ステップの最大反復回数
            tol: 収束判定許容差
            callback: 各ステップ完了時のコールバック関数

        Returns:
            解析結果の辞書
        """
        print("=== 材料非線形解析開始 ===")
        print(f"  - 荷重ステップ数: {n_steps}")
        print(f"  - 最大反復数: {max_iter}")
        print(f"  - 収束判定許容差: {tol}")

        # 自由度数の決定
        max_dof_per_node = self._get_max_dof_per_node(mesh)
        n_dof = len(mesh.nodes) * max_dof_per_node

        # 全荷重ベクトルの組み立て
        F_total = self.assemble_load_vector(mesh, boundary, elements)

        # 初期変位
        u = np.zeros(n_dof)

        # 結果格納用
        step_results: List[Dict[str, Any]] = []
        self.convergence_history = []

        # 荷重増分ループ
        for step in range(n_steps):
            lambda_factor = (step + 1) / n_steps
            F_ext = lambda_factor * F_total

            print(f"\n--- Step {step + 1}/{n_steps} (lambda = {lambda_factor:.3f}) ---")

            # Newton-Raphson反復
            converged, u, n_iter = self._newton_raphson_iteration(
                mesh, material, boundary, elements,
                u, F_ext, max_iter, tol, max_dof_per_node
            )

            if not converged:
                print(f"  [警告] Step {step + 1}で収束しませんでした")
                # 状態をロールバック
                for elem_id, element in elements.items():
                    if hasattr(element, 'rollback_state'):
                        element.rollback_state()
            else:
                # 状態変数をコミット
                for elem_id, element in elements.items():
                    if hasattr(element, 'commit_state'):
                        element.commit_state()

            # ステップ結果を保存
            step_result = {
                'step': step + 1,
                'lambda': lambda_factor,
                'displacement': u.copy(),
                'converged': converged,
                'iterations': n_iter
            }
            step_results.append(step_result)

            # コールバック
            if callback is not None:
                callback(step_result)

        # 最終結果の整形
        results = {
            'displacement': u,
            'node_displacements': self._format_node_displacements(u, mesh),
            'step_results': step_results,
            'convergence_history': self.convergence_history,
            'analysis_type': 'material_nonlinear'
        }

        print("\n=== 材料非線形解析完了 ===")

        return results

    def _newton_raphson_iteration(
        self,
        mesh: MeshModel,
        material: Material,
        boundary: BoundaryCondition,
        elements: Dict[int, Any],
        u_init: np.ndarray,
        F_ext: np.ndarray,
        max_iter: int,
        tol: float,
        max_dof_per_node: int
    ) -> tuple:
        """Newton-Raphson反復を実行

        Args:
            mesh: メッシュデータ
            material: 材料データ
            boundary: 境界条件
            elements: 要素辞書
            u_init: 初期変位ベクトル
            F_ext: 外力ベクトル
            max_iter: 最大反復回数
            tol: 収束許容差
            max_dof_per_node: 節点あたり最大自由度

        Returns:
            (converged, u, n_iter): 収束フラグ、変位、反復回数
        """
        u = u_init.copy()
        du = np.zeros_like(u)

        for iteration in range(max_iter):
            # 内力ベクトルの組み立て
            F_int = self._assemble_internal_forces(mesh, elements, u, max_dof_per_node)

            # 残差ベクトル
            R = F_ext - F_int

            # 境界条件を考慮した残差
            R_mod = self._apply_bc_to_residual(R, boundary, max_dof_per_node)

            # 収束判定
            R_norm = np.linalg.norm(R_mod)
            F_norm = max(np.linalg.norm(F_ext), 1.0)
            relative_residual = R_norm / F_norm

            # 変位増分ノルム（初回以降）
            if iteration > 0:
                du_norm = np.linalg.norm(du)
                u_norm = max(np.linalg.norm(u), 1.0)
                relative_du = du_norm / u_norm
            else:
                relative_du = float('inf')

            # 収束履歴を記録
            self.convergence_history.append({
                'iteration': iteration + 1,
                'residual_norm': R_norm,
                'relative_residual': relative_residual,
                'relative_du': relative_du if iteration > 0 else None
            })

            print(f"    Iter {iteration + 1}: |R|/|F| = {relative_residual:.2e}", end='')
            if iteration > 0:
                print(f", |du|/|u| = {relative_du:.2e}")
            else:
                print()

            # 収束判定
            if relative_residual < tol:
                if iteration == 0 or relative_du < tol:
                    print(f"    収束しました (iteration = {iteration + 1})")
                    return True, u, iteration + 1

            # 接線剛性行列の組み立て
            K_tan = self._assemble_tangent_stiffness(mesh, material, elements, u, max_dof_per_node)

            # 境界条件の適用
            K_mod, R_mod = self.apply_boundary_conditions(K_tan, R, boundary)

            # 変位増分の計算
            try:
                du = self.solve_linear_system(K_mod, R_mod)
            except ValueError as e:
                print(f"    [エラー] 線形ソルバーが失敗: {e}")
                return False, u, iteration + 1

            # 変位の更新
            u = u + du

        # 最大反復数に到達
        print(f"    最大反復数 ({max_iter}) に到達、収束せず")
        return False, u, max_iter

    def _assemble_internal_forces(
        self,
        mesh: MeshModel,
        elements: Dict[int, Any],
        u: np.ndarray,
        max_dof_per_node: int
    ) -> np.ndarray:
        """内力ベクトルを組み立て

        Args:
            mesh: メッシュデータ
            elements: 要素辞書
            u: 変位ベクトル
            max_dof_per_node: 節点あたり最大自由度

        Returns:
            内力ベクトル
        """
        n_dof = len(u)
        F_int = np.zeros(n_dof)

        for elem_id, elem_data in mesh.elements.items():
            if elem_id not in elements:
                continue

            element = elements[elem_id]
            node_ids = elem_data['nodes']

            # 要素の自由度インデックス
            dof_indices = []
            elem_dof_per_node = element.get_dof_per_node()
            for node_id in node_ids:
                base_dof = (node_id - 1) * max_dof_per_node
                for i in range(elem_dof_per_node):
                    dof_indices.append(base_dof + i)

            # 要素変位を抽出
            u_elem = np.array([u[i] if i < n_dof else 0.0 for i in dof_indices])

            # 要素内力を計算
            if hasattr(element, 'get_internal_force'):
                f_elem = element.get_internal_force(u_elem)
            else:
                # 線形要素の場合は剛性行列×変位
                K_elem = element.get_stiffness_matrix()
                f_elem = K_elem @ u_elem

            # 全体ベクトルに組み込み
            for i, dof in enumerate(dof_indices):
                if dof < n_dof:
                    F_int[dof] += f_elem[i]

        return F_int

    def _assemble_tangent_stiffness(
        self,
        mesh: MeshModel,
        material: Material,
        elements: Dict[int, Any],
        u: np.ndarray,
        max_dof_per_node: int
    ) -> csr_matrix:
        """接線剛性行列を組み立て

        Args:
            mesh: メッシュデータ
            material: 材料データ
            elements: 要素辞書
            u: 変位ベクトル
            max_dof_per_node: 節点あたり最大自由度

        Returns:
            接線剛性行列（CSR形式）
        """
        n_dof = len(u)
        K_global = lil_matrix((n_dof, n_dof))

        for elem_id, elem_data in mesh.elements.items():
            if elem_id not in elements:
                continue

            element = elements[elem_id]
            node_ids = elem_data['nodes']

            # 要素の自由度インデックス
            dof_indices = []
            elem_dof_per_node = element.get_dof_per_node()
            for node_id in node_ids:
                base_dof = (node_id - 1) * max_dof_per_node
                for i in range(elem_dof_per_node):
                    dof_indices.append(base_dof + i)

            # 要素変位を抽出
            u_elem = np.array([u[i] if i < n_dof else 0.0 for i in dof_indices])

            # 接線剛性行列を取得
            if hasattr(element, 'get_tangent_stiffness_matrix'):
                K_elem = element.get_tangent_stiffness_matrix(u_elem)
            else:
                K_elem = element.get_stiffness_matrix()

            # 全体行列に組み込み
            for i, dof_i in enumerate(dof_indices):
                for j, dof_j in enumerate(dof_indices):
                    if dof_i < n_dof and dof_j < n_dof:
                        K_global[dof_i, dof_j] += K_elem[i, j]

        return K_global.tocsr()

    def _apply_bc_to_residual(
        self,
        R: np.ndarray,
        boundary: BoundaryCondition,
        max_dof_per_node: int
    ) -> np.ndarray:
        """境界条件を残差ベクトルに適用

        拘束自由度の残差を0にする

        Args:
            R: 残差ベクトル
            boundary: 境界条件
            max_dof_per_node: 節点あたり最大自由度

        Returns:
            修正された残差ベクトル
        """
        R_mod = R.copy()

        for node_id, restraint in boundary.restraints.items():
            base_dof = (node_id - 1) * max_dof_per_node

            for i, is_restrained in enumerate(restraint.dof_restraints):
                if is_restrained and i < max_dof_per_node:
                    dof = base_dof + i
                    if dof < len(R_mod):
                        R_mod[dof] = 0.0

        return R_mod

    def _get_max_dof_per_node(self, mesh: MeshModel) -> int:
        """最大自由度/節点を決定

        Args:
            mesh: メッシュデータ

        Returns:
            節点あたりの最大自由度数
        """
        has_solid_only = True
        for elem_data in mesh.elements.values():
            elem_type = elem_data.get('type', 'bar')
            if elem_type not in ['tetra', 'hexa', 'wedge']:
                has_solid_only = False
                break

        return 3 if has_solid_only else 6

    def _format_node_displacements(
        self,
        u: np.ndarray,
        mesh: MeshModel
    ) -> Dict[int, Dict[str, float]]:
        """変位ベクトルを節点変位に整形

        Args:
            u: 変位ベクトル
            mesh: メッシュデータ

        Returns:
            節点ID -> {dx, dy, dz, rx, ry, rz} の辞書
        """
        max_dof_per_node = self._get_max_dof_per_node(mesh)
        node_displacements: Dict[int, Dict[str, float]] = {}

        for node_id in mesh.nodes.keys():
            base_dof = (node_id - 1) * max_dof_per_node

            if max_dof_per_node == 6:
                node_displacements[node_id] = {
                    'dx': u[base_dof] if base_dof < len(u) else 0.0,
                    'dy': u[base_dof + 1] if base_dof + 1 < len(u) else 0.0,
                    'dz': u[base_dof + 2] if base_dof + 2 < len(u) else 0.0,
                    'rx': u[base_dof + 3] if base_dof + 3 < len(u) else 0.0,
                    'ry': u[base_dof + 4] if base_dof + 4 < len(u) else 0.0,
                    'rz': u[base_dof + 5] if base_dof + 5 < len(u) else 0.0
                }
            else:
                node_displacements[node_id] = {
                    'dx': u[base_dof] if base_dof < len(u) else 0.0,
                    'dy': u[base_dof + 1] if base_dof + 1 < len(u) else 0.0,
                    'dz': u[base_dof + 2] if base_dof + 2 < len(u) else 0.0
                }

        return node_displacements
