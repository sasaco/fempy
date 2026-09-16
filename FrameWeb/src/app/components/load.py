from fem.models.fa_load import LoadDir, FA_NodeLoad, FA_ElemLoad, FA_HeatLoad, FA_ForcedDisp


class CaseComb:
    """ケース組合せクラス

    Properties:
        nSupportCase (int):支点ケース番号
        nSpringCase (int):分布バネケース番号
        nMaterialCase (int):材料特性ケース番号
        nJointCase (int):材端ケース番号
    """
    def __init__(self, nSupCase: int, nSprCase: int, nMatCase: int, nJntCase: int) -> None:
        """ケース組合せクラス

        Args:
            nSupCase (int): 支点ケース番号
            nSprCase (int): 分布バネケース番号
            nMatCase (int): 材料特性ケース番号
            nJntCase (int): 材端ケース番号
        """
        self.nSupportCase = nSupCase
        self.nSpringCase = nSprCase
        self.nMaterialCase = nMatCase
        self.nJointCase = nJntCase
        return None


class LoadCase:
    """荷重ケースクラス

    Properties:
        id (str):基本荷重ケースid
        iCaseComb (int):ケース組合せインデックス
        rate (float):荷重割増係数
        symbol (str):荷重記号
        nodeLoads (list[NodeLoad]):節点荷重リスト
        elemLoads (list[ElemLoad]):要素分布荷重リスト
        heatLoads (list[HeatLoad]):温度荷重リスト
        forcedDisps (list[ForcedDisp]):強制変位リスト
    """
    def __init__(self, caseId: str, iCaseComb: int, rate: float, symbol: str) -> None:
        """荷重ケースクラス

        Args:
            caseId (str): 基本荷重ケースid
            iCaseComb (int): ケース組合せインデックス
            rate (float): 荷重割増係数
            symbol (str): 荷重記号
        """
        self.id = caseId
        self.iCaseComb = iCaseComb
        self.rate = rate
        self.symbol = symbol
        self.nodeLoads: list[NodeLoad] = []
        self.elemLoads: list[ElemLoad] = []
        self.heatLoads: list[HeatLoad] = []
        self.forcedDisps: list[ForcedDisp] = []
        return None


class NodeLoad(FA_NodeLoad):
    """節点荷重クラス

    Properties:
        iNode (int):節点インデックス
        fx (float):全体座標X軸方向の荷重(kN)
        fy (float):全体座標Y軸方向の荷重(kN)
        fz (float):全体座標Z軸方向の荷重(kN)
        rx (float):全体座標X軸まわりのモーメント荷重(kNm)
        ry (float):全体座標Y軸まわりのモーメント荷重(kNm)
        rz (float):全体座標Z軸まわりのモーメント荷重(kNm)
        type (int):荷重タイプ{1:元々の節点荷重,2:要素荷重の集中荷重}
    """
    def __init__(self, iNode: int, fx: float, fy: float, fz: float,\
                 rx: float, ry: float, rz: float, type: int) -> None:
        """節点荷重クラス

        Args:
            iNode (int): 節点インデックス
            fx (float): 全体座標X軸方向の荷重(kN)
            fy (float): 全体座標Y軸方向の荷重(kN)
            fz (float): 全体座標Z軸方向の荷重(kN)
            rx (float): 全体座標X軸まわりのモーメント荷重(kNm)
            ry (float): 全体座標Y軸まわりのモーメント荷重(kNm)
            rz (float): 全体座標Z軸まわりのモーメント荷重(kNm)
            type (int): 荷重タイプ{1:元々の節点荷重,2:要素荷重の集中荷重}
        """
        super().__init__(iNode, fx, fy, fz, rx, ry ,rz)
        self.type = type
        return None
    

class ForcedDisp(FA_ForcedDisp):
    """強制変位クラス

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
        """強制変位クラス

        Args:
            iNode (int): 節点インデックス
            dx (float): 全体座標X軸方向の強制変位(m)
            dy (float): 全体座標Y軸方向の強制変位(m)
            dz (float): 全体座標Z軸方向の強制変位(m)
            ax (float): 全体座標X軸まわりの強制回転角(rad)
            ay (float): 全体座標Y軸まわりの強制回転角(rad)
            az (float): 全体座標Z軸まわりの強制回転角(rad)
        """
        super().__init__(iNode, dx, dy, dz, ax, ay, az)
        return None
    

class ElemLoad(FA_ElemLoad):
    """要素分布荷重クラス

    Properties:
        iBeam (int):梁要素インデックス
        dir (LoadDir):荷重載荷方向
        pi (float):i端荷重値(kN)※dir=Lrの場合は(kNm)
        pj (float):j端荷重値(kN)※dir=Lrの場合は(kNm)
    """
    def __init__(self, iBeam: int, dir: LoadDir, pi: float, pj: float) -> None:
        """要素分布荷重クラス

        Args:
            iBeam (int): 梁要素インデックス
            dir (LoadDir): 荷重載荷方向
            pi (float): i端荷重値(kN)※dir=Lrの場合は(kNm)
            pj (float): j端荷重値(kN)※dir=Lrの場合は(kNm)
        """
        super().__init__(iBeam, dir, pi, pj)


class HeatLoad(FA_HeatLoad):
    """温度荷重クラス

    Properties:
        iBeam (int):梁要素インデックス
        heat (float):荷重温度(°)
    """
    def __init__(self, iBeam: int, heat: float) -> None:
        """温度荷重クラス

        Args:
            iBeam (int): 梁要素インデックス
            heat (float): 荷重温度(°)
        """
        super().__init__(iBeam, heat)


