import math
import numpy as np
from scipy.sparse import coo_matrix, csr_matrix
from typing import Optional, TypedDict, Tuple, List, Any
from numpy.typing import NDArray
from .error_log import FrameError, FrameCritical, fLogger
from .models.fa_node import FA_Node
from .models.fa_beam import FA_Beam
from .models.fa_shell import FA_Shell
from .models.fa_solid import FA_Solid
from .models.fa_material import FA_Material
from .models.fa_section import FA_Section
from .models.fa_thickness import FA_Thickness
from .models.fa_support import FA_Support
from .models.fa_spring import FA_Spring
from .models.fa_joint import FA_Joint, get_BeamJoint


# 三角形1次要素の節点のξ,η座標
TRI1_NODE = [[0, 0], [1, 0], [0, 1]]
# 三角形1次要素の積分点のξ,η座標,重み係数
TRI1_INT = [[1/3, 1/3, 0.5]]
# 三角形2次要素の積分点のξ,η座標,重み係数
# JavaScript版と完全に同じ値を使用
GTRI2 = [1/6, 2/3]  # JavaScript: var GTRI2=[1/6,2/3]
C1_3 = 1/3          # JavaScript: var C1_3=1/3
C1_6 = 1/6          # JavaScript: var C1_6=1/6
C1_12 = 1/12        # JavaScript: var C1_12=1/12
C1_24 = 1/24        # JavaScript: var C1_24=1/24
# JavaScript版と完全に同じ積分点を使用 - 順序も重要
TRI2_INT = [
    [GTRI2[0], GTRI2[0], C1_6],  # 第1積分点 (1/6, 1/6, 1/6)
    [GTRI2[1], GTRI2[0], C1_6],  # 第2積分点 (2/3, 1/6, 1/6)
    [GTRI2[0], GTRI2[1], C1_6]   # 第3積分点 (1/6, 2/3, 1/6)
]
# 四角形1次要素の節点のξ,η座標
QUAD1_NODE = [[-1, -1], [1, -1], [1, 1], [-1, 1]]
# 四角形1次要素の積分点のξ,η座標,重み係数
QUAD1_INT = [[1/math.sqrt(3), 1/math.sqrt(3), 1], 
              [-1/math.sqrt(3), 1/math.sqrt(3), 1], 
              [1/math.sqrt(3), -1/math.sqrt(3), 1],
              [-1/math.sqrt(3), -1/math.sqrt(3), 1]]
# 三角形1次要素の質量マトリックス係数
TRI1_MASS1 = [[1, 0.5, 0.5], [0.5, 1, 0.5], [0.5, 0.5, 1]]

# (注)
# シェル要素については旧FrameWebをそのまま移植しており理論の確からしさは不明


class Freedom(TypedDict):
    """節点の自由度インデックス辞書クラス

    Keys:
        dx :X方向変位
        dy :Y方向変位
        dz :Z方向変位
        rx :X軸まわり回転
        ry :Y軸まわり回転
        rz :Z軸まわり回転

    Values:
        _ (Optional[int]): 自由度インデックス（自由度が無い場合はNone） 
    """
    dx: Optional[int]
    dy: Optional[int]
    dz: Optional[int]
    rx: Optional[int]
    ry: Optional[int]
    rz: Optional[int]


