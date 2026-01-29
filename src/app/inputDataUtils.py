import copy
from typing import Optional, Tuple
import numpy as np
from helper import isInt, isFloat, convInt
from utils import get_nodeIndex, get_memIndex, get_matIndex, get_secIndex, get_loadDir
from error_handling import logger, MyError, MyCritical
from components.node import Node, find_nodeIndex
from components.member import Member
from components.support import Support
from components.joint import Joint
from components.spring import Spring
from components.section_material import Material, Section, Thickness
from components.load import NodeLoad, ElemLoad, HeatLoad


# region 材料特性データの作成関数
def make_sectionMaterial(secMat: dict[str, dict], mode: int) -> Tuple[list[Section], list[Material], list[Thickness]]:
    """1つ目の材料特性ケースにおける断面、材料データの作成

    Args:
        secMat (dict[str, dict]): 材料特性データ
        mode (int): 2Dか3Dか{2 or 3}

    Returns:
        _ (Tuple[list[Section], list[Material], list[Thickness]]): 断面リスト、材料リスト、厚さリスト
    """
    sections: list[Section] = []  # 断面
    materials: list[Material] = []  # 材料
    thicknesses: list[Thickness] = []  # 厚さ
    for nStr in secMat.keys():
        # 材料特性番号の取得
        if not isInt(nStr):  # 整数化できない材料特性番号のデータはスキップ
            errMsg = f"材料特性番号が不正な材料特性データを無視しました: 材料特性({str(nStr)})"
            logger.warning(errMsg)
            continue
        nTmp = convInt(nStr)  # 材料特性番号
        # 断面、材料データの取得
        secMatDict = secMat[nStr]
        name = str(secMatDict['n']) if 'n' in secMatDict else "NoName"
        a, iy, iz, j, e, g, poi, cte = get_secMatValues(secMatDict, mode)
        # データの登録
        if (a is None) or (e is None) or (poi is None) or (cte is None):  # a,e,poi,cteのいずれかが不正のデータはスキップ
            errMsg = f"不正な材料特性データを無視しました: 材料特性番号({str(nTmp)})"
            logger.warning(errMsg)
            continue
        if (iy is None) or (iz is None) or (j is None):
            if mode == 3:
                # iy,iz,jのいずれかが不正で3Dモードの場合は厚さデータのみ設定
                if e is not None and g is not None and poi is not None and cte is not None:
                    materials.append(Material(nTmp, name, e, g, poi, cte))
                    thicknesses.append(Thickness(nTmp, name, a))
                errMsg = f"次の材料特性データはパネル専用として設定しました: 材料特性番号({str(nTmp)})"
                logger.info(errMsg)
            else:
                errMsg = f"不正な材料特性データを無視しました: 材料特性番号({str(nTmp)})"
                logger.warning(errMsg)
                continue
        else:
            if mode == 3:  # 3Dモードの場合は同じデータで断面および厚さを設定
                thicknesses.append(Thickness(nTmp, name, a))
            sections.append(Section(nTmp, name, a, iz, iy, j))
            if e is not None and g is not None and poi is not None and cte is not None:
                materials.append(Material(nTmp, name, e, g, poi, cte))
            if (a == 0) or (iy == 0) or (iz == 0) or (j == 0) or (e == 0) or (g == 0) or (poi == 0):
                errMsg = f"一部の定数が0の材料特性が設定されました: 材料特性番号({str(nTmp)})"
                logger.info(errMsg)
    return sections, materials, thicknesses


