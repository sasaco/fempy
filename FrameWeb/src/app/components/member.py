import copy
from typing import Optional
from components.node import Node, get_coordinate, find_nodeIndex
from fem.models.fa_beam import FA_Beam, cal_eMatrix

class Beam(FA_Beam):
    """梁要素クラス

    Properties:
        indI (int):i端節点インデックス
        indJ (int):j端節点インデックス
        iMat (int):材料インデックス
        iSec (int):断面インデックス
        angle (float):要素座標軸のデフォルト設定からの回転角(°)
        eMatrix (ndarray[3,3]):全体→要素座標系の基底変換行列
        leng (float):梁要素の長さ(m)
        nodes (list[Node]):節点リスト
    """
    def __init__(self, indI: int, indJ: int, iMat: int, iSec: int, angle: float, nodes: list[Node]) -> None:
        """梁要素クラス

        Args:
            indI (int): i端節点インデックス
            indJ (int): j端節点インデックス
            iMat (int): 材料インデックス
            iSec (int): 断面インデックス
            angle (float): 要素座標軸のデフォルト設定からの回転角(°)
            nodes (list[Node]): 節点リスト
        """
        self.angle = angle
        xi = nodes[indI].x
        yi = nodes[indI].y
        zi = nodes[indI].z
        xj = nodes[indJ].x
        yj = nodes[indJ].y
        zj = nodes[indJ].z
        eMat = cal_eMatrix(xi, yi, zi, xj, yj, zj, angle)
        super().__init__(indI, indJ, iMat, iSec, eMat, nodes)

class Member(Beam):
    """部材クラス

    Properties:
        num (int):部材番号
        indI (int):i端節点インデックス
        indJ (int):j端節点インデックス
        iMat (int):材料インデックス
        iSec (int):断面インデックス
        angle (float):要素座標軸のデフォルト設定からの回転角(°)
        eMatrix (ndarray[3,3]):全体→要素座標系の基底変換行列
        leng (float):部材長さ(m)
        nodes (list[Node]):節点リスト
        beams (list[Beam]):梁要素リスト
        elems (list[int]):部材を構成する梁要素インデックスのリスト（i端から順に）
    """
    def __init__(self, nMem: int, indI: int, indJ: int, iSec: int, iMat: int, \
                 angle: float, nodes: list[Node], beams: list[Beam]) -> None:
        """部材クラス

        Args:
            nMem (int): 部材番号
            indI (int): i端節点インデックス
            indJ (int): j端節点インデックス
            iSec (int): 断面インデックス
            iMat (int): 材料インデックス
            angle (float): 要素座標軸のデフォルト設定からの回転角(°)
            nodes (list[Node]): 節点リスト
            beams (list[Beam]): 梁要素リスト
        """
        super().__init__(indI, indJ, iMat, iSec, angle, nodes)
        beamNew = Beam(indI, indJ, iMat, iSec, angle, nodes)
        beams.append(beamNew)
        iBeam = len(beams) - 1
        self.beams = beams
        self.elems: list[int] = [iBeam]
        self.num = nMem
        return None


    def devide_byLength(self, lenI: float, type: int, lCase: Optional[str] = None) -> None:
        """梁要素を部材i端からの距離で分割する

        Args:
            lenI (float): 要素i端からの距離(m)
            type (int): 分割タイプ{1:剛域境界, 2:着目点, 3:要素荷重作用点}
            lCase (Optional[str]): 分割タイプが3の場合は基本荷重ケース名
        """
        beams = self.beams
        nodes = self.nodes
        (xNew, yNew, zNew) = self.get_coordinateByLength(lenI)
        iNode = find_nodeIndex(xNew, yNew, zNew, nodes)
        if iNode is None:  # 新しい節点を作成し梁要素を分割する
            # 節点の作成
            nodes.append(Node(xNew, yNew, zNew))
            iNode = len(nodes) - 1
            # 要素の分割
            totalLen: float = 0.0
            for iBeam in self.elems:
                totalLen += beams[iBeam].leng
                if lenI < totalLen:
                    break  # 分割対象の梁要素の特定
            beam0 = beams[iBeam]  # 分割対象の梁要素
            iElem = self.elems.index(iBeam)
            indJ = beam0.indJ  # 分割前の梁要素のj端節点インデックス（=分割後に追加される梁要素のj端節点インデックス）
            beam0.indJ = iNode  # 分割前の梁要素のj端節点インデックスを置き換え（梁要素の短縮）
            beamNew = Beam(iNode, indJ, beam0.iMat, beam0.iSec, beam0.angle, nodes)
            beamNew.eMatrix = copy.deepcopy(beam0.eMatrix)
            beams.append(beamNew)
            iBeamNew = len(beams) - 1
            self.elems.insert(iElem + 1, iBeamNew)
        # 節点の属性を反映させる
        node = nodes[iNode]
        node.nMem = self.num
        if type == 1:  # 剛域の境界
            node.isRigid = True
        elif type == 2:  # 着目点
            node.isPoint = True
        elif type == 3:  # 要素荷重の作用点
            node.isELoad = True
            if not lCase is None:
                node.lCases.append(lCase)
        return None
    

    def get_coordinateByLength(self, lenI: float) -> tuple[float, float, float]:
        """部材i端からある距離離れた位置の座標を返す

        Args:
            lenI (float): 部材i端からの距離(m)

        Returns:
            _ (tuple[float, float, float]): X座標(m)、Y座標(m)、Z座標(m)
        """
        beams = self.beams
        nodes = self.nodes
        lenRatio = lenI / self.leng
        node0 = nodes[beams[self.elems[0]].indI]  # 部材の最i端側の節点
        node1 = nodes[beams[self.elems[-1]].indJ]  # 部材の最j端側の節点
        xNew = get_coordinate(node0.x + (node1.x - node0.x) * lenRatio)
        yNew = get_coordinate(node0.y + (node1.y - node0.y) * lenRatio)
        zNew = get_coordinate(node0.z + (node1.z - node0.z) * lenRatio)
        return (xNew, yNew, zNew)