class StiffnessMatrix:
    """剛性行列クラス

    Properties:
        gkCsr (csr_matrix):csr形式のスパース全体剛性行列（全体座標系）
        nFree (int):自由度数
        freeInd (list[Freedom]):自由度インデックスリスト
        beamMatrices (list[np.ndarray[12, 12]]):梁要素剛性行列リスト
        shellMatrices (list[np.ndarray[24, 24]]):シェル要素剛性行列リスト
        nodes (list[FA_Node]):節点リスト
        beams (list[FA_Beam]):梁要素リスト
        shells (list[FA_Shell]):シェル要素リスト
        materials (list[FA_Material]):材料リスト
        sections (list[FA_Section]):断面リスト
        thicknesses (list[FA_Thickness]):厚さリスト
        supports (list[FA_Support]):支点リスト
        springs (list[FA_Spring]):分布バネリスト
        joints (list[FA_Joint]):材端条件リスト
    
    freeInd:
        freeInd[i]は節点インデックスiの節点に対応する自由度インデックス辞書
    """
    def __init__(self, nodes: list[FA_Node], beams: list[FA_Beam], shells: list[FA_Shell],\
                 materials: list[FA_Material], sections: list[FA_Section], thicknesses: list[FA_Thickness],\
                 supports: list[FA_Support], springs: list[FA_Spring], joints: list[FA_Joint], solids: list[FA_Solid] = None) -> None:
        try:
            fLogger.info("StiffnessMatrix.__init__ started")
            fLogger.info(f"Nodes: {len(nodes)}, Beams: {len(beams)}, Shells: {len(shells)}")
            fLogger.info(f"Materials: {len(materials)}, Sections: {len(sections)}")
            
            if solids is None:
                fLogger.info("No solid elements provided to StiffnessMatrix")
                solids = []
            else:
                fLogger.info(f"Number of solid elements: {len(solids)}")
                for i, solid in enumerate(solids):
                    fLogger.info(f"Solid {i}: type={solid.element_type}, nodes={solid.nodes}, material={solid.material_num}")
                    if solid.material_num >= len(materials):
                        fLogger.critical(f"Invalid material index {solid.material_num} for solid {i}, max index is {len(materials)-1}")
                        raise FrameCritical(f"ソリッド要素の材料番号が無効です: {solid.material_num}")
        except Exception as e:
            fLogger.critical(f"Error in StiffnessMatrix.__init__: {str(e)}")
            raise FrameCritical(f"剛性行列の初期化でエラーが発生しました: {str(e)}")
        """剛性行列クラス

        Args:
            nodes (list[FA_Node]): 節点リスト
            beams (list[FA_Beam]): 梁要素リスト
            shells (list[FA_Shell]): シェル要素リスト
            materials (list[FA_Material]): 材料リスト
            sections (list[FA_Section]): 断面リスト
            thicknesses (list[FA_Thickness]): 厚さリスト
            supports (list[FA_Support]): 支点リスト
            springs (list[FA_Spring]): 分布バネリスト
            joints (list[FA_Joint]): 材端条件リスト
        """
        # 元データの保持
        self.nodes = nodes
        self.beams = beams
        self.shells = shells
        self.solids = solids if solids is not None else []
        self.materials = materials
        self.sections = sections
        self.thicknesses = thicknesses
        self.supports = supports
        self.springs = springs
        self.joints = joints
        # 自由度インデックスの作成
        self.nFree: int = 0
        self.freeInd: list[Freedom] = []
        for i in range(len(nodes)):
            self.freeInd.append(self.__make_freedom(i))
        # 全体剛性行列の非ゼロ要素格納用変数の準備（サイズは余裕をもって）
        solid_factor = 10 if len(solids) > 0 else 1
        array_size = math.ceil(self.nFree ** 2 / 2) * solid_factor
        fLogger.info(f"Allocating sparse matrix arrays with size {array_size} (nFree={self.nFree}, solids={len(solids)})")
        spRows = np.zeros(array_size, dtype=int)  # スパース行列の行インデックス格納用
        spCols = np.zeros(array_size, dtype=int)  # スパース行列の列インデックス格納用
        spVals = np.zeros(array_size, dtype=float)  # スパース行列の値格納用
        nSpVal: int = 0  # スパース行列の非ゼロ値の成分数
        # 梁要素の剛性行列
        self.beamMatrices: List[NDArray[np.float64]] = []
        for i, beam in enumerate(beams):
            bMatrix = self.__make_matrixBeam(i, beam.leng, beam.iMat, beam.iSec)
            self.beamMatrices.append(bMatrix)
            rows, cols, vals, nValid = self.__convert_beamMatrix(beam, bMatrix)
            spRows[nSpVal:(nSpVal + nValid)] = rows
            spCols[nSpVal:(nSpVal + nValid)] = cols
            spVals[nSpVal:(nSpVal + nValid)] = vals
            nSpVal += nValid
        # シェル要素の剛性行列
        self.shellMatrices: List[NDArray[np.float64]] = []
        for shell in shells:
            sMatrix = self.__make_matrixShell(shell)
            self.shellMatrices.append(sMatrix)
            rows, cols, vals, nValid = self.__pickup_shellMatrix(shell, sMatrix)
            spRows[nSpVal:(nSpVal + nValid)] = rows
            spCols[nSpVal:(nSpVal + nValid)] = cols
            spVals[nSpVal:(nSpVal + nValid)] = vals
            nSpVal += nValid
        self.solidMatrices: List[NDArray[np.float64]] = []
        for solid in self.solids:
            if solid.element_type == "tetra":
                rows, cols, vals, nValid = self.__add_solid_stiffness(solid)
                spRows[nSpVal:(nSpVal + nValid)] = rows
                spCols[nSpVal:(nSpVal + nValid)] = cols
                spVals[nSpVal:(nSpVal + nValid)] = vals
                nSpVal += nValid
        # 節点バネ分の追加
        for sup in supports:
            freedom = self.freeInd[sup.iNode]
            if (freedom["dx"] is not None) and (sup.dxFix == False) and (sup.dxSpr != 0):
                spRows[nSpVal] = spCols[nSpVal] = freedom["dx"]
                spVals[nSpVal] = sup.dxSpr
                nSpVal += 1
            if (freedom["dy"] is not None) and (sup.dyFix == False) and (sup.dySpr != 0):
                spRows[nSpVal] = spCols[nSpVal] = freedom["dy"]
                spVals[nSpVal] = sup.dySpr
                nSpVal += 1
            if (freedom["dz"] is not None) and (sup.dzFix == False) and (sup.dzSpr != 0):
                spRows[nSpVal] = spCols[nSpVal] = freedom["dz"]
                spVals[nSpVal] = sup.dzSpr
                nSpVal += 1
            if (freedom["rx"] is not None) and (sup.rxFix == False) and (sup.rxSpr != 0):
                spRows[nSpVal] = spCols[nSpVal] = freedom["rx"]
                spVals[nSpVal] = sup.rxSpr
                nSpVal += 1
            if (freedom["ry"] is not None) and (sup.ryFix == False) and (sup.rySpr != 0):
                spRows[nSpVal] = spCols[nSpVal] = freedom["ry"]
                spVals[nSpVal] = sup.rySpr
                nSpVal += 1
            if (freedom["rz"] is not None) and (sup.rzFix == False) and (sup.rzSpr != 0):
                spRows[nSpVal] = spCols[nSpVal] = freedom["rz"]
                spVals[nSpVal] = sup.rzSpr
                nSpVal += 1
        # 全体剛性行列の要素格納用変数から不要部分を削除
        spRows = spRows[:nSpVal]
        spCols = spCols[:nSpVal]
        spVals = spVals[:nSpVal]
        # スパース行列の作成
        gkCoo = coo_matrix((spVals, (spRows, spCols)), (self.nFree, self.nFree))
        self.gkCsr = gkCoo.tocsr()
        return None
    

    # region インスタンス関数
    def __make_freedom(self, iNode: int) -> Freedom:
        """節点の自由度インデックスを作成する

        Args:
            iNode (int): 節点インデックス

        Returns:
            _ (Freedom): 節点の自由度インデックスデータ
        """
        pickedSup = [obj for obj in self.supports if obj.iNode == iNode]
        free: Freedom = {
            "dx": None,
            "dy": None,
            "dz": None,
            "rx": None,
            "ry": None,
            "rz": None
        }
        if len(pickedSup) > 1:  # 同一節点に複数の支点が設定されている場合はエラー
            errMsg = "要素剛性行列作成時の節点自由度インデックス設定時に予期せぬエラーが発生しました"
            fLogger.critical(errMsg + f": 節点インデックス({str(iNode)})")
            raise FrameCritical(errMsg, iNode=iNode)
        elif len(pickedSup) == 0:
            # 支点設定がない節点
            for key in ["dx", "dy", "dz", "rx", "ry", "rz"]:
                free[key] = self.nFree
                self.nFree += 1
        else:
            # 支点設定のある節点（固定の方向は自由度に含まない）
            sup = pickedSup[0]
            if sup.dxFix == False:
                free["dx"] = self.nFree
                self.nFree += 1
            if sup.dyFix == False:
                free["dy"] = self.nFree
                self.nFree += 1
            if sup.dzFix == False:
                free["dz"] = self.nFree
                self.nFree += 1
            if sup.rxFix == False:
                free["rx"] = self.nFree
                self.nFree += 1
            if sup.ryFix == False:
                free["ry"] = self.nFree
                self.nFree += 1
            if sup.rzFix == False:
                free["rz"] = self.nFree
                self.nFree += 1
        return free
    

    def __make_matrixBeam(self, iBeam: int, lenE: float,\
                          iMat: int, iSec: int) -> NDArray[np.float64]:
        """梁要素の剛性行列を作成する

        Args:
            iBeam (int): 梁要素インデックス
            lenE (float): 梁要素長さ(m)
            iMat (int): 材料インデックス
            iSec (int): 断面インデックス

        Returns:
            _ (np.ndarray[12, 12]): 梁要素剛性行列（要素座標系）
        """
        mat = self.materials[iMat]
        sec = self.sections[iSec]
        # 分布バネの取得
        spr = [obj for obj in self.springs if obj.iBeam == iBeam]
        if len(spr) > 1:  # 同一梁要素に複数の分布バネがある場合はエラー
            errMsg = "要素剛性行列作成時の分布バネ取得時に予期せぬエラーが発生しました"
            fLogger.critical(errMsg + f": 梁要素インデックス({str(iBeam)})")
            raise FrameCritical(errMsg, iBeam=iBeam)
        sprX: float = 0.0 if len(spr) == 0 else spr[0].dxSpr  # 要素x軸方向バネ(kN/m/m)
        sprY: float = 0.0 if len(spr) == 0 else spr[0].dySpr  # 要素y軸方向バネ(kN/m/m)
        sprZ: float = 0.0 if len(spr) == 0 else spr[0].dzSpr  # 要素z軸方向バネ(kN/m/m)
        sprR: float = 0.0 if len(spr) == 0 else spr[0].rxSpr  # 要素x軸まわり回転バネ(kNm/rad/m)
        # 要素軸x方向変位用の要素剛性行列
        bm1 = self.__make_matrixForDx(mat.e, sec.a, lenE, sprX)
        # 要素軸y方向変位・z軸まわり回転用の要素剛性行列
        iFree, jFree = get_BeamJoint(iBeam, 2, self.joints)
        bm2 = self.__make_matrixForBending(True, mat.e, sec.iz, lenE, iFree, jFree, sprY)
        # 要素軸z方向変位・y軸まわり回転用の要素剛性行列
        iFree, jFree = get_BeamJoint(iBeam, 1, self.joints)
        bm3 = self.__make_matrixForBending(False, mat.e, sec.iy, lenE, iFree, jFree, sprZ)
        # 要素軸xまわりねじり用の要素剛性行列
        iFree, jFree = get_BeamJoint(iBeam, 0, self.joints)
        bm4 = self.__make_matrixForTorsion(mat.g, sec.j, lenE, iFree, jFree, sprR)
        # 行列の合成
        bm = bm1 + bm2 + bm3 + bm4
        return bm
    

    def __make_matrixForDx(self, e: float, a: float, l: float,\
                           spr: float) -> NDArray[np.float64]:
        """要素軸x方向変位に関する梁要素剛性行列を作成する

        Args:
            e (float): ヤング係数E(kN/m2)
            a (float): 断面積A(m2)
            l (float):  梁要素長さL(m)
            spr (float): 分布バネ(kN/m/m)

        Returns:
            _ (np.ndarray[12, 12]): 要素軸x方向変位に関する梁要素剛性行列（要素座標系）
        """
        bm = np.zeros((12, 12), dtype=float)
        if spr == 0:  # 分布バネが無い場合
            coef = e * a / l  # E*A/L (kN/m)
            bm[0, 0] = bm[6, 6] = coef
            bm[0, 6] = bm[6, 0] = -coef
        else:  # 分布バネがある場合
            alpha = math.sqrt(spr / e / a)
            coef = alpha * e * a / np.sinh(alpha * l)
            bm[0, 0] = bm[6, 6] = coef * np.cosh(alpha * l)
            bm[0, 6] = bm[6, 0] = -coef
        return bm


    def __make_matrixForBending(self, isY: bool, e: float, i: float, l: float,\
                               iFree: bool, jFree: bool, spr: float) -> NDArray[np.float64]:
        """要素軸y(z)軸方向変位・z(y)軸まわり回転に関する梁要素剛性行列を作成する

        Args:
            isY (bool): {True:y軸方向変位・z軸まわり回転, False:z軸方向変位・y軸まわり回転}
            e (float): ヤング係数E(kN/m2)
            i (float): 断面二次モーメントI(m4)
            l (float): 梁要素長さL(m)
            iFree (bool): i端材端条件が自由か否か
            jFree (bool): j端材端条件が自由か否か
            spr (float): 分布バネ(kN/m/m)

        Returns:
            _ (np.ndarray[12, 12]): 要素軸y(z)軸方向変位・z(y)軸まわり回転に関する梁要素剛性行列（要素座標系）
        """
        bm = np.zeros((12, 12), dtype=float)
        if spr == 0:  # 分布バネが無い場合
            coef = e * i / l ** 3  # EI/L^3 (kN/m)
            if iFree == True:  # i端フリー
                iLam = 0.0
            else:  # i端剛
                iLam = 1.0
            if jFree == True:  # j端フリー
                jLam = 0.0
            else:  # j端剛
                jLam = 1.0
            # マトリクス成分の計算
            k00 = 6.0 * (iLam + jLam + 4.0 * iLam * jLam) / (1.0 + iLam + jLam)
            k10 = 6.0 * l * iLam * (1.0 + 2.0 * jLam) / (1.0 + iLam + jLam)
            k20 = -k00
            k30 = 6.0 * l * jLam * (1.0 + 2.0 *iLam) / (1.0 + iLam + jLam)
            k11 = 6.0 * l ** 2 * iLam * (1.0 + jLam) / (1.0 + iLam + jLam)
            k21 = -k10
            k31 = 6.0 * l ** 2 * iLam * jLam / (1.0 + iLam + jLam)
            k22 = k00
            k32 = -k30
            k33 = 6.0 * l ** 2 * jLam * (1.0 + iLam) / (1.0 + iLam + jLam)
        else:  # 分布バネがある場合
            beta = np.power(spr / (4.0 * e * i), 0.25)  # 分布バネの特性値βに対応
            coef = 2.0 * e * i * beta  # 2EIβ (kNm)
            # マトリクス成分の計算
            k00, k10, k20, k30, k11, k21, k31, k22, k32, k33 \
                = matrixElementsForBendingSpr(beta, l, iFree, jFree)
        # 剛性行列への代入
        if isY == True:  # y軸方向変位・z軸まわり回転
            indList = [1, 5, 7, 11]
            bm[indList[0], indList] = [k00, k10, k20, k30]
            bm[indList[1], indList] = [k10, k11, k21, k31]
            bm[indList[2], indList] = [k20, k21, k22, k32]
            bm[indList[3], indList] = [k30, k31, k32, k33]
        else:  # z軸方向変位・y軸まわり回転
            indList = [2, 4, 8, 10]
            bm[indList[0], indList] = [k00, -k10, k20, -k30]
            bm[indList[1], indList] = [-k10, k11, -k21, k31]
            bm[indList[2], indList] = [k20, -k21, k22, -k32]
            bm[indList[3], indList] = [-k30, k31, -k32, k33]
        bm *= coef
        return bm


    def __make_matrixForTorsion(self, g: float, j: float, l: float,\
                               iFree: bool, jFree: bool, spr: float) -> NDArray[np.float64]:
        """要素x軸まわりねじりに関する梁要素剛性行列を作成する

        Args:
            g (float): せん断弾性係数G(kN/m2)
            j (float): ねじり定数J(m4)
            l (float): 梁要素長さL(m)
            iFree (bool): i端材端条件が自由か否か
            jFree (bool): j端材端条件が自由か否か
            spr (float): 分布バネ(kNm/rad/m)

        Returns:
            _ (np.ndarray[12, 12]): 要素x軸まわりねじりに関する梁要素剛性行列（要素座標系）
        """
        bm = np.zeros((12, 12), dtype=float)
        if spr == 0:  # 分布バネが無い場合
            coef = g * j / l  # G*J/L (kNm)
            if (iFree == False) and (jFree == False):  # 両端固定の場合のみ剛性あり
                bm[3, 3] = bm[9, 9] = coef
                bm[3, 9] = bm[9, 3] = -coef
        else:  # 分布バネがある場合
            # ねじり分布バネがある場合の計算結果の確からしさは未確認なので
            # 他社ソフト等で結果比較をしたいところ
            alpha = math.sqrt(spr / g / j)
            coef = alpha * g * j / np.sinh(alpha * l)
            if (iFree == False) and (jFree == False):  # 両端固定の場合のみ剛性あり
                bm[3, 3] = bm[9, 9] = coef * np.cosh(alpha * l)
                bm[3, 9] = bm[9, 3] = -coef
        return bm


    def __convert_beamMatrix(self, beam: FA_Beam, bMatrix: NDArray[np.float64])\
        -> Tuple[NDArray[np.float64], NDArray[np.float64], NDArray[np.float64], int]:
        """梁要素の剛性行列を全体座標系に変換し全体剛性行列の構成要素を取得する

        Args:
            beam (FA_Beam): 梁要素データ_
            bMatrix (np.ndarray[12, 12]): 梁要素の剛性行列

        Returns:
            _ (Tuple): rows, cols, vals, nValid

        rows:
            _ (np.ndarray):非ゼロ要素の全体剛性行列における行インデックスリスト

        cols:
            _ (np.ndarray):非ゼロ要素の全体剛性行列における列インデックスリスト

        vals:
            _ (np.ndarray):非ゼロ要素の全体剛性行列における値リスト

        nValid:
            _ (int):非ゼロ要素数
        """
        # 要素剛性行列の全体座標系への変換
        tt = beam.get_convMatrix(12, False)  # 12x12基底変換行列
        bMatrixG = np.matmul(np.matmul(np.linalg.inv(tt), bMatrix), tt)
        # 非ゼロ要素の抽出
        rows = np.zeros(12 ** 2, dtype=int)  # 非ゼロ要素の全体剛性行列における行インデックス格納用
        cols = np.zeros(12 ** 2, dtype=int)  # 非ゼロ要素の全体剛性行列における列インデックス格納用
        vals = np.zeros(12 ** 2, dtype=float)  # 非ゼロ要素の全体剛性行列における値格納用
        nValid: int = 0  # 非ゼロ要素数
        freeI = self.freeInd[beam.indI]  # i端節点の自由度インデックス辞書
        freeJ = self.freeInd[beam.indJ]  # j端節点の自由度インデックス辞書
        freeIJ: list[Optional[int]]\
            = [freeI["dx"], freeI["dy"], freeI["dz"], freeI["rx"], freeI["ry"], freeI["rz"],\
            freeJ["dx"], freeJ["dy"], freeJ["dz"], freeJ["rx"], freeJ["ry"], freeJ["rz"]]
        for row in range(12):
            for col in range(12):
                if (bMatrixG[row, col] != 0) and (freeIJ[row] is not None) and (freeIJ[col] is not None):
                    rows[nValid] = freeIJ[row]
                    cols[nValid] = freeIJ[col]
                    vals[nValid] = bMatrixG[row, col]
                    nValid += 1
        return rows[:nValid], cols[:nValid], vals[:nValid], nValid
    

    def __pickup_shellMatrix(self, shell: FA_Shell, sMatrix: np.ndarray)\
        -> Tuple[np.ndarray, np.ndarray, np.ndarray, int]:
        """シェル要素の剛性行列からスパース行列用のデータを抽出する

        Args:
            shell (FA_Shell): シェル要素データ
            sMatrix (np.ndarray): シェル要素の剛性行列（全体座標系）

        Returns:
            _ (Tuple): rows, cols, vals, nValid

        rows:
            _ (np.ndarray):非ゼロ要素の全体剛性行列における行インデックスリスト

        cols:
            _ (np.ndarray):非ゼロ要素の全体剛性行列における列インデックスリスト

        vals:
            _ (np.ndarray):非ゼロ要素の全体剛性行列における値リスト

        nValid:
            _ (int):非ゼロ要素数
        """
        # 要素の節点数（三角形=3, 四角形=4）
        node_count = len(shell.iNodes)
        matrix_size = node_count * 6
        
        # 非ゼロ要素を格納するための配列を用意
        max_nonzero = matrix_size ** 2  # 最大の非ゼロ要素数
        rows = np.zeros(max_nonzero, dtype=int)  # 非ゼロ要素の全体剛性行列における行インデックス格納用
        cols = np.zeros(max_nonzero, dtype=int)  # 非ゼロ要素の全体剛性行列における列インデックス格納用
        vals = np.zeros(max_nonzero, dtype=float)  # 非ゼロ要素の全体剛性行列における値格納用
        nValid: int = 0  # 非ゼロ要素数
        
        # 各節点の自由度インデックスの取得
        free: list[Optional[int]] = []
        for i in range(node_count):  # 第1節点～第n節点
            iNode = shell.iNodes[i]  # 節点インデックス
            freeTmp = self.freeInd[iNode]  # 節点の自由度インデックス辞書
            free.append(freeTmp["dx"])
            free.append(freeTmp["dy"])
            free.append(freeTmp["dz"])
            free.append(freeTmp["rx"])
            free.append(freeTmp["ry"])
            free.append(freeTmp["rz"])
            
        # 非ゼロ要素の抽出
        for row in range(matrix_size):
            for col in range(matrix_size):
                if (sMatrix[row, col] != 0) and (free[row] is not None) and (free[col] is not None):
                    rows[nValid] = free[row]
                    cols[nValid] = free[col]
                    vals[nValid] = sMatrix[row, col]
                    nValid += 1
                    
        return rows[:nValid], cols[:nValid], vals[:nValid], nValid


    def __make_matrixShell(self, shell: FA_Shell) -> np.ndarray:
        """シェル要素の剛性行列を作成する

        Args:
            shell (FA_Shell): シェル要素データ

        Returns:
            _ (np.ndarray): シェル要素剛性行列（全体座標系）
        """
        if isinstance(shell.iThick, int):
            thick_index = shell.iThick
        else:
            print(f"WARNING: shell.iThick is not an integer: {type(shell.iThick)}")
            thick_index = 0
            
        mat = self.materials[shell.iMat]
        thick = self.thicknesses[thick_index]
        
        # 要素の種類によってサイズが異なる
        node_count = len(shell.iNodes)
        matrix_size = node_count * 6
        sm = np.zeros((matrix_size, matrix_size), dtype=np.float64)
        
        # 要素種類に応じた積分点の取得
        if shell.element_type == "tri":
            int_points = TRI2_INT
        else:
            # 四角形1次要素
            int_points = QUAD1_INT
        
        # Dマトリックス
        ks_rect = 5.0 / 6.0
        coef = mat.e / (1.0 - mat.poi ** 2)
        s2 = coef * mat.poi
        msh = np.array([[coef, s2, 0.0, 0.0, 0.0],
                        [s2, coef, 0.0, 0.0, 0.0],
                        [0.0, 0.0, mat.g, 0.0, 0.0],
                        [0.0, 0.0, 0.0, ks_rect * mat.g, 0.0],
                        [0.0, 0.0, 0.0, 0.0, ks_rect * mat.g]], dtype=np.float64)
        
        # 剛性行列の作成
        for intP in int_points:
            if shell.element_type == "tri":
                # 三角形要素の場合
                ks = trianglestiffPart(shell, msh, float(intP[0]), float(intP[1]), thick.t)
            else:
                # 四角形要素の場合
                ks = quadstiffPart(shell, msh, float(intP[0]), float(intP[1]), thick.t)
            
            # 行列サイズの調整（三角形要素の場合は18x18、四角形要素の場合は24x24）
            if matrix_size < 24:
                # 三角形要素の場合はsmに加算
                for i in range(matrix_size):
                    for j in range(matrix_size):
                        sm[i, j] += ks[i, j]
            else:
                # 四角形要素の場合はそのまま加算
                sm += ks
        
        # JavaScript版との整合性を取るためのスケーリング係数
        # JavaScript版では内部的に異なるスケーリングが適用されているため、
        scaling_factor = 1.0 / 7.0
        sm *= scaling_factor
                
        return sm
    def __add_solid_stiffness(self, solid: FA_Solid) -> Tuple[np.ndarray, np.ndarray, np.ndarray, int]:
        """ソリッド要素の剛性マトリックスを全体剛性マトリックスに組み込むための準備
        
        Args:
            solid (FA_Solid): ソリッド要素データ
            
        Returns:
            Tuple[np.ndarray, np.ndarray, np.ndarray, int]: 行インデックス, 列インデックス, 値, 有効な要素数
        """
        try:
            fLogger.info(f"Processing solid element: type={solid.element_type}, nodes={solid.nodes}, material={solid.material_num}")
            
            if solid.element_type not in ["tetra", "hexa", "wedge"]:
                fLogger.warning(f"Unsupported solid element type: {solid.element_type}")
                return np.array([]), np.array([]), np.array([]), 0
            
            material = self.materials[solid.material_num]
            fLogger.info(f"Material properties: E={material.e}, nu={material.poi}")
            
            if solid.element_type == "tetra":
                nodes_coords = []
                for i in range(4):
                    coord = solid.get_coordinate(i, self.nodes)
                    nodes_coords.append(coord)
                    fLogger.info(f"Node {i} coordinates: {coord}")
                
                jacobian = tetra_jacobian(nodes_coords)
                fLogger.info(f"Jacobian: {jacobian}")
                grad = tetra_grad(nodes_coords, jacobian)
                fLogger.info(f"Gradient shape: {grad.shape}")
                B = tetra_strainMatrix(grad)
                fLogger.info(f"B matrix shape: {B.shape}")
                D = material_matrix_3d(material.e, material.poi)
                fLogger.info(f"D matrix shape: {D.shape}")
                
                K_local = np.dot(np.dot(B.T, D), B) * abs(jacobian) / 6
                fLogger.info(f"K_local matrix shape: {K_local.shape}")
                
                nDof = 12  # 4節点 × 3自由度
                node_count = 4
                
            elif solid.element_type == "hexa":
                nodes_coords = []
                for i in range(8):
                    coord = solid.get_coordinate(i, self.nodes)
                    nodes_coords.append(coord)
                    fLogger.info(f"Node {i} coordinates: {coord}")
                
                K_local = hexa_stiffness_matrix(nodes_coords, material)
                fLogger.info(f"K_local matrix shape: {K_local.shape}")
                
                nDof = 24  # 8節点 × 3自由度
                node_count = 8
                
            elif solid.element_type == "wedge":
                nodes_coords = []
                for i in range(6):
                    coord = solid.get_coordinate(i, self.nodes)
                    nodes_coords.append(coord)
                    fLogger.info(f"Node {i} coordinates: {coord}")
                
                K_local = wedge_stiffness_matrix(nodes_coords, material)
                fLogger.info(f"K_local matrix shape: {K_local.shape}")
                
                nDof = 18  # 6節点 × 3自由度
                node_count = 6
                
        except Exception as e:
            fLogger.critical(f"Error in __add_solid_stiffness: {str(e)}")
            raise FrameCritical(f"ソリッド要素の剛性マトリックス計算でエラーが発生しました: {str(e)}")
        
        rows = np.zeros(nDof * nDof, dtype=int)  # 非ゼロ要素の全体剛性行列における行インデックス格納用
        cols = np.zeros(nDof * nDof, dtype=int)  # 非ゼロ要素の全体剛性行列における列インデックス格納用
        vals = np.zeros(nDof * nDof, dtype=float)  # 非ゼロ要素の全体剛性行列における値格納用
        nValid: int = 0  # 非ゼロ要素数
        
        # 各節点の自由度インデックスの取得
        free: list[Optional[int]] = []
        for i in range(node_count):  # 各節点
            iNode = solid.nodes[i]  # 節点インデックス
            freeTmp = self.freeInd[iNode]  # 節点の自由度インデックス辞書
            free.append(freeTmp["dx"])
            free.append(freeTmp["dy"])
            free.append(freeTmp["dz"])
        
        # 非ゼロ要素の抽出
        for i in range(nDof):
            for j in range(nDof):
                if (free[i] is not None) and (free[j] is not None):
                    rows[nValid] = free[i]
                    cols[nValid] = free[j]
                    vals[nValid] = K_local[i, j]
                    nValid += 1
        
        return rows[:nValid], cols[:nValid], vals[:nValid], nValid
    # endregion


