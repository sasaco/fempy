"""
Newton-Raphson非線形ソルバー

材料非線形解析のための反復ソルバー
既存のSolverクラスを継承し、拘束消去と非正則化のNewton方程式解法を使用
"""
from typing import Dict, Any, List, Optional, Callable
import numpy as np
import warnings
from scipy.sparse import lil_matrix, csr_matrix, diags
from scipy.sparse.linalg import spsolve, MatrixRankWarning

from ..solver import Solver
from ..mesh import MeshModel
from ..material import Material
from ..boundary_condition import BoundaryCondition


class NonlinearConvergenceError(RuntimeError):
    """荷重ステップ失敗。displacementは最後に収束した変位のコピー。"""

    def __init__(self, step: int, load_factor: float, displacement: np.ndarray):
        super().__init__(f'Nonlinear analysis did not converge at step {step} '
                         f'(load factor {load_factor:g})')
        self.step = step
        self.load_factor = load_factor
        self.displacement = displacement.copy()


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

    def __init__(self):
        super().__init__()
        self.convergence_history: List[Dict[str, Any]] = []
        self._last_internal_force: Optional[np.ndarray] = None

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

        Raises:
            NonlinearConvergenceError: 荷重ステップが収束せず、最後の確定状態へ戻した場合。
            ValueError: 反復パラメータや境界条件が無効な場合。
        """
        for name, value in [('n_steps', n_steps), ('max_iter', max_iter)]:
            if isinstance(value, (bool, np.bool_)) or not isinstance(value, (int, np.integer)) or value <= 0:
                raise ValueError(f'{name} must be a positive integer')
        if not np.isfinite(tol) or tol <= 0:
            raise ValueError('tol must be finite and positive')
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
        self.displacement = u.copy()
        self._last_internal_force = None

        # 結果格納用
        step_results: List[Dict[str, Any]] = []
        self.convergence_history = []

        # 荷重増分ループ
        for step in range(n_steps):
            lambda_factor = (step + 1) / n_steps
            F_ext = lambda_factor * F_total

            print(f"\n--- Step {step + 1}/{n_steps} (lambda = {lambda_factor:.3f}) ---")

            # Newton-Raphson反復
            committed_u = u.copy()
            history_start = len(self.convergence_history)
            try:
                converged, u, n_iter = self._newton_raphson_iteration(
                    mesh, material, boundary, elements,
                    u, F_ext, max_iter, tol, max_dof_per_node, lambda_factor
                )
                if not converged:
                    raise NonlinearConvergenceError(step + 1, lambda_factor, committed_u)
            except Exception:
                self.displacement = committed_u.copy()
                for element in elements.values():
                    if hasattr(element, 'rollback_state'):
                        element.rollback_state()
                raise
            finally:
                for record in self.convergence_history[history_start:]:
                    record['step'] = step + 1
                    record['lambda'] = lambda_factor

            for element in elements.values():
                if hasattr(element, 'commit_state'):
                    element.commit_state()
            self.displacement = u.copy()

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
            'reaction_forces': self._format_reactions(
                self._last_internal_force - F_total, boundary, max_dof_per_node),
            'converged': True,
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
        max_dof_per_node: int,
        load_factor: float = 1.0
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
        prescribed, springs = self._get_boundary_dofs(boundary, len(u), max_dof_per_node)
        # このステップの強制変位を先に満たす。反復ごとに加算しない。
        for dof, value in prescribed.items():
            u[dof] = load_factor * value

        for iteration in range(max_iter):
            # 内力ベクトルの組み立て
            F_int = self._assemble_internal_forces(mesh, elements, u, max_dof_per_node)

            # 残差ベクトル
            R = F_ext - F_int
            if not np.all(np.isfinite(R)):
                return False, u, iteration + 1
            R_with_springs = R.copy()
            for dof, stiffness in springs.items():
                R_with_springs[dof] -= stiffness * u[dof]

            # 境界条件を考慮した残差
            R_mod = self._apply_bc_to_residual(R_with_springs, boundary, max_dof_per_node)

            # 収束判定
            R_norm = np.linalg.norm(R_mod)
            # 固定DOFへ直接加えた荷重は自由DOFの釣合い精度の尺度に含めない。
            F_free = self._apply_bc_to_residual(F_ext, boundary, max_dof_per_node)
            F_norm = max(np.linalg.norm(F_free), 1.0)
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
                    self._last_internal_force = F_int.copy()
                    return True, u, iteration + 1

            # 接線剛性行列の組み立て
            K_tan = self._assemble_tangent_stiffness(mesh, material, elements, u, max_dof_per_node)

            # 境界条件の適用
            K_mod, R_mod = self.apply_boundary_conditions(
                K_tan, R, boundary, max_dof_per_node,
                current_displacement=u, load_factor=load_factor)

            # 変位増分の計算
            try:
                du = self._solve_newton_system(K_mod, R_mod)
            except ValueError as e:
                print(f"    [エラー] 線形ソルバーが失敗: {e}")
                return False, u, iteration + 1

            # 変位の更新
            u = u + du

        # 最大反復数に到達
        print(f"    最大反復数 ({max_iter}) に到達、収束せず")
        return False, u, max_iter

    @staticmethod
    def _solve_newton_system(K: csr_matrix, R: np.ndarray) -> np.ndarray:
        """対称スケーリング後に直接解法で解く。人工剛性による正則化は行わない。"""
        if not np.all(np.isfinite(K.data)) or not np.all(np.isfinite(R)):
            raise ValueError('Non-finite Newton equation')
        row_scale = np.asarray(abs(K).max(axis=1).toarray()).ravel()
        if np.any(row_scale == 0):
            raise ValueError('Singular Newton stiffness: zero row')
        scaling = 1.0 / np.sqrt(row_scale)
        D = diags(scaling)
        try:
            with warnings.catch_warnings():
                warnings.simplefilter('error', MatrixRankWarning)
                scaled_solution = spsolve((D @ K @ D).tocsc(), scaling * R)
        except (MatrixRankWarning, RuntimeError) as error:
            raise ValueError('Singular Newton stiffness') from error
        du = scaling * scaled_solution
        if not np.all(np.isfinite(du)):
            raise ValueError('Non-finite Newton increment')
        error = np.linalg.norm(K @ du - R, ord=np.inf)
        if error > 1e-8 * max(np.linalg.norm(R, ord=np.inf), 1.0):
            raise ValueError('Newton equation residual exceeds tolerance')
        return du

    def _format_reactions(self, reaction: np.ndarray, boundary: BoundaryCondition,
                          stride: int) -> Dict[int, Dict[str, float]]:
        """構造要素内力−外力。ばね支持では -k*u と等しい。"""
        names = ('fx', 'fy', 'fz', 'mx', 'my', 'mz')
        result = {}
        for node_id, restraint in boundary.restraints.items():
            values = {names[i]: float(reaction[self._node_dof_start(node_id, stride) + i])
                      for i, fixed in enumerate(restraint.dof_restraints[:stride]) if fixed}
            if values:
                result[node_id] = values
        return result

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
                base_dof = self._node_dof_start(node_id, max_dof_per_node)
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
                base_dof = self._node_dof_start(node_id, max_dof_per_node)
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

        prescribed, _ = self._get_boundary_dofs(boundary, len(R), max_dof_per_node)
        R_mod[list(prescribed)] = 0.0

        return R_mod

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
            base_dof = self._node_dof_start(node_id, max_dof_per_node)

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
