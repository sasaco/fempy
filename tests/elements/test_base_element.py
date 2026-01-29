import unittest
import numpy as np
from src.fem.elements.base_element import BaseElement

class TestElement(BaseElement):
    """テスト用の具体的な要素クラス"""
    
    def get_stiffness_matrix(self) -> np.ndarray:
        return np.eye(6)  # ダミーの剛性行列
        
    def get_mass_matrix(self) -> np.ndarray:
        return np.eye(6)  # ダミーの質量行列
        
    def get_name(self) -> str:
        return "TestElement"
        
    def get_dof_per_node(self) -> int:
        return 3  # 3自由度（x, y, z方向の変位）

class TestBaseElement(unittest.TestCase):
    def setUp(self):
        """テストの前準備"""
        self.element_id = 1
        self.node_ids = [1, 2]
        self.material_id = 1
        self.element = TestElement(self.element_id, self.node_ids, self.material_id)

    def test_initialization(self):
        """初期化テスト"""
        self.assertEqual(self.element.element_id, self.element_id)
        self.assertEqual(self.element.node_ids, self.node_ids)
        self.assertEqual(self.element.material_id, self.material_id)

    def test_invalid_input(self):
        """無効な入力値の検証"""
        with self.assertRaises(ValueError):
            BaseElement(-1, self.node_ids, self.material_id)  # 無効な要素ID
        with self.assertRaises(ValueError):
            BaseElement(self.element_id, [], self.material_id)  # 空の節点リスト
        with self.assertRaises(ValueError):
            BaseElement(self.element_id, self.node_ids, -1)  # 無効な材料ID

    def test_set_node_coordinates(self):
        """節点座標の設定テスト"""
        coordinates = {
            1: np.array([0.0, 0.0, 0.0]),
            2: np.array([1.0, 0.0, 0.0])
        }
        self.element.set_node_coordinates(coordinates)
        self.assertTrue(np.array_equal(self.element.get_element_coordinates(), 
                                     np.array([[0.0, 0.0, 0.0], [1.0, 0.0, 0.0]])))

    def test_nonexistent_node(self):
        """存在しない節点IDのエラー処理"""
        coordinates = {
            1: np.array([0.0, 0.0, 0.0])
        }
        with self.assertRaises(ValueError):
            self.element.set_node_coordinates(coordinates)

    def test_get_element_length(self):
        """要素長さの計算テスト"""
        coordinates = {
            1: np.array([0.0, 0.0, 0.0]),
            2: np.array([1.0, 0.0, 0.0])
        }
        self.element.set_node_coordinates(coordinates)
        self.assertAlmostEqual(self.element.get_element_length(), 1.0)

    def test_get_element_volume(self):
        """要素体積の未実装確認"""
        with self.assertRaises(NotImplementedError):
            self.element.get_element_volume()

    def test_get_shape_functions(self):
        """形状関数の未実装確認"""
        with self.assertRaises(NotImplementedError):
            self.element.get_shape_functions(np.array([0.0]))

    def test_get_shape_derivatives(self):
        """形状関数の導関数の未実装確認"""
        with self.assertRaises(NotImplementedError):
            self.element.get_shape_derivatives(np.array([0.0]))

    def test_get_jacobian(self):
        """ヤコビアン行列の計算テスト"""
        coordinates = {
            1: np.array([0.0, 0.0, 0.0]),
            2: np.array([1.0, 0.0, 0.0])
        }
        self.element.set_node_coordinates(coordinates)
        jacobian = self.element.get_jacobian(np.array([0.0]))
        self.assertAlmostEqual(jacobian, 0.5)  # 2節点要素の場合、ヤコビアンは要素長さの半分

if __name__ == '__main__':
    unittest.main() 