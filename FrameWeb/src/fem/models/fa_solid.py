from error_log import FrameCritical, fLogger
from models.fa_node import FA_Node
import numpy as np
from typing import List

class FA_Solid:
    """フレーム計算用ソリッド要素クラス

    Properties:
        number (int): 要素番号
        element_type (str): 要素タイプ（"tetra"、"hexa"または"wedge"）
        material_num (int): 材料番号
        nodes (list[int]): 節点番号リスト
        node_count (int): 節点数（4、6または8）
    """
    def __init__(self, number: int, element_type: str, material_num: int, nodes: List[int]) -> None:
        """フレーム計算用ソリッド要素クラス

        Args:
            number (int): 要素番号
            element_type (str): 要素タイプ（"tetra"、"hexa"または"wedge"）
            material_num (int): 材料番号
            nodes (List[int]): 節点番号リスト（4節点、6節点または8節点）
        """
        self.number = number
        self.element_type = element_type
        self.material_num = material_num
        self.nodes = nodes
        
        if element_type == "tetra":
            if len(nodes) != 4:
                errMsg = "ソリッド要素の作成時に予期せぬエラーが発生しました"
                fLogger.critical(errMsg + f": 4面体要素の節点数が無効({len(nodes)}), 4節点が必要です")
                raise FrameCritical(errMsg)
        elif element_type == "hexa":
            if len(nodes) != 8:
                errMsg = "ソリッド要素の作成時に予期せぬエラーが発生しました"
                fLogger.critical(errMsg + f": 6面体要素の節点数が無効({len(nodes)}), 8節点が必要です")
                raise FrameCritical(errMsg)
        elif element_type == "wedge":
            if len(nodes) != 6:
                errMsg = "ソリッド要素の作成時に予期せぬエラーが発生しました"
                fLogger.critical(errMsg + f": 楔形要素の節点数が無効({len(nodes)}), 6節点が必要です")
                raise FrameCritical(errMsg)
        else:
            errMsg = "ソリッド要素の作成時に予期せぬエラーが発生しました"
            fLogger.critical(errMsg + f": 要素タイプが無効({element_type}), 'tetra'、'hexa'または'wedge'が必要です")
            raise FrameCritical(errMsg)
            
        self.node_count = len(nodes)
    
    def get_coordinate(self, node_index: int, all_nodes: List[FA_Node]) -> List[float]:
        """節点の座標を返す

        Args:
            node_index (int): 節点インデックス（0～node_count-1）
            all_nodes (List[FA_Node]): 全節点リスト

        Returns:
            List[float]: x,y,z座標(m)をリストにしたもの
        """
        if (node_index < 0) or (node_index >= self.node_count):
            errMsg = "Solid要素の座標取得時に予期せぬエラーが発生しました"
            fLogger.critical(errMsg + f": 節点インデックスが無効({node_index}), 0～{self.node_count-1}の範囲である必要があります")
            raise FrameCritical(errMsg)
        iNode = self.nodes[node_index]
        node = all_nodes[iNode]
        return [node.x, node.y, node.z]
