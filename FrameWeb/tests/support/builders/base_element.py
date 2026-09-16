import numpy as np

from fem.elements.base_element import BaseElement


class DummyElement(BaseElement):
    """テスト用の具体的な要素クラス"""

    def get_stiffness_matrix(self) -> np.ndarray:
        return np.eye(6)  # ダミーの剛性行列

    def get_mass_matrix(self) -> np.ndarray:
        return np.eye(6)  # ダミーの質量行列

    def get_name(self) -> str:
        return "TestElement"

    def get_dof_per_node(self) -> int:
        return 3  # 3自由度（x, y, z方向の変位）
