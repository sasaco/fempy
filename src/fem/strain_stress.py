"""
ひずみ・応力計算モジュール

歪から応力への変換および要素ごとの応力計算を行う
"""
import numpy as np
import math
from typing import List, Tuple, Dict, Any, Optional
from .models.fa_shell import FA_Shell
from .models.fa_node import FA_Node
from .models.fa_solid import FA_Solid
from .stiffness_matrix import (
    jacobianMatrix, jacobInv, tri_shapeFunction, tri_shapeFunction3,
    tri_strainMatrix1, tri_strainMatrix2, quad_shapeFunction, strainMatrix1,
    tetra_jacobian, tetra_grad, tetra_strainMatrix, material_matrix_3d,
    TRI1_NODE, QUAD1_NODE, tri_shapeFunction2, jacobian,
    quadstiffPart, trianglestiffPart, shapeFunction
)


class Strain:
    """ひずみクラス - JavaScript版のStrainクラスに相当"""
    def __init__(self, values: List[float]):
        self.xx = values[0]
        self.yy = values[1]
        self.zz = values[2] if len(values) > 2 else 0.0
        self.xy = values[3] if len(values) > 3 else 0.0
        self.yz = values[4] if len(values) > 4 else 0.0
        self.zx = values[5] if len(values) > 5 else 0.0
    
    def rotate(self, d: np.ndarray) -> None:
        """ひずみを回転させる"""
        xx = self.xx
        yy = self.yy
        zz = self.zz
        xy = self.xy
        yz = self.yz
        zx = self.zx
        
        d00 = d[0, 0]
        d01 = d[0, 1]
        d02 = d[0, 2]
        d10 = d[1, 0]
        d11 = d[1, 1]
        d12 = d[1, 2]
        d20 = d[2, 0]
        d21 = d[2, 1]
        d22 = d[2, 2]
        
        self.xx = d00*d00*xx + d01*d01*yy + d02*d02*zz + 2*d00*d01*xy + 2*d01*d02*yz + 2*d02*d00*zx
        self.yy = d10*d10*xx + d11*d11*yy + d12*d12*zz + 2*d10*d11*xy + 2*d11*d12*yz + 2*d12*d10*zx
        self.zz = d20*d20*xx + d21*d21*yy + d22*d22*zz + 2*d20*d21*xy + 2*d21*d22*yz + 2*d22*d20*zx
        self.xy = d00*d10*xx + d01*d11*yy + d02*d12*zz + (d00*d11+d01*d10)*xy + (d01*d12+d02*d11)*yz + (d02*d10+d00*d12)*zx
        self.yz = d10*d20*xx + d11*d21*yy + d12*d22*zz + (d10*d21+d11*d20)*xy + (d11*d22+d12*d21)*yz + (d12*d20+d10*d22)*zx
        self.zx = d20*d00*xx + d21*d01*yy + d22*d02*zz + (d20*d01+d21*d00)*xy + (d21*d02+d22*d01)*yz + (d22*d00+d20*d02)*zx
    
    def inner_product(self, stress: 'Stress') -> float:
        """応力とのひずみエネルギー計算"""
        return (self.xx * stress.xx + 
                self.yy * stress.yy + 
                self.zz * stress.zz + 
                self.xy * stress.xy + 
                self.yz * stress.yz + 
                self.zx * stress.zx)
    
    def to_list(self) -> List[float]:
        """リスト形式に変換"""
        return [self.xx, self.yy, self.zz, self.xy, self.yz, self.zx]


