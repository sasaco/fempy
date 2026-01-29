from fem.models.fa_support import FA_Support

class Support(FA_Support):
    """支点クラス

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
        for2D (bool):2Dモード用の補助支点か否か
    """
    def __init__(self, iNode: int, dxSup: float, dySup: float, dzSup: float,\
                 rxSup: float, rySup: float, rzSup: float, for2D: bool) -> None:
        """支点クラス

        Args:
            iNode (int): 節点インデックス
            dxSup (float): 全体座標系X軸方向変位の拘束条件{0:自由,1:拘束,それ以外:バネ値(kN/m)}
            dySup (float): 全体座標系Y軸方向変位の拘束条件{0:自由,1:拘束,それ以外:バネ値(kN/m)}
            dzSup (float): 全体座標系Z軸方向変位の拘束条件{0:自由,1:拘束,それ以外:バネ値(kN/m)}
            rxSup (float): 全体座標系X軸まわり回転の拘束条件{0:自由,1:拘束,それ以外:バネ値(kNm/rad)}
            rySup (float): 全体座標系Y軸まわり回転の拘束条件{0:自由,1:拘束,それ以外:バネ値(kNm/rad)}
            rzSup (float): 全体座標系Z軸まわり回転の拘束条件{0:自由,1:拘束,それ以外:バネ値(kNm/rad)}
            for2D (bool): 2Dモード用の補助支点か否か
        """
        dxFix = True if dxSup == 1 else False
        dyFix = True if dySup == 1 else False
        dzFix = True if dzSup == 1 else False
        rxFix = True if rxSup == 1 else False
        ryFix = True if rySup == 1 else False
        rzFix = True if rzSup == 1 else False
        dxSpr = dxSup if dxFix == False else 0.0
        dySpr = dySup if dyFix == False else 0.0
        dzSpr = dzSup if dzFix == False else 0.0
        rxSpr = rxSup if rxFix == False else 0.0
        rySpr = rySup if ryFix == False else 0.0
        rzSpr = rzSup if rzFix == False else 0.0
        self.for2D = for2D
        super().__init__(iNode, dxFix, dyFix, dzFix, rxFix, ryFix, rzFix, dxSpr, dySpr, dzSpr, rxSpr, rySpr, rzSpr)
        return None