def add_sectionMaterial(sections0: list[Section], materials0: list[Material], thicknesses0: list[Thickness],\
                        secMat: dict[str, dict], mode: int) -> Tuple[list[Section], list[Material], list[Thickness]]:
    """2つ目以降の材料特性ケースにおける断面、材料データの作成

    Args:
        sections0 (list[Section]): 1つ目の材料特性ケースの断面リスト
        materials0 (list[Material]): 1つ目の材料特性ケースの材料リスト
        thicknesses0 (list[Thickness]): 1つ目の材料特性ケースの厚さリスト
        secMat (dict[str, dict]): 材料特性データ
        mode (int): 2Dか3Dか{2 or 3}

    Returns:
        _ (Tuple[list[Section], list[Material], list[Thickness]]): 断面リスト、材料リスト、厚さリスト
    """
    # 1つ目の材料特性をコピーして、有効な入力がある項目だけ置き換える
    sections = copy.deepcopy(sections0)  # 断面
    materials = copy.deepcopy(materials0)  # 材料
    thicknesses = copy.deepcopy(thicknesses0)  # 厚さ
    for nStr in secMat.keys():
        # 材料特性番号の取得
        if not isInt(nStr):  # 整数化できない材料特性番号のデータはスキップ
            errMsg = f"材料特性番号が不正な材料特性データを無視しました: 材料特性({str(nStr)})"
            logger.warning(errMsg)
            continue
        nTmp = convInt(nStr)  # 材料特性番号
        # 断面、材料データの取得
        secMatDict = secMat[nStr]
        name = str(secMatDict['n']) if 'n' in secMatDict else "NoName"
        a, iy, iz, j, e, g, poi, cte = get_secMatValues(secMatDict, mode)
        # 断面（有効な数値がある場合に置き換える）
        pickedSec = [obj for obj in sections if obj.num == nTmp]
        if len(pickedSec) > 1:
            errMsg = "第2ケース以降の材料特性データ作成時に予期せぬエラーが発生しました"
            logger.critical(errMsg + f": 材料特性番号({str(nTmp)})")
            raise MyCritical(errMsg)
        elif len(pickedSec) == 1:
            sec = pickedSec[0]
            sec.name = name
            if a is not None:
                sec.a = a
            if iy is not None:
                sec.iy = iy
            if iz is not None:
                sec.iz = iz
            if j is not None:
                sec.j = j
        # 材料（有効な数値がある場合に置き換える）
        pickedMat = [obj for obj in materials if obj.num == nTmp]
        if len(pickedMat) > 1:
            errMsg = "第2ケース以降の材料特性データ作成時に予期せぬエラーが発生しました"
            logger.critical(errMsg + f": 材料特性番号({str(nTmp)})")
            raise MyCritical(errMsg)
        elif len(pickedMat) == 1:
            mat = pickedMat[0]
            mat.name = name
            if e is not None:
                mat.e = e
            if g is not None:
                mat.g = g
            if poi is not None:
                mat.poi = poi
            if cte is not None:
                mat.cte = cte
        # 厚さ（有効な数値がある場合に置き換える）
        if mode == 3:
            pickedThick = [obj for obj in thicknesses if obj.num == nTmp]
            if len(pickedThick) > 1:
                errMsg = "第2ケース以降の材料特性データ作成時に予期せぬエラーが発生しました"
                logger.critical(errMsg + f": 材料特性番号({str(nTmp)})")
                raise MyCritical(errMsg)
            elif len(pickedThick) == 1:
                thick = pickedThick[0]
                thick.name = name
                if a is not None:
                    thick.t = a
    return sections, materials, thicknesses


def get_secMatValues(secMatDict: dict, mode: int)\
    -> Tuple[Optional[float], Optional[float], Optional[float], Optional[float],\
             Optional[float], Optional[float], Optional[float], Optional[float]]:
    """断面、材料の諸元を取得する

    Args:
        secMatDict (dict): 材料特性データ辞書
        mode (int): 2Dか3Dか{2 or 3}

    Returns:
        _ (Tuple[Optional[float], Optional[float], Optional[float], Optional[float], Optional[float], Optional[float], Optional[float], Optional[float]]): 順に断面積(m2)、y軸、z軸まわり断面二次モーメント(m4)、ねじり定数(m4)、ヤング係数(kN/m2)、せん断弾性係数(kN/m2)、ポアソン比、線膨張係数(/℃)
    """
    logger.info(f"Processing material properties: {secMatDict}")
    for key in ['A', 'Iz', 'Iy', 'J', 'E', 'G', 'nu', 'Xp', 'density', 'n']:
        if key in secMatDict:
            logger.info(f"  {key}: {secMatDict[key]}")
        else:
            logger.warning(f"  Missing property: {key}")

    # 断面データ（断面積a、Z軸まわり断面二次iz、Y軸まわり断面二次iy、ねじり定数j）
    a = float(secMatDict['A']) if ('A' in secMatDict) and isFloat(secMatDict['A']) else None
    iz = float(secMatDict['Iz']) if ('Iz' in secMatDict) and isFloat(secMatDict['Iz']) else None
    if mode == 2:  # 2Dモード
        iy = 0.0
        j = 0.0
    else:  # 3Dモード
        iy = float(secMatDict['Iy']) if ('Iy' in secMatDict) and isFloat(secMatDict['Iy']) else None
        j = float(secMatDict['J']) if ('J' in secMatDict) and isFloat(secMatDict['J']) else None
    if (a is not None) and (a < 0):
        a = None
    if (iy is not None) and (iy < 0):
        iy = None
    if (iz is not None) and (iz < 0):
        iz = None
    if (j is not None) and (j < 0):
        j = None
    # 材料データ（ヤング係数e、せん断弾性係数g、線膨張係数cte、ポアソン比poi）
    e = float(secMatDict['E']) if ('E' in secMatDict) and isFloat(secMatDict['E']) else None
    cte = float(secMatDict['Xp']) if ('Xp' in secMatDict) and isFloat(secMatDict['Xp']) else None
    
    if ('nu' in secMatDict) and isFloat(secMatDict['nu']):
        poi = float(secMatDict['nu'])
    else:
        poi = None
        
    if ('density' in secMatDict) and isFloat(secMatDict['density']):
        density = float(secMatDict['density']) # 質量
    else:
        density = 2400.0  # デフォルト値（一般的な構造材料）
    
    if mode == 2:  # 2Dモード
        g = 0.0
        poi = 0
    else:  # 3Dモード
        g = float(secMatDict['G']) if ('G' in secMatDict) and isFloat(secMatDict['G']) else None
        if g is None and e is not None and poi is not None:
            g = e / (2.0 * (1.0 + poi))
        if g is not None and e is not None and poi is None:
            poi = e / (2.0 * g) - 1.0

    if (e is not None) and (e < 0):
        e = None
    if (g is not None) and (g < 0):
        g = None
    if (cte is not None) and (cte < 0):
        cte = None
    if (poi is not None) and (poi < 0):
        poi = None
    # 不安定構造の回避のため0値に微小な値を設定する
    if (a is not None) and (a == 0):
        a = 0.000000001
    if (iy is not None) and (iy == 0):
        iy = 0.000000001
    if (iz is not None) and (iz == 0):
        iz = 0.000000001
    if (j is not None) and (j == 0):
        j = 0.000000001
    if (e is not None) and (e == 0):
        e = 0.000000001
    if (g is not None) and (g == 0):
        g = 0.000000001
    return a, iy, iz, j, e, g, poi, cte