class Stress:
    """応力クラス - JavaScript版のStressクラスに相当"""
    def __init__(self, values: List[float]):
        self.xx = values[0]
        self.yy = values[1]
        self.zz = values[2] if len(values) > 2 else 0.0
        self.xy = values[3] if len(values) > 3 else 0.0
        self.yz = values[4] if len(values) > 4 else 0.0
        self.zx = values[5] if len(values) > 5 else 0.0
    
    def rotate(self, d: np.ndarray) -> None:
        """応力を回転させる"""
        xx = self.xx
        yy = self.yy
        zz = self.zz
        xy = self.xy
        yz = self.yz
        zx = self.zx
        
        d00 = d[0, 0]
        d01 = d[0, 1]
        d02 = d[0, 2]
        d10 = d[1, 0]
        d11 = d[1, 1]
        d12 = d[1, 2]
        d20 = d[2, 0]
        d21 = d[2, 1]
        d22 = d[2, 2]
        
        self.xx = d00*d00*xx + d01*d01*yy + d02*d02*zz + 2*d00*d01*xy + 2*d01*d02*yz + 2*d02*d00*zx
        self.yy = d10*d10*xx + d11*d11*yy + d12*d12*zz + 2*d10*d11*xy + 2*d11*d12*yz + 2*d12*d10*zx
        self.zz = d20*d20*xx + d21*d21*yy + d22*d22*zz + 2*d20*d21*xy + 2*d21*d22*yz + 2*d22*d20*zx
        self.xy = d00*d10*xx + d01*d11*yy + d02*d12*zz + (d00*d11+d01*d10)*xy + (d01*d12+d02*d11)*yz + (d02*d10+d00*d12)*zx
        self.yz = d10*d20*xx + d11*d21*yy + d12*d22*zz + (d10*d21+d11*d20)*xy + (d11*d22+d12*d21)*yz + (d12*d20+d10*d22)*zx
        self.zx = d20*d00*xx + d21*d01*yy + d22*d02*zz + (d20*d01+d21*d00)*xy + (d21*d02+d22*d01)*yz + (d22*d00+d20*d02)*zx
    
    def to_list(self) -> List[float]:
        """リスト形式に変換"""
        return [self.xx, self.yy, self.zz, self.xy, self.yz, self.zx]


def strain_part(shell: FA_Shell, v: np.ndarray, xsi: float, eta: float, zeta: float, t: float, element_id: int = 0) -> np.ndarray:
    """要素内の歪ベクトルを返す - JavaScript版のstrainPartメソッドに相当
    
    Args:
        shell: シェル要素
        v: 節点変位ベクトル
        xsi: ξ座標
        eta: η座標
        zeta: ζ座標
        t: 要素厚さ
        element_id: 要素ID（JavaScript版との一致のために使用）
        
    Returns:
        歪ベクトル (6要素: [εx, εy, εz, γxy, γyz, γzx])
    """
    p = []
    for node_id in shell.iNodes:
        node = shell.nodes[node_id]
        p.append([node.x, node.y, node.z])
    
    d = shell.dirMatrix
    n = shell.normalVector
    
    if len(shell.iNodes) == 3:  # 三角形要素
        # JavaScript版と完全に同じ計算順序で実装
        sf1 = tri_shapeFunction(xsi, eta)
        ja = jacobianMatrix(shell, t, sf1)
        jinv = jacobInv(ja, d)
        sf3 = tri_shapeFunction3(p, d, xsi, eta)
        
        # JavaScript版のTriElement1.prototype.strainMatrixと同じ実装
        b1 = tri_strainMatrix1(sf1, jinv)
        b2 = tri_strainMatrix2(sf3, jinv)
        
        count = len(shell.iNodes)
        m1 = np.zeros((3, 6), dtype=np.float64)
        matrix = np.zeros((6*count, 3), dtype=np.float64)
        z = 0.5 * t * zeta
        
        # JavaScript版と完全に同じループ構造と計算順序
        for i in range(count):
            i2 = 2*i
            i3 = 3*i
            i6 = 6*i
            
            # JavaScript版と同じ初期化
            for i1 in range(3):
                for j1 in range(6):
                    m1[i1, j1] = 0.0
            
            # JavaScript版と同じ代入順序
            for i1 in range(3):
                m1[i1, 0] = b1[i2, i1]
                m1[i1, 1] = b1[i2+1, i1]
                
                m1[i1, 2] = z * b2[i3, i1]
                m1[i1, 3] = z * b2[i3+1, i1]
                m1[i1, 4] = z * b2[i3+2, i1]
                m1[i1, 5] = 0.0
            
            # JavaScript版と同じ行列計算
            for i1 in range(3):
                m1i = m1[i1]
                for j1 in range(3):
                    dj = d[j1]
                    s1 = 0.0
                    s2 = 0.0
                    for k1 in range(3):
                        s1 += m1i[k1] * dj[k1]
                        s2 += m1i[k1+3] * dj[k1]
                    
                    matrix[i6+j1, i1] = s1
                    matrix[i6+3+j1, i1] = s2
        
        # JavaScript版と同じ行列-ベクトル積
        strain = np.zeros(3, dtype=np.float64)
        for i in range(6*count):
            for j in range(3):
                strain[j] += matrix[i, j] * v[i]
        
        # JavaScript版と同じく3要素の歪ベクトルを6要素に拡張
        result = np.zeros(6, dtype=np.float64)
        result[0] = strain[0]  # εx
        result[1] = strain[1]  # εy
        result[2] = 0.0        # εz
        result[3] = strain[2]  # γxy
        result[4] = 0.0        # γyz
        result[5] = 0.0        # γzx
        
        return result
    else:  # 四角形要素
        # JavaScript版のShellElement.prototype.strainPartと同じ実装
        sf = quad_shapeFunction(xsi, eta)
        
        ja = jacobianMatrix(shell, t, sf)
        
        z = 0.5 * t * zeta
        count = len(shell.iNodes)  # 4節点
        m1 = np.zeros((5, 6), dtype=np.float64)
        matrix = np.zeros((6*count, 5), dtype=np.float64)
        
        # JavaScript版のstrainMatrix関数と同じ実装
        for i in range(count):
            bi = strainMatrix1(ja, sf, d)[i]
            
            # JavaScript版と同じ代入順序
            m1[0, 0] = bi[0]
            m1[0, 4] = z * bi[0]
            m1[1, 1] = bi[1]
            m1[1, 3] = -z * bi[1]
            m1[2, 0] = bi[1]
            m1[2, 1] = bi[0]
            m1[2, 3] = -z * bi[0]
            m1[2, 4] = z * bi[1]
            m1[3, 1] = bi[2]
            m1[3, 2] = bi[1]
            m1[3, 3] = -0.5 * t * bi[3] - z * bi[2]
            m1[4, 0] = bi[2]
            m1[4, 2] = bi[0]
            m1[4, 4] = 0.5 * t * bi[3] + z * bi[2]
            
            ib = 6 * i
            for i1 in range(5):
                m1i = m1[i1]
                for j1 in range(3):
                    dj = d[j1]
                    s1 = 0.0
                    s2 = 0.0
                    for k1 in range(3):
                        s1 += m1i[k1] * dj[k1]
                        s2 += m1i[k1+3] * dj[k1]
                    
                    matrix[ib+j1, i1] = s1
                    matrix[ib+3+j1, i1] = s2
        
        # JavaScript版と同じ行列-ベクトル積
        strain = np.zeros(5, dtype=np.float64)
        for i in range(6*count):
            for j in range(5):
                strain[j] += matrix[i, j] * v[i]
        
        # 5要素の歪ベクトルを6要素に拡張
        result = np.zeros(6, dtype=np.float64)
        result[0] = strain[0]  # εx
        result[1] = strain[1]  # εy
        result[2] = 0.0        # εz
        result[3] = strain[2]  # γxy
        result[4] = strain[3]  # γyz
        result[5] = strain[4]  # γzx
        
        return result


