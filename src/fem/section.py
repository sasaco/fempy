"""
断面形状を管理するモジュール
JavaScript版のSection機能に対応
"""
from typing import Dict, Optional, Tuple
import numpy as np
from abc import ABC, abstractmethod
import math


class BaseSection(ABC):
    """断面形状の基本クラス"""
    
    def __init__(self, name: str):
        self.name = name
        
    @abstractmethod
    def get_area(self) -> float:
        """断面積を取得"""
        pass
        
    @abstractmethod
    def get_moment_of_inertia(self) -> Tuple[float, float]:
        """断面二次モーメントを取得 (Iy, Iz)"""
        pass
        
    @abstractmethod
    def get_torsion_constant(self) -> float:
        """ねじり定数を取得"""
        pass
        
    @abstractmethod
    def get_shear_coefficient(self) -> Tuple[float, float]:
        """せん断補正係数を取得 (kappa_y, kappa_z)"""
        pass


class CircleSection(BaseSection):
    """円形断面クラス"""
    
    def __init__(self, diameter: float, name: str = "Circle"):
        """
        Args:
            diameter: 直径
            name: 断面名称
        """
        super().__init__(name)
        self.diameter = diameter
        self.radius = diameter / 2.0
        
    def get_area(self) -> float:
        """断面積を取得"""
        return math.pi * self.radius ** 2
        
    def get_moment_of_inertia(self) -> Tuple[float, float]:
        """断面二次モーメントを取得 (Iy, Iz)"""
        I = math.pi * self.radius ** 4 / 4.0
        return I, I
        
    def get_torsion_constant(self) -> float:
        """ねじり定数を取得（円形断面の場合、極断面二次モーメント）"""
        return math.pi * self.radius ** 4 / 2.0
        
    def get_shear_coefficient(self) -> Tuple[float, float]:
        """せん断補正係数を取得"""
        # 円形断面の場合
        kappa = 6.0 / 7.0
        return kappa, kappa
        
    def get_section_modulus(self) -> Tuple[float, float]:
        """断面係数を取得 (Zy, Zz)"""
        Z = math.pi * self.radius ** 3 / 4.0
        return Z, Z


class RectSection(BaseSection):
    """矩形断面クラス"""
    
    def __init__(self, width: float, height: float, name: str = "Rectangle"):
        """
        Args:
            width: 幅 (y方向)
            height: 高さ (z方向)
            name: 断面名称
        """
        super().__init__(name)
        self.width = width
        self.height = height
        
    def get_area(self) -> float:
        """断面積を取得"""
        return self.width * self.height
        
    def get_moment_of_inertia(self) -> Tuple[float, float]:
        """断面二次モーメントを取得 (Iy, Iz)"""
        Iy = self.width * self.height ** 3 / 12.0
        Iz = self.height * self.width ** 3 / 12.0
        return Iy, Iz
        
    def get_torsion_constant(self) -> float:
        """ねじり定数を取得（矩形断面の場合の近似式）"""
        a = max(self.width, self.height) / 2.0
        b = min(self.width, self.height) / 2.0
        
        # 矩形断面のねじり定数の近似式
        if a / b >= 10:
            # 薄い矩形
            return (2 * a) * (2 * b) ** 3 / 3.0
        else:
            # 一般的な矩形（級数展開の第5項まで）
            alpha = 0.0
            for n in range(5):
                m = 2 * n + 1
                alpha += (-1) ** n / m ** 5 * (1 - np.tanh(m * math.pi * a / (2 * b)))
            return 16 * a * b ** 3 / 3.0 * (1 - 192 * b / (math.pi ** 5 * a) * alpha)
            
    def get_shear_coefficient(self) -> Tuple[float, float]:
        """せん断補正係数を取得"""
        # 矩形断面の場合（Timoshenko梁理論）
        return 5.0 / 6.0, 5.0 / 6.0
        
    def get_section_modulus(self) -> Tuple[float, float]:
        """断面係数を取得 (Zy, Zz)"""
        Zy = self.width * self.height ** 2 / 6.0
        Zz = self.height * self.width ** 2 / 6.0
        return Zy, Zz