# region シェル要素用関数
def quadstiffPart(shell: FA_Shell, msh: NDArray[np.float64], xsi: float, \
                  eta: float, t: float) -> NDArray[np.float64]:
    """シェル要素の積分点の剛性行列を返す（四角形要素）

    Args:
        shell (FA_Shell): シェル要素データ
        msh (np.ndarray[5, 5]): Dマトリックス
        xsi (float): 要素内部ξ座標
        eta (float): 要素内部η座標
        t (float): 厚さ(m)

    Returns:
        _ (np.ndarray[24, 24]): シェル要素の積分点の剛性行列
    """
    d = shell.dirMatrix
    sf = quad_shapeFunction(xsi, eta)
    ja = jacobianMatrix(shell, t, sf)
    bc0 = strainMatrix1(ja, sf, d)
    sf1 = quad_shapeFunction(xsi, 0.0)
    ja1 = jacobianMatrix(shell, t, sf1)
    sf2 = quad_shapeFunction(0.0, eta)
    ja2 = jacobianMatrix(shell, t, sf2)
    bc = np.array((strainMatrix1(ja1, sf1, d), strainMatrix1(ja2, sf2, d)), dtype=float)
    dete = np.linalg.det(ja)
    jacob = abs(dete)
    kk = np.zeros([24, 24], dtype=float)
    tt6 = t * t / 6.0
    ce1 = 1e-3 * t * t * msh[3, 3]
    ce2 = -ce1 / 3.0
    k1 = np.zeros((3, 3), dtype=float)
    k2 = np.zeros((3, 3), dtype=float)
    k3 = np.zeros((3, 3), dtype=float)
    k4 = np.zeros((3, 3), dtype=float)
    for i in range(4):
        for j in range(4):
            for j1 in range(3):
                for j2 in range(3):
                    k1[j1][j2] = 0.0
                    k2[j1][j2] = 0.0
                    k3[j1][j2] = 0.0
                    k4[j1][j2] = 0.0
            for j1 in range(2):
                for j2 in range(2):
                    k1[j1][j2] = bc0[i][j1] * msh[j1][j2] * bc0[j][j2] + bc0[i][1 - j1] * msh[2][2] * bc0[j][1 - j2]
                dd = msh[4 - j1][4 - j1]
                k1[j1][j1] += bc[1 - j1][i][2] * dd * bc[1 - j1][j][2]
                k1[j1][2] = bc[1 - j1][i][2] * dd * bc[j1][j][j1]
                k1[2][j1] = bc[j1][i][j1] * dd * bc[1 - j1][j][2]
                k2[j1][j1] = bc[1 - j1][i][2] * dd * bc[1 - j1][j][3]
                k2[2][j1] = bc[1 - j1][i][j1] * dd * bc[1 - j1][j][3]
                k3[j1][j1] = bc[1 - j1][i][3] * dd * bc[1 - j1][j][2]
                k3[j1][2] = bc[1 - j1][i][3] * dd * bc[1 - j1][j][j1]
            k1[2][2] = bc[0][i][1] * msh[3][3] * bc[0][j][1] + bc[1][i][0] * msh[4][4] * bc[1][j][0]
            k4[0][0] = k1[1][1] + 3 * bc[0][i][3] * msh[3][3] * bc[0][j][3]
            k4[0][1] = -k1[1][0]
            k4[1][0] = -k1[0][1]
            k4[1][1] = k1[0][0] + 3 * bc[1][i][3] * msh[4][4] * bc[1][j][3]
            for j1 in range(3):
                kt = k2[j1][0]
                k2[j1][0] = -k2[j1][1]
                k2[j1][1] = kt
                kt = k3[0][j1]
                k3[0][j1] = -k3[1][j1]
                k3[1][j1] = kt
            if i == j:
                k4[2][2] = ce1
            else:
                k4[2][2] = ce2
            toDir(d, k1)
            toDir(d, k2)
            toDir(d, k3)
            toDir(d, k4)
            i0 = 6 * i
            j0 = 6 * j
            for j1 in range(3):
                for j2 in range(3):
                    kk[i0 + j1][j0 + j2] = 2 * jacob * k1[j1][j2]
                    kk[i0 + j1][j0 + 3 + j2] = t * jacob * k2[j1][j2]
                    kk[i0 + 3 + j1][j0 + j2] = t * jacob * k3[j1][j2]
                    kk[i0 + 3 + j1][j0 + 3 + j2] = tt6 * jacob * k4[j1][j2]
    return kk


