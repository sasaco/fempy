from typing import Tuple
from error_log import FrameCritical, fLogger


class FA_Joint:
    """フレーム計算用材端条件クラス

    Properties:
        iBeam (int):梁要素インデックス
        xiFree (bool):i端の要素座標系x軸まわりねじりの材端条件がピンか否か
        yiFree (bool):i端の要素座標系y軸まわり回転の材端条件がピンか否か
        ziFree (bool):i端の要素座標系z軸まわり回転の材端条件がピンか否か
        xjFree (bool):j端の要素座標系x軸まわりねじりの材端条件がピンか否か
        yjFree (bool):j端の要素座標系y軸まわり回転の材端条件がピンか否か
        zjFree (bool):j端の要素座標系z軸まわり回転の材端条件がピンか否か
    """
    def __init__(self, iBeam: int, xi: bool, yi: bool, zi: bool, xj: bool, yj: bool, zj: bool) -> None:
        """フレーム計算用材端条件クラス

        Args:
            iBeam (int): 梁要素インデックス
            xiFree (bool): i端の要素座標系x軸まわりねじりの材端条件がピンか否か
            yiFree (bool): i端の要素座標系y軸まわり回転の材端条件がピンか否か
            ziFree (bool): i端の要素座標系z軸まわり回転の材端条件がピンか否か
            xjFree (bool): j端の要素座標系x軸まわりねじりの材端条件がピンか否か
            yjFree (bool): j端の要素座標系y軸まわり回転の材端条件がピンか否か
            zjFree (bool): j端の要素座標系z軸まわり回転の材端条件がピンか否か
        """
        self.iBeam = iBeam
        self.xiFree = xi
        self.yiFree = yi
        self.ziFree = zi
        self.xjFree = xj
        self.yjFree = yj
        self.zjFree = zj
        return None


def get_BeamJoint(iBeam: int, type: int, joints: list[FA_Joint]) -> Tuple[bool, bool]:
    """梁要素両端の材端条件を取得する

    Args:
        iBeam (int): 梁要素インデックス
        type (int): タイプ{0:x軸まわり,1:y軸まわり,2:z軸まわり}
        joints (list[FA_Joint]): 材端条件リスト

    Returns:
        Tuple[bool, bool]: i端およびj端の材端条件がピンか否か
    """
    pickedJnt = [obj for obj in joints if obj.iBeam == iBeam]
    if len(pickedJnt) > 1:
        errMsg = "材端条件の取得時に予期せぬエラーが発生しました"
        fLogger.critical(errMsg + f": 梁要素インデックス({str(iBeam)})")
        raise FrameCritical(errMsg, iBeam=iBeam)
    elif len(pickedJnt) == 1:
        joint = pickedJnt[0]
        if type == 0:  # 要素座標系x軸まわり
            return (joint.xiFree, joint.xjFree)
        elif type == 1:  # 要素座標系y軸まわり
            return (joint.yiFree, joint.yjFree)
        elif type == 2:  # 要素座標系z軸まわり
            return (joint.ziFree, joint.zjFree)
        else:
            errMsg = "材端条件の取得時に予期せぬエラーが発生しました"
            fLogger.critical(errMsg + f": 梁要素インデックス({str(iBeam)})")
            raise FrameCritical(errMsg, iBeam=iBeam)
    else:  # 材端条件が無ければ両端とも剛
        return (False, False)
