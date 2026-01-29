import unittest
import numpy as np
from src.fem.elements.bar_element import BarElement, BEBarElement
from src.fem.material import Material, BarParameter

class TestBarElement(unittest.TestCase):
    def setUp(self):
        """テストの前準備"""
        # 節点座標
        self.coords = {
            1: np.array([0.0, 0.0, 0.0]),
            2: np.array([1.0, 0.0, 0.0])
        }
        # 材料特性
        self.mat = Material()
        self.mat.materials = {1: type('dummy', (), {
            'E': 210e9,
            'G': 80e9,
            'density': 7850
        })()}
        # 断面特性
        self.bar_param = BarParameter(0.01, 1e-6, 2e-6)
        self.bar_param.J = 0.5e-6
        # 要素生成
        self.elem = BEBarElement(1, [1,2], 1, 1)
        self.elem.set_node_coordinates(self.coords)
        self.elem.set_material_properties(self.mat, self.bar_param)

    def test_invalid_node_count(self):
        """2節点以外で例外"""
        with self.assertRaises(ValueError):
            BarElement(1, [1], 1, 1)
        with self.assertRaises(ValueError):
            BarElement(1, [1,2,3], 1, 1)

    def test_basic_stiffness_matrix(self):
        """単純な梁の剛性行列計算"""
        K = self.elem.get_stiffness_matrix()
        self.assertEqual(K.shape, (12,12))
        # 軸剛性の一部を検証
        self.assertAlmostEqual(K[0,0], 210e9*0.01/1.0)
        self.assertAlmostEqual(K[0,6], -210e9*0.01/1.0)

    def test_mass_matrix(self):
        """質量行列の計算"""
        M = self.elem.get_mass_matrix()
        self.assertEqual(M.shape, (12,12))
        # 主対角成分が全て非負
        self.assertTrue(np.all(np.diag(M) >= 0))

    def test_boundary_conditions(self):
        """境界条件の影響"""
        # 回転角を設定
        elem = BEBarElement(1, [1,2], 1, 1, angle=45.0)
        elem.set_node_coordinates(self.coords)
        elem.set_material_properties(self.mat, self.bar_param)
        K = elem.get_stiffness_matrix()
        # 回転後の剛性行列は対称性を保持
        self.assertTrue(np.allclose(K, K.T))

    def test_stress_strain_calculation(self):
        """応力・ひずみ計算"""
        # 変位ベクトル（軸方向引張り）
        u = np.zeros(12)
        u[0] = 0.001  # 節点1のx方向変位
        u[6] = 0.002  # 節点2のx方向変位
        
        # 内力計算
        f = self.elem.get_stiffness_matrix() @ u
        
        # 軸力の検証
        expected_force = 210e9 * 0.01 * (0.002 - 0.001) / 1.0  # E * A * strain
        self.assertAlmostEqual(f[0], -expected_force)  # 節点1の軸力
        self.assertAlmostEqual(f[6], expected_force)   # 節点2の軸力

if __name__ == '__main__':
    unittest.main() 