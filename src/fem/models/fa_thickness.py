class FA_Thickness:
    """フレーム計算用厚さクラス
    
    @brief シェル・プレート要素の厚さ特性を定義するクラス
    
    シェル要素の厚さを定義し、剛性計算と応力計算に使用されます。
    
    @note 単位はメートル
    @note 一様厚さを仮定

    Properties:
        t (float):厚さ(m)
    """
    def __init__(self, t: float) -> None:
        """フレーム計算用厚さクラス

        @brief 厚さを指定して厚さ特性を作成
        
        @param t 厚さ（メートル）

        Args:
            t (float): 厚さ(m)
        """
        self.t = t
        return None