# endregion


# region 梁要素の分割関数
def devide_byRigid(rigid: list[dict], members: list[Member], sections: list[Section], materials: list[Material]) -> None:
    """剛域で梁要素を分割する

    Args:
        rigid (list[dict]): 剛域データ
        members (list[Member]): 部材リスト
        sections (list[Section]): 断面リスト
        materials (list[Material]): 材料リスト
    """
    for rigidTmp in rigid:
        # 部材番号の取得
        if (not 'm' in rigidTmp) or (not isInt(rigidTmp['m'])):  # 整数化できない部材番号のデータはスキップ
            errMsg = "部材番号が不正な剛域データを無視しました"
            logger.warning(errMsg)
            continue
        nMem = convInt(rigidTmp['m'])
        # 材料特性番号の取得
        if (not 'e' in rigidTmp) or (not isInt(rigidTmp['e'])):  # 整数化できない材料特性番号のデータはスキップ
            errMsg = f"材料特性番号が不正な剛域データを無視しました: 部材({str(nMem)})"
            logger.warning(errMsg)
            continue
        nMat = convInt(rigidTmp['e'])
        # 部材の取得
        iMem = get_memIndex(nMem, members)
        if iMem is None:  # 存在しない部材のデータはスキップ
            errMsg = f"入力されていない部材に対する剛域データを無視しました: 部材({str(nMem)})"
            logger.warning(errMsg)
            continue
        memTmp = members[iMem]
        # 材料特性の取得
        iMat = get_matIndex(nMat, materials)
        iSec = get_secIndex(nMat, sections)
        if (iMat is None) or (iSec is None):
            errMsg = "剛域データ作成時に予期せぬエラーが発生しました"
            logger.critical(errMsg + f": 部材({str(nMem)})")
            raise MyCritical(errMsg, member=memTmp)
        # i端、j端からの距離を取得
        lenI = float(rigidTmp['Ilength']) if ('Ilength' in rigidTmp) and isFloat(rigidTmp['Ilength']) else 0.0
        lenJ = float(rigidTmp['Jlength']) if ('Jlength' in rigidTmp) and isFloat(rigidTmp['Jlength']) else 0.0
        if memTmp.leng < (lenI + lenJ):  # i端側とj端側の剛域が被るデータはエラー
            errMsg = "両端の剛域範囲がラッピングしている剛域データがあります"
            logger.error(errMsg + f": 部材({str(nMem)})")
            raise MyError(errMsg, member=memTmp)
        # i端側の剛域の分割
        if lenI < 0:  # i端からの距離が負の場合はエラー
            errMsg = "i端の剛域長さに負値が入力されている剛域データがあります"
            logger.error(errMsg + f": 部材({str(nMem)})")
            raise MyError(errMsg, member=memTmp)
        elif lenI > 0:
            memTmp.devide_byLength(lenI, 1)
            memTmp.beams[memTmp.elems[0]].iMat = iMat
            memTmp.beams[memTmp.elems[0]].iSec = iSec
        # j端側の剛域の分割
        if lenJ < 0:  # j端からの距離が負の場合はエラー
            errMsg = f"j端の剛域長さに負値が入力されている剛域データがあります"
            logger.error(errMsg + f": 部材({str(nMem)})")
            raise MyError(errMsg, member=memTmp)
        elif lenJ > 0:
            memTmp.devide_byLength(memTmp.leng - lenJ, 1)
            memTmp.beams[memTmp.elems[-1]].iMat = iMat
            memTmp.beams[memTmp.elems[-1]].iSec = iSec
    return None