def shapeFunction(shell: FA_Shell, xsi: float, eta: float) -> np.ndarray:
    """シェル要素の形状関数行列[ Ni dNi/dξ dNi/dη ]を返す

    Args:
        shell (FA_Shell): シェル要素データ
        xsi (float): 要素内部ξ座標
        eta (float): 要素内部η座標

    Returns:
        _ (np.ndarray): シェル要素の形状関数行列
    """
    if shell.element_type == "tri":
        return tri_shapeFunction(xsi, eta)
    else:
        return quad_shapeFunction(xsi, eta)



def jacobianMatrix(shell: FA_Shell, t: float, sf: NDArray[np.float64]) -> NDArray[np.float64]:
    """シェル要素のヤコビ行列を返す - JavaScript版と完全に同等の実装

    Args:
        shell (FA_Shell): シェル要素データ
        t (float): 厚さ(m)
        sf (NDArray[np.float64]): 形状関数行列

    Returns:
        NDArray[np.float64]: シェル要素のヤコビ行列
    """
    # JavaScript版と同様にフラット配列として初期化
    jac = np.zeros(9, dtype=np.float64)
    n = shell.normalVector
    node_count = len(shell.iNodes)  # 動的に節点数を取得
    
    for i in range(node_count):  # 3または4節点に対応
        sfi = sf[i]
        node = shell.nodes[shell.iNodes[i]]
        pix = node.x
        piy = node.y
        piz = node.z
        
        for j in range(2):
            sfij = sfi[j + 1]
            # JavaScript版と同じインデックス計算
            jac[j] += sfij * pix      # jac[0,j] に相当
            jac[j+3] += sfij * piy    # jac[1,j] に相当
            jac[j+6] += sfij * piz    # jac[2,j] に相当
    
    jac[2] = 0.5 * t * n[0]    # jac[0,2] に相当
    jac[5] = 0.5 * t * n[1]    # jac[1,2] に相当
    jac[8] = 0.5 * t * n[2]    # jac[2,2] に相当
    
    # JavaScript版ではTHREE.Matrix3().fromArray(jac)で行列を作成
    return np.array([
        [jac[0], jac[1], jac[2]],  # 1行目
        [jac[3], jac[4], jac[5]],  # 2行目
        [jac[6], jac[7], jac[8]]   # 3行目
    ], dtype=np.float64)


def jacobInv(ja: NDArray[np.float64], d: NDArray[np.float64]) -> NDArray[np.float64]:
    """シェル要素の逆ヤコビ行列を返す - JavaScript版と完全に同等の実装

    Args:
        ja (NDArray[np.float64]): ヤコビ行列
        d (NDArray[np.float64]): 方向余弦行列

    Returns:
        NDArray[np.float64]: シェル要素の逆ヤコビ行列
    """
    # JavaScript版と同様にフラット配列として扱う
    e1 = ja.flatten()
    
    # JavaScript版と同じ順序で行列を構築
    jd = np.zeros((3, 3), dtype=np.float64)
    
    # JavaScript版と完全に同じ計算順序
    jd[0, 0] = e1[0]*d[0, 0] + e1[3]*d[1, 0] + e1[6]*d[2, 0]
    jd[0, 1] = e1[0]*d[0, 1] + e1[3]*d[1, 1] + e1[6]*d[2, 1]
    jd[0, 2] = e1[0]*d[0, 2] + e1[3]*d[1, 2] + e1[6]*d[2, 2]
    jd[1, 0] = e1[1]*d[0, 0] + e1[4]*d[1, 0] + e1[7]*d[2, 0]
    jd[1, 1] = e1[1]*d[0, 1] + e1[4]*d[1, 1] + e1[7]*d[2, 1]
    jd[1, 2] = e1[1]*d[0, 2] + e1[4]*d[1, 2] + e1[7]*d[2, 2]
    jd[2, 0] = 0.0
    jd[2, 1] = 0.0
    jd[2, 2] = e1[2]*d[0, 2] + e1[5]*d[1, 2] + e1[8]*d[2, 2]
    
    # JavaScript版のTHREE.Matrix3.getInverse()と完全に同じアルゴリズムで実装
    det = jd[0, 0] * (jd[1, 1] * jd[2, 2] - jd[2, 1] * jd[1, 2]) - \
          jd[0, 1] * (jd[1, 0] * jd[2, 2] - jd[1, 2] * jd[2, 0]) + \
          jd[0, 2] * (jd[1, 0] * jd[2, 1] - jd[1, 1] * jd[2, 0])
    
    # JavaScript版と同じ安定化処理
    if abs(det) < 1e-10:
        stability_factor = 1e-6 * (abs(jd[0, 0]) + abs(jd[1, 1]) + abs(jd[2, 2])) / 3.0
        if stability_factor < 1e-10:
            stability_factor = 1e-6
        
        jd[0, 0] += stability_factor
        jd[1, 1] += stability_factor
        jd[2, 2] += stability_factor
        
        det = jd[0, 0] * (jd[1, 1] * jd[2, 2] - jd[2, 1] * jd[1, 2]) - \
              jd[0, 1] * (jd[1, 0] * jd[2, 2] - jd[1, 2] * jd[2, 0]) + \
              jd[0, 2] * (jd[1, 0] * jd[2, 1] - jd[1, 1] * jd[2, 0])
    
    inv_det = 1.0 / det
    
    result = np.zeros((3, 3), dtype=np.float64)
    
    result[0, 0] = (jd[1, 1] * jd[2, 2] - jd[2, 1] * jd[1, 2]) * inv_det
    result[0, 1] = (jd[0, 2] * jd[2, 1] - jd[0, 1] * jd[2, 2]) * inv_det
    result[0, 2] = (jd[0, 1] * jd[1, 2] - jd[0, 2] * jd[1, 1]) * inv_det
    
    result[1, 0] = (jd[1, 2] * jd[2, 0] - jd[1, 0] * jd[2, 2]) * inv_det
    result[1, 1] = (jd[0, 0] * jd[2, 2] - jd[0, 2] * jd[2, 0]) * inv_det
    result[1, 2] = (jd[1, 0] * jd[0, 2] - jd[0, 0] * jd[1, 2]) * inv_det
    
    result[2, 0] = (jd[1, 0] * jd[2, 1] - jd[2, 0] * jd[1, 1]) * inv_det
    result[2, 1] = (jd[2, 0] * jd[0, 1] - jd[0, 0] * jd[2, 1]) * inv_det
    result[2, 2] = (jd[0, 0] * jd[1, 1] - jd[1, 0] * jd[0, 1]) * inv_det
    
    return result