def strain_stress(shell: FA_Shell, disp_dict: Dict[int, Dict[str, float]], 
                 material_matrix: np.ndarray, thickness: float, element_id: int = 0) -> Tuple[List[Strain], List[Stress], List[float], List[Strain], List[Stress], List[float]]:
    """要素の歪・応力を返す - JavaScript版のstrainStressメソッドに相当
    
    Args:
        shell: シェル要素
        disp_dict: 節点変位辞書 {節点ID: {"dx": dx, "dy": dy, ...}}
        material_matrix: 材料マトリックス
        thickness: 要素厚さ
        element_id: 要素ID（JavaScript版との一致のために使用）
        
    Returns:
        (strain1, stress1, energy1, strain2, stress2, energy2)のタプル
    """
    p = []
    for node_id in shell.iNodes:
        node = shell.nodes[node_id]
        p.append([node.x, node.y, node.z])
    
    d = shell.dirMatrix
    n = shell.normalVector
    t = thickness
    
    v = np.zeros(6 * len(shell.iNodes), dtype=np.float64)
    for i, node_id in enumerate(shell.iNodes):
        disp = disp_dict[node_id]
        v[6*i] = disp["dx"]
        v[6*i+1] = disp["dy"]
        v[6*i+2] = disp["dz"]
        v[6*i+3] = disp["rx"]
        v[6*i+4] = disp["ry"]
        v[6*i+5] = disp["rz"]
    
    strain1 = []
    stress1 = []
    energy1 = []
    strain2 = []
    stress2 = []
    energy2 = []
    
    if len(shell.iNodes) == 3:  # 三角形要素
        node_points = [(0, 0), (1, 0), (0, 1)]
    else:  # 四角形要素の場合
        node_points = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
    
    for i, (xsi, eta) in enumerate(node_points):
        # JavaScript版と完全に同じ計算順序
        eps1 = strain_part(shell, v, xsi, eta, 1, t, element_id)
        eps2 = strain_part(shell, v, xsi, eta, -1, t, element_id)
        
        if len(shell.iNodes) == 3:  # 三角形要素
            # JavaScript版のTriElement1.prototype.toStrainと同じ変換
            eps1_vec = np.array([eps1[0], eps1[1], 0.0, eps1[3], 0.0, 0.0], dtype=np.float64)
            eps2_vec = np.array([eps2[0], eps2[1], 0.0, eps2[3], 0.0, 0.0], dtype=np.float64)
            
            eps1_5 = np.array([eps1[0], eps1[1], eps1[3], 0.0, 0.0], dtype=np.float64)
            eps2_5 = np.array([eps2[0], eps2[1], eps2[3], 0.0, 0.0], dtype=np.float64)
        else:
            eps1_vec = np.array([eps1[0], eps1[1], 0.0, eps1[3], eps1[4], eps1[5]], dtype=np.float64)
            eps2_vec = np.array([eps2[0], eps2[1], 0.0, eps2[3], eps2[4], eps2[5]], dtype=np.float64)
            
            eps1_5 = np.array([eps1[0], eps1[1], eps1[3], eps1[4], eps1[5]], dtype=np.float64)
            eps2_5 = np.array([eps2[0], eps2[1], eps2[3], eps2[4], eps2[5]], dtype=np.float64)
        
        # JavaScript版と同じ材料マトリクスの適用
        str1_5 = np.dot(material_matrix, eps1_5)
        str2_5 = np.dot(material_matrix, eps2_5)
        
        if len(shell.iNodes) == 3:  # 三角形要素
            # JavaScript版のTriElement1.prototype.toStressと同じ変換
            str1_vec = np.zeros(6, dtype=np.float64)
            str1_vec[0] = str1_5[0]
            str1_vec[1] = str1_5[1]
            str1_vec[3] = str1_5[2]
            
            str2_vec = np.zeros(6, dtype=np.float64)
            str2_vec[0] = str2_5[0]
            str2_vec[1] = str2_5[1]
            str2_vec[3] = str2_5[2]
        else:
            str1_vec = np.zeros(6, dtype=np.float64)
            str1_vec[0] = str1_5[0]
            str1_vec[1] = str1_5[1]
            str1_vec[3] = str1_5[2]
            str1_vec[4] = str1_5[3]
            str1_vec[5] = str1_5[4]
            
            str2_vec = np.zeros(6, dtype=np.float64)
            str2_vec[0] = str2_5[0]
            str2_vec[1] = str2_5[1]
            str2_vec[3] = str2_5[2]
            str2_vec[4] = str2_5[3]
            str2_vec[5] = str2_5[4]
        
        # JavaScript版と同じStrainとStressオブジェクト生成
        s1 = Strain(eps1_vec.tolist())
        s2 = Strain(eps2_vec.tolist())
        
        ss1 = Stress(str1_vec.tolist())
        ss2 = Stress(str2_vec.tolist())
        
        s1.rotate(d)
        ss1.rotate(d)
        s2.rotate(d)
        ss2.rotate(d)
        
        e1 = 0.5 * s1.inner_product(ss1)
        e2 = 0.5 * s2.inner_product(ss2)
        
        strain1.append(s1)
        stress1.append(ss1)
        energy1.append(e1)
        strain2.append(s2)
        stress2.append(ss2)
        energy2.append(e2)
    
    return strain1, stress1, energy1, strain2, stress2, energy2


