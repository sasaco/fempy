"""
有限要素法（FEM）解析パッケージ
"""
# 統合モデル（推奨）
from .model import FemModel

# 個別モジュール
from .mesh import MeshModel
from .boundary_condition import BoundaryCondition, Restraint, Load, DistributedLoad
from .material import Material, MaterialProperty, ShellParameter, BarParameter
from .section import Section, CircleSection, RectSection, ISection, TubeSection
from .solver import Solver
from .file_io import read_model, write_model, read_result, write_result

# 要素クラス
from .elements import (
    BaseElement,
    BarElement, BEBarElement, TBarElement,
    ShellElement, 
    SolidElement,
    AdvancedElement
)

# 結果処理
from .result_processor import ResultProcessor

# 注記: 旧実装（FEMCalculation）は2025年6月に削除されました
# 新実装（FemModel）をご使用ください

__all__ = [
    # 統合モデル
    'FemModel',
    
    # 個別モジュール
    'MeshModel',
    'BoundaryCondition', 'Restraint', 'Load', 'DistributedLoad',
    'Material', 'MaterialProperty', 'ShellParameter', 'BarParameter',
    'Section', 'CircleSection', 'RectSection', 'ISection', 'TubeSection',
    'Solver',
    'read_model', 'write_model', 'read_result', 'write_result',
    
    # 要素クラス
    'BaseElement',
    'BarElement', 'BEBarElement', 'TBarElement',
    'ShellElement',
    'SolidElement',
    'AdvancedElement',
    
    # 結果処理
    'ResultProcessor'
]

__version__ = '2.0.0'