def devide_byPoints(points: list[dict], members: list[Member]) -> None:
    """着目点で梁要素を分割する

    Args:
        points (list[dict]): 着目点データ
        members (list[Member]): 部材リスト
    """
    for pTmp in points:
        # 部材番号の取得
        if (not 'm' in pTmp) or (not isInt(pTmp['m'])):  # 整数化できない部材番号のデータはスキップ
            errMsg = "部材番号が不正な着目点データを無視しました"
            logger.warning(errMsg)
            continue
        nMem = convInt(pTmp['m'])  # 部材番号
        if (not 'Points' in pTmp) or (len(pTmp['Points']) <= 0):  # 着目点位置がないデータはスキップ
            errMsg = f"着目点位置の入力がない着目点データを無視しました: 部材({str(nMem)})"
            logger.warning(errMsg)
            continue
        # 部材の取得
        iMem = get_memIndex(nMem, members)  # 部材インデックス
        if iMem is None:  # 該当する部材が無いデータはスキップ
            errMsg = f"入力されていない部材に対する着目点データを無視しました: 部材({str(nMem)})"
            logger.warning(errMsg)
            continue
        memTmp = members[iMem]
        # 梁要素の分割
        for p in pTmp['Points']:
            if not isFloat(p):  # 実数化できない着目点はスキップ
                errMsg = f"位置が不正な着目点データを無視しました: 部材({str(nMem)}) 位置({str(p)})"
                logger.warning(errMsg)
                continue
            lenI = float(p)  # i端からの距離
            if (lenI <= 0.0) or (lenI >= memTmp.leng):  # 部材内にない着目点はスキップ
                errMsg = f"位置が不正な着目点データを無視しました: 部材({str(nMem)}) 位置({lenI:.3f})"
                logger.warning(errMsg)
                continue
            memTmp.devide_byLength(lenI, 2)
    return None


def devide_byElemLoads(eLoads: list[dict], members: list[Member], lCase: str) -> None:
    """要素荷重の作用位置で梁要素を分割する

    Args:
        eLoads (list[dict]): 要素荷重データリスト
        members (list[Member]): 部材リスト
        lCase (str): 基本荷重ケース名
    """
    for eLoadTmp in eLoads:
        # 載荷部材の取得
        nMem = convInt(eLoadTmp['m']) if ('m' in eLoadTmp) and isInt(eLoadTmp['m']) else 0
        iMem = get_memIndex(nMem, members)
        if iMem is None:  # 該当する部材が無い場合はスキップ
            continue
        memTmp = members[iMem]
        # 要素荷重タイプの取得
        mark = convInt(eLoadTmp['mark']) if ('mark' in eLoadTmp) and isInt(eLoadTmp['mark']) else 0
        # 梁要素の分割
        if mark == 2:  # 分布荷重
            dist1 = float(eLoadTmp['L1']) if ('L1' in eLoadTmp) and isFloat(eLoadTmp['L1']) else 0.0
            dist2 = float(eLoadTmp['L2']) if ('L2' in eLoadTmp) and isFloat(eLoadTmp['L2']) else 0.0
            if dist2 < 0:  # L2が負値の場合は荷重幅を表すのでj端からの距離に変換
                dist2 = memTmp.leng - dist1 - abs(dist2)
            if (dist1 >= 0) and (dist2 >= 0) and (dist1 + dist2 < memTmp.leng):
                if dist1 > 0:
                    memTmp.devide_byLength(dist1, 3, lCase)
                if dist2 > 0:
                    memTmp.devide_byLength(memTmp.leng - dist2, 3, lCase)
        elif (mark == 1) or (mark == 11):  # 集中荷重
            for key in ['L1', 'L2']:
                lenI = float(eLoadTmp[key]) if (key in eLoadTmp) and isFloat(eLoadTmp[key]) else 0.0
                if (lenI > 0) and (lenI < memTmp.leng):
                    memTmp.devide_byLength(lenI, 3, lCase)
    return None