def element_strain_stress(shell: FA_Shell, disp_dict: Dict[int, Dict[str, float]], 
                         material_matrix: np.ndarray, thickness: float, element_id: int = 0) -> Tuple[Strain, Stress, float, Strain, Stress, float]:
    """要素の平均歪・応力を返す - JavaScript版のelementStrainStressメソッドに相当
    
    JavaScript版の実装:
    ShellElement.prototype.elementStrainStress=function(p,u,d1,sp){
      var d=dirMatrix(p),n=normalVector(p),v=this.toArray(u,6);
      var t=sp.thickness,cf=1/this.intP.length;
      var strain1=[0,0,0,0,0,0],stress1=[0,0,0,0,0,0],energy1=0;
      var strain2=[0,0,0,0,0,0],stress2=[0,0,0,0,0,0],energy2=0;
      for(var i=0;i<this.intP.length;i++){
        var ip=this.intP[i];
        var eps1=this.strainPart(p,v,n,d,ip[0],ip[1],1,t);
        var eps2=this.strainPart(p,v,n,d,ip[0],ip[1],-1,t);
        strain1=numeric.add(strain1,eps1);
        strain2=numeric.add(strain2,eps2);
        var str1=numeric.dotMV(d1,eps1);
        var str2=numeric.dotMV(d1,eps2);
        stress1=numeric.add(stress1,str1);
        stress2=numeric.add(stress2,str2);
        energy1+=numeric.dotVV(eps1,str1);
        energy2+=numeric.dotVV(eps2,str2);
      }
      strain1=numeric.mul(strain1,cf);
      stress1=numeric.mul(stress1,cf);
      energy1*=0.5*cf;
      strain2=numeric.mul(strain1,cf);  // バグ: strain2ではなくstrain1を使用
      stress2=numeric.mul(stress1,cf);  // バグ: stress2ではなくstress1を使用
      energy2*=0.5*cf;
      return [this.toStrain(strain1),this.toStress(stress1),energy1,
              this.toStrain(strain2),this.toStress(stress2),energy2];
    };
    
    Args:
        shell: シェル要素
        disp_dict: 節点変位辞書 {節点ID: {"dx": dx, "dy": dy, ...}}
        material_matrix: 材料マトリックス
        thickness: 要素厚さ
        element_id: 要素ID（JavaScript版との一致のために使用）
        
    Returns:
        (strain1, stress1, energy1, strain2, stress2, energy2)のタプル
    """
    p = []
    for node_id in shell.iNodes:
        node = shell.nodes[node_id]
        p.append([node.x, node.y, node.z])
    
    d = shell.dirMatrix
    n = shell.normalVector
    t = thickness
    
    v = np.zeros(6 * len(shell.iNodes), dtype=np.float64)
    for i, node_id in enumerate(shell.iNodes):
        disp = disp_dict[node_id]
        v[6*i] = disp["dx"]
        v[6*i+1] = disp["dy"]
        v[6*i+2] = disp["dz"]
        v[6*i+3] = disp["rx"]
        v[6*i+4] = disp["ry"]
        v[6*i+5] = disp["rz"]
    
    
    # JavaScript版と完全に同じ積分点を使用
    if len(shell.iNodes) == 3:  # 三角形要素
        # JavaScript版では三角形要素の場合、TRI2_INTを使用
        from .stiffness_matrix import TRI2_INT
        int_points = TRI2_INT
    else:  # 四角形要素
        from .stiffness_matrix import QUAD1_INT
        int_points = QUAD1_INT
    
    # JavaScript版と同じ初期化 - ShellElement.prototype.elementStrainStressと同じ
    strain1 = np.zeros(6, dtype=np.float64)
    stress1 = np.zeros(6, dtype=np.float64)
    energy1 = 0.0
    strain2 = np.zeros(6, dtype=np.float64)
    stress2 = np.zeros(6, dtype=np.float64)
    energy2 = 0.0
    
    cf = 1.0 / len(int_points)  # 積分点の数による係数
    
    for xsi, eta, weight in int_points:
        # JavaScript版と完全に同じ計算順序
        eps1 = strain_part(shell, v, xsi, eta, 1, t, element_id)
        eps2 = strain_part(shell, v, xsi, eta, -1, t, element_id)
        
        # JavaScript版と同じく加算
        strain1 = np.add(strain1, eps1)
        strain2 = np.add(strain2, eps2)
        
        if len(shell.iNodes) == 3:  # 三角形要素
            # JavaScript版のTriElement1.prototype.toStrainと同じ変換
            # JavaScript版では以下のように計算している:
            tri_material = np.array([
                [material_matrix[0, 0], material_matrix[0, 1], material_matrix[0, 3]],
                [material_matrix[1, 0], material_matrix[1, 1], material_matrix[1, 3]],
                [material_matrix[3, 0], material_matrix[3, 1], material_matrix[3, 3]]
            ])
            
            eps1_3 = np.array([eps1[0], eps1[1], eps1[3]], dtype=np.float64)
            eps2_3 = np.array([eps2[0], eps2[1], eps2[3]], dtype=np.float64)
            
            str1_3 = np.dot(tri_material, eps1_3)
            str2_3 = np.dot(tri_material, eps2_3)
            
            str1_5 = np.array([str1_3[0], str1_3[1], 0.0, str1_3[2], 0.0], dtype=np.float64)
            str2_5 = np.array([str2_3[0], str2_3[1], 0.0, str2_3[2], 0.0], dtype=np.float64)
        else:
            eps1_5 = np.array([eps1[0], eps1[1], eps1[3], eps1[4], eps1[5]], dtype=np.float64)
            eps2_5 = np.array([eps2[0], eps2[1], eps2[3], eps2[4], eps2[5]], dtype=np.float64)
            
            matrix_size = material_matrix.shape[1]
            if matrix_size >= 6:  # 6x6の場合
                quad_material = np.array([
                    [material_matrix[0, 0], material_matrix[0, 1], material_matrix[0, 3], material_matrix[0, 4], material_matrix[0, 5]],
                    [material_matrix[1, 0], material_matrix[1, 1], material_matrix[1, 3], material_matrix[1, 4], material_matrix[1, 5]],
                    [material_matrix[3, 0], material_matrix[3, 1], material_matrix[3, 3], material_matrix[3, 4], material_matrix[3, 5]],
                    [material_matrix[4, 0], material_matrix[4, 1], material_matrix[4, 3], material_matrix[4, 4], material_matrix[4, 5]],
                    [material_matrix[5, 0], material_matrix[5, 1], material_matrix[5, 3], material_matrix[5, 4], material_matrix[5, 5]]
                ])
            else:  # 5x5の場合
                quad_material = np.array([
                    [material_matrix[0, 0], material_matrix[0, 1], material_matrix[0, 3], material_matrix[0, 4], 0.0],
                    [material_matrix[1, 0], material_matrix[1, 1], material_matrix[1, 3], material_matrix[1, 4], 0.0],
                    [material_matrix[3, 0], material_matrix[3, 1], material_matrix[3, 3], material_matrix[3, 4], 0.0],
                    [material_matrix[4, 0], material_matrix[4, 1], material_matrix[4, 3], material_matrix[4, 4], 0.0],
                    [0.0, 0.0, 0.0, 0.0, 0.0]
                ])
            
            str1_5 = np.dot(quad_material, eps1_5)
            str2_5 = np.dot(quad_material, eps2_5)
        
        # JavaScript版と同じ応力ベクトルの形式に変換
        if len(shell.iNodes) == 3:  # 三角形要素
            str1_vec = np.zeros(6, dtype=np.float64)
            str1_vec[0] = str1_5[0]
            str1_vec[1] = str1_5[1]
            str1_vec[3] = str1_5[2]
            
            str2_vec = np.zeros(6, dtype=np.float64)
            str2_vec[0] = str2_5[0]
            str2_vec[1] = str2_5[1]
            str2_vec[3] = str2_5[2]
        else:
            str1_vec = np.zeros(6, dtype=np.float64)
            str1_vec[0] = str1_5[0]
            str1_vec[1] = str1_5[1]
            str1_vec[3] = str1_5[2]
            str1_vec[4] = str1_5[3]
            str1_vec[5] = str1_5[4]
            
            str2_vec = np.zeros(6, dtype=np.float64)
            str2_vec[0] = str2_5[0]
            str2_vec[1] = str2_5[1]
            str2_vec[3] = str2_5[2]
            str2_vec[4] = str2_5[3]
            str2_vec[5] = str2_5[4]
        
        # JavaScript版と同じく加算
        stress1 = np.add(stress1, str1_vec)
        stress2 = np.add(stress2, str2_vec)
        
        # JavaScript版と同じエネルギー計算
        # JavaScript: energy1+=numeric.dotVV(eps1,str1);
        # JavaScript: energy2+=numeric.dotVV(eps2,str2);
        energy1 += float(np.dot(eps1, str1_vec))  # 修正: JavaScript版と同じく内積を使用
        energy2 += float(np.dot(eps2, str2_vec))  # 修正: JavaScript版と同じく内積を使用
    
    # JavaScript版と完全に同じスケーリング（バグを含む）
    strain1 = strain1 * cf
    stress1 = stress1 * cf
    
    # JavaScript: energy1*=0.5*cf;
    energy1 = 0.5 * energy1 * cf 
    
    # 有限要素法解析として正しい実装に修正
    # strain2とstress2には、それぞれの累積値にcfを掛ける
    strain2 = strain2 * cf  # 正しい: strain2の累積値にcfを掛ける
    stress2 = stress2 * cf  # 正しい: stress2の累積値にcfを掛ける
    
    # JavaScript: energy2*=0.5*cf;
    energy2 = 0.5 * energy2 * cf
    
    # JavaScript版と同じ変換
    if len(shell.iNodes) == 3:
        strain1_list = [strain1[0], strain1[1], 0.0, strain1[3], 0.0, 0.0]
        stress1_list = [stress1[0], stress1[1], 0.0, stress1[3], 0.0, 0.0]
        strain2_list = [strain2[0], strain2[1], 0.0, strain2[3], 0.0, 0.0]
        stress2_list = [stress2[0], stress2[1], 0.0, stress2[3], 0.0, 0.0]
    else:
        strain1_list = strain1.tolist()
        stress1_list = stress1.tolist()
        strain2_list = strain2.tolist()
        stress2_list = stress2.tolist()
    
    elem_strain1 = Strain(strain1_list)
    elem_stress1 = Stress(stress1_list)
    elem_strain2 = Strain(strain2_list)
    elem_stress2 = Stress(stress2_list)
    
    # JavaScript版と同じ回転
    elem_strain1.rotate(d)
    elem_stress1.rotate(d)
    elem_strain2.rotate(d)
    elem_stress2.rotate(d)
    
    return elem_strain1, elem_stress1, energy1, elem_strain2, elem_stress2, energy2


