"""io / pressure contracts."""

import json
import os
import tempfile
import unittest

import pytest

from fem.boundary_condition import BoundaryCondition, Pressure
from fem.file_io import read_model, write_model
from fem.material import Material, MaterialProperty
from fem.solver import Solver

pytestmark = pytest.mark.integration


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
            density=7850.0,  # kg/m³
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


class TestPressureIntegration(unittest.TestCase):
    """面圧機能の統合テストクラス"""

    def setUp(self):
        """テストの前準備"""
        self.solver = Solver()

    def test_shell_with_pressure_load_json(self):
        """シェル要素に面圧を適用した解析テスト（JSON形式）"""
        # テスト用のモデルデータを作成
        model_data = {
            "nodes": {"1": [0.0, 0.0, 0.0], "2": [1.0, 0.0, 0.0], "3": [1.0, 1.0, 0.0], "4": [0.0, 1.0, 0.0]},
            "elements": {"1": {"type": "shell", "nodes": [1, 2, 3, 4], "material_id": 1, "thickness": 0.01}},
            "materials": {"1": {"name": "Steel", "E": 2.05e11, "nu": 0.3, "density": 7850.0}},
            "boundary_conditions": {
                "restraints": {"1": {"dof": [True, True, True, False, False, False], "values": None}},
                "pressures": [{"element_id": 1, "face": "F1", "pressure": 1000.0}],
            },
        }

        # 一時ファイルに書き込み
        with tempfile.NamedTemporaryFile(mode="w", suffix=".json", delete=False) as f:
            json.dump(model_data, f)
            temp_file = f.name

        try:
            # モデルを読み込み
            model = read_model(temp_file)

            # モデルデータの検証
            self.assertIn("mesh", model)
            self.assertIn("boundary", model)
            self.assertIn("material", model)

            # 面圧条件の確認
            pressures = model["boundary"].get_pressures()
            self.assertEqual(len(pressures), 1)
            self.assertEqual(pressures[0].element_id, 1)
            self.assertEqual(pressures[0].face, "F1")
            self.assertEqual(pressures[0].pressure, 1000.0)

        finally:
            # 一時ファイルを削除
            os.unlink(temp_file)

    def test_shell_with_pressure_load_fem(self):
        """シェル要素に面圧を適用した解析テスト（original source .fem形式）"""
        # original source形式のテストデータを作成
        fem_content = """# original source形式の面圧テストデータ
Node 1 0.0 0.0 0.0
Node 2 1.0 0.0 0.0
Node 3 1.0 1.0 0.0
Node 4 0.0 1.0 0.0

Material 1 2.05e11 0.3 78846153846.15384 7850.0 45.0 1.0
ShellParameter 1 0.01

QuadElement1 1 1 1 1 2 3 4

Restraint 1 1 0 1 0 1 0 0 0 0 0 0 0

Pressure 1 F1 1000.0
"""

        # 一時ファイルに書き込み（UTF-8エンコーディングを明示）
        with tempfile.NamedTemporaryFile(mode="w", suffix=".fem", delete=False, encoding="utf-8") as f:
            f.write(fem_content)
            temp_file = f.name

        try:
            # モデルを読み込み
            model = read_model(temp_file)

            # モデルデータの検証
            self.assertIn("mesh", model)
            self.assertIn("boundary", model)
            self.assertIn("material", model)

            # 面圧条件の確認
            pressures = model["boundary"].get_pressures()
            self.assertEqual(len(pressures), 1)
            self.assertEqual(pressures[0].element_id, 1)
            self.assertEqual(pressures[0].face, "F1")
            self.assertEqual(pressures[0].pressure, 1000.0)

        finally:
            # 一時ファイルを削除
            os.unlink(temp_file)

    def test_multiple_pressure_loads(self):
        """複数の面圧荷重のテスト"""
        # 複数の面圧条件を持つモデルを作成
        model_data = {
            "nodes": {"1": [0.0, 0.0, 0.0], "2": [1.0, 0.0, 0.0], "3": [1.0, 1.0, 0.0], "4": [0.0, 1.0, 0.0]},
            "elements": {"1": {"type": "shell", "nodes": [1, 2, 3, 4], "material_id": 1, "thickness": 0.01}},
            "materials": {"1": {"name": "Steel", "E": 2.05e11, "nu": 0.3, "density": 7850.0}},
            "boundary_conditions": {
                "restraints": {"1": {"dof": [True, True, True, False, False, False], "values": None}},
                "pressures": [
                    {"element_id": 1, "face": "F1", "pressure": 1000.0},
                    {"element_id": 1, "face": "F2", "pressure": 2000.0},
                ],
            },
        }

        # 一時ファイルに書き込み
        with tempfile.NamedTemporaryFile(mode="w", suffix=".json", delete=False) as f:
            json.dump(model_data, f)
            temp_file = f.name

        try:
            # モデルを読み込み
            model = read_model(temp_file)

            # 複数の面圧条件の確認
            pressures = model["boundary"].get_pressures()
            self.assertEqual(len(pressures), 2)

            # 各面圧条件の確認
            pressure_f1 = next(p for p in pressures if p.face == "F1")
            pressure_f2 = next(p for p in pressures if p.face == "F2")

            self.assertEqual(pressure_f1.pressure, 1000.0)
            self.assertEqual(pressure_f2.pressure, 2000.0)

        finally:
            # 一時ファイルを削除
            os.unlink(temp_file)

    def test_pressure_file_io_roundtrip(self):
        """面圧条件のファイル入出力ラウンドトリップテスト"""
        # 元のモデルデータ
        original_data = {
            "nodes": {"1": [0.0, 0.0, 0.0], "2": [1.0, 0.0, 0.0], "3": [1.0, 1.0, 0.0], "4": [0.0, 1.0, 0.0]},
            "elements": {"1": {"type": "shell", "nodes": [1, 2, 3, 4], "material_id": 1, "thickness": 0.01}},
            "materials": {"1": {"name": "Steel", "E": 2.05e11, "nu": 0.3, "density": 7850.0}},
            "boundary_conditions": {
                "restraints": {"1": {"dof": [True, True, True, False, False, False], "values": None}},
                "pressures": [{"element_id": 1, "face": "F1", "pressure": 1000.0}],
            },
        }

        # 一時ファイルに書き込み
        with tempfile.NamedTemporaryFile(mode="w", suffix=".json", delete=False) as f:
            json.dump(original_data, f)
            temp_file = f.name

        try:
            # モデルを読み込み
            model = read_model(temp_file)

            # 別の一時ファイルに書き込み
            with tempfile.NamedTemporaryFile(mode="w", suffix=".json", delete=False) as f2:
                write_model(model, f2.name)
                temp_file2 = f2.name

            try:
                # 再度読み込み
                model2 = read_model(temp_file2)

                # 面圧条件が保持されていることを確認
                pressures1 = model["boundary"].get_pressures()
                pressures2 = model2["boundary"].get_pressures()

                self.assertEqual(len(pressures1), len(pressures2))
                self.assertEqual(pressures1[0].element_id, pressures2[0].element_id)
                self.assertEqual(pressures1[0].face, pressures2[0].face)
                self.assertEqual(pressures1[0].pressure, pressures2[0].pressure)

            finally:
                os.unlink(temp_file2)

        finally:
            os.unlink(temp_file)