# endregion


# region 要素荷重データの作成関数
def make_eNodeLoads(eLoad: dict, mark: int, member: Member) -> list[NodeLoad]:
    """要素荷重の集中荷重による節点荷重データを作成する

    Args:
        eLoad (dict): 集中荷重の要素荷重データ
        mark (int): 荷重タイプ{1:集中荷重,11:集中モーメント}
        member (Member): 部材データ

    Returns:
        _ (list[NodeLoad]): 節点荷重リスト
    """
    nodeLoads: list[NodeLoad] = []
    for i in range(1, 3):  # P1,L1→P2,L2
        # 荷重値の取得
        pKey = 'P' + str(i)
        p = float(eLoad[pKey]) if (pKey in eLoad) and isFloat(eLoad[pKey]) else 0.0
        if p == 0:  # 荷重値が0ならスキップ
            errMsg = f"荷重値が0の要素荷重の集中荷重データを無視しました: 部材({str(member.num)} {pKey})"
            logger.warning(errMsg)
            continue
        # 載荷位置の取得
        lKey = 'L' + str(i)
        lenI = float(eLoad[lKey]) if (lKey in eLoad) and isFloat(eLoad[lKey]) else 0.0
        if (lenI < 0) or (lenI > member.leng):  # 部材外の位置ならスキップ
            errMsg = f"部材外に設定された要素荷重の集中荷重データを無視しました: 部材({str(member.num)} {pKey})"
            logger.warning(errMsg)
            continue
        # 載荷方向の取得
        dirStr = str(eLoad['direction']) if 'direction' in eLoad else ""
        dir = get_loadDir(dirStr)
        if (dir is None) or (dir == "Lr"):  # 集中荷重に非対応な荷重方向ならスキップ
            errMsg = f"不正な荷重方向の要素荷重の集中荷重データを無視しました: 部材({str(member.num)} {pKey})"
            logger.warning(errMsg)
            continue
        # 載荷節点の取得
        (x, y, z) = member.get_coordinateByLength(lenI)
        iNode = find_nodeIndex(x, y, z, member.nodes)
        if iNode is None:
            errMsg = "要素荷重の集中荷重データ作成時に予期せぬエラーが発生しました"
            logger.critical(errMsg + f": 部材({str(member.num)} {pKey})")
            raise MyCritical(errMsg)
        # 荷重値の設定
        fx: float = 0.0
        fy: float = 0.0
        fz: float = 0.0
        rx: float = 0.0
        ry: float = 0.0
        rz: float = 0.0
        if dir == "GX":  # 全体座標系X軸
            if mark == 1:
                fx = p
            elif mark == 11:
                rx = p
        elif dir == "GY":  # 全体座標系Y軸
            if mark == 1:
                fy = p
            elif mark == 11:
                ry = p
        elif dir == "GZ":  # 全体座標系Z軸
            if mark == 1:
                fz = p
            elif mark == 11:
                rz = p
        else:  # 要素座標系での荷重方向設定
            # 荷重方向が要素座標系の場合は全体座標系に変換
            invMat = np.linalg.inv(member.eMatrix)  # 3x3基底変換行列の逆行列
            if dir == "Lx":
                flVec = np.array([p, 0.0, 0.0], dtype=float).T
            elif dir == "Ly":
                flVec = np.array([0.0, p, 0.0], dtype=float).T
            elif dir == "Lz":
                flVec = np.array([0.0, 0.0, p], dtype=float).T
            fgVec = np.matmul(invMat, flVec)
            if mark == 1:
                fx = float(fgVec[0])
                fy = float(fgVec[1])
                fz = float(fgVec[2])
            elif mark == 11:
                rx = float(fgVec[0])
                ry = float(fgVec[1])
                rz = float(fgVec[2])
        nodeLoads.append(NodeLoad(iNode, fx, fy, fz, rx, ry, rz, 2))
    return nodeLoads


