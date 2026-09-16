"""elements / base contracts."""

import unittest

import numpy as np
import pytest

from fem.elements.base_element import BaseElement
from tests.support.builders.base_element import DummyElement

pytestmark = pytest.mark.unit


class TestBaseElement(unittest.TestCase):
    def setUp(self):
        """テストの前準備"""
        self.element_id = 1
        self.node_ids = [1, 2]
        self.material_id = 1
        self.element = DummyElement(self.element_id, self.node_ids, self.material_id)

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
        coordinates = {1: np.array([0.0, 0.0, 0.0]), 2: np.array([1.0, 0.0, 0.0])}
        self.element.set_node_coordinates(coordinates)
        self.assertTrue(
            np.array_equal(
                self.element.get_element_coordinates(), np.array([[0.0, 0.0, 0.0], [1.0, 0.0, 0.0]])
            )
        )

    def test_nonexistent_node(self):
        """存在しない節点IDのエラー処理"""
        coordinates = {1: np.array([0.0, 0.0, 0.0])}
        with self.assertRaises(ValueError):
            self.element.set_node_coordinates(coordinates)

    def test_get_element_length(self):
        """要素長さの計算テスト"""
        coordinates = {1: np.array([0.0, 0.0, 0.0]), 2: np.array([1.0, 0.0, 0.0])}
        self.element.set_node_coordinates(coordinates)
        self.assertAlmostEqual(self.element.get_element_length(), 1.0)

    def test_get_jacobian(self):
        """ヤコビアン行列の計算テスト"""
        coordinates = {1: np.array([0.0, 0.0, 0.0]), 2: np.array([1.0, 0.0, 0.0])}
        self.element.set_node_coordinates(coordinates)
        jacobian = self.element.get_jacobian(np.array([0.0]))
        self.assertAlmostEqual(jacobian, 0.5)  # 2節点要素の場合、ヤコビアンは要素長さの半分


@pytest.mark.parametrize(
    "method,args",
    [
        ("get_element_volume", ()),
        ("get_shape_functions", (np.array([0.0]),)),
        ("get_shape_derivatives", (np.array([0.0]),)),
    ],
)
def test_unimplemented_geometry_contract(method, args):
    element = DummyElement(1, [1, 2], 1)
    with pytest.raises(NotImplementedError):
        getattr(element, method)(*args)
