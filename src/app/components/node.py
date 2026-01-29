from typing import Optional
from decimal import Decimal, ROUND_HALF_UP
from helper import isFloat
from fem.models.fa_node import FA_Node

class Node(FA_Node):
    """節点クラス

    Properties:
        x (float):x座標(m)
        y (float):y座標(m)
        z (float):z座標(m)
        nNode (int):節点番号※isNode=Falseなら0
        isNode (bool):入力データに含まれる節点か否か
        isPoint (bool):着目点か否か
        isELoad (bool):要素荷重の作用点か否か
        isRigid (bool):剛域の境界点か否か
        nMem (int):isPoint、isELoad、isRigidのいずれかがTrueの場合は部材番号、全てFalsenの場合は0
        lCases (list[str]):isELoadがTrueの場合は該当する基本荷重ケース名のリスト
    """
    def __init__(self, x: float, y: float, z: float) -> None:
        """節点クラス

        Args:
            x (float): X座標(m)
            y (float): y座標(m)
            z (float): z座標(m)
        """
        super().__init__(x, y, z)
        self.nNode: int = 0
        self.isNode: bool = False
        self.isPoint: bool = False
        self.isELoad: bool = False
        self.isRigid: bool = False
        self.nMem: int = 0
        self.lCases: list[str] = []
        return None


def get_coordinate(value) -> Optional[float]:
    """座標値(m)をfloat型で返す

    Args:
        value (any): 座標の元データ（文字列や数値など）

    Returns:
        _ (Optional[float]): 小数点以下3桁（1mm単位）で丸められた座標値(m)
    """
    if isFloat(value):
        val = float(value)
        val_dec = Decimal(str(val)).quantize(Decimal('0.001'), ROUND_HALF_UP)
        return float(val_dec)
    else:
        return None
    

def find_nodeIndex(x: float, y: float, z: float, nodes: list[Node]) -> Optional[int]:
    """節点リスト内である座標の節点を探す

    Args:
        x (float): X座標(m)
        y (float): Y座標(m)
        z (float): Z座標(m)
        nodes (list[Node]): 節点リスト

    Returns:
        _ (Optional[int]): 節点インデックス（見つからない場合、複数ある場合はNone）
    """
    pickedNode = [obj for obj in nodes if (obj.x == x) and (obj.y == y) and (obj.z == z)]
    if (len(pickedNode) == 0) or (len(pickedNode) > 1):
        return None
    ind: int = nodes.index(pickedNode[0])
    return ind