def make_eElemLoads(eLoad: dict, member: Member) -> list[ElemLoad]:
    """要素分布荷重データを作成する

    Args:
        eLoad (dict): 分布荷重の要素荷重データ
        member (Member): 部材データ

    Returns:
        _ (list[ElemLoad]): 要素分布荷重リスト
    """
    elemLoads: list[ElemLoad] = []
    # 荷重載荷範囲の取得
    dist1 = float(eLoad['L1']) if ('L1' in eLoad) and isFloat(eLoad['L1']) else 0.0
    dist2 = float(eLoad['L2']) if ('L2' in eLoad) and isFloat(eLoad['L2']) else 0.0
    if dist2 < 0:  # L2が負値の場合は荷重幅を表すのでj端からの距離に変換
        dist2 = member.leng - dist1 - abs(dist2)
    if (dist1 < 0) or (dist2 < 0) or (dist1 + dist2 >= member.leng):  # 載荷位置が不正なデータはスキップ
        errMsg = f"載荷位置が不正な要素荷重の分布荷重データを無視しました: 部材({str(member.num)})"
        logger.warning(errMsg)
        return elemLoads
    lLen = member.leng - dist1 - dist2  # 荷重載荷範囲の長さ(m)
    # 荷重値の取得
    p1 = float(eLoad['P1']) if ('P1' in eLoad) and isFloat(eLoad['P1']) else 0.0
    p2 = float(eLoad['P2']) if ('P2' in eLoad) and isFloat(eLoad['P2']) else 0.0
    if (p1 == 0) and (p2 == 0):  # 荷重値が0の場合はスキップ
        errMsg = f"荷重値が0の要素荷重の分布荷重データを無視しました: 部材({str(member.num)})"
        logger.warning(errMsg)
        return elemLoads
    # 載荷方向の取得
    dirStr = str(eLoad['direction']) if 'direction' in eLoad else ""
    dir = get_loadDir(dirStr)
    if dir is None:  # 荷重載荷方向が不定の場合はスキップ
        errMsg = f"不正な載荷方向の要素荷重の分布荷重データを無視しました: 部材({str(member.num)})"
        logger.warning(errMsg)
        return elemLoads
    # 載荷開始・終了節点の取得
    (x0, y0, z0) = member.get_coordinateByLength(dist1)
    iNode0 = find_nodeIndex(x0, y0, z0, member.nodes)  # 荷重開始点の節点インデックス
    (x1, y1, z1) = member.get_coordinateByLength(member.leng - dist2)
    iNode1 = find_nodeIndex(x1, y1, z1, member.nodes)  # 荷重終了点の節点インデックス
    if (iNode0 is None) or (iNode1 is None):
        errMsg = "要素荷重の分布荷重データ作成時に予期せぬエラーが発生しました"
        logger.critical(errMsg + f": 部材({str(member.num)})")
        raise MyCritical(errMsg)
    # 梁要素への割り当て
    totalLen = 0.0
    beams = member.beams
    endFlag: int = 0
    for iBeam in member.elems:
        if totalLen == 0:  # 荷重開始点がまだ見つかっていない状態
            if beams[iBeam].indI != iNode0:
                continue  # 荷重載荷対象の梁要素に達していないので次の梁要素へ進む
            f1 = p1  # 荷重開始位置での荷重値確定
            if beams[iBeam].indJ == iNode1:
                # 分布荷重が1つの梁要素で完結する場合
                f2 = p2  # 荷重終了位置での荷重値確定
                endFlag = 1
            else:
                # 分布荷重が複数の梁要素に渡る場合
                totalLen += beams[iBeam].leng
                f2 = p1 + (p2 - p1) * totalLen / lLen  # 梁要素j端位置での荷重値
        else:  # 荷重載荷範囲を探索中の状態
            f1 = f2  # 梁要素i端の荷重値は1つ前の梁要素j端の荷重値
            if beams[iBeam].indJ == iNode1:
                # 荷重が載荷される最後の梁要素の場合
                f2 = p2  # 荷重終了位置での荷重値確定
                endFlag = 1
            else:
                # まだ最後の梁要素に達していない場合
                totalLen += beams[iBeam].leng
                f2 = p1 + (p2 - p1) * totalLen / lLen  # 梁要素j端位置での荷重値
        elemLoads.append(ElemLoad(iBeam, dir, f1, f2))
        if endFlag == 1:
            break
    return elemLoads


def make_heatLoads(eLoad: dict, member: Member) -> list[HeatLoad]:
    """温度荷重データを作成する

    Args:
        eLoad (dict): 温度荷重の要素荷重データ
        member (Member): 部材データ

    Returns:
        _ (list[FA_HeatLoad]): 温度荷重リスト
    """
    heatLoads: list[HeatLoad] = []
    # 温度荷重値の取得
    heat = float(eLoad['P1']) if ('P1' in eLoad) and isFloat(eLoad['P1']) else 0.0
    if heat != 0:
        for iBeam in member.elems:
            heatLoads.append(HeatLoad(iBeam, heat))
    else:
        errMsg = f"荷重値が0の要素荷重の温度荷重データを無視しました: 部材({str(member.num)})"
        logger.warning(errMsg)
    return heatLoads
