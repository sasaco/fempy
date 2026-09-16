from typing import Literal


# 荷重方向
LoadDir = Literal["Lx", "Ly", "Lz", "GX", "GY", "GZ", "Lr"]


class FA_NodeLoad:
    """フレーム計算用節点荷重クラス

    Properties:
        iNode (int):節点インデックス
        fx (float):全体座標X軸方向の荷重(kN)
        fy (float):全体座標Y軸方向の荷重(kN)
        fz (float):全体座標Z軸方向の荷重(kN)
        rx (float):全体座標X軸まわりのモーメント荷重(kNm)
        ry (float):全体座標Y軸まわりのモーメント荷重(kNm)
        rz (float):全体座標Z軸まわりのモーメント荷重(kNm)
    """
    def __init__(self, iNode: int, fx: float, fy: float, fz: float,\
                 rx: float, ry: float, rz: float) -> None:
        """フレーム計算用節点荷重クラス

        Args:
            iNode (int): 節点インデックス
            fx (float): 全体座標X軸方向の荷重(kN)
            fy (float): 全体座標Y軸方向の荷重(kN)
            fz (float): 全体座標Z軸方向の荷重(kN)
            rx (float): 全体座標X軸まわりのモーメント荷重(kNm)
            ry (float): 全体座標Y軸まわりのモーメント荷重(kNm)
            rz (float): 全体座標Z軸まわりのモーメント荷重(kNm)
        """
        self.iNode = iNode
        self.fx = fx
        self.fy = fy
        self.fz = fz
        self.rx = rx
        self.ry = ry
        self.rz = rz
        return None
    

class FA_ForcedDisp:
    """フレーム計算用強制変位クラス

    Properties:
        iNode (int):節点インデックス
        dx (float):全体座標X軸方向の強制変位(m)
        dy (float):全体座標Y軸方向の強制変位(m)
        dz (float):全体座標Z軸方向の強制変位(m)
        ax (float):全体座標X軸まわりの強制回転角(rad)
        ay (float):全体座標Y軸まわりの強制回転角(rad)
        az (float):全体座標Z軸まわりの強制回転角(rad)
    """
    def __init__(self, iNode: int, dx: float, dy: float, dz: float, \
                 ax: float, ay: float, az: float) -> None:
        """フレーム計算用強制変位クラス

        Args:
            iNode (int): 節点インデックス
            dx (float): 全体座標X軸方向の強制変位(m)
            dy (float): 全体座標Y軸方向の強制変位(m)
            dz (float): 全体座標Z軸方向の強制変位(m)
            ax (float): 全体座標X軸まわりの強制回転角(rad)
            ay (float): 全体座標Y軸まわりの強制回転角(rad)
            az (float): 全体座標Z軸まわりの強制回転角(rad)
        """
        self.iNode = iNode
        self.dx = dx
        self.dy = dy
        self.dz = dz
        self.ax = ax
        self.ay = ay
        self.az = az
        return None
    

class FA_ElemLoad:
    """フレーム計算用要素分布荷重クラス

    Properties:
        iBeam (int):梁要素インデックス
        dir (LoadDir):荷重載荷方向
        pi (float):i端荷重値(kN)※dir=Lrの場合は(kNm)
        pj (float):j端荷重値(kN)※dir=Lrの場合は(kNm)
    """
    def __init__(self, iBeam: int, dir: LoadDir, pi: float, pj: float) -> None:
        """フレーム計算用要素分布荷重クラス

        Args:
            iBeam (int): 梁要素インデックス
            dir (LoadDir): 荷重載荷方向
            pi (float): i端荷重値(kN)※dir=Lrの場合は(kNm)
            pj (float): j端荷重値(kN)※dir=Lrの場合は(kNm)
        """
        self.iBeam = iBeam
        self.dir: LoadDir = dir
        self.pi = pi
        self.pj = pj
        return None
    

class FA_HeatLoad:
    """フレーム計算用温度荷重クラス

    Properties:
        iBeam (int):梁要素インデックス
        heat (float):荷重温度(°)
    """
    def __init__(self, iBeam: int, heat: float) -> None:
        """フレーム計算用温度荷重クラス

        Args:
            iBeam (int): 梁要素インデックス
            heat (float): 荷重温度(°)
        """
        self.iBeam = iBeam
        self.heat = heat
        return None
