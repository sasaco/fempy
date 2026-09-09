"""Equilibrium strategies; numerical policies are deliberately distinct.

Direct algebra uses diagonal scaling and compensated refinement; Newton algebra
uses row-maximum scaling, a rank check and the existing increment residual bound.
The algebra functions return values without mutating accepted solver state.
"""
import warnings
from typing import Any, Dict
import numpy as np
from scipy.sparse import csr_matrix, diags
from scipy.sparse.linalg import splu, MatrixRankWarning
from .mesh import MeshModel
from .material import Material
from .boundary_condition import BoundaryCondition
from .precision import sparse_product, add_correction

class NonlinearConvergenceError(RuntimeError):
    """荷重ステップ失敗。displacementは最後に収束した変位のコピー。"""

    def __init__(self, step: int, load_factor: float, displacement: np.ndarray):
        super().__init__(f'Nonlinear analysis did not converge at step {step} '
                         f'(load factor {load_factor:g})')
        self.step = step
        self.load_factor = load_factor
        self.displacement = displacement.copy()

def solve_direct_system(K: csr_matrix, F: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    """Equilibrated direct solve; a mechanism is an error even at zero load."""
    from scipy.sparse.linalg import splu
    K = K.tocsr()
    if not np.all(np.isfinite(K.data)) or not np.all(np.isfinite(F)):
        raise ValueError('Linear system must be finite')
    diagonal = np.abs(K.diagonal())
    if np.any(diagonal <= 0):
        raise ValueError('Singular stiffness matrix: zero diagonal')
    scale = 1/np.sqrt(diagonal)
    D = diags(scale)
    equilibrated = (D@K@D).tocsc()
    try:
        lu = splu(equilibrated)
    except RuntimeError as error:
        raise ValueError('Singular stiffness matrix') from error
    pivots = np.abs(lu.U.diagonal())
    if np.min(pivots) <= np.finfo(float).eps * K.shape[0] * max(1., np.max(pivots)):
        raise ValueError('Singular stiffness matrix: numerical rank deficiency')
    u = scale*lu.solve(scale*F)
    # Keep the displacement's low part and product rounding errors during
    # refinement. Ordinary K*u cannot resolve small support reactions.
    low = np.zeros_like(u)
    for _ in range(4):
        residual = F-sparse_product(K, u, low)
        u, low = add_correction(u, low, scale*lu.solve(scale*residual))
    residual = sparse_product(K, u, low)-F
    bound = np.linalg.norm(np.abs(equilibrated)@np.abs(u/scale)+np.abs(scale*F), ord=np.inf)
    if not np.all(np.isfinite(u)) or np.linalg.norm(scale*residual, ord=np.inf) > 1e-10*max(bound, np.finfo(float).tiny):
        raise ValueError('Linear system failed equilibrium check')
    return u, low

def solve_newton_system(K: csr_matrix, R: np.ndarray) -> np.ndarray:
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
            factorization = splu((D @ K @ D).tocsc())
            pivots = np.abs(factorization.U.diagonal())
            rank_floor = np.finfo(float).eps * K.shape[0] * np.max(pivots)
            if np.min(pivots) <= rank_floor:
                raise ValueError('Numerically singular Newton stiffness')
            scaled_solution = factorization.solve(scaling * R)
    except (MatrixRankWarning, RuntimeError) as error:
        raise ValueError('Singular Newton stiffness') from error
    du = scaling * scaled_solution
    if not np.all(np.isfinite(du)):
        raise ValueError('Non-finite Newton increment')
    error = np.linalg.norm(K @ du - R, ord=np.inf)
    if error > 1e-8 * max(np.linalg.norm(R, ord=np.inf), 1.0):
        raise ValueError('Newton equation residual exceeds tolerance')
    return du

def direct_step(self, mesh, boundary, elements, F, factor):
    """One structural solve, retaining the existing compensated refinements."""
    K = self.assembled_stiffness
    # 境界条件の適用
    stride = self.layout.stride
    K_mod, F_mod = self.apply_boundary_conditions(K, F, boundary, stride, load_factor=factor)

    # 線形方程式を解く
    # A rotation released by every incident beam is absent from the model,
    # not a structural mechanism. Eliminate only exactly empty, unloaded
    # rotational rows; free translations and coupled rigid modes still fail.
    empty = np.asarray(np.abs(K_mod).sum(axis=1)).ravel() == 0
    absent = empty & (np.arange(len(F_mod)) % stride >= 3) & (F_mod == 0)
    active = np.flatnonzero(~absent)
    if np.any(absent):
        u = np.zeros(len(F_mod))
        u[active], active_low = solve_direct_system(K_mod[active][:, active], F_mod[active])
        low = np.zeros_like(u)
        low[active] = active_low
    else:
        u, low = solve_direct_system(K_mod, F_mod)

    correction, internal = _refine_beam_equilibrium(self, mesh, boundary, elements, K_mod, F, u, low, absent)

    return u, internal, correction, 1

def _refine_beam_equilibrium(self, mesh, boundary, elements, constrained_k, loads, u, low, absent):
    """Refine linear frames with element forces and two-part displacements.

    The assembled matrix is only the correction operator. Its large
    diagonal terms can lose the short member's small deformation, so the
    residual is evaluated from the constitutive element kinematics.
    """
    from math import fsum
    from scipy.sparse.linalg import splu
    from .elements.loaded_bar_element import LoadedBarElement
    if not elements or not all(isinstance(e, LoadedBarElement) for e in elements.values()):
        if getattr(self, 'assembled_stiffness_correction', None) is not None:
            return _refine_stiffness_parts(self, boundary, constrained_k, loads, u, low, absent)
        return low, sparse_product(self.assembled_stiffness, u, low)
    prescribed, springs = self._get_boundary_dofs(boundary, len(u), 6)
    free = np.array([i for i in range(len(u)) if i not in prescribed and not absent[i]], dtype=int)
    indices = {key: ix for key, _, ix in self.layout.elements(elements)}
    lu = None
    for iteration in range(16):
        terms = [[] for _ in u]
        force_scale = max(1., np.max(np.abs(loads)))
        for key, element in elements.items():
            ix = indices[key]
            values = element.get_internal_force(u[ix], displacement_correction=low[ix])
            force_scale = max(force_scale, np.max(np.abs(values)))
            for dof, value in zip(ix, values):
                terms[dof].append(value)
        internal = np.array([fsum(row) for row in terms])
        residual = loads-internal
        for dof, stiffness in springs.items():
            residual[dof] = fsum([residual[dof], -stiffness*u[dof], -stiffness*low[dof]])
        if not len(free) or np.max(np.abs(residual[free])) <= 1e-11*force_scale:
            return low, internal
        if lu is None:
            k = constrained_k[free][:, free]
            scale = 1/np.sqrt(np.abs(k.diagonal()))
            lu = splu((diags(scale)@k@diags(scale)).tocsc())
        delta = scale*lu.solve(scale*residual[free])
        # Error-free TwoSum retains the part rounded off by u += delta.
        u[free], low[free] = add_correction(u[free], low[free], delta)
    raise ValueError('Linear frame failed constitutive equilibrium refinement')

def _refine_stiffness_parts(self, boundary, constrained_k, loads, u, low, absent):
    from scipy.sparse.linalg import splu
    from math import fsum
    prescribed, springs = self._get_boundary_dofs(boundary, len(u), self.layout.stride)
    free = np.array([i for i in range(len(u)) if i not in prescribed and not absent[i]], dtype=int)
    lu = None
    for _ in range(16):
        internal = sparse_product(self.assembled_stiffness, u, low)
        internal += sparse_product(self.assembled_stiffness_correction, u, low)
        residual = loads-internal
        for dof, stiffness in springs.items():
            residual[dof] = fsum([residual[dof], -stiffness*u[dof], -stiffness*low[dof]])
        # Loads or reactions on prescribed DOFs must not hide an error on
        # an independently loaded free DOF.
        scale_force = max(1., max(np.abs(loads[free]), default=0.))
        if not len(free) or np.max(np.abs(residual[free])) <= 1e-11*scale_force:
            return low, internal
        if lu is None:
            k = constrained_k[free][:, free]
            scale = 1/np.sqrt(np.abs(k.diagonal()))
            lu = splu((diags(scale)@k@diags(scale)).tocsc())
        u[free], low[free] = add_correction(u[free], low[free], scale*lu.solve(scale*residual[free]))
    raise ValueError('Quadratic solid failed compensated stiffness equilibrium refinement')

def newton_iteration(
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
    prescribed, _ = self._get_boundary_dofs(boundary, len(u), max_dof_per_node)
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
        R_mod = self._equilibrium_residual(R, u, boundary, max_dof_per_node)

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
                # Zero residual does not establish uniqueness in an unrestrained
                # system. Check the constrained tangent before accepting it.
                if iteration == 0:
                    tangent = self._assemble_tangent_stiffness(
                        mesh, material, elements, u, max_dof_per_node)
                    constrained, rhs = self.apply_boundary_conditions(
                        tangent, R, boundary, max_dof_per_node,
                        current_displacement=u, load_factor=load_factor)
                    try:
                        self._solve_newton_system(constrained, rhs)
                    except ValueError:
                        return False, u, iteration + 1
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

        # A reversal starts with the tangent stored at the committed point.
        # Its full Newton step can jump across both skeletons and oscillate.
        # Backtrack using free-DOF equilibrium, always from committed history.
        for backtrack in range(24):
            increment = du * (0.5 ** backtrack)
            candidate = u + increment
            candidate_force = self._assemble_internal_forces(
                mesh, elements, candidate, max_dof_per_node)
            candidate_norm = np.linalg.norm(self._equilibrium_residual(
                F_ext - candidate_force, candidate, boundary, max_dof_per_node))
            if (candidate_norm < tol * F_norm or
                    candidate_norm <= (1 - 1e-4 * (0.5 ** backtrack)) * R_norm):
                u, du = candidate, increment
                break
        else:
            return False, u, iteration + 1

    # 最大反復数に到達
    print(f"    最大反復数 ({max_iter}) に到達、収束せず")
    return False, u, max_iter
