class FA_Node:
    """フレーム計算用節点クラス
    
    @brief 構造解析用の節点（格点）を表現するクラス
    
    3次元空間内の点を座標で定義し、構造要素の接続点として使用されます。
    
    @note 座標系は右手系を使用
    @note 単位はメートル

    Properties:
        x (float):x座標(m)
        y (float):y座標(m)
        z (float):z座標(m)
    """
    def __init__(self, x: float, y: float, z: float) -> None:
        """フレーム計算用節点クラス

        @brief 座標を指定して節点を作成
        
        @param x X座標（メートル）
        @param y Y座標（メートル）
        @param z Z座標（メートル）

        Args:
            x (float): X座標(m)
            y (float): y座標(m)
            z (float): z座標(m)
        """
        self.x = x
        self.y = y
        self.z = z
        return None