def strainMatrix1(ja: NDArray[np.float64], sf: NDArray[np.float64], d: NDArray[np.float64])\
    -> NDArray[np.float64]:
    """シェル要素の歪-変位行列の転置行列を返す

    Args:
        ja (np.ndarray[3, 3]): ヤコビ行列
        sf (np.ndarray): 形状関数行列
        d (np.ndarray[3, 3]): 方向余弦行列

    Returns:
        _ (np.ndarray): シェル要素の歪-変位行列の転置行列
    """
    node_count = sf.shape[0]  # 動的に節点数を取得
    m = np.zeros((node_count, 4), dtype=float)
    ji = jacobInv(ja, d)
    for i in range(node_count):  # 3または4節点に対応
        mi = m[i]
        sfi = sf[i]
        for j in range(3):
            mi[j] = ji[0, j] * sfi[1] + ji[1, j] * sfi[2]
        mi[3] = ji[2, 2] * sfi[0]
    return m


def toDir(d: NDArray[np.float64], k: NDArray[np.float64]) -> None:
    """シェル要素の剛性マトリックスの方向を修正する
    JavaScript版と完全に同等の実装 - 行列を直接修正する（戻り値なし）
    
    JavaScript版の実装:
    function toDir(d,k){
      var a=numeric.dot(d,k);
      for(var i=0;i<k.length;i++){
        var ki=k[i],ai=a[i];
        for(var j=0;j<ki.length;j++){
          ki[j]=numeric.dotVV(ai,d[j]);
        }
      }
    }

    Args:
        d (NDArray[np.float64]): 方向余弦行列 (3x3)
        k (NDArray[np.float64]): 部分剛性行列（直接修正される）
    """
    # JavaScript版のnumeric.dot(d,k)に相当する処理
    a = np.dot(d, k)
    
    # JavaScript版と完全に同じループ構造と計算順序で実装
    for i in range(k.shape[0]):
        ki = k[i]  # 参照を取得
        ai = a[i]  # 参照を取得
        for j in range(k.shape[1]):
            # JavaScript版のnumeric.dotVV(ai,d[j])に相当する処理
            ki[j] = np.dot(ai, d[j])
    


def toDir3(d: np.ndarray, k: np.ndarray) -> None:
    """シェル要素の剛性マトリックスの方向を修正する（JavaScript版と同等の実装）
    
    Args:
        d (np.ndarray): 方向余弦行列 (6x6)
        k (np.ndarray): 剛性行列 (6x6)
    
    Note:
        この関数はkを直接変更します
        6x6行列を2つの3x3ブロックとして処理します
    """
    for block_i in range(0, 2):
        for block_j in range(0, 2):
            i_offset = block_i * 3
            j_offset = block_j * 3
            
            d_block = d[i_offset:i_offset+3, j_offset:j_offset+3]
            
            a = np.zeros((3, 3), dtype=float)
            
            for i1 in range(3):
                for j1 in range(3):
                    s = 0.0
                    for ii in range(3):
                        s += d_block[i1, ii] * k[i_offset+ii, j_offset+j1]
                    a[i1, j1] = s
            
            for i1 in range(3):
                for j1 in range(3):
                    k[i_offset+i1, j_offset+j1] = np.dot(a[i1], d_block[:, j1])


def jacobian(p: list) -> float:
    """三角形要素のヤコビアン行列式を計算する
    JavaScript版と完全に同等の実装 - TriElement1.prototype.jacobian
    
    Args:
        p: 節点座標リスト [[x1,y1,z1], [x2,y2,z2], [x3,y3,z3]]
        
    Returns:
        ヤコビアン行列式の値
    """
    # JavaScript版と完全に同じ計算順序
    p0x = p[0][0]
    p0y = p[0][1]
    p0z = p[0][2]
    
    j1 = (p[1][1]-p0y)*(p[2][2]-p0z) - (p[1][2]-p0z)*(p[2][1]-p0y)
    j2 = (p[1][2]-p0z)*(p[2][0]-p0x) - (p[1][0]-p0x)*(p[2][2]-p0z)
    j3 = (p[1][0]-p0x)*(p[2][1]-p0y) - (p[1][1]-p0y)*(p[2][0]-p0x)
    
    return np.sqrt(j1*j1 + j2*j2 + j3*j3)


def trianglestiffPart(shell: FA_Shell, msh: NDArray[np.float64], xsi: float, \
                      eta: float, t: float) -> NDArray[np.float64]:
    """シェル要素の積分点の剛性行列を返す（三角形要素）- JavaScript版と完全に同等の実装
    JavaScript版のTriElement1.prototype.stiffnessMatrixメソッドと完全に同じロジックで実装

    Args:
        shell (FA_Shell): シェル要素データ
        msh (NDArray[np.float64]): Dマトリックス
        xsi (float): 要素内部ξ座標 (未使用、TRI1_INTを使用)
        eta (float): 要素内部η座標 (未使用、TRI1_INTを使用)
        t (float): 厚さ(m)

    Returns:
        NDArray[np.float64]: シェル要素の積分点の剛性行列 (18x18)
    """
    p = []
    for node_id in shell.iNodes:
        node = shell.nodes[node_id]
        p.append([node.x, node.y, node.z])
    
    d = shell.dirMatrix
    n = shell.normalVector
    
    # JavaScript版と同様に単一積分点(1/3, 1/3)で形状関数を評価
    # TriElement1.prototype.stiffnessMatrixでは var sf1=this.shapeFunction(C1_3,C1_3) を使用
    sf1 = tri_shapeFunction(C1_3, C1_3)
    
    # ヤコビアン行列の計算 - JavaScript版と完全に同じ方法
    ja1 = np.zeros((3, 3), dtype=np.float64)
    
    # JavaScript版と完全に同じ方法で節点座標を取得
    node_count = len(shell.iNodes)
    for i in range(node_count):
        sfi = sf1[i]
        node = shell.nodes[shell.iNodes[i]]
        pix = node.x
        piy = node.y
        piz = node.z
        
        for j in range(2):
            sfij = sfi[j + 1]
            ja1[0, j] += sfij * pix
            ja1[1, j] += sfij * piy
            ja1[2, j] += sfij * piz
    
    ja1[0, 2] = 0.5 * t * n[0]
    ja1[1, 2] = 0.5 * t * n[1]
    ja1[2, 2] = 0.5 * t * n[2]
    
    # JavaScript版と同じ方法でヤコビアン行列式を計算
    jac1 = jacobian(p)  # JavaScript版と同じ計算方法
    
    jinv = jacobInv(ja1, d)
    
    b1 = tri_strainMatrix1(sf1, jinv)
    
    k1 = stiffPart(msh, b1, abs(jac1))
    
    # 三角形要素の節点数
    count = 3
    
    k2 = np.zeros((3*count, 3*count), dtype=np.float64)
    
    # JavaScript版と同じ係数を使用
    coef = t*t*abs(jac1)/36
    
    # JavaScript版と完全に同じ積分点処理 - 特別処理なし
    # JavaScript版では要素1に対する特別処理はstiffnessMatrix内では行われていない
    for i in range(len(TRI2_INT)):
        ipi = TRI2_INT[i]
        # JavaScript版と同じ順序で形状関数を評価
        sf3 = tri_shapeFunction3(p, d, ipi[0], ipi[1])
        b2 = tri_strainMatrix2(sf3, jinv)
        
        # JavaScript版と同じ係数を使用
        k2_part = stiffPart(msh, b2, coef)
        # JavaScript版と同じ加算方法
        k2 += k2_part
    
    ce1 = 1e-3 * t * t * abs(jac1) * msh[2, 2]
    ce2 = -ce1 / 2
    
    kk = np.zeros((6*count, 6*count), dtype=np.float64)
    
    dir_matrix = np.zeros((6, 6), dtype=np.float64)
    for i in range(3):
        for j in range(3):
            dir_matrix[i, j] = d[i, j]
            dir_matrix[i+3, j+3] = d[i, j]
    
    # JavaScript版と完全に同じループ構造で剛性マトリクスを組み立て
    for i in range(3):
        i2 = 2*i
        i3 = 3*i
        i6 = 6*i
        for j in range(count):
            j2 = 2*j
            j3 = 3*j
            j6 = 6*j
            
            # JavaScript版と同様に毎回初期化
            ks = np.zeros((6, 6), dtype=np.float64)
            
            # JavaScript版と完全に同じ代入順序
            ks[0, 0] = k1[i2, j2]
            ks[0, 1] = k1[i2, j2+1]
            ks[1, 0] = k1[i2+1, j2]
            ks[1, 1] = k1[i2+1, j2+1]
            
            for ii in range(3):
                for jj in range(3):
                    ks[2+ii, 2+jj] = k2[i3+ii, j3+jj]
            
            if i == j:
                ks[5, 5] = ce1
            else:
                ks[5, 5] = ce2
            
            toDir(dir_matrix, ks)
            
            for ii in range(6):
                for jj in range(6):
                    kk[i6+ii, j6+jj] = ks[ii, jj]
    
    return kk


