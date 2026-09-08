"""
シェル要素クラス
JavaScript版のShellElement.jsに対応
V0技術移植: TriElement1（三角形）とQuadElement1（四角形）の統合実装
"""
from typing import Dict, List, Tuple, Optional, Any
import numpy as np
from .base_element import BaseElement
from ..material import Material, ShellParameter


class ShellElement(BaseElement):
    """シェル要素クラス（3節点三角形または4節点四角形要素）
    
    V0技術移植:
    - TriElement1: 3節点三角形要素（V0/src/ShellElement.js）
    - QuadElement1: 4節点四角形要素
    - V1数値安定化技術: Bar要素100%成功パターンの適用
    """
    
    def __init__(self, element_id: int, node_ids: List[int], material_id: int,
                 thickness: float):
        """
        Args:
            element_id: 要素ID
            node_ids: 構成節点ID（3節点=三角形、4節点=四角形）
            material_id: 材料ID
            thickness: 板厚
        """
        # ✅ V0技術の移植: 3節点（三角形）と4節点（四角形）の両方をサポート
        if len(node_ids) == 3:
            self.element_type = "triangle"
            self.n_nodes = 3
        elif len(node_ids) == 4:
            self.element_type = "quadrilateral"
            self.n_nodes = 4
        else:
            raise ValueError("Shell element must have exactly 3 or 4 nodes")
            
        super().__init__(element_id, node_ids, material_id)
        self.thickness = thickness
        self.material: Optional[Material] = None
        self.shell_param: Optional[ShellParameter] = None
        
        # 🔧 V1数値安定化パラメータ（Bar要素成功パターン移植）
        self.tolerance = 1e-9
        self.max_iterations = 6
        
    def get_name(self) -> str:
        """要素タイプ名を取得（V0互換）"""
        if self.element_type == "triangle":
            return "TriElement1"  # V0互換
        else:
            return "QuadElement1"  # V0互換
        
    def get_dof_per_node(self) -> int:
        """節点あたりの自由度数を取得"""
        return 6  # 3並進 + 3回転
        
    def get_node_count(self) -> int:
        """節点数を取得"""
        return self.n_nodes
        
    def get_matrix_size(self) -> int:
        """要素行列サイズを取得（動的決定）"""
        return self.n_nodes * 6  # 3節点=18x18, 4節点=24x24
        
    def set_material_properties(self, material: Material, 
                              shell_param: Optional[ShellParameter] = None) -> None:
        """材料特性を設定"""
        self.material = material
        self.shell_param = shell_param or ShellParameter(
            thickness=self.thickness,
            material_id=self.material_id
        )
        
    def get_shape_functions(self, xi: np.ndarray) -> np.ndarray:
        """形状関数を取得（V0 TriElement1技術移植）
        
        Args:
            xi: 自然座標 [xi, eta]
            
        Returns:
            形状関数の値
        """
        if self.element_type == "triangle":
            # ✅ V0のTriElement1.prototype.shapeFunction移植
            # JavaScript: [[1-xsi-eta,-1,-1],[xsi,1,0],[eta,0,1]]
            xi_val, eta_val = xi[0], xi[1]
            N = np.array([
                1 - xi_val - eta_val,  # N1
                xi_val,                 # N2  
                eta_val                 # N3
            ])
            return N
        else:
            # 既存の4節点四角形実装
            xi_val, eta_val = xi[0], xi[1]
            N = np.array([
                0.25 * (1 - xi_val) * (1 - eta_val),
                0.25 * (1 + xi_val) * (1 - eta_val),
                0.25 * (1 + xi_val) * (1 + eta_val),
                0.25 * (1 - xi_val) * (1 + eta_val)
            ])
            return N
        
    def get_shape_derivatives(self, xi: np.ndarray) -> np.ndarray:
        """形状関数の微分を取得（V0技術移植）
        
        Args:
            xi: 自然座標 [xi, eta]
            
        Returns:
            形状関数の微分値 (2, n_nodes)
        """
        if self.element_type == "triangle":
            # ✅ V0のTriElement1.prototype.shapeFunction微分移植
            # dN1/dxi = -1, dN1/deta = -1
            # dN2/dxi =  1, dN2/deta =  0  
            # dN3/dxi =  0, dN3/deta =  1
            dN_dxi = np.array([
                [-1, 1, 0],   # dN/dxi
                [-1, 0, 1]    # dN/deta
            ])
            return dN_dxi
        else:
            # 既存の4節点四角形実装
            xi_val, eta_val = xi[0], xi[1]
            dN_dxi = np.array([
                [-0.25 * (1 - eta_val), 0.25 * (1 - eta_val), 
                  0.25 * (1 + eta_val), -0.25 * (1 + eta_val)],
                [-0.25 * (1 - xi_val), -0.25 * (1 + xi_val),
                  0.25 * (1 + xi_val), 0.25 * (1 - xi_val)]
            ])
            return dN_dxi
        
    def get_gauss_points(self) -> Tuple[np.ndarray, np.ndarray]:
        """ガウス積分点と重みを取得（V0技術移植）
        
        Returns:
            (積分点座標, 重み)
        """
        if self.element_type == "triangle":
            # ✅ V0のTRI1_INT移植: 三角形1次要素の積分点
            # [[C1_3,C1_3,0.5]] → 重心点での1点積分
            xi = np.array([
                [1.0/3.0, 1.0/3.0]  # 三角形の重心
            ])
            w = np.array([0.5])  # 三角形の面積重み
            return xi, w
        else:
            # 既存の2x2 ガウス積分（四角形）
            gp_1d = 1.0 / np.sqrt(3)
            xi = np.array([
                [-gp_1d, -gp_1d],
                [ gp_1d, -gp_1d],
                [ gp_1d,  gp_1d],
                [-gp_1d,  gp_1d]
            ])
            w = np.array([1.0, 1.0, 1.0, 1.0])
            return xi, w
            
    def _local_frame(self):
        """Planar shell basis. Invalid geometry must never become support stiffness."""
        coords = np.asarray(self.get_element_coordinates(), dtype=float)
        relative = coords-coords[0]
        scale = np.max(np.linalg.norm(relative, axis=1))
        edge = relative[1]
        normal = np.cross(edge, relative[-1])
        if (not np.all(np.isfinite(coords)) or scale == 0 or
                np.linalg.norm(edge) <= 1e-12*scale or
                np.linalg.norm(normal) <= 1e-12*scale**2):
            raise ValueError(f'Degenerate shell element {self.element_id}')
        ex = edge/np.linalg.norm(edge)
        normal = normal/np.linalg.norm(normal)
        basis = np.array([ex, np.cross(normal, ex), normal])
        local = relative@basis.T
        if np.max(np.abs(local[:, 2])) > 1e-10*scale:
            raise ValueError(f'Non-planar shell element {self.element_id}')
        return local[:, :2], basis

    def _gradient(self, xi, coords):
        derivatives = self.get_shape_derivatives(xi)
        jacobian = derivatives@coords
        determinant = np.linalg.det(jacobian)
        if determinant <= 1e-12*np.linalg.norm(jacobian)**2:
            raise ValueError(f'Degenerate or inverted shell element {self.element_id}')
        return np.linalg.solve(jacobian, derivatives), determinant, jacobian

    def get_jacobian_determinant(self, xi: np.ndarray) -> float:
        coords, _ = self._local_frame()
        return self._gradient(xi, coords)[1]

    def get_stress_strain_matrix(self) -> np.ndarray:
        if self.material is None:
            raise ValueError('Material properties not set')
        return self.material.get_elastic_matrix_plane_stress(self.material_id)

    def _strain_matrices(self, xi, coords):
        gradient, determinant, _ = self._gradient(xi, coords)
        shape = self.get_shape_functions(xi)
        membrane = np.zeros((3, self.get_matrix_size()))
        bending = np.zeros_like(membrane)
        shear = np.zeros((2, self.get_matrix_size()))
        for i, (dx, dy) in enumerate(gradient.T):
            j = 6*i
            membrane[0, j], membrane[1, j+1] = dx, dy
            membrane[2, j:j+2] = [dy, dx]
            # Physical right-handed rotations: u(z)=u0+z*ry, v(z)=v0-z*rx.
            bending[0, j+4], bending[1, j+3] = dx, -dy
            bending[2, j+3:j+5] = [-dx, dy]
            shear[0, j+2], shear[0, j+4] = dx, shape[i]
            shear[1, j+2], shear[1, j+3] = dy, -shape[i]
        return membrane, bending, shear, determinant

    def _assumed_quad_shear(self, xi, coords):
        """MITC4 edge-midpoint covariant shear interpolation (not MITC4+).

        Ko/Lee/Bathe (2016), section 2, equation (5). Transform with the
        Jacobian at each point, including skew quadrilaterals.
        """
        r, s = xi
        covariant = np.zeros((2, self.get_matrix_size()))
        for row, points, weights in (
                (0, [(0., -1.), (0., 1.)], [(1-s)/2, (1+s)/2]),
                (1, [(-1., 0.), (1., 0.)], [(1-r)/2, (1+r)/2])):
            for point, weight in zip(points, weights):
                shear = self._strain_matrices(np.array(point), coords)[2]
                jacobian = self._gradient(np.array(point), coords)[2]
                covariant[row] += weight*(jacobian@shear)[row]
        jacobian = self._gradient(xi, coords)[2]
        return np.linalg.solve(jacobian, covariant)

    def get_stiffness_matrix(self) -> np.ndarray:
        """Planar Mindlin shell in physical global displacement/rotation DOFs.

        Membrane and bending use full integration. Quad transverse shear uses
        MITC4 tying; triangles use three-point integration (thin-plate locking
        remains a limitation). The legacy drilling *difference* regularizer
        has a constant null mode. No diagonal shift or fallback is permitted.
        """
        coords, basis = self._local_frame()
        t = self.thickness
        if not np.isfinite(t) or t <= 0:
            raise ValueError('Shell thickness must be finite and positive')
        elastic = self.get_stress_strain_matrix()
        shear_modulus = self.material.materials[self.material_id].G
        stiffness = np.zeros((self.get_matrix_size(), self.get_matrix_size()))
        if self.n_nodes == 3:
            points = np.array([[1/6, 1/6], [2/3, 1/6], [1/6, 2/3]])
            weights = np.full(3, 1/6)
        else:
            points, weights = self.get_gauss_points()
        area = 0.
        for xi, weight in zip(points, weights):
            membrane, bending, shear, determinant = self._strain_matrices(xi, coords)
            if self.n_nodes == 4:
                shear = self._assumed_quad_shear(xi, coords)
            measure = determinant*weight
            stiffness += measure*(t*membrane.T@elastic@membrane
                                  + t**3/12*bending.T@elastic@bending
                                  + (5/6)*shear_modulus*t*shear.T@shear)
            area += measure
        # V0 ShellElement.js uses drilling differences, not springs to ground.
        # Preserve the former coefficient scale and its constant null mode.
        drill = np.full((self.n_nodes, self.n_nodes), -1/(self.n_nodes-1))
        np.fill_diagonal(drill, 1.)
        indices = np.arange(self.n_nodes)*6+5
        stiffness[np.ix_(indices, indices)] += 1e-3*shear_modulus*t*area/self.n_nodes*drill
        transform = np.kron(np.eye(2*self.n_nodes), basis)
        stiffness = transform.T@stiffness@transform
        return (stiffness+stiffness.T)/2

    def get_mass_matrix(self) -> np.ndarray:
        """シェル要素の質量行列を取得（動的サイズ対応）"""
        if self.material is None:
            raise ValueError("Material properties not set")
            
        coords = self.get_element_coordinates()
        t = self.thickness
        rho = self.material.materials[self.material_id].density
        
        # 動的サイズ（3節点=18x18, 4節点=24x24）
        matrix_size = self.get_matrix_size()
        Me = np.zeros((matrix_size, matrix_size))
        
        # ガウス積分
        xi_gp, w_gp = self.get_gauss_points()
        
        for i, (xi, w) in enumerate(zip(xi_gp, w_gp)):
            # 形状関数
            N = self.get_shape_functions(xi)
            
            # ヤコビアン
            det_J = self.get_jacobian_determinant(xi)
            
            # 質量行列への寄与（集中質量近似）
            for j in range(self.n_nodes):
                for k in range(self.n_nodes):
                    mass_factor = rho * t * N[j] * N[k] * abs(det_J) * w
                    # 並進質量
                    for d in range(3):
                        Me[j*6+d, k*6+d] += mass_factor
                    # 回転慣性（簡略化）
                    for d in range(3, 6):
                        Me[j*6+d, k*6+d] += mass_factor * (t**2 / 12)
                        
        return Me
        
    def calculate_stress_strain(self, displacement: np.ndarray) -> Dict[str, Any]:
        """応力とひずみを計算（動的サイズ対応）
        
        Args:
            displacement: 節点変位ベクトル（18要素=三角形, 24要素=四角形）
            
        Returns:
            応力・ひずみの辞書
        """
        planar, basis = self._local_frame()
        coords = np.column_stack([planar, np.zeros(self.n_nodes)])
        local = np.asarray(displacement).reshape(self.n_nodes, 6).copy()
        local[:, :3] = local[:, :3]@basis.T
        local[:, 3:] = local[:, 3:]@basis.T
        displacement = local.ravel()

        t = self.thickness
        D = self.get_stress_strain_matrix()
        
        # ガウス点での応力・ひずみ
        xi_gp, _ = self.get_gauss_points()
        stress_gp = []
        strain_gp = []
        
        for xi in xi_gp:
            # 形状関数微分
            dN_dxi = self.get_shape_derivatives(xi)
            
            if self.element_type == "triangle":
                det_J = self.get_jacobian_determinant(xi)
                J_inv = np.linalg.inv(dN_dxi @ coords[:, :2])
            else:
                J = dN_dxi @ coords[:, :2]
                J_inv = np.linalg.inv(J)
                
            dN_dx = J_inv @ dN_dxi
            
            # 変位の抽出（動的サイズ）
            u = np.zeros(self.n_nodes * 2)  # 面内変位
            for i in range(self.n_nodes):
                u[i*2] = displacement[i*6]      # u
                u[i*2+1] = displacement[i*6+1]  # v
                
            # ひずみ計算
            B = np.zeros((3, self.n_nodes * 2))
            for j in range(self.n_nodes):
                B[0, j*2] = dN_dx[0, j]
                B[1, j*2+1] = dN_dx[1, j]
                B[2, j*2] = dN_dx[1, j]
                B[2, j*2+1] = dN_dx[0, j]
                
            strain = B @ u
            stress = D @ strain
            
            strain_gp.append(strain)
            stress_gp.append(stress)
            
        return {
            'gauss_points': xi_gp,
            'strain': np.array(strain_gp),
            'stress': np.array(stress_gp)
        }
        
    def get_equivalent_nodal_loads(self, load_type: str, values: List[float],
                                   face: Optional[str] = None) -> np.ndarray:
        """Consistent surface pressure, in force/area, on V0 faces F1/F2.

        F1 follows the node-order normal and positive pressure acts inward.
        F2 reverses that normal. Neither face denotes an edge. See
        docs/v0/src/ShellElement.js::{Tri,Quad}Element1.prototype.border.
        """
        if load_type != 'pressure':
            raise NotImplementedError(f'Unsupported shell load type: {load_type}')
        if face not in ('F1', 'F2'):
            raise ValueError('Shell pressure face must be F1 or F2')
        if len(values) != 1 or not np.isfinite(values[0]):
            raise ValueError('Shell pressure requires one finite value')
        coords, basis = self._local_frame()
        result = np.zeros((self.n_nodes, 6))
        traction = (-1 if face == 'F1' else 1)*float(values[0])*basis[2]
        points, weights = self.get_gauss_points()
        for xi, weight in zip(points, weights):
            measure = self._gradient(xi, coords)[1]*weight
            result[:, :3] += self.get_shape_functions(xi)[:, None]*traction*measure
        return result.ravel()
