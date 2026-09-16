from fem.models.fa_joint import FA_Joint

class Joint(FA_Joint):
    """材端条件クラス

    Properties:
        iBeam (int):梁要素インデックス
        xiFree (bool):i端の要素座標系x軸まわりねじりの材端条件がピンか否か
        yiFree (bool):i端の要素座標系y軸まわり回転の材端条件がピンか否か
        ziFree (bool):i端の要素座標系z軸まわり回転の材端条件がピンか否か
        xjFree (bool):j端の要素座標系x軸まわりねじりの材端条件がピンか否か
        yjFree (bool):j端の要素座標系y軸まわり回転の材端条件がピンか否か
        zjFree (bool):j端の要素座標系z軸まわり回転の材端条件がピンか否か
    """
    def __init__(self, iBeam: int, xi: int, yi: int, zi: int, xj: int, yj: int, zj: int) -> None:
        """材端条件クラス

        Args:
            iBeam (int): 梁要素インデックス
            xi (int): i端の要素座標系x軸まわりの拘束条件{0:自由,1:拘束}
            yi (int): i端の要素座標系y軸まわりの拘束条件{0:自由,1:拘束}
            zi (int): i端の要素座標系z軸まわりの拘束条件{0:自由,1:拘束}
            xj (int): j端の要素座標系x軸まわりの拘束条件{0:自由,1:拘束}
            yj (int): j端の要素座標系y軸まわりの拘束条件{0:自由,1:拘束}
            zj (int): j端の要素座標系z軸まわりの拘束条件{0:自由,1:拘束}
        """
        xiFree = True if xi == 0 else False
        yiFree = True if yi == 0 else False
        ziFree = True if zi == 0 else False
        xjFree = True if xj == 0 else False
        yjFree = True if yj == 0 else False
        zjFree = True if zj == 0 else False
        super().__init__(iBeam, xiFree, yiFree, ziFree, xjFree, yjFree, zjFree)
    