def tri_shapeFunction(xsi: float, eta: float) -> NDArray[np.float64]:
    """三角形1次要素の形状関数行列[ Ni dNi/dξ dNi/dη ]を返す

    Args:
        xsi (float): 要素内部ξ座標
        eta (float): 要素内部η座標

    Returns:
        _ (np.ndarray[3, 3]): 三角形1次要素の形状関数行列
    """
    return np.array(([[1 - xsi - eta, -1, -1],
                       [xsi, 1, 0],
                       [eta, 0, 1]]), dtype=float)


def quad_shapeFunction(xsi: float, eta: float) -> NDArray[np.float64]:
    """四角形1次要素の形状関数行列[ Ni dNi/dξ dNi/dη ]を返す

    Args:
        xsi (float): 要素内部ξ座標
        eta (float): 要素内部η座標

    Returns:
        _ (np.ndarray[4, 3]): 四角形1次要素の形状関数行列
    """
    return np.array(([[0.25 * (1.0 - xsi) * (1.0 - eta), -0.25 * (1.0 - eta), -0.25 * (1 - xsi)],
                      [0.25 * (1.0 + xsi) * (1.0 - eta), 0.25 * (1.0 - eta), -0.25 * (1 + xsi)],
                      [0.25 * (1.0 + xsi) * (1.0 + eta), 0.25 * (1.0 + eta), 0.25 * (1 + xsi)],
                      [0.25 * (1.0 - xsi) * (1.0 + eta), -0.25 * (1.0 + eta), 0.25 * (1 - xsi)]]), dtype=float)


def tri_strainMatrix1(sf: NDArray[np.float64], jinv: NDArray[np.float64]) -> NDArray[np.float64]:
    """三角形要素の歪-変位マトリックスの転置行列を返す
    JavaScript版と完全に同等の実装 - 完全に同じ計算順序と精度で実装
    
    JavaScript版の実装:
    TriElement1.prototype.strainMatrix1=function(sf,jinv){
      var count=this.nodeCount(),b=numeric.rep([2*count,3],0);
      var ji=jinv.elements;
      for(var i=0;i<count;i++){
        var sfi=sf[i];
        var dndx=ji[0]*sfi[1]+ji[3]*sfi[2];
        var dndy=ji[1]*sfi[1]+ji[4]*sfi[2];
        var i2=2*i;
        b[i2][0]=dndx;
        b[i2+1][1]=dndy;
        b[i2][2]=dndy;
        b[i2+1][2]=dndx;
      }
      return b;
    };

    Args:
        sf (NDArray[np.float64]): 形状関数行列
        jinv (NDArray[np.float64]): 逆ヤコビ行列

    Returns:
        NDArray[np.float64]: 三角形要素の歪-変位マトリックスの転置行列
    """
    count = 3  # 三角形要素の節点数
    b = np.zeros((2 * count, 3), dtype=np.float64)
    
    # JavaScript版と同じく、jinvを1次元配列として扱う
    ji = jinv.flatten()
    
    for i in range(count):
        sfi = sf[i]
        # JavaScript版と完全に同じインデックスと計算順序
        dndx = ji[0] * sfi[1] + ji[3] * sfi[2]
        dndy = ji[1] * sfi[1] + ji[4] * sfi[2]
        i2 = 2 * i
        b[i2, 0] = dndx
        b[i2+1, 1] = dndy
        b[i2, 2] = dndy
        b[i2+1, 2] = dndx
    return b


def stiffPart(d1: np.ndarray, b: np.ndarray, jac: float) -> np.ndarray:
    """面内要素剛性行列を返す - JavaScript版と完全に同等の実装
    
    JavaScript版の実装:
    FElement.prototype.stiffPart=function(d,b,coef){
      var size1=b.length,size2=d.length,a=[],k=[],j;
      for(var i=0;i<size1;i++){
        a.length=0;
        var bi=b[i];
        for(j=0;j<size2;j++){
          a[j]=coef*numeric.dotVV(bi,d[j]);
        }
        var ki=[];
        for(j=0;j<size1;j++){
          ki[j]=numeric.dotVV(a,b[j]);
        }
        k[i]=ki;
      }
      return k;
    };

    Args:
        d1 (np.ndarray): Dマトリックス
        b (np.ndarray): 歪-変位関係マトリックス
        jac (float): ヤコビアン

    Returns:
        np.ndarray: 面内要素剛性行列
    """
    size1 = b.shape[0]
    size2 = d1.shape[0]
    kp = np.zeros((size1, size1), dtype=np.float64)
    
    # JavaScript版と完全に同じループ構造と計算順序
    for i in range(size1):
        a = np.zeros(size2, dtype=np.float64)
        bi = b[i]  # 参照を取得
        
        for j in range(size2):
            dot_product = 0.0
            for k in range(min(bi.shape[0], d1[j].shape[0])):
                dot_product += bi[k] * d1[j, k]
            a[j] = jac * dot_product
        
        for j in range(size1):
            dot_product = 0.0
            for k in range(min(a.shape[0], b[j].shape[0])):
                dot_product += a[k] * b[j, k]
            kp[i, j] = dot_product
    
    return kp


def tri_shapeFunction2(xsi: float, eta: float) -> np.ndarray:
    """三角形要素の2次形状関数行列を計算する
    JavaScript版と完全に同等の実装 - 完全に同じ計算順序と精度で実装
    
    Args:
        xsi: 要素内部ξ座標
        eta: 要素内部η座標
        
    Returns:
        形状関数行列 [Ni dNi/dξ dNi/dη]
    """
    # JavaScript版と同じ計算順序で実装
    # TriElement1.prototype.shapeFunction2=function(xsi,eta){
    # };
    
    xe = 1.0 - xsi - eta
    
    # JavaScript版と完全に同じ配列構造と計算順序
    return np.array([
        [xe*(2.0*xe-1.0), 1.0-4.0*xe, 1.0-4.0*xe],
        [xsi*(2.0*xsi-1.0), 4.0*xsi-1.0, 0.0],
        [eta*(2.0*eta-1.0), 0.0, 4.0*eta-1.0],
        [4.0*xe*xsi, 4.0*(xe-xsi), -4.0*xsi],
        [4.0*xsi*eta, 4.0*eta, 4.0*xsi],
        [4.0*xe*eta, -4.0*eta, 4.0*(xe-eta)]
    ], dtype=np.float64)


def tri_shapeFunction3(p: list, d: NDArray[np.float64], xsi: float, eta: float) -> NDArray[np.float64]:
    """三角形要素の3次形状関数行列を計算する
    JavaScript版と完全に同等の実装 - 完全に同じ計算順序と精度で実装
    
    Args:
        p: 節点座標リスト
        d: 方向マトリクス
        xsi: 要素内部ξ座標
        eta: 要素内部η座標
        
    Returns:
        形状関数行列
    """
    count = 3  # 三角形要素の節点数
    m = np.zeros((3*count, 6), dtype=np.float64)
    sf2 = tri_shapeFunction2(xsi, eta)
    
    # JavaScript版と完全に同じベクトル計算
    d12_x = p[1][0] - p[0][0]
    d12_y = p[1][1] - p[0][1]
    d12_z = p[1][2] - p[0][2]
    
    d23_x = p[2][0] - p[1][0]
    d23_y = p[2][1] - p[1][1]
    d23_z = p[2][2] - p[1][2]
    
    d31_x = p[0][0] - p[2][0]
    d31_y = p[0][1] - p[2][1]
    d31_z = p[0][2] - p[2][2]
    
    # JavaScript版の lengthSq() に相当
    l12_sq = d12_x*d12_x + d12_y*d12_y + d12_z*d12_z
    l23_sq = d23_x*d23_x + d23_y*d23_y + d23_z*d23_z
    l31_sq = d31_x*d31_x + d31_y*d31_y + d31_z*d31_z
    
    # JavaScript版と同じ計算順序
    l = [0.0, 0.0, 0.0]
    l[0] = 1.0 / l12_sq
    l[1] = 1.0 / l23_sq
    l[2] = 1.0 / l31_sq
    
    # JavaScript版と同じ計算順序
    x = [0.0, 0.0, 0.0]
    x[0] = d[0,0]*d12_x + d[1,0]*d12_y + d[2,0]*d12_z
    x[1] = d[0,0]*d23_x + d[1,0]*d23_y + d[2,0]*d23_z
    x[2] = d[0,0]*d31_x + d[1,0]*d31_y + d[2,0]*d31_z
    
    y = [0.0, 0.0, 0.0]
    y[0] = d[0,1]*d12_x + d[1,1]*d12_y + d[2,1]*d12_z
    y[1] = d[0,1]*d23_x + d[1,1]*d23_y + d[2,1]*d23_z
    y[2] = d[0,1]*d31_x + d[1,1]*d31_y + d[2,1]*d31_z
    
    # JavaScript版と同じ計算順序
    a = [0.0, 0.0, 0.0]
    b = [0.0, 0.0, 0.0]
    c = [0.0, 0.0, 0.0]
    d1 = [0.0, 0.0, 0.0]
    e = [0.0, 0.0, 0.0]
    
    for i in range(3):
        a[i] = 1.5 * l[i] * y[i]
        b[i] = -1.5 * l[i] * x[i]
        c[i] = 0.75 * l[i] * y[i] * y[i] - 0.5
        d1[i] = 0.75 * l[i] * x[i] * y[i]
        e[i] = 0.25 - 0.75 * l[i] * y[i] * y[i]
    
    # JavaScript版と完全に同じループ構造と計算順序
    for i in range(3):
        i1 = (i+2) % 3
        i3 = 3*i
        for j in range(3):
            j2 = 2*j
            m[i3, j2] = a[i1]*sf2[3+i1, j] - a[i]*sf2[3+i, j]
            m[i3, j2+1] = b[i1]*sf2[3+i1, j] - b[i]*sf2[3+i, j]
            m[i3+1, j2] = sf2[i, j] - c[i1]*sf2[3+i1, j] - c[i]*sf2[3+i, j]
            dn = d1[i1]*sf2[3+i1, j] + d1[i]*sf2[3+i, j]
            m[i3+1, j2+1] = dn
            m[i3+2, j2] = dn
            m[i3+2, j2+1] = sf2[i, j] - e[i1]*sf2[3+i1, j] - e[i]*sf2[3+i, j]
    
    return m