# endregion


def make_supports(supRaws: list[dict], nodes: list[Node], mode: int) -> list[Support]:
    """支点データを作成する

    Args:
        supRaws (list[dict]): 元々の支点・節点バネデータ
        nodes (list[Node]): 節点リスト
        mode (int): 2Dか3Dか{2 or 3}

    Returns:
        _ (list[Support]): 支点リスト
    """
    supports: list[Support] = []
    for supRaw in supRaws:
        # 節点の取得
        nNode = convInt(supRaw['n']) if ('n' in supRaw) and isInt(supRaw['n']) else 0
        iNode = get_nodeIndex(nNode, nodes)
        if iNode is None:  # 節点リストに無い支点はスキップ
            errMsg = f"入力されていない節点に対する支点データを無視しました: 節点番号({str(nNode)})"
            logger.warning(errMsg)
            continue
        # 同一節点への複数支点データ入力のチェック
        picked = [obj for obj in supports if obj.iNode == iNode]
        if len(picked) >= 1:
            errMsg = "同一節点に複数の支点データが入力されています"
            logger.error(errMsg + f": 節点番号({str(nNode)})")
            raise MyError(errMsg, node=nodes[iNode])
        # バネ値は負値を受け付けないため絶対値を取得する
        dxSup = abs(float(supRaw['tx'])) if ('tx' in supRaw) and isFloat(supRaw['tx']) else 0.0
        dySup = abs(float(supRaw['ty'])) if ('ty' in supRaw) and isFloat(supRaw['ty']) else 0.0
        dzSup = abs(float(supRaw['tz'])) if ('tz' in supRaw) and isFloat(supRaw['tz']) else 0.0
        rxSup = abs(float(supRaw['rx'])) if ('rx' in supRaw) and isFloat(supRaw['rx']) else 0.0
        rySup = abs(float(supRaw['ry'])) if ('ry' in supRaw) and isFloat(supRaw['ry']) else 0.0
        rzSup = abs(float(supRaw['rz'])) if ('rz' in supRaw) and isFloat(supRaw['rz']) else 0.0
        if mode == 2:  # 2Dモードの場合
            dzSup = 1.0
            rxSup = 1.0
            rySup = 1.0
        supports.append(Support(iNode, dxSup, dySup, dzSup, rxSup, rySup, rzSup, False))
    # 2Dデータの場合は全節点にdz、rx、ry固定の支点データを作成する
    if mode == 2:
        remainings: list[int] = []  # 支点データ未作成の節点インデックスを格納する
        for i in range(len(nodes)):
            picked = [obj for obj in supports if obj.iNode == i]
            if len(picked) == 0:
                remainings.append(i)
        for i in remainings:
            supports.append(Support(i, 0.0, 0.0, 1.0, 1.0, 1.0, 0.0, True))
    return supports


def make_springs(sprRaws: list[dict], members: list[Member], mode: int) -> list[Spring]:
    """要素分布バネデータを作成する

    Args:
        sprRaws (list[dict]): 元々の部材分布バネデータ
        members (list[Member]): 部材リスト
        mode (int): 2Dか3Dか{2 or 3}

    Returns:
        _ (list[Spring]): 要素分布バネリスト
    """
    springs: list[Spring] = []
    for sprRaw in sprRaws:
        # 該当部材の取得
        nMem = convInt(sprRaw['m']) if ('m' in sprRaw) and isInt(sprRaw['m']) else 0
        iMem = get_memIndex(nMem, members)
        if iMem is None:  # 部材リストに無い分布バネはスキップ
            errMsg = f"入力されていない部材に対する分布バネデータを無視しました: 部材({str(nMem)})"
            logger.warning(errMsg)
            continue
        memTmp = members[iMem]
        # バネ値は負値を受け付けないので絶対値を取得する
        dxSpr = abs(float(sprRaw['tx'])) if ('tx' in sprRaw) and isFloat(sprRaw['tx']) else 0.0
        dySpr = abs(float(sprRaw['ty'])) if ('ty' in sprRaw) and isFloat(sprRaw['ty']) else 0.0
        dzSpr = abs(float(sprRaw['tz'])) if ('tz' in sprRaw) and isFloat(sprRaw['tz']) else 0.0
        rxSpr = abs(float(sprRaw['tr'])) if ('tr' in sprRaw) and isFloat(sprRaw['tr']) else 0.0
        if mode == 2:
            dzSpr = 0.0
            rxSpr = 0.0
        if (dxSpr == 0) and (dySpr == 0) and (dzSpr == 0) and (rxSpr == 0):  # バネ値が全て0の場合はスキップ
            errMsg = f"バネ定数が全て0の分布バネデータを無視しました: 部材({str(nMem)})"
            logger.warning(errMsg)
            continue
        # 分布バネの登録
        for iBeam in memTmp.elems:
            picked = [obj for obj in springs if obj.iBeam == iBeam]
            if len(picked) > 1:
                errMsg = "分布バネデータ作成時に予期せぬエラーが発生しました"
                logger.critical(errMsg + f": 部材({str(nMem)})")
                raise MyCritical(errMsg)
            if len(picked) == 1:  # 1部材に複数のデータが設定されている場合は加算
                picked[0].dxSpr += dxSpr
                picked[0].dySpr += dySpr
                picked[0].dzSpr += dzSpr
                picked[0].rxSpr += rxSpr
                errMsg = f"同一部材の設定されているバネ定数を加算しました: 部材({str(nMem)})"
                logger.info(errMsg)
            else:  # 新規に分布バネを登録
                springs.append(Spring(iBeam, dxSpr, dySpr, dzSpr, rxSpr))
    return springs