def calculate_shell_results(shell: FA_Shell, disp_dict: Dict[int, Dict[str, float]], 
                           material_matrix: np.ndarray, thickness: float, element_id: int = 0) -> Dict[str, Any]:
    """シェル要素の計算結果を取得する
    
    Args:
        shell: シェル要素
        disp_dict: 節点変位辞書 {節点ID: {"dx": dx, "dy": dy, ...}}
        material_matrix: 材料マトリックス
        thickness: 要素厚さ
        element_id: 要素ID（JavaScript版との一致のために使用）
        
    Returns:
        計算結果辞書
    """
    # JavaScript版と同じ計算順序で実行
    if len(shell.iNodes) == 3:  # 三角形要素の場合
        strain1, stress1, energy1, strain2, stress2, energy2 = strain_stress(
            shell, disp_dict, material_matrix, thickness, element_id
        )
        
        for s in strain1:
            s.zz = 0.0
            s.yz = 0.0
            s.zx = 0.0
        
        for s in stress1:
            s.zz = 0.0
            s.yz = 0.0
            s.zx = 0.0
            
        for s in strain2:
            s.zz = 0.0
            s.yz = 0.0
            s.zx = 0.0
        
        for s in stress2:
            s.zz = 0.0
            s.yz = 0.0
            s.zx = 0.0
    else:
        strain1, stress1, energy1, strain2, stress2, energy2 = strain_stress(
            shell, disp_dict, material_matrix, thickness, element_id
        )
    
    elem_strain1, elem_stress1, elem_energy1, elem_strain2, elem_stress2, elem_energy2 = element_strain_stress(
        shell, disp_dict, material_matrix, thickness, element_id
    )
    
    if len(shell.iNodes) == 3:
        elem_strain1.zz = 0.0
        elem_strain1.yz = 0.0
        elem_strain1.zx = 0.0
        
        elem_stress1.zz = 0.0
        elem_stress1.yz = 0.0
        elem_stress1.zx = 0.0
        
        elem_strain2.zz = 0.0
        elem_strain2.yz = 0.0
        elem_strain2.zx = 0.0
        
        elem_stress2.zz = 0.0
        elem_stress2.yz = 0.0
        elem_stress2.zx = 0.0
    
    result = {
        "nodeStrain1": [s.to_list() for s in strain1],
        "nodeStress1": [s.to_list() for s in stress1],
        "nodeEnergy1": energy1,
        "nodeStrain2": [s.to_list() for s in strain2],
        "nodeStress2": [s.to_list() for s in stress2],
        "nodeEnergy2": energy2,
        "elemStrain1": elem_strain1.to_list(),
        "elemStress1": elem_stress1.to_list(),
        "elemEnergy1": elem_energy1,
        "elemStrain2": elem_strain2.to_list(),
        "elemStress2": elem_stress2.to_list(),
        "elemEnergy2": elem_energy2
    }
    
    return result

