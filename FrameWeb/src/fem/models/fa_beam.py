import math
import numpy as np
from models.fa_node import FA_Node


class FA_Beam:
    """フレーム計算用梁要素クラス
    
    @brief 梁・柱要素を表現するクラス
    
    2つの節点を結ぶ線要素で、軸力・せん断力・曲げモーメント・ねじりモーメントを
    伝達できます。局所座標系と全体座標系の変換行列を持ちます。
    
    @note 12自由度要素（各端点で6自由度）
    @note Timoshenko梁理論に基づく

    Properties:
        indI (int):i端節点インデックス
        indJ (int):j端節点インデックス
        iMat (int):材料インデックス
        iSec (int):断面インデックス
        eMatrix (ndarray[3,3]):全体→要素座標系の基底変換行列
        leng (float):梁要素の長さ(m)
        nodes (list[FA_Node]):節点リスト
    """
    def __init__(self, indI: int, indJ: int, iMat: int, iSec: int, \
                 eMat: np.ndarray, nodes: list[FA_Node]) -> None:
        """フレーム計算用梁要素クラス

        @brief 梁要素の端点と特性を指定して梁要素を作成
        
        @param indI i端節点インデックス
        @param indJ j端節点インデックス
        @param iMat 材料特性インデックス
        @param iSec 断面特性インデックス
        @param eMat 全体→要素座標系の基底変換行列（3x3）
        @param nodes 節点リスト

        Args:
            indI (int): i端節点インデックス
            indJ (int): j端節点インデックス
            iMat (int): 材料インデックス
            iSec (int): 断面インデックス
            eMat (np.ndarray): 全体→要素座標系の基底変換行列
            nodes (list[FA_Node]): 節点リスト
        """
        self.indI = indI
        self.indJ = indJ
        self.iMat = iMat
        self.iSec = iSec
        self.nodes = nodes
        self.eMatrix = eMat
        return None


    @property
    def leng(self) -> float:
        """梁要素の長さを計算する

        Returns:
            _ (float): 梁要素の長さ(m)
        """
        xi = self.nodes[self.indI].x
        yi = self.nodes[self.indI].y
        zi = self.nodes[self.indI].z
        xj = self.nodes[self.indJ].x
        yj = self.nodes[self.indJ].y
        zj = self.nodes[self.indJ].z
        length = math.sqrt((xj - xi) ** 2 + (yj - yi) ** 2 + (zj - zi) ** 2)
        return length
    

    def get_convMatrix(self, size: int, isInv: bool) -> np.ndarray:
        """size*sizeの基底変換行列を返す（sizeが3の倍数でない場合は繰り上げる）

        Args:
            size (int): 基底変換行列の行および列のサイズ（3の倍数）
            isInv (bool): 逆行列に変換するか否か

        Returns:
            _ (np.ndarray): size*sizeの基底変換行列
        """
        if size <= 0:
            size = 3
        if size % 3 != 0:  # sizeが3の倍数でない場合はsize超で最も近い3の倍数に繰り上げ
            size = ((size // 3) + 1) * 3
        cMatrix = np.zeros((size, size), dtype=float)
        # 対角要素に3x3の基底変換行列を代入していく
        for i in range(0, size, 3):
            cMatrix[i:(i + 3), i:(i + 3)] = self.eMatrix
        # isInvがTrueの場合は逆行列に変換する
        if isInv == True:
            return np.linalg.inv(cMatrix)
        else:
            return cMatrix


def cal_eMatrix(xi: float, yi: float, zi: float, xj: float, \
                yj: float, zj: float, angle: float) -> np.ndarray:
    """梁要素の全体→要素座標系への基底変換行列を計算する

    @brief 梁要素の局所座標系を定義する変換行列を計算
    
    @param xi i端X座標（メートル）
    @param yi i端Y座標（メートル）
    @param zi i端Z座標（メートル）
    @param xj j端X座標（メートル）
    @param yj j端Y座標（メートル）
    @param zj j端Z座標（メートル）
    @param angle 要素座標軸の回転角（度）
    
    @return 3x3の基底変換行列
    
    @note 要素x軸は部材軸方向、y軸・z軸は主軸方向

    Args:
        xi (float): i端X座標
        yi (float): i端Y座標
        zi (float): i端Z座標
        xj (float): j端X座標
        yj (float): j端Y座標
        zj (float): j端Z座標
        angle (float): 要素座標軸の回転角(°)

    Returns:
        _ (np.ndarray): 3x3の基底変換行列
    """
    dx = xj - xi
    dy = yj - yi
    dz = zj - zi
    rad = math.radians(angle)
    leng = math.sqrt(dx ** 2 + dy ** 2 + dz ** 2)
    # デフォルトの基底変換行列の計算
    if (dx == 0) and (dy == 0):  # 要素x軸が全体Z軸と平行な場合
        bMatDefault = np.array([[0.0, 0.0, np.sign(dz)],\
                                [np.sign(dz), 0.0, 0.0],\
                                [0.0, 1.0, 0.0]], dtype=float)
    else:  # 要素x軸と全体Z軸が平行でない場合
        dxn = dx / leng
        dyn = dy / leng
        dzn = dz / leng
        hLen = math.sqrt(dxn ** 2 + dyn ** 2)
        bMatDefault = np.array([[dxn, dyn, dzn],\
                                [-dyn / hLen, dxn / hLen, 0.0],\
                                [-dxn * dzn / hLen, -dyn * dzn / hLen, hLen]], dtype=float)
    # 要素x軸まわりの回転による基底変換行列の計算
    bMatAngle = np.array([[1.0, 0.0, 0.0],\
                          [0.0, math.cos(rad), math.sin(rad)],\
                          [0.0, -math.sin(rad), math.cos(rad)]], dtype=float)
    # 基底変換行列の計算
    eMat = np.matmul(bMatAngle, bMatDefault)
    return eMat
