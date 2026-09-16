from typing import List, Optional
from error_handling import logger, MyError

class Solid:
    """ソリッド要素クラス
    
    Properties:
        nSolid (int): ソリッド要素番号
        iNodes (List[int]): 節点インデックスリスト
        iMat (int): 材料特性インデックス
        element_type (str): 要素タイプ（"tetra"、"hexa"または"wedge"）
    """
    def __init__(self, nSolid: int, iNodes: List[int], iMat: int, element_type: str = "tetra") -> None:
        """ソリッド要素クラス
        
        Args:
            nSolid (int): ソリッド要素番号
            iNodes (List[int]): 節点インデックスリスト
            iMat (int): 材料特性インデックス
            element_type (str): 要素タイプ（"tetra"、"hexa"または"wedge"）
        """
        self.nSolid = nSolid
        self.iNodes = iNodes
        self.iMat = iMat
        self.element_type = element_type
        
        if element_type == "tetra" and len(iNodes) != 4:
            errMsg = "4面体要素の節点数が不正です"
            logger.error(errMsg + f": 要素番号({str(nSolid)}), 節点数({len(iNodes)})")
            raise MyError(errMsg)
        elif element_type == "hexa" and len(iNodes) != 8:
            errMsg = "6面体要素の節点数が不正です"
            logger.error(errMsg + f": 要素番号({str(nSolid)}), 節点数({len(iNodes)})")
            raise MyError(errMsg)
        elif element_type == "wedge" and len(iNodes) != 6:
            errMsg = "楔形要素の節点数が不正です"
            logger.error(errMsg + f": 要素番号({str(nSolid)}), 節点数({len(iNodes)})")
            raise MyError(errMsg)