def calculate_tetra_strain_stress(solid: FA_Solid, disp_dict: Dict[int, Dict[str, float]], 
                                 material: Any, all_nodes: List[FA_Node]) -> Tuple[Strain, Stress, float]:
    """4面体要素のひずみ・応力計算
    
    Args:
        solid: FA_Solid要素
        disp_dict: 節点変位辞書 {節点ID: {"dx": dx, "dy": dy, ...}}
        material: 材料特性
        all_nodes: 全節点リスト
        
    Returns:
        Tuple[Strain, Stress, float]: (ひずみ, 応力, ひずみエネルギー)
    """
    u = np.zeros(12)  # 4節点 × 3自由度
    for i, node_id in enumerate(solid.nodes):
        disp = disp_dict[node_id]
        u[i*3] = disp["dx"]
        u[i*3+1] = disp["dy"] 
        u[i*3+2] = disp["dz"]
    
    nodes_coords = []
    for i in range(4):
        coord = solid.get_coordinate(i, all_nodes)
        nodes_coords.append(coord)
    
    jacobian = tetra_jacobian(nodes_coords)
    grad = tetra_grad(nodes_coords, jacobian)
    B = tetra_strainMatrix(grad)
    
    strain_values = np.dot(B, u)
    
    D = material_matrix_3d(material.e, material.poi)
    stress_values = np.dot(D, strain_values)
    
    volume = abs(jacobian) / 6
    energy = 0.5 * np.dot(strain_values, stress_values) * volume
    
    return Strain(strain_values.tolist()), Stress(stress_values.tolist()), energy


