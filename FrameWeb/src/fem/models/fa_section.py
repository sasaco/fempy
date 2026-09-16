class FA_Section:
    """フレーム計算用断面クラス
    
    @brief 梁・柱要素の断面特性を定義するクラス
    
    断面積、断面二次モーメント、ねじり定数等の幾何学的特性を定義し、
    要素の剛性計算に使用されます。
    
    @note 単位系：面積はm²、断面二次モーメントはm⁴
    @note 主軸方向の断面特性を定義

    Properties:
        a (float):断面積(m2)
        iz (float):z軸まわりの断面二次モーメント(m4)
        iy (float):y軸まわりの断面二次モーメント(m4)
        j (float):ねじり定数(m4)
    """
    def __init__(self, a: float, iz: float, iy: float, j: float) -> None:
        """フレーム計算用断面クラス

        @brief 断面特性を指定して断面を作成
        
        @param a 断面積（m²）
        @param iz z軸回りの断面二次モーメント（m⁴）
        @param iy y軸回りの断面二次モーメント（m⁴）
        @param j ねじり定数（m⁴）

        Args:
            a (float): 断面積(m2)
            iz (float): z軸まわりの断面二次モーメント(m4)
            iy (float): y軸まわりの断面二次モーメント(m4)
            j (float): ねじり定数(m4)
        """
        self.a = a
        self.iz = iz
        self.iy = iy
        self.j = j
        return None