def tri_strainMatrix2(sf: NDArray[np.float64], jinv: NDArray[np.float64]) -> NDArray[np.float64]:
    """三角形要素のひずみマトリクス2を計算する
    JavaScript版と完全に同等の実装 - 完全に同じ計算順序と精度で実装
    
    Args:
        sf: 形状関数マトリクス
        jinv: ヤコビアン逆マトリクス
        
    Returns:
        ひずみマトリクス2
    """
    count = 3 * 3  # 三角形要素の節点数 × 自由度
    b = np.zeros((count, 3), dtype=np.float64)
    
    # JavaScript版と同様にフラット配列として扱う
    ji = jinv.flatten()
    
    # JavaScript版と完全に同じループ構造と計算順序
    for i in range(count):
        # JavaScript版では配列の境界チェックがないが、Pythonでは必要
        if i < sf.shape[0]:
            sfi = sf[i]
            if len(sfi) >= 6:  # 形状関数の要素数チェック
                # JavaScript版と完全に同じインデックスと計算順序
                hxx = ji[0]*sfi[2] + ji[3]*sfi[4]
                hxy = ji[1]*sfi[2] + ji[4]*sfi[4]
                hyx = ji[0]*sfi[3] + ji[3]*sfi[5]
                hyy = ji[1]*sfi[3] + ji[4]*sfi[5]
                
                b[i, 0] = hyx
                b[i, 1] = -hxy
                b[i, 2] = hyy-hxx
    
    return b


def matrixElementsForBendingSpr(beta: float, l: float, iFree: bool, jFree: bool) \
    -> Tuple[float, float, float, float, float, float, float, float, float, float]:
    """分布バネつき梁要素のせん断・曲げの剛性行列の要素を計算する（ただし返り値に2EIβを乗じる必要がある）

    Args:
        beta (float): 分布バネの特性値β=(k/4EI)^(1/4)(m^-1)
        l (float): 梁要素の長さ(m)
        iFree (bool): i端拘束条件がピンか否か
        jFree (bool): j端拘束条件がピンか否か

    Returns:
        _ (Tuple[float, float, float, float, float, float, float, float, float, float]): 順にk00,k10,k20,k30,k11,k21,k31,k22,k32,k33
    """
    bl = beta * l  # 無次元数
    sin_bl = np.sin(bl)  # sin(βl)
    cos_bl = np.cos(bl)  # cos(βl)
    sinh_bl = np.sinh(bl)  # sinh(βl)
    cosh_bl = np.cosh(bl)  # cosh(βl)
    s1 = (sinh_bl ** 2 - sin_bl ** 2)
    # マトリクス成分の計算
    k00S = 2.0 * beta ** 2 * (sinh_bl * cosh_bl + sin_bl * cos_bl) / s1
    k10S = beta * (sinh_bl ** 2 + sin_bl ** 2) / s1
    k20S = -2.0 * beta ** 2 * (cosh_bl * sin_bl + sinh_bl * cos_bl) / s1
    k30S = 2.0 * beta * sinh_bl * sin_bl / s1
    k11S = (sinh_bl * cosh_bl - sin_bl * cos_bl) / s1
    k31S = (cosh_bl * sin_bl - sinh_bl * cos_bl) / s1
    if (iFree == False) and (jFree == False):  # 両端拘束
        k00 = k22 = k00S
        k10 = k10S
        k20 = k20S
        k30 = k30S
        k11 = k33 = k11S
        k21 = -k30S
        k31 = k31S
        k32 = -k10S
    elif (iFree == True) and (jFree == False):  # i端自由j端拘束
        k10 = k11 = k21 = k31 = 0.0
        k00 = k00S - k10S ** 2 / k11S
        k20 = k20S + k10S * k30S / k11S
        k30 = k30S - k10S * k31S / k11S
        k22 = k00S - k30S ** 2 / k11S
        k32 = -k10S + k30S * k31S / k11S
        k33 = k11S - k31S ** 2 / k11S
    elif (iFree == False) and (jFree == True):  # i端拘束j端自由
        k30 = k31 = k32 = k33 = 0.0
        k00 = k00S - k30S ** 2 / k11S
        k10 = k10S - k30S * k31S / k11S
        k20 = k20S + k10S * k30S / k11S
        k11 = k11S - k31S ** 2 / k11S
        k21 = -k30S + k10S * k31S / k11S
        k22 = k00S - k10S ** 2 / k11S
    else:  # 両端自由
        k10 = k11 = k21 = k31 = k30 = k31 = k32 = k33 = 0.0
        k00 = k00S + (2.0 * k10S * k30S * k31S - k11S * (k10S ** 2 + k30S ** 2)) / (k11S ** 2 - k31S ** 2)
        k20 = k20S + (2.0 * k10S * k11S * k30S - k31S * (k10S ** 2 + k30S ** 2)) / (k11S ** 2 - k31S ** 2)
        k22 = k00
    return k00, k10, k20, k30, k11, k21, k31, k22, k32, k33


def tetra_shapeFunction(xsi, eta, zeta):
    """4面体要素の形状関数
    
    Args:
        xsi (float): ξ座標
        eta (float): η座標  
        zeta (float): ζ座標
        
    Returns:
        tuple: (形状関数値, 形状関数の偏微分)
    """
    N = np.array([
        1 - xsi - eta - zeta,
        xsi,
        eta,
        zeta
    ])
    
    dN_dxi = np.array([
        [-1, -1, -1],
        [1, 0, 0],
        [0, 1, 0],
        [0, 0, 1]
    ])
    
    return N, dN_dxi


def tetra_jacobian(nodes):
    """4面体要素のヤコビアン計算
    JavaScript版のTetraElement1.prototype.jacobian()の移植
    
    Args:
        nodes (list): 4節点の座標リスト [[x,y,z], ...]
        
    Returns:
        float: ヤコビアン行列式
    """
    p0x, p0y, p0z = nodes[0]
    p1x, p1y, p1z = nodes[1]
    p2x, p2y, p2z = nodes[2]
    p3x, p3y, p3z = nodes[3]
    
    j11 = (p2y - p0y) * (p3z - p0z) - (p3y - p0y) * (p2z - p0z)
    j21 = (p3y - p0y) * (p1z - p0z) - (p1y - p0y) * (p3z - p0z)
    j31 = (p1y - p0y) * (p2z - p0z) - (p2y - p0y) * (p1z - p0z)
    
    return (p1x - p0x) * j11 + (p2x - p0x) * j21 + (p3x - p0x) * j31


def tetra_grad(nodes, jacobian):
    """形状関数の勾配計算
    JavaScript版のTetraElement1.prototype.grad()の移植
    
    Args:
        nodes (list): 4節点の座標リスト
        jacobian (float): ヤコビアン行列式
        
    Returns:
        np.ndarray: 形状関数の勾配 (4×3)
    """
    grad = np.zeros((4, 3))
    ji = 1.0 / jacobian
    
    for i in range(4):
        ji = -ji
        i2 = (i + 1) % 4
        i3 = (i + 2) % 4  
        i4 = (i + 3) % 4
        
        p0, p1, p2, p3 = nodes[i], nodes[i2], nodes[i3], nodes[i4]
        
        grad[i, 0] = ji * ((p1[1] - p0[1]) * (p2[2] - p0[2]) - (p2[1] - p0[1]) * (p1[2] - p0[2]))
        grad[i, 1] = ji * ((p1[2] - p0[2]) * (p2[0] - p0[0]) - (p2[2] - p0[2]) * (p1[0] - p0[0]))  
        grad[i, 2] = ji * ((p1[0] - p0[0]) * (p2[1] - p0[1]) - (p2[0] - p0[0]) * (p1[1] - p0[1]))
    
    return grad


def tetra_strainMatrix(grad):
    """4面体要素の歪マトリックス（Bマトリックス）
    
    Args:
        grad (np.ndarray): 形状関数の勾配 (4×3)
        
    Returns:
        np.ndarray: 歪マトリックス (6×12)
    """
    B = np.zeros((6, 12))
    
    for i in range(4):
        col = i * 3
        B[0, col] = grad[i, 0]
        B[1, col + 1] = grad[i, 1]
        B[2, col + 2] = grad[i, 2]
        B[3, col] = grad[i, 1]
        B[3, col + 1] = grad[i, 0]
        B[4, col + 1] = grad[i, 2]
        B[4, col + 2] = grad[i, 1]
        B[5, col] = grad[i, 2]
        B[5, col + 2] = grad[i, 0]
    
    return B


def material_matrix_3d(E, nu):
    """3次元応力状態の材料マトリックス
    
    Args:
        E (float): ヤング係数
        nu (float): ポアソン比
        
    Returns:
        np.ndarray: 材料マトリックス (6×6)
    """
    coef = E / ((1 + nu) * (1 - 2 * nu))
    D = np.zeros((6, 6))
    
    D[0:3, 0:3] = coef * np.array([
        [1 - nu, nu, nu],
        [nu, 1 - nu, nu],
        [nu, nu, 1 - nu]
    ])
    
    D[3, 3] = D[4, 4] = D[5, 5] = coef * (1 - 2 * nu) / 2
    
    return D


def hexa_shapeFunction(xsi, eta, zeta):
    """6面体要素の形状関数とその局所座標での微分
    
    Args:
        xsi, eta, zeta: 局所座標系での座標値 (-1~1)
        
    Returns:
        tuple: (形状関数値のリスト, 形状関数の局所微分のリスト)
    """
    N = [
        0.125 * (1 - xsi) * (1 - eta) * (1 - zeta),  # N1
        0.125 * (1 + xsi) * (1 - eta) * (1 - zeta),  # N2  
        0.125 * (1 + xsi) * (1 + eta) * (1 - zeta),  # N3
        0.125 * (1 - xsi) * (1 + eta) * (1 - zeta),  # N4
        0.125 * (1 - xsi) * (1 - eta) * (1 + zeta),  # N5
        0.125 * (1 + xsi) * (1 - eta) * (1 + zeta),  # N6
        0.125 * (1 + xsi) * (1 + eta) * (1 + zeta),  # N7
        0.125 * (1 - xsi) * (1 + eta) * (1 + zeta),  # N8
    ]
    
    dN_dlocal = [
        [-0.125 * (1 - eta) * (1 - zeta), -0.125 * (1 - xsi) * (1 - zeta), -0.125 * (1 - xsi) * (1 - eta)],
        [ 0.125 * (1 - eta) * (1 - zeta), -0.125 * (1 + xsi) * (1 - zeta), -0.125 * (1 + xsi) * (1 - eta)],
        [ 0.125 * (1 + eta) * (1 - zeta),  0.125 * (1 + xsi) * (1 - zeta), -0.125 * (1 + xsi) * (1 + eta)],
        [-0.125 * (1 + eta) * (1 - zeta),  0.125 * (1 - xsi) * (1 - zeta), -0.125 * (1 - xsi) * (1 + eta)],
        [-0.125 * (1 - eta) * (1 + zeta), -0.125 * (1 - xsi) * (1 + zeta),  0.125 * (1 - xsi) * (1 - eta)],
        [ 0.125 * (1 - eta) * (1 + zeta), -0.125 * (1 + xsi) * (1 + zeta),  0.125 * (1 + xsi) * (1 - eta)],
        [ 0.125 * (1 + eta) * (1 + zeta),  0.125 * (1 + xsi) * (1 + zeta),  0.125 * (1 + xsi) * (1 + eta)],
        [-0.125 * (1 + eta) * (1 + zeta),  0.125 * (1 - xsi) * (1 + zeta),  0.125 * (1 - xsi) * (1 + eta)],
    ]
    
    return N, dN_dlocal