def calculate_hexa_strain_stress(solid: FA_Solid, disp_dict: Dict[int, Dict[str, float]], 
                                material: Any, all_nodes: List[FA_Node]) -> Tuple[Strain, Stress, float]:
    """6面体要素のひずみ・応力計算
    
    Args:
        solid: FA_Solid要素（hexaタイプ）
        disp_dict: 節点変位辞書 {節点ID: {"dx": dx, "dy": dy, ...}}
        material: 材料特性
        all_nodes: 全節点リスト
        
    Returns:
        Tuple[Strain, Stress, float]: (ひずみ, 応力, ひずみエネルギー)
    """
    from .stiffness_matrix import hexa_shapeFunction, hexa_jacobian, hexa_grad_N_global, hexa_strainMatrix, material_matrix_3d
    
    u = np.zeros(24)
    for i, node_id in enumerate(solid.nodes):
        disp = disp_dict[node_id]
        u[i*3] = disp["dx"]
        u[i*3+1] = disp["dy"] 
        u[i*3+2] = disp["dz"]
    
    nodes_coords = []
    for i in range(8):
        coord = solid.get_coordinate(i, all_nodes)
        nodes_coords.append(coord)
    
    xsi, eta, zeta = 0.0, 0.0, 0.0
    N, dN_dlocal = hexa_shapeFunction(xsi, eta, zeta)
    J, detJ = hexa_jacobian(nodes_coords, dN_dlocal)
    dN_dglobal = hexa_grad_N_global(J, dN_dlocal)
    B = hexa_strainMatrix(dN_dglobal)
    
    strain_values = np.dot(B, u)
    
    D = material_matrix_3d(material.e, material.poi)
    stress_values = np.dot(D, strain_values)
    
    volume = abs(detJ)  # 単位立方体の場合は8、一般的な要素では適切な体積計算が必要
    energy = 0.5 * np.dot(strain_values, stress_values) * volume
    
    return Strain(strain_values.tolist()), Stress(stress_values.tolist()), energy


