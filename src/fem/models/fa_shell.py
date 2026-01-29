import numpy as np
from error_log import FrameCritical, fLogger
from models.fa_node import FA_Node


# (注)
# シェル要素については旧FrameWebをそのまま移植しており理論の確からしさは不明


class FA_Shell:
    """フレーム計算用シェル要素クラス
    
    @brief プレート・シェル構造を表現する面要素クラス
    
    3または4節点で構成される面要素で、面内力・面外曲げ・せん断変形を
    考慮できます。薄肉構造の解析に使用されます。
    
    @note 三角形要素（3節点）または四角形要素（4節点）
    @note Mindlin-Reissner理論に基づく

    Properties:
        iNodes (list[int]):節点インデックスリスト（3または4節点）
        iMat (int):材料インデックス
        iThick (int):厚さインデックス
        normalVector (np.ndarray[1, 3]):法線ベクトル
        dirMatrix (np.ndarray[3, 3]):方向余弦行列
        nodes (list[FA_Node]):節点リスト
        element_type (str): "tri"または"quad"のいずれか（三角形または四角形）
    """
    def __init__(self, iNodes: list[int], iMat: int, iThick: int, nodes: list[FA_Node]) -> None:
        """フレーム計算用シェル要素クラス

        @brief シェル要素の節点と特性を指定してシェル要素を作成
        
        @param iNodes 節点インデックスリスト（3または4節点）
        @param iMat 材料特性インデックス
        @param iThick 厚さ特性インデックス
        @param nodes 節点リスト

        Args:
            iNodes (list[int]): 節点インデックスリスト
            iMat (int): 材料インデックス
            iThick (int): 厚さインデックス
            nodes (list[FA_Node]): 節点リスト
        """
        self.iNodes: list[int] = iNodes
        self.iMat = iMat
        
        if isinstance(iThick, list):
            print(f"警告: iThickがリスト型として渡されました。0に設定します。")
            self.iThick = 0
        elif not isinstance(iThick, int):
            print(f"警告: iThickが整数型ではありません ({type(iThick)})。0に設定します。")
            self.iThick = 0
        else:
            self.iThick = iThick
            
        self.nodes = nodes
        
        # 三角形または四角形の判定
        if len(iNodes) == 3:
            self.element_type = "tri"
        elif len(iNodes) == 4:
            self.element_type = "quad"
        else:
            errMsg = "シェル要素の作成時に予期せぬエラーが発生しました"
            fLogger.critical(errMsg + f": 節点数が無効({str(len(iNodes))})")
            raise FrameCritical(errMsg)
        
        return None


    @property
    def normalVector(self) -> np.ndarray[1, 3]:
        """法線ベクトルを返す

        Returns:
            _ (np.ndarray[1, 3]): 法線ベクトル
        """
        if self.element_type == "tri":
            # 三角形要素の場合
            a = np.subtract(self.get_coordinate(1), self.get_coordinate(0))
            b = np.subtract(self.get_coordinate(2), self.get_coordinate(0))
            c = np.cross(a, b)
            norm_c = np.linalg.norm(c)
            
            if norm_c < 1e-10:
                print(f"警告: シェル要素の法線ベクトル計算で数値的に不安定な状態が検出されました (norm={norm_c})")
                z_coords = [self.get_coordinate(i)[2] for i in range(3)]
                z_diff = max(z_coords) - min(z_coords)
                
                if z_diff < 1e-10:
                    return np.array([0.0, 0.0, 1.0])
                else:
                    y_coords = [self.get_coordinate(i)[1] for i in range(3)]
                    y_diff = max(y_coords) - min(y_coords)
                    
                    if y_diff < 1e-10:
                        return np.array([0.0, 1.0, 0.0])
                    else:
                        x_coords = [self.get_coordinate(i)[0] for i in range(3)]
                        x_diff = max(x_coords) - min(x_coords)
                        
                        if x_diff < 1e-10:
                            return np.array([1.0, 0.0, 0.0])
                        else:
                            return np.array([0.0, 0.0, 1.0])
            
            d = c / norm_c
            return d
        else:
            # 四角形要素の場合
            a = np.subtract(self.get_coordinate(2), self.get_coordinate(0))
            b = np.subtract(self.get_coordinate(3), self.get_coordinate(1))
            c = np.cross(a, b)
            norm_c = np.linalg.norm(c)
            
            if norm_c < 1e-10:
                print(f"警告: シェル要素の法線ベクトル計算で数値的に不安定な状態が検出されました (norm={norm_c})")
                z_coords = [self.get_coordinate(i)[2] for i in range(4)]
                z_diff = max(z_coords) - min(z_coords)
                
                if z_diff < 1e-10:
                    return np.array([0.0, 0.0, 1.0])
                else:
                    y_coords = [self.get_coordinate(i)[1] for i in range(4)]
                    y_diff = max(y_coords) - min(y_coords)
                    
                    if y_diff < 1e-10:
                        return np.array([0.0, 1.0, 0.0])
                    else:
                        x_coords = [self.get_coordinate(i)[0] for i in range(4)]
                        x_diff = max(x_coords) - min(x_coords)
                        
                        if x_diff < 1e-10:
                            return np.array([1.0, 0.0, 0.0])
                        else:
                            return np.array([0.0, 0.0, 1.0])
                
            d = c / norm_c
            return d
    

    @property
    def dirMatrix(self) -> np.ndarray[3, 3]:
        """方向余弦行列を返す

        Returns:
            _ (np.ndarray[3, 3]): 方向余弦行列
        """
        v3 = self.normalVector
        
        if abs(v3[2]) > 0.99:
            v1 = np.array([1.0, 0.0, 0.0])
            v2 = np.array([0.0, 1.0, 0.0])
            d = np.array([
                [v1[0], v2[0], v3[0]],
                [v1[1], v2[1], v3[1]],
                [v1[2], v2[2], v3[2]]
            ], dtype=float)
            return d
        
        if abs(v3[1]) > 0.99:
            v1 = np.array([1.0, 0.0, 0.0])
            v2 = np.array([0.0, 0.0, 1.0])
            d = np.array([
                [v1[0], v2[0], v3[0]],
                [v1[1], v2[1], v3[1]],
                [v1[2], v2[2], v3[2]]
            ], dtype=float)
            return d
        
        if abs(v3[0]) > 0.99:
            v1 = np.array([0.0, 1.0, 0.0])
            v2 = np.array([0.0, 0.0, 1.0])
            d = np.array([
                [v1[0], v2[0], v3[0]],
                [v1[1], v2[1], v3[1]],
                [v1[2], v2[2], v3[2]]
            ], dtype=float)
            return d
        
        v2 = np.subtract(self.get_coordinate(1), self.get_coordinate(0))
        v2 = np.cross(v3, v2)
        norm_v2 = np.linalg.norm(v2)
        
        if norm_v2 < 1e-10:
            if abs(v3[2]) > 0.9:
                v2 = np.cross(v3, np.array([1.0, 0.0, 0.0]))
            else:
                v2 = np.cross(v3, np.array([0.0, 0.0, 1.0]))
            norm_v2 = np.linalg.norm(v2)
            if norm_v2 < 1e-10:
                v2 = np.array([0.0, 1.0, 0.0])
                norm_v2 = 1.0
            
        v2 = v2 / norm_v2
        v1 = np.cross(v2, v3)
        d = np.array([
            [v1[0], v2[0], v3[0]],
            [v1[1], v2[1], v3[1]],
            [v1[2], v2[2], v3[2]]
        ], dtype=float)
        return d
    

    def get_coordinate(self, iCorner: int) -> list[float]:
        """頂点節点の座標を返す

        Args:
            iCorner (int): 頂点インデックス{0～2または0～3}

        Returns:
            _ (list[float]): x,y,z座標(m)をリストにしたもの
        """
        max_corner = 2 if self.element_type == "tri" else 3
        if (iCorner < 0) or (iCorner > max_corner):
            errMsg = "Shell要素の座標取得時に予期せぬエラーが発生しました"
            fLogger.critical(errMsg)
            raise FrameCritical(errMsg)
        iNode = self.iNodes[iCorner]
        x = self.nodes[iNode].x
        y = self.nodes[iNode].y
        z = self.nodes[iNode].z
        return [x, y, z]




