class FA_Spring:
    """フレーム計算用要素分布バネクラス

    Properties:
        iBeam (int):梁要素インデックス
        dxSpr (float):要素座標系x軸方向分布バネ(kN/m/m)
        dySpr (float):要素座標系y軸方向分布バネ(kN/m/m)
        dzSpr (float):要素座標系z軸方向分布バネ(kN/m/m)
        rxSpr (float):要素座標系x軸まわり回転分布バネ(kNm/rad/m)
    """
    def __init__(self, iBeam: int, dxSpr: float, dySpr: float, dzSpr: float, rxSpr: float) -> None:
        """フレーム計算用要素分布バネクラス

        Args:
            iBeam (int): 梁要素インデックス
            dxSpr (float): 要素座標系x軸方向分布バネ(kN/m/m)
            dySpr (float): 要素座標系y軸方向分布バネ(kN/m/m)
            dzSpr (float): 要素座標系z軸方向分布バネ(kN/m/m)
            rxSpr (float): 要素座標系x軸まわり回転分布バネ(kNm/rad/m)
        """
        self.iBeam = iBeam
        self.dxSpr = dxSpr
        self.dySpr = dySpr
        self.dzSpr = dzSpr
        self.rxSpr = rxSpr
        return None
    