def calculate_wedge_strain_stress(solid: FA_Solid, disp_dict: Dict[int, Dict[str, float]], 
                                 material: Any, all_nodes: List[FA_Node]) -> Tuple[Strain, Stress, float]:
    """楔形要素のひずみ・応力計算
    
    Args:
        solid: FA_Solid要素（wedgeタイプ）
        disp_dict: 節点変位辞書 {節点ID: {"dx": dx, "dy": dy, ...}}
        material: 材料特性
        all_nodes: 全節点リスト
        
    Returns:
        Tuple[Strain, Stress, float]: (ひずみ, 応力, ひずみエネルギー)
    """
    from .stiffness_matrix import wedge_shapeFunction, wedge_jacobian, wedge_grad_N_global, wedge_strainMatrix, material_matrix_3d
    
    u = np.zeros(18)
    for i, node_id in enumerate(solid.nodes):
        disp = disp_dict[node_id]
        u[i*3] = disp["dx"]
        u[i*3+1] = disp["dy"] 
        u[i*3+2] = disp["dz"]
    
    nodes_coords = []
    for i in range(6):
        coord = solid.get_coordinate(i, all_nodes)
        nodes_coords.append(coord)
    
    xsi, eta, zeta = 1/3, 1/3, 0.0
    N, dN_dlocal = wedge_shapeFunction(xsi, eta, zeta)
    J, detJ = wedge_jacobian(nodes_coords, dN_dlocal)
    dN_dglobal = wedge_grad_N_global(J, dN_dlocal)
    B = wedge_strainMatrix(dN_dglobal)
    
    strain_values = np.dot(B, u)
    
    D = material_matrix_3d(material.e, material.poi)
    stress_values = np.dot(D, strain_values)
    
    volume = 0
    for int_point in [[1/3, 1/3, -1/math.sqrt(3), 0.5], [1/3, 1/3, 1/math.sqrt(3), 0.5]]:
        xsi_v, eta_v, zeta_v, weight_v = int_point
        N_v, dN_dlocal_v = wedge_shapeFunction(xsi_v, eta_v, zeta_v)
        J_v, detJ_v = wedge_jacobian(nodes_coords, dN_dlocal_v)
        volume += abs(detJ_v) * weight_v
    
    energy = 0.5 * np.dot(strain_values, stress_values) * volume
    
    return Strain(strain_values.tolist()), Stress(stress_values.tolist()), energy
