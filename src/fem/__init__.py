"""
有限要素法（FEM）解析パッケージ

線形解析と材料非線形解析（JR総研剛性低減RC型）をサポート
"""
# 統合モデル（推奨）
from .model import FemModel

# 個別モジュール
from .mesh import MeshModel
from .boundary_condition import BoundaryCondition, Restraint, Load, DistributedLoad
from .material import Material, MaterialProperty, ShellParameter, BarParameter, NonlinearMaterialProperty
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
from .elements.nonlinear_bar_element import NonlinearBarElement

# 非線形解析モジュール
from .nonlinear import (
    NonlinearSolver,
    HysteresisState,
    BaseHysteresis,
    JRStiffnessReductionParams,
    JRStiffnessReductionModel
)

# 結果処理
from .result_processor import ResultProcessor
from ._version import __version__
from .capabilities import (
    UnsupportedCapabilityError,
    canonical_element_type,
    get_capability_registry,
    get_element_capability,
)
from .diagnostics import (
    InputValidationError,
    ModalConvergenceError,
    NumericalConditionError,
    StructuralMechanismError,
    UnsupportedAnalysisError,
)

# 注記: 旧実装（FEMCalculation）は2025年6月に削除されました
# 新実装（FemModel）をご使用ください

__all__ = [
    # 統合モデル
    'FemModel',

    # 個別モジュール
    'MeshModel',
    'BoundaryCondition', 'Restraint', 'Load', 'DistributedLoad',
    'Material', 'MaterialProperty', 'ShellParameter', 'BarParameter', 'NonlinearMaterialProperty',
    'Section', 'CircleSection', 'RectSection', 'ISection', 'TubeSection',
    'Solver',
    'read_model', 'write_model', 'read_result', 'write_result',

    # 要素クラス
    'BaseElement',
    'BarElement', 'BEBarElement', 'TBarElement',
    'NonlinearBarElement',
    'ShellElement',
    'SolidElement',
    'AdvancedElement',

    # 非線形解析
    'NonlinearSolver',
    'HysteresisState',
    'BaseHysteresis',
    'JRStiffnessReductionParams',
    'JRStiffnessReductionModel',

    # 結果処理
    'ResultProcessor',

    # 機能対応表と解析前検証
    'UnsupportedCapabilityError',
    'canonical_element_type',
    'get_capability_registry',
    'get_element_capability',
    'InputValidationError',
    'UnsupportedAnalysisError',
    'StructuralMechanismError',
    'NumericalConditionError',
    'ModalConvergenceError',
]
