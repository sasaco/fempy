from typing import Tuple, Optional
from error_handling import MyCritical, logger
from components.node import Node
from components.member import Member
from components.shell import Shell
from components.joint import Joint
from components.section_material import Section, Material, Thickness
from components.load import LoadDir, CaseComb


def get_nodeIndex(nNode: int, nodes: list[Node]) -> Optional[int]:
    """ある節点番号の節点リスト内でのインデックスを返す

    Args:
        nNode (int): 節点番号
        nodes (list[Node]): 節点リスト

    Returns:
        _ (Optional[int]): 節点インデックス（見つからない場合、複数ある場合はNone）
    """
    pickedNode = [obj for obj in nodes if obj.nNode == nNode]
    if (len(pickedNode) == 0) or (len(pickedNode) > 1):
        return None
    ind: int = nodes.index(pickedNode[0])
    return ind


def get_memIndex(nMem: int, members: list[Member]) -> Optional[int]:
    """ある部材番号の部材リスト内でのインデックスを返す

    Args:
        nMem (int): 部材番号
        members (list[Member]): 部材リスト

    Returns:
        Optional[int]: 部材インデックス（見つからない場合はNone）
    """
    picked = [obj for obj in members if obj.num == nMem]
    if (len(picked) == 0) or (len(picked) > 1):
        return None
    ind: int = members.index(picked[0])
    return ind


def get_shellIndex(nPanel: int, shells: list[Shell]) -> Optional[int]:
    """あるパネル番号のシェル要素リスト内でのインデックスを返す

    Args:
        nPanel (int): パネル番号
        shells (list[Shell]): シェル要素リスト

    Returns:
        Optional[int]: シェルインデックス（見つからない場合はNone）
    """
    picked = [obj for obj in shells if obj.num == nPanel]
    if (len(picked) == 0) or (len(picked) > 1):
        return None
    ind: int = shells.index(picked[0])
    return ind


def get_joint(iBeam: int, type: int, joints: list[Joint]) -> Tuple[bool, bool]:
    """梁要素両端の材端条件を取得する

    Args:
        iBeam (int): 梁要素インデックス
        type (int): タイプ{0:x軸まわり,1:y軸まわり,2:z軸まわり}
        joints (list[Joint]): 材端条件リスト

    Returns:
        _ (Tuple[bool, bool]): i端およびj端の材端条件がピンか否か
    """
    picked = [obj for obj in joints if obj.iBeam == iBeam]
    if len(picked) > 1:
        errMsg = f"材端条件の取得時に予期せぬエラーが発生しました: 梁要素インデックス({str(iBeam)})"
        logger.critical(errMsg)
        raise MyCritical(errMsg)
    elif len(picked) == 1:
        joint = picked[0]
        if type == 0:  # 要素座標系x軸まわり
            return (joint.xiFree, joint.xjFree)
        elif type == 1:  # 要素座標系y軸まわり
            return (joint.yiFree, joint.yjFree)
        elif type == 2:  # 要素座標系z軸まわり
            return (joint.ziFree, joint.zjFree)
        else:
            errMsg = f"材端条件の取得時に予期せぬエラーが発生しました: 梁要素インデックス({str(iBeam)})"
            logger.critical(errMsg)
            raise MyCritical(errMsg)
    else:  # 材端条件が無ければ両端とも剛
        return (False, False)


def get_secIndex(nMat: int, sections: list[Section]) -> Optional[int]:
    """ある材料特性番号に対応する断面インデックスを返す

    Args:
        nMat (int): 材料特性番号
        sections (list[Section]): 断面リスト

    Returns:
        _ (Optional[int]): 断面インデックス（ない場合、複数ある場合はNone）
    """
    picked = [obj for obj in sections if obj.num == nMat]
    if (len(picked) == 0) or (len(picked) > 1):
        return None
    else:
        ind: int = sections.index(picked[0])
        return ind


def get_matIndex(nMat: int, materials: list[Material]) -> Optional[int]:
    """ある材料特性番号に対応する材料インデックスを返す

    Args:
        nMat (int): 材料特性番号
        materials (list[Material]): 材料リスト

    Returns:
        _ (Optional[int]): 材料インデックス（ない場合、複数ある場合はNone）
    """
    picked = [obj for obj in materials if obj.num == nMat]
    if (len(picked) == 0) or (len(picked) > 1):
        return None
    else:
        ind: int = materials.index(picked[0])
        return ind
    

def get_thickIndex(nMat: int, thicknesses: list[Thickness]) -> Optional[int]:
    """ある材料特性番号に対応する厚さインデックスを返す

    Args:
        nMat (int): 材料特性番号
        thicknesses (list[Thickness]): 厚さリスト

    Returns:
        _ (Optional[int]): 厚さインデックス（ない場合、複数ある場合はNone）
    """
    picked = [obj for obj in thicknesses if obj.num == nMat]
    if (len(picked) == 0) or (len(picked) > 1):
        return None
    else:
        ind: int = thicknesses.index(picked[0])
        return ind


def get_loadDir(dirStr: str) -> Optional[LoadDir]:
    """荷重載荷方向を取得する

    Args:
        dirStr (str): 荷重載荷方向を表す文字列{x,y,z,gx,gy,gz,r}

    Returns:
        _ (Optional[LoadDir]): 荷重載荷方向
    """
    dirStr = dirStr.lower()
    if dirStr == "x":
        return "Lx"
    elif dirStr == "y":
        return "Ly"
    elif dirStr == "z":
        return "Lz"
    elif dirStr == "gx":
        return "GX"
    elif dirStr == "gy":
        return "GY"
    elif dirStr == "gz":
        return "GZ"
    elif dirStr == "r":
        return "Lr"
    else:
        return None
    

def get_combIndex(nSupCase: int, nSprCase: int, nMatCase: int, nJntCase: int,\
                  caseCombs: list[CaseComb]) -> Optional[int]:
    """ケース組合せリスト内でのインデックスを返す

    Args:
        nSupCase (int): 支点ケース番号
        nSprCase (int): 分布バネケース番号
        nMatCase (int): 材料特性ケース番号
        nJntCase (int): 材端ケース番号
        caseCombs (list[CaseComb]): ケース組合せリスト

    Returns:
        _ (Optional[int]): ケース組合せインデックス（見つからない場合はNone)
    """
    picked = [obj for obj in caseCombs\
              if (obj.nSupportCase == nSupCase) and (obj.nSpringCase == nSprCase)\
              and (obj.nMaterialCase == nMatCase) and (obj.nJointCase == nJntCase)]
    if (len(picked) == 0) or (len(picked) > 1):
        return None
    ind: int = caseCombs.index(picked[0])
    return ind




