from components.node import Node
from fem.models.fa_shell import FA_Shell


# (注)
# シェル要素については旧FrameWebをそのまま移植しており理論の確からしさは不明


class Shell(FA_Shell):
    """シェル要素クラス

    Properties:
        num (int):パネル番号
        iNodes: (list[int]):節点インデックスリスト（3または4節点）
        iMat: (int):材料インデックス
        iThick: (int):厚さインデックス
        normalVector (np.ndarray[1, 3]):法線ベクトル
        dirMatrix (np.ndarray[3, 3]):方向余弦行列
        nodes: (list[FA_Node]):節点リスト
    """
    def __init__(self, nPanel: int, i1: int, i2: int, i3: int, i4=None, \
                 iMat: int=0, iThick: int=0, nodes: list[Node]=None) -> None:
        """シェル要素クラス

        Args:
            nPanel (int): パネル番号
            i1 (int): 第1節点インデックス
            i2 (int): 第2節点インデックス
            i3 (int): 第3節点インデックス
            i4 (int, optional): 第4節点インデックス（三角形要素の場合はNone）
            iMat (int): 材料インデックス
            iThick (int): 厚さインデックス
            nodes (list[Node]): 節点リスト
        """
        self.num = nPanel
        if i4 is None:
            super().__init__([i1, i2, i3], iMat, iThick, nodes)
        else:
            super().__init__([i1, i2, i3, i4], iMat, iThick, nodes)
        return None
    

