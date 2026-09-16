class FA_Material:
    """フレーム計算用材料クラス
    
    @brief 構造材料の力学的特性を定義するクラス
    
    鋼材、コンクリート等の材料特性を定義し、要素の剛性計算に使用されます。
    
    @note 単位系：力はkN、長さはm
    @note 温度荷重計算時に線膨張係数を使用

    Properties:
        e (float):ヤング係数(kN/m2)
        g (float):せん断弾性係数(kN/m2)
        cte (float):線膨張係数(/℃)
        poi (float):ポアソン比
    """
    def __init__(self, e: float, g: float, poi: float, cte: float) -> None:
        """フレーム計算用材料クラス

        @brief 材料特性を指定して材料を作成
        
        @param e ヤング係数（kN/m²）- 材料の縦弾性係数
        @param g せん断弾性係数（kN/m²）- 材料のせん断弾性係数
        @param poi ポアソン比 - 横ひずみと縦ひずみの比
        @param cte 線膨張係数（/℃）- 温度荷重計算用

        Args:
            e (float): ヤング係数(kN/m2)
            g (float): せん断弾性係数(kN/m2)
            poi (float): ポアソン比
            cte (float): 線膨張係数(/℃)
        """
        self.e = e
        self.poi = poi
        self.cte = cte
        self.g = g
        return None