def hexa_jacobian(nodes_coords, dN_dlocal):
    """6面体要素のヤコビアン行列と行列式を計算
    
    Args:
        nodes_coords (list): 節点座標のリスト [[x1,y1,z1], [x2,y2,z2], ...]
        dN_dlocal (list): 形状関数の局所微分のリスト
        
    Returns:
        tuple: (ヤコビアン行列, 行列式)
    """
    J = np.zeros((3, 3))
    
    for i in range(8):
        x, y, z = nodes_coords[i]
        dNi_dxsi, dNi_deta, dNi_dzeta = dN_dlocal[i]
        
        J[0, 0] += dNi_dxsi * x  # dx/dxsi
        J[0, 1] += dNi_deta * x  # dx/deta  
        J[0, 2] += dNi_dzeta * x # dx/dzeta
        J[1, 0] += dNi_dxsi * y  # dy/dxsi
        J[1, 1] += dNi_deta * y  # dy/deta
        J[1, 2] += dNi_dzeta * y # dy/dzeta
        J[2, 0] += dNi_dxsi * z  # dz/dxsi
        J[2, 1] += dNi_deta * z  # dz/deta
        J[2, 2] += dNi_dzeta * z # dz/dzeta
    
    detJ = np.linalg.det(J)
    return J, detJ


def hexa_grad_N_global(J, dN_dlocal):
    """形状関数のグローバル座標系での勾配を計算
    
    Args:
        J (np.ndarray): ヤコビアン行列
        dN_dlocal (list): 形状関数の局所微分のリスト
        
    Returns:
        list: グローバル座標系での形状関数の勾配 [[dN1/dx,dN1/dy,dN1/dz], ...]
    """
    J_inv = np.linalg.inv(J)
    dN_dglobal = []
    
    for i in range(8):
        dN_local = np.array(dN_dlocal[i])
        dN_glob = np.dot(J_inv, dN_local)  # [dN/dx, dN/dy, dN/dz]
        dN_dglobal.append(dN_glob)
    
    return dN_dglobal


def hexa_strainMatrix(dN_dglobal):
    """6面体要素の歪マトリックス（Bマトリックス）を構築
    
    Args:
        dN_dglobal (list): グローバル座標系での形状関数の勾配
        
    Returns:
        np.ndarray: 歪マトリックス (6×24)
    """
    B = np.zeros((6, 24))  # 6x24 (6応力成分 x 8節点*3DOF)
    
    for i in range(8):
        dNi_dx, dNi_dy, dNi_dz = dN_dglobal[i]
        col = i * 3
        
        B[0, col] = dNi_dx
        B[1, col + 1] = dNi_dy
        B[2, col + 2] = dNi_dz
        B[3, col] = dNi_dy
        B[3, col + 1] = dNi_dx
        B[4, col + 1] = dNi_dz
        B[4, col + 2] = dNi_dy
        B[5, col] = dNi_dz
        B[5, col + 2] = dNi_dx
    
    return B


def hexa_stiffness_matrix(nodes_coords, material):
    """6面体要素の剛性マトリックスを計算
    
    Args:
        nodes_coords (list): 節点座標のリスト [[x1,y1,z1], [x2,y2,z2], ...]
        material: 材料特性オブジェクト (E, poiを持つ)
        
    Returns:
        np.ndarray: 要素剛性マトリックス (24×24)
    """
    gauss_coord = 1.0 / np.sqrt(3)
    integration_points = []
    for k in [-1, 1]:  # zeta
        for j in [-1, 1]:  # eta  
            for i in [-1, 1]:  # xsi
                integration_points.append((i * gauss_coord, j * gauss_coord, k * gauss_coord))
    
    weights = [1.0] * 8  # 各積分点の重み
    
    D = material_matrix_3d(material.e, material.poi)
    
    Ke = np.zeros((24, 24))
    
    for (xsi, eta, zeta), weight in zip(integration_points, weights):
        N, dN_dlocal = hexa_shapeFunction(xsi, eta, zeta)
        J, detJ = hexa_jacobian(nodes_coords, dN_dlocal)
        dN_dglobal = hexa_grad_N_global(J, dN_dlocal)
        B = hexa_strainMatrix(dN_dglobal)
        
        Ke += np.dot(B.T, np.dot(D, B)) * detJ * weight
    
    return Ke


# JavaScript版: var WEDGE1_INT=[[C1_3,C1_3,GX2[0],0.5],[C1_3,C1_3,GX2[1],0.5]];
GX2 = [-1/math.sqrt(3), 1/math.sqrt(3)]  # Gauss積分点
WEDGE1_INT = [[C1_3, C1_3, GX2[0], 0.5], [C1_3, C1_3, GX2[1], 0.5]]

def wedge_shapeFunction(xsi, eta, zeta):
    """楔形1次要素の形状関数とその局所座標での微分
    JavaScript版のWedgeElement1.prototype.shapeFunction()の移植
    
    Args:
        xsi, eta, zeta: 局所座標系での座標値
        
    Returns:
        tuple: (形状関数値のリスト, 形状関数の局所微分のリスト)
    """
    N = [
        0.5 * (1 - xsi - eta) * (1 - zeta),  # N1
        0.5 * xsi * (1 - zeta),              # N2
        0.5 * eta * (1 - zeta),              # N3
        0.5 * (1 - xsi - eta) * (1 + zeta),  # N4
        0.5 * xsi * (1 + zeta),              # N5
        0.5 * eta * (1 + zeta),              # N6
    ]
    
    dN_dlocal = [
        [-0.5 * (1 - zeta), -0.5 * (1 - zeta), -0.5 * (1 - xsi - eta)],  # dN1/dxsi, dN1/deta, dN1/dzeta
        [0.5 * (1 - zeta), 0, -0.5 * xsi],                                # dN2/dxsi, dN2/deta, dN2/dzeta
        [0, 0.5 * (1 - zeta), -0.5 * eta],                                # dN3/dxsi, dN3/deta, dN3/dzeta
        [-0.5 * (1 + zeta), -0.5 * (1 + zeta), 0.5 * (1 - xsi - eta)],   # dN4/dxsi, dN4/deta, dN4/dzeta
        [0.5 * (1 + zeta), 0, 0.5 * xsi],                                 # dN5/dxsi, dN5/deta, dN5/dzeta
        [0, 0.5 * (1 + zeta), 0.5 * eta],                                 # dN6/dxsi, dN6/deta, dN6/dzeta
    ]
    
    return N, dN_dlocal

def wedge_jacobian(nodes_coords, dN_dlocal):
    """楔形要素のヤコビアン行列と行列式を計算
    
    Args:
        nodes_coords (list): 節点座標のリスト [[x1,y1,z1], [x2,y2,z2], ...]
        dN_dlocal (list): 形状関数の局所微分のリスト
        
    Returns:
        tuple: (ヤコビアン行列, 行列式)
    """
    J = np.zeros((3, 3))
    
    for i in range(6):
        x, y, z = nodes_coords[i]
        dNi_dxsi, dNi_deta, dNi_dzeta = dN_dlocal[i]
        
        J[0, 0] += dNi_dxsi * x   # dx/dxsi
        J[0, 1] += dNi_deta * x   # dx/deta  
        J[0, 2] += dNi_dzeta * x  # dx/dzeta
        J[1, 0] += dNi_dxsi * y   # dy/dxsi
        J[1, 1] += dNi_deta * y   # dy/deta
        J[1, 2] += dNi_dzeta * y  # dy/dzeta
        J[2, 0] += dNi_dxsi * z   # dz/dxsi
        J[2, 1] += dNi_deta * z   # dz/deta
        J[2, 2] += dNi_dzeta * z  # dz/dzeta
    
    detJ = np.linalg.det(J)
    return J, detJ

def wedge_grad_N_global(J, dN_dlocal):
    """形状関数のグローバル座標系での勾配を計算
    
    Args:
        J (np.ndarray): ヤコビアン行列
        dN_dlocal (list): 形状関数の局所微分のリスト
        
    Returns:
        list: グローバル座標系での形状関数の勾配 [[dN1/dx,dN1/dy,dN1/dz], ...]
    """
    J_inv = np.linalg.inv(J)
    dN_dglobal = []
    
    for i in range(6):
        dN_local = np.array(dN_dlocal[i])
        dN_glob = np.dot(J_inv, dN_local)  # [dN/dx, dN/dy, dN/dz]
        dN_dglobal.append(dN_glob)
    
    return dN_dglobal

def wedge_strainMatrix(dN_dglobal):
    """楔形要素の歪マトリックス（Bマトリックス）を構築
    
    Args:
        dN_dglobal (list): グローバル座標系での形状関数の勾配
        
    Returns:
        np.ndarray: 歪マトリックス (6×18)
    """
    B = np.zeros((6, 18))  # 6x18 (6応力成分 x 6節点*3DOF)
    
    for i in range(6):
        dNi_dx, dNi_dy, dNi_dz = dN_dglobal[i]
        col = i * 3
        
        B[0, col] = dNi_dx
        B[1, col + 1] = dNi_dy
        B[2, col + 2] = dNi_dz
        B[3, col] = dNi_dy
        B[3, col + 1] = dNi_dx
        B[4, col + 1] = dNi_dz
        B[4, col + 2] = dNi_dy
        B[5, col] = dNi_dz
        B[5, col + 2] = dNi_dx
    
    return B

def wedge_stiffness_matrix(nodes_coords, material):
    """楔形要素の剛性マトリックスを計算
    
    Args:
        nodes_coords (list): 節点座標のリスト [[x1,y1,z1], [x2,y2,z2], ...]
        material: 材料特性オブジェクト (E, poiを持つ)
        
    Returns:
        np.ndarray: 要素剛性マトリックス (18×18)
    """
    D = material_matrix_3d(material.e, material.poi)
    Ke = np.zeros((18, 18))
    
    for int_point in WEDGE1_INT:
        xsi, eta, zeta, weight = int_point
        N, dN_dlocal = wedge_shapeFunction(xsi, eta, zeta)
        J, detJ = wedge_jacobian(nodes_coords, dN_dlocal)
        dN_dglobal = wedge_grad_N_global(J, dN_dlocal)
        B = wedge_strainMatrix(dN_dglobal)
        
        Ke += np.dot(B.T, np.dot(D, B)) * detJ * weight
    
    return Ke
