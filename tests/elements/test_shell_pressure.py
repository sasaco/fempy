"""
シェル要素の面圧機能テスト
V0の面圧機能をPython版に移植した機能のテスト
"""
import unittest
import numpy as np
import sys
import os

# プロジェクトルートをパスに追加
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '..'))

from src.fem.elements.shell_element import ShellElement
from src.fem.boundary_condition import Pressure, BoundaryCondition
from src.fem.material import Material, MaterialProperty


class TestShellPressure(unittest.TestCase):
    """シェル要素の面圧機能テストクラス"""
    
    def setUp(self):
        """テストの前準備"""
        # テスト用の材料を作成
        self.material = Material()
        steel = MaterialProperty(
            name="Steel",
            E=2.05e11,  # Pa
            nu=0.3,
            density=7850.0  # kg/m³
        )
        self.material.add_material(1, steel)
        
        # テスト用の境界条件を作成
        self.boundary = BoundaryCondition()
        
    def test_pressure_class_creation(self):
        """面圧条件クラスの作成テスト"""
        pressure = Pressure(element_id=1, face="F1", pressure=1000.0)
        
        self.assertEqual(pressure.element_id, 1)
        self.assertEqual(pressure.face, "F1")
        self.assertEqual(pressure.pressure, 1000.0)
        
    def test_pressure_class_string_representation(self):
        """面圧条件クラスの文字列表現テスト"""
        pressure = Pressure(element_id=1, face="F1", pressure=1000.0)
        expected = "Pressure\t1\tF1\t1000.0"
        self.assertEqual(str(pressure), expected)
        
    def test_boundary_condition_pressure_management(self):
        """境界条件での面圧管理テスト"""
        # 面圧条件を追加
        self.boundary.add_pressure(1, "F1", 1000.0)
        self.boundary.add_pressure(2, "F2", 2000.0)
        
        # 面圧条件の取得
        pressures = self.boundary.get_pressures()
        self.assertEqual(len(pressures), 2)
        
        # 個別の面圧条件を確認
        self.assertEqual(pressures[0].element_id, 1)
        self.assertEqual(pressures[0].face, "F1")
        self.assertEqual(pressures[0].pressure, 1000.0)
        
        self.assertEqual(pressures[1].element_id, 2)
        self.assertEqual(pressures[1].face, "F2")
        self.assertEqual(pressures[1].pressure, 2000.0)
        
    def test_triangle_shell_pressure_equivalent_loads(self):
        """三角形シェル要素の面圧等価節点荷重計算テスト"""
        # 3節点三角形シェル要素を作成
        shell = ShellElement(
            element_id=1,
            node_ids=[1, 2, 3],
            material_id=1,
            thickness=0.01
        )
        
        # 節点座標を設定（XY平面内の三角形）
        coordinates = {
            1: np.array([0.0, 0.0, 0.0]),
            2: np.array([1.0, 0.0, 0.0]),
            3: np.array([0.5, 1.0, 0.0])
        }
        shell.set_node_coordinates(coordinates)
        shell.set_material_properties(self.material.materials[1])
        
        # 面圧の等価節点荷重を計算（F1面：節点順序の法線側の表面）
        equiv_loads = shell.get_equivalent_nodal_loads(
            'pressure', [1000.0], "F1"
        )
        
        # 結果の検証
        self.assertEqual(len(equiv_loads), 18)  # 3節点 × 6自由度
        
        # 荷重が適用されていることを確認（数値的検証）
        total_force = np.sum(np.abs(equiv_loads))
        self.assertGreater(total_force, 0.0, "面圧荷重が適用されていません")
        
        # V0 F1 is the full surface; inward traction is -Z for these nodes.
        expected = np.zeros((3, 6))
        expected[:, 2] = -1000*.5/3
        np.testing.assert_allclose(equiv_loads.reshape(3, 6), expected, atol=1e-12)
        
    def test_quadrilateral_shell_pressure_equivalent_loads(self):
        """四角形シェル要素の面圧等価節点荷重計算テスト"""
        # 4節点四角形シェル要素を作成
        shell = ShellElement(
            element_id=1,
            node_ids=[1, 2, 3, 4],
            material_id=1,
            thickness=0.01
        )
        
        # 節点座標を設定（XY平面内の四角形）
        coordinates = {
            1: np.array([0.0, 0.0, 0.0]),
            2: np.array([1.0, 0.0, 0.0]),
            3: np.array([1.0, 1.0, 0.0]),
            4: np.array([0.0, 1.0, 0.0])
        }
        shell.set_node_coordinates(coordinates)
        shell.set_material_properties(self.material.materials[1])
        
        # 面圧の等価節点荷重を計算（F1面：節点順序の法線側の表面）
        equiv_loads = shell.get_equivalent_nodal_loads(
            'pressure', [1000.0], "F1"
        )
        
        # 結果の検証
        self.assertEqual(len(equiv_loads), 24)  # 4節点 × 6自由度
        
        # 荷重が適用されていることを確認
        total_force = np.sum(np.abs(equiv_loads))
        self.assertGreater(total_force, 0.0, "面圧荷重が適用されていません")
        
    def test_pressure_direction_validation(self):
        """面圧の方向性テスト"""
        # 三角形シェル要素を作成
        shell = ShellElement(
            element_id=1,
            node_ids=[1, 2, 3],
            material_id=1,
            thickness=0.01
        )
        
        # 節点座標を設定
        coordinates = {
            1: np.array([0.0, 0.0, 0.0]),
            2: np.array([1.0, 0.0, 0.0]),
            3: np.array([0.5, 1.0, 0.0])
        }
        shell.set_node_coordinates(coordinates)
        shell.set_material_properties(self.material.materials[1])
        
        # V0 F1/F2 are opposite sides of the same area, not adjacent edges.
        equiv_loads_f1 = shell.get_equivalent_nodal_loads(
            'pressure', [1000.0], "F1"
        )
        
        # F2 reverses the F1 normal.
        equiv_loads_f2 = shell.get_equivalent_nodal_loads(
            'pressure', [1000.0], "F2"
        )
        
        np.testing.assert_allclose(equiv_loads_f1, -equiv_loads_f2, atol=1e-12)
        self.assertAlmostEqual(equiv_loads_f1.reshape(3, 6)[:, 2].sum(), -500., places=10)
        self.assertAlmostEqual(equiv_loads_f2.reshape(3, 6)[:, 2].sum(), 500., places=10)
        
    def test_pressure_magnitude_validation(self):
        """面圧の大きさテスト"""
        # 三角形シェル要素を作成
        shell = ShellElement(
            element_id=1,
            node_ids=[1, 2, 3],
            material_id=1,
            thickness=0.01
        )
        
        # 節点座標を設定
        coordinates = {
            1: np.array([0.0, 0.0, 0.0]),
            2: np.array([1.0, 0.0, 0.0]),
            3: np.array([0.5, 1.0, 0.0])
        }
        shell.set_node_coordinates(coordinates)
        shell.set_material_properties(self.material.materials[1])
        
        # 異なる面圧値でテスト
        pressure_values = [100.0, 1000.0, 10000.0]
        
        for pressure_value in pressure_values:
            equiv_loads = shell.get_equivalent_nodal_loads(
                'pressure', [pressure_value], "F1"
            )
            
            # 荷重の大きさが面圧値に比例することを確認
            total_force = np.sum(np.abs(equiv_loads))
            self.assertAlmostEqual(total_force, .5*pressure_value, places=10)
            
    def test_invalid_pressure_parameters(self):
        """無効な面圧パラメータのテスト"""
        # 三角形シェル要素を作成
        shell = ShellElement(
            element_id=1,
            node_ids=[1, 2, 3],
            material_id=1,
            thickness=0.01
        )
        
        # 節点座標を設定
        coordinates = {
            1: np.array([0.0, 0.0, 0.0]),
            2: np.array([1.0, 0.0, 0.0]),
            3: np.array([0.5, 1.0, 0.0])
        }
        shell.set_node_coordinates(coordinates)
        shell.set_material_properties(self.material.materials[1])
        
        # 無効な荷重タイプ
        with self.assertRaises(NotImplementedError):
            shell.get_equivalent_nodal_loads('invalid_type', [1000.0], "F1")
            
        # 無効な荷重値
        with self.assertRaises(ValueError):
            shell.get_equivalent_nodal_loads('pressure', [], "F1")
            
        # 無効な面指定
        with self.assertRaises(ValueError):
            shell.get_equivalent_nodal_loads('pressure', [1000.0], None)
            
    def test_boundary_condition_clear(self):
        """境界条件のクリアテスト"""
        # 面圧条件を追加
        self.boundary.add_pressure(1, "F1", 1000.0)
        self.boundary.add_pressure(2, "F2", 2000.0)
        
        # 面圧条件が追加されていることを確認
        self.assertEqual(len(self.boundary.pressures), 2)
        
        # クリア
        self.boundary.clear()
        
        # 面圧条件がクリアされていることを確認
        self.assertEqual(len(self.boundary.pressures), 0)


if __name__ == '__main__':
    unittest.main()