def make_joints(jntRaws: list[dict], members: list[Member], mode: int) -> list[Joint]:
    """材端条件データを作成する

    Args:
        jntRaws (list[dict]): 元々の結合データ
        members (list[Member]): 部材リスト
        mode (int): 2Dか3Dか{2 or 3}

    Returns:
        _ (list[Joint]): 材端条件リスト
    """
    joints: list[Joint] = []
    for jntRaw in jntRaws:
        # 該当部材の取得
        nMem = convInt(jntRaw['m']) if ('m' in jntRaw) and isInt(jntRaw['m']) else 0
        iMem = get_memIndex(nMem, members)
        if iMem is None:  # 部材リストに無い結合データはスキップ
            errMsg = f"該当部材がない結合条件を無視しました: 部材({str(nMem)})"
            logger.warning(errMsg)
            continue
        memTmp = members[iMem]
        # 材端条件の取得
        xi = convInt(jntRaw['xi']) if ('xi' in jntRaw) and isInt(jntRaw['xi']) else 1
        yi = convInt(jntRaw['yi']) if ('yi' in jntRaw) and isInt(jntRaw['yi']) else 1
        zi = convInt(jntRaw['zi']) if ('zi' in jntRaw) and isInt(jntRaw['zi']) else 1
        xj = convInt(jntRaw['xj']) if ('xj' in jntRaw) and isInt(jntRaw['xj']) else 1
        yj = convInt(jntRaw['yj']) if ('yj' in jntRaw) and isInt(jntRaw['yj']) else 1
        zj = convInt(jntRaw['zj']) if ('zj' in jntRaw) and isInt(jntRaw['zj']) else 1
        if mode == 2:
            xi = 1
            yi = 1
            xj = 1
            yj = 1
        # 梁要素の取得
        iBeamI = memTmp.elems[0]  # 始端側の梁要素インデックス
        iBeamJ = memTmp.elems[-1]  # 終端側の梁要素インデックス
        picked = [obj for obj in joints if (obj.iBeam == iBeamI) or (obj.iBeam == iBeamJ)]
        if len(picked) >= 1:
            errMsg = "同一部材に複数の結合条件が設定されています"
            logger.error(errMsg + f": 部材({str(nMem)})")
            raise MyError(errMsg, member=memTmp)
        # 材端条件の登録
        if (xi == 1) and (yi == 1) and (zi == 1) and (xj == 1) and (yj == 1) and (zj == 1):
            errMsg = f"材端条件が全て拘束の材端条件データを無視しました: 部材({str(memTmp.num)})"
            logger.warning(errMsg)
            continue
        if iBeamI == iBeamJ:  # 部材が1梁要素で構成される場合
            joints.append(Joint(iBeamI, xi, yi, zi, xj, yj, zj))
        else:  # 部材が複数梁要素で構成される場合
            # i端側
            if (xi == 0) or (yi == 0) or (zi == 0):
                joints.append(Joint(iBeamI, xi, yi, zi, 1, 1, 1))
            # j端側
            if (xj == 0) or (yj == 0) or (zj == 0):
                joints.append(Joint(iBeamJ, 1, 1, 1, xj, yj, zj))
    return joints


