class FA_Support:
    """フレーム計算用支点クラス

    Properties:
        iNode (int):節点インデックス
        dxFix (bool):全体座標系X軸方向変位が拘束か否か
        dyFix (bool):全体座標系Y軸方向変位が拘束か否か
        dzFix (bool):全体座標系Z軸方向変位が拘束か否か
        rxFix (bool):全体座標系X軸まわり回転が拘束か否か
        ryFix (bool):全体座標系Y軸まわり回転が拘束か否か
        rzFix (bool):全体座標系Z軸まわり回転が拘束か否か
        dxSpr (float):全体座標系X軸方向バネ(kN/m)
        dySpr (float):全体座標系Y軸方向バネ(kN/m)
        dzSpr (float):全体座標系Z軸方向バネ(kN/m)
        rxSpr (float):全体座標系X軸まわり回転バネ(kNm/rad)
        rySpr (float):全体座標系Y軸まわり回転バネ(kNm/rad)
        rzSpr (float):全体座標系Z軸まわり回転バネ(kNm/rad)
    """
    def __init__(self, iNode: int, dxFix: bool, dyFix: bool, dzFix: bool,\
                 rxFix: bool, ryFix: bool, rzFix: bool, dxSpr: float, dySpr: float, dzSpr: float,\
                 rxSpr: float, rySpr: float, rzSpr: float) -> None:
        """フレーム計算用支点クラス

        Args:
            iNode (int):節点インデックス
            dxFix (bool): 全体座標系X軸方向変位が拘束か否か
            dyFix (bool): 全体座標系Y軸方向変位が拘束か否か
            dzFix (bool): 全体座標系Z軸方向変位が拘束か否か
            rxFix (bool): 全体座標系X軸まわり回転が拘束か否か
            ryFix (bool): 全体座標系Y軸まわり回転が拘束か否か
            rzFix (bool): 全体座標系Z軸まわり回転が拘束か否か
            dxSpr (float): 全体座標系X軸方向バネ(kN/m)
            dySpr (float): 全体座標系Y軸方向バネ(kN/m)
            dzSpr (float): 全体座標系Z軸方向バネ(kN/m)
            rxSpr (float): 全体座標系X軸まわり回転バネ(kNm/rad)
            rySpr (float): 全体座標系Y軸まわり回転バネ(kNm/rad)
            rzSpr (float): 全体座標系Z軸まわり回転バネ(kNm/rad)
        """
        self.iNode = iNode
        self.dxFix = dxFix
        self.dyFix = dyFix
        self.dzFix = dzFix
        self.rxFix = rxFix
        self.ryFix = ryFix
        self.rzFix = rzFix
        self.dxSpr = dxSpr if dxFix == False else 0.0
        self.dySpr = dySpr if dyFix == False else 0.0
        self.dzSpr = dzSpr if dzFix == False else 0.0
        self.rxSpr = rxSpr if rxFix == False else 0.0
        self.rySpr = rySpr if ryFix == False else 0.0
        self.rzSpr = rzSpr if rzFix == False else 0.0
        return None

