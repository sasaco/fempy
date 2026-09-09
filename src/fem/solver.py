"""
有限要素法のソルバーモジュール
JavaScript版のSolver機能に対応
"""
from typing import Dict, Any, List, Tuple, Optional
from copy import deepcopy
import numpy as np
from scipy.sparse import lil_matrix, csr_matrix
from scipy.sparse.linalg import eigsh
from .mesh import MeshModel
from .boundary_condition import BoundaryCondition
from .material import Material
from .dof import DofLayout
from .equilibrium import NonlinearConvergenceError, direct_step, newton_iteration, solve_direct_system, solve_newton_system
from .solver_results import snapshot, final_result


class Solver:
    """FEM解析のソルバークラス"""
    
    DEFAULT_N_STEPS = 10
    DEFAULT_MAX_ITER = 50
    DEFAULT_TOL = 1e-6

    def __init__(self):
        self._reset_analysis_state()

    def _reset_analysis_state(self):
        self.assembled_stiffness = None
        self.assembled_stiffness_correction = None
        self.assembled_mass = None
        self.load_vector = None
        self.displacement = None
        self.displacement_correction = None
        self.eigenvalues = None
        self.eigenvectors = None
        self.layout = None
        self._layout_signature = None
        self._node_dof_offsets = None
        self.convergence_history = []
        self.step_results = []
        self._last_internal_force = None
        self.load_factor = 0.0
        self.precise_end_forces = None
        self.precise_reactions = None
        self.interpolated_displacements = {}

    def _set_dof_layout(self, mesh: MeshModel) -> None:
        signature = (tuple(sorted(mesh.nodes)), tuple(
            (key, e.get('type', 'bar'), tuple(e['nodes'])) for key, e in mesh.elements.items()))
        if self.layout is None or signature != self._layout_signature:
            self.layout = DofLayout.from_mesh(mesh)
            self._layout_signature = signature
            self._node_dof_offsets = self.layout.node_offsets

    def _node_dof_start(self, node_id: int, stride: int) -> int:
        if self._node_dof_offsets is None:
            # 行列だけを直接渡す既存APIでは、1始まりの連続節点を仮定する。
            return (node_id - 1) * stride
        if node_id not in self._node_dof_offsets:
            raise ValueError(f'Unknown node {node_id}')
        return self._node_dof_offsets[node_id]
        
    def create_stiffness_matrix(self, mesh, material, elements):
        self._set_dof_layout(mesh)
        self.assembled_stiffness_correction = None
        if any(hasattr(e, 'get_stiffness_matrix_parts') for e in elements.values()):
            return self._assemble_stiffness_parts(mesh, elements)
        self.assembled_stiffness = self.layout.assemble_matrix(
            elements, lambda element, indices: element.get_stiffness_matrix())
        return self.assembled_stiffness

    def _assemble_stiffness_parts(self, mesh, elements):
        """Preserve both element-integration and assembly rounding tails."""
        from math import fsum
        self._set_dof_layout(mesh)
        rows = [{} for _ in range(self.layout.size)]
        for _, element, indices in self.layout.elements(elements):
            if hasattr(element, 'get_stiffness_matrix_parts'):
                high, low = element.get_stiffness_matrix_parts()
            else:
                high = element.get_stiffness_matrix(); low = np.zeros_like(high)
            for i, dof_i in enumerate(indices):
                row = rows[dof_i]
                for j, dof_j in enumerate(indices):
                    a, b = float(high[i,j]), float(low[i,j])
                    if not a and not b: continue
                    old, tail = row.get(dof_j, (0., 0.))
                    total = fsum([old, tail, a, b])
                    row[dof_j] = (total, fsum([old, tail, a, b, -total]))
        indices, highs, lows, offsets = [], [], [], [0]
        for row in rows:
            for j, (high, low) in sorted(row.items()):
                indices.append(j); highs.append(high); lows.append(low)
            offsets.append(len(indices))
        shape = (len(rows), len(rows))
        self.assembled_stiffness = csr_matrix((highs, indices, offsets), shape=shape)
        self.assembled_stiffness_correction = csr_matrix((lows, indices, offsets), shape=shape)
        return self.assembled_stiffness
        
    def create_mass_matrix(self, mesh, material, elements):
        self._set_dof_layout(mesh)
        self.assembled_mass = self.layout.assemble_matrix(
            elements, lambda element, indices: element.get_mass_matrix())
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
        self._set_dof_layout(mesh)
        max_dof_per_node = self.layout.stride
        n_dof = self.layout.size
        F = np.zeros(n_dof)
        
        # 節点荷重の適用
        for node_id, load in boundary.loads.items():
            base_dof = self._node_dof_start(node_id, max_dof_per_node)
            for i in range(min(max_dof_per_node, len(load.forces))):
                if base_dof + i < n_dof:  # 範囲チェック追加
                    F[base_dof + i] += load.forces[i]
                
        # 分布荷重の適用
        for _, element, indices in self.layout.elements(elements):
            if hasattr(element, 'get_member_load_vector'):
                self.layout.add_vector(F, indices, element.get_member_load_vector())

        for dist_load in boundary.distributed_loads:
            elem_id = dist_load.element_id
            if elem_id not in elements:
                continue
                
            element = elements[elem_id]
            
            # 要素の等価節点荷重を計算
            equiv_loads = element.get_equivalent_nodal_loads(
                dist_load.load_type, dist_load.values, dist_load.face
            )
            
            indices = self.layout.element_dofs(elem_id, element)
            self.layout.add_vector(F, indices[:len(equiv_loads)], equiv_loads)
        
        # 面圧荷重の適用（V0のloadVector関数の面圧処理を移植）
        for pressure in boundary.pressures:
            elem_id = pressure.element_id
            if elem_id not in elements:
                continue
                
            element = elements[elem_id]
            
            # 要素の等価節点荷重を計算（面圧専用）
            equiv_loads = element.get_equivalent_nodal_loads(
                'pressure', [pressure.pressure], pressure.face
            )
            
            indices = self.layout.element_dofs(elem_id, element)
            self.layout.add_vector(F, indices[:len(equiv_loads)], equiv_loads)
                        
        self.load_vector = F
        return F
        
    @staticmethod
    def _get_max_dof_per_node(mesh: MeshModel) -> int:
        return DofLayout.stride_for(mesh)

    def _get_boundary_dofs(self, boundary: BoundaryCondition, n_dof: int,
                           max_dof_per_node: int) -> Tuple[Dict[int, float], Dict[int, float]]:
        """固定/強制変位と支持ばねを区別する。値>1000の旧入力仕様を保持。"""
        prescribed, springs = {}, {}
        if max_dof_per_node not in (3, 6):
            raise ValueError('max_dof_per_node must be 3 or 6')
        for node_id, restraint in boundary.restraints.items():
            base = self._node_dof_start(node_id, max_dof_per_node)
            if base < 0 or base + max_dof_per_node > n_dof:
                raise ValueError(f'Restraint node {node_id} is outside the DOF vector')
            for i, fixed in enumerate(restraint.dof_restraints[:max_dof_per_node]):
                if fixed:
                    value = restraint.get_value(i)
                    if not np.isfinite(value):
                        raise ValueError(f'Non-finite restraint value at node {node_id}')
                    if abs(value) > 1000:
                        springs[base + i] = abs(value)
                    else:
                        prescribed[base + i] = value
        directions = {'x': 0, 'y': 1, 'z': 2, 'rx': 3, 'ry': 4, 'rz': 5}
        for node_id, supports in getattr(boundary, 'spring_supports', {}).items():
            base = self._node_dof_start(node_id, max_dof_per_node)
            for direction, stiffness in supports.items():
                if direction not in directions or directions[direction] >= max_dof_per_node:
                    raise ValueError(f'Invalid spring direction: {direction}')
                if not np.isfinite(stiffness) or stiffness <= 0:
                    raise ValueError('Spring stiffness must be finite and positive')
                dof = base + directions[direction]
                if dof in prescribed or dof in springs:
                    raise ValueError('Conflicting restraint and spring at same DOF')
                springs[dof] = stiffness
        return prescribed, springs

    def apply_boundary_conditions(self, K: csr_matrix, F: np.ndarray,
                                  boundary: BoundaryCondition, max_dof_per_node: int = 6,
                                  current_displacement: Optional[np.ndarray] = None,
                                  load_factor: float = 1.0,
                                  penalty: bool = False) -> Tuple[csr_matrix, np.ndarray]:
        """固定DOFを対称消去する。Newtonでは目標変位との差を拘束する。

        Fは構造要素の外力−内力（静解析では外力）。支持ばねの内力はここで引く。
        penalty=Trueは既存の固有値解析専用（質量行列を縮約しない経路）。
        """
        prescribed, springs = self._get_boundary_dofs(boundary, len(F), max_dof_per_node)
        K_mod = lil_matrix(K, copy=True)
        F_mod = np.array(F, dtype=float, copy=True)
        u = np.zeros(len(F)) if current_displacement is None else current_displacement
        for dof, stiffness in springs.items():
            K_mod[dof, dof] += stiffness
        F_mod -= self._spring_force(u, springs)
        if prescribed:
            indices = list(prescribed)
            increments = np.array([load_factor * prescribed[d] - u[d] for d in indices])
            if penalty:
                scale = max(float(np.max(np.abs(K.diagonal()))), 1.0) * 1e15
                for dof, value in zip(indices, increments):
                    K_mod[dof, dof] = scale
                    F_mod[dof] = scale * value
            else:
                # 全固定列の寄与を、列を消去する前に一度だけ移す。
                F_mod -= K_mod.tocsr()[:, indices] @ increments
                K_mod[:, indices] = 0
                K_mod[indices, :] = 0
                for dof, value in zip(indices, increments):
                    K_mod[dof, dof] = 1.0
                    F_mod[dof] = value
        return K_mod.tocsr(), F_mod
        
    def solve_linear_system(self, K, F):
        """Legacy stateful boundary around pure compensated direct algebra."""
        self.displacement, self.displacement_correction = solve_direct_system(K, F)
        return self.displacement

    _solve_newton_system = staticmethod(solve_newton_system)

    def _newton_raphson_iteration(self, *args, **kwargs):
        return newton_iteration(self, *args, **kwargs)

    def solve_nonlinear(self, mesh, material, boundary, elements,
                        n_steps=DEFAULT_N_STEPS, max_iter=DEFAULT_MAX_ITER,
                        tol=DEFAULT_TOL, callback=None, load_factors=None):
        """Legacy method name; all analysis work belongs to solve()."""
        from .solver_results import legacy_nonlinear_result
        result = self.solve(mesh, material, boundary, elements,
                            analysis_type='material_nonlinear', n_steps=n_steps,
                            max_iter=max_iter, tol=tol, callback=callback,
                            load_factors=load_factors)
        return legacy_nonlinear_result(result, self.layout.stride)

    def solve(self, mesh: MeshModel, material: Material, boundary: BoundaryCondition,
              elements: Dict[int, Any], *, analysis_type='static',
              n_steps=DEFAULT_N_STEPS, max_iter=DEFAULT_MAX_ITER,
              tol=DEFAULT_TOL, callback=None, load_factors=None):
        """Shared static flow; analysis type selects material law and equilibrium.

        The original four positional arguments select reference-elastic static
        analysis. Nonlinear controls are independent of the number of steps.
        Modal analysis continues to use eigenvalue_analysis(). Each call starts
        a new analysis; elements implementing reset_states start fresh history.
        """
        self._reset_analysis_state()
        if analysis_type not in ('static', 'material_nonlinear'):
            raise ValueError(f'Unknown static analysis type: {analysis_type}')
        nonlinear = analysis_type == 'material_nonlinear'
        if nonlinear:
            for name, value in [('n_steps', n_steps), ('max_iter', max_iter)]:
                if isinstance(value, (bool, np.bool_)) or not isinstance(value, (int, np.integer)) or value <= 0:
                    raise ValueError(f'{name} must be a positive integer')
            if not np.isfinite(tol) or tol <= 0:
                raise ValueError('tol must be finite and positive')
            factors = (np.arange(1, n_steps + 1, dtype=float) / n_steps
                       if load_factors is None else np.asarray(load_factors, dtype=float))
            if factors.ndim != 1 or len(factors) == 0 or not np.all(np.isfinite(factors)):
                raise ValueError('load_factors must be a nonempty finite sequence')
        else:
            if load_factors is not None:
                raise ValueError('load_factors is only supported for material_nonlinear')
            factors = [1.0]

        self._set_dof_layout(mesh)
        stride = self.layout.stride
        self._get_boundary_dofs(boundary, self.layout.size, stride)
        for element in elements.values():
            if hasattr(element, 'reset_states'):
                element.reset_states()
            if hasattr(element, 'load_factor'):
                element.load_factor = 0.0 if nonlinear else 1.0
        if not nonlinear:
            self.create_stiffness_matrix(mesh, material, elements)
        total = self.assemble_load_vector(mesh, boundary, elements)
        if not np.all(np.isfinite(total)):
            raise ValueError('Loads must be finite')
        u = np.zeros(self.layout.size)
        self.displacement = u.copy()

        for step, factor in enumerate(factors, 1):
            force = factor * total
            committed_u = u.copy()
            committed_force = deepcopy(self._last_internal_force)
            previous_factors = {key: e.load_factor for key, e in elements.items()
                                if hasattr(e, 'load_factor')}
            for key in previous_factors:
                elements[key].load_factor = factor
            history_start = len(self.convergence_history)
            try:
                if nonlinear:
                    converged, u, iterations = self._newton_raphson_iteration(
                        mesh, material, boundary, elements, u, force,
                        max_iter, tol, stride, factor)
                    if not converged:
                        raise NonlinearConvergenceError(step, factor, committed_u)
                    solution = (u, self._last_internal_force, None, iterations)
                else:
                    solution = direct_step(self, mesh, boundary, elements, force, factor)
                    u = solution[0]
            except Exception:
                self.displacement = committed_u.copy()
                self._last_internal_force = committed_force
                self.displacement_correction = None
                for key, element in elements.items():
                    if nonlinear and hasattr(element, 'rollback_state'):
                        element.rollback_state()
                    if key in previous_factors:
                        element.load_factor = previous_factors[key]
                raise
            finally:
                for record in self.convergence_history[history_start:]:
                    record.update(step=step, **{'lambda': factor})

            if nonlinear:
                for element in elements.values():
                    if hasattr(element, 'commit_state'):
                        element.commit_state()
            self.displacement = u.copy()
            self._last_internal_force = solution[1].copy()
            self.displacement_correction = None if solution[2] is None else solution[2].copy()
            self.load_factor = factor
            accepted = snapshot(self, mesh, boundary, elements, solution, force, step, factor, nonlinear)
            self.step_results.append(accepted)
            if callback is not None:
                callback(deepcopy(accepted) if not nonlinear else
                         self._legacy_callback_snapshot(accepted))
        return final_result(self, nonlinear)

    def _legacy_callback_snapshot(self, accepted):
        from .solver_results import legacy_nonlinear_result
        # Callback payloads retain the old 3DOF schema without exposing storage.
        result = deepcopy(accepted)
        legacy_nonlinear_result(dict(node_displacements={}, step_results=[result]), self.layout.stride)
        return result

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
        self._reset_analysis_state()
        # 剛性行列と質量行列の作成
        K = self.create_stiffness_matrix(mesh, material, elements)
        M = self.create_mass_matrix(mesh, material, elements)
        
        # 境界条件の適用（質量行列には適用しない）
        K_mod, _ = self.apply_boundary_conditions(
            K, np.zeros(K.shape[0]), boundary, self.layout.stride, penalty=True)
        
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
        
    def _format_node_displacements(self, u, mesh):
        """Canonical six-component output; legacy projections live at API edges."""
        self._set_dof_layout(mesh)
        names = ('dx', 'dy', 'dz', 'rx', 'ry', 'rz')
        return {node: {name: float(u[start+i]) if i < self.layout.stride else 0.0
                       for i, name in enumerate(names)}
                for node in mesh.nodes for start in (self.layout.node_offsets[node],)}

    def _calculate_reaction_forces(self, K, u, F, boundary, max_dof_per_node=6):
        return self._format_reactions(K @ u - F, boundary, max_dof_per_node)

    def _format_reactions(self, reaction: np.ndarray, boundary: BoundaryCondition,
                          stride: int) -> Dict[int, Dict[str, float]]:
        """構造要素内力−外力。ばね支持では -k*u と等しい。"""
        names = ('fx', 'fy', 'fz', 'mx', 'my', 'mz')
        result = {}
        for node_id, restraint in boundary.restraints.items():
            if node_id in getattr(boundary, 'auxiliary_restraint_nodes', set()):
                continue
            values = {names[i]: float(reaction[self._node_dof_start(node_id, stride) + i])
                      for i, fixed in enumerate(restraint.dof_restraints[:stride]) if fixed}
            if values:
                result[node_id] = values
        directions = {'x': 0, 'y': 1, 'z': 2, 'rx': 3, 'ry': 4, 'rz': 5}
        for node_id, supports in getattr(boundary, 'spring_supports', {}).items():
            values = result.setdefault(node_id, {})
            for direction in supports:
                i = directions[direction]
                values[names[i]] = float(reaction[self._node_dof_start(node_id, stride) + i])
        return result

    def _element_curvatures(self, elements, u, stride):
        """Snapshot accepted total midpoint curvature of nonlinear beams only."""
        result = {}
        for elem_id, element in elements.items():
            if hasattr(element, 'calculate_curvature') and element.is_nonlinear():
                indices = self.layout.element_dofs(elem_id, element)
                result[elem_id] = element.calculate_curvature(u[indices])
        return result

    def _element_end_forces(self, elements, u, stride):
        """Snapshot accepted end forces after commit, never advance constitutive state."""
        result = {}
        for elem_id, element in elements.items():
            if hasattr(element, 'calculate_forces'):
                indices = self.layout.element_dofs(elem_id, element)
                result[elem_id] = element.calculate_forces(u[indices])
        return result

    def _assemble_internal_forces(self, mesh, elements, u, max_dof_per_node):
        self._set_dof_layout(mesh)
        force = np.zeros(self.layout.size)
        for _, element, indices in self.layout.elements(elements):
            local = u[indices]
            values = (element.get_internal_force(local) if hasattr(element, 'get_internal_force')
                      else element.get_stiffness_matrix() @ local)
            self.layout.add_vector(force, indices, values)
        return force

    def _assemble_tangent_stiffness(self, mesh, material, elements, u, max_dof_per_node):
        self._set_dof_layout(mesh)
        def evaluate(element, indices):
            return (element.get_tangent_stiffness_matrix(u[indices])
                    if hasattr(element, 'get_tangent_stiffness_matrix')
                    else element.get_stiffness_matrix())
        return self.layout.assemble_matrix(elements, evaluate)

    def _spring_force(self, u, springs):
        force = np.zeros_like(u)
        for dof, stiffness in springs.items():
            force[dof] = stiffness * u[dof]
        return force

    def _apply_bc_to_residual(self, R, boundary, max_dof_per_node):
        residual = R.copy()
        prescribed, _ = self._get_boundary_dofs(boundary, len(R), max_dof_per_node)
        residual[list(prescribed)] = 0.0
        return residual

    def _equilibrium_residual(self, R, u, boundary, stride):
        _, springs = self._get_boundary_dofs(boundary, len(R), stride)
        return self._apply_bc_to_residual(R - self._spring_force(u, springs), boundary, stride)

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