class TubeSection(CircleSection):
    """円管断面クラス"""
    
    def __init__(self, outer_diameter: float, thickness: float, name: str = "Tube"):
        """
        Args:
            outer_diameter: 外径
            thickness: 肉厚
            name: 断面名称
        """
        super().__init__(outer_diameter, name)
        self.thickness = thickness
        self.inner_radius = self.radius - thickness
        
        if self.inner_radius < 0:
            raise ValueError("肉厚が外径の半分を超えています")
            
    def get_area(self) -> float:
        """断面積を取得"""
        return math.pi * (self.radius ** 2 - self.inner_radius ** 2)
        
    def get_moment_of_inertia(self) -> Tuple[float, float]:
        """断面二次モーメントを取得 (Iy, Iz)"""
        I = math.pi * (self.radius ** 4 - self.inner_radius ** 4) / 4.0
        return I, I
        
    def get_torsion_constant(self) -> float:
        """ねじり定数を取得"""
        return math.pi * (self.radius ** 4 - self.inner_radius ** 4) / 2.0
        
    def get_section_modulus(self) -> Tuple[float, float]:
        """断面係数を取得 (Zy, Zz)"""
        Z = math.pi * (self.radius ** 4 - self.inner_radius ** 4) / (4.0 * self.radius)
        return Z, Z


class ISection(BaseSection):
    """I形断面クラス"""
    
    def __init__(self, height: float, flange_width: float, web_thickness: float,
                 flange_thickness: float, name: str = "I-Section"):
        """
        Args:
            height: 全高
            flange_width: フランジ幅
            web_thickness: ウェブ厚
            flange_thickness: フランジ厚
            name: 断面名称
        """
        super().__init__(name)
        self.height = height
        self.flange_width = flange_width
        self.web_thickness = web_thickness
        self.flange_thickness = flange_thickness
        
    def get_area(self) -> float:
        """断面積を取得"""
        web_area = self.web_thickness * (self.height - 2 * self.flange_thickness)
        flange_area = 2 * self.flange_width * self.flange_thickness
        return web_area + flange_area
        
    def get_moment_of_inertia(self) -> Tuple[float, float]:
        """断面二次モーメントを取得 (Iy, Iz)"""
        # y軸周り（強軸）
        h_web = self.height - 2 * self.flange_thickness
        Iy = (self.web_thickness * h_web ** 3 / 12.0 +
              2 * self.flange_width * self.flange_thickness ** 3 / 12.0 +
              2 * self.flange_width * self.flange_thickness * ((self.height - self.flange_thickness) / 2.0) ** 2)
              
        # z軸周り（弱軸）
        Iz = (h_web * self.web_thickness ** 3 / 12.0 +
              2 * self.flange_thickness * self.flange_width ** 3 / 12.0)
              
        return Iy, Iz
        
    def get_torsion_constant(self) -> float:
        """ねじり定数を取得（薄肉開断面の近似式）"""
        h_web = self.height - 2 * self.flange_thickness
        return (2 * self.flange_width * self.flange_thickness ** 3 +
                h_web * self.web_thickness ** 3) / 3.0
                
    def get_shear_coefficient(self) -> Tuple[float, float]:
        """せん断補正係数を取得"""
        # I形断面の場合（ウェブのみがせん断を負担すると仮定）
        area = self.get_area()
        web_area = self.web_thickness * (self.height - 2 * self.flange_thickness)
        kappa = web_area / area
        return kappa, kappa


class Section:
    """断面データベースを管理するクラス"""
    
    def __init__(self):
        self.sections: Dict[int, BaseSection] = {}
        self._initialize_default_sections()
        
    def _initialize_default_sections(self) -> None:
        """デフォルト断面を初期化"""
        # 標準的な断面を追加
        self.add_section(1, CircleSection(0.1))  # φ100mm
        self.add_section(2, RectSection(0.1, 0.15))  # 100x150mm
        self.add_section(3, TubeSection(0.1, 0.005))  # φ100mm, t=5mm
        self.add_section(4, ISection(0.3, 0.15, 0.008, 0.012))  # H-300x150x8x12
        
    def add_section(self, section_id: int, section: BaseSection) -> None:
        """断面を追加"""
        self.sections[section_id] = section
        
    def get_section(self, section_id: int) -> Optional[BaseSection]:
        """断面を取得"""
        return self.sections.get(section_id)
        
    def create_bar_parameter(self, section_id: int, material_id: Optional[int] = None) -> Dict:
        """断面から梁パラメータを作成"""
        section = self.get_section(section_id)
        if not section:
            raise ValueError(f"Section ID {section_id} not found")
            
        Iy, Iz = section.get_moment_of_inertia()
        kappa_y, kappa_z = section.get_shear_coefficient()
        
        return {
            'area': section.get_area(),
            'Iy': Iy,
            'Iz': Iz,
            'J': section.get_torsion_constant(),
            'kappa_y': kappa_y,
            'kappa_z': kappa_z,
            'material_id': material_id
        } 