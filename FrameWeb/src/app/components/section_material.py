from fem.models.fa_section import FA_Section
from fem.models.fa_material import FA_Material
from fem.models.fa_thickness import FA_Thickness

class Section(FA_Section):
    """断面クラス

    Properties:
        num (int):材料特性番号
        name (str):材料特性の名称
        a (float):断面積(m2)
        iz (float):z軸まわりの断面二次モーメント(m4)
        iy (float):y軸まわりの断面二次モーメント(m4)
        j (float):ねじり定数(m4)
    """
    def __init__(self, num: int, name: str,\
                 a: float, iz: float, iy: float, j: float) -> None:
        """断面クラス

        Args:
            num (int): 材料特性番号
            name (str): 材料特性の名称
            a (float): 断面積(m2)
            iz (float): z軸まわりの断面二次モーメント(m4)
            iy (float): y軸まわりの断面二次モーメント(m4)
            j (float): ねじり定数(m4)
        """
        super().__init__(a, iz, iy, j)
        self.num = num
        self.name = name
        return None
    

class Thickness(FA_Thickness):
    """厚さクラス

    Properties:
        num (int):材料特性番号
        name (str):材料特性の名称
        t (float):厚さ(m)
    """
    def __init__(self, num: int, name: str, t: float) -> None:
        """厚さクラス

        Args:
            num (int): 材料特性番号
            name (str): 材料特性の名称
            t (float): 厚さ(m)
        """
        super().__init__(t)
        self.num = num
        self.name = name
        return None


class Material(FA_Material):
    """材料クラス

    Properties:
        num (int):材料特性番号
        name (str):材料特性の名称
        e (float):ヤング係数(kN/m2)
        poi (float):ポアソン比
        cte (float):線膨張係数(/℃)
    """
    def __init__(self, num: int, name: str, e: float, g: float, poi: float, cte: float) -> None:
        """材料クラス

        Args:
            num (int): 材料特性番号
            name (str): 材料特性の名称
            e (float): ヤング係数(kN/m2)
            g (float): せん断弾性係数(kN/m2)
            poi (float): ポアソン比
            cte (float): 線膨張係数(/℃)
        """
        super().__init__(e, g, poi, cte)
        self.num = num
        self.name = name
        return None




