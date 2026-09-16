"""elements/beam / elastic contracts."""

import unittest

import numpy as np
import pytest

from fem.elements.bar_element import BarElement, BEBarElement, TBarElement
from fem.elements.nonlinear_bar_element import NonlinearBarElement
from fem.material import BarParameter, Material
from tests.support.builders.nonlinear_beam import RIGIDITIES, beam, force
from tests.support.oracles.elastic_beam import elastic_matrix

pytestmark = pytest.mark.unit


class TestBarElement(unittest.TestCase):
    def setUp(self):
        """テストの前準備"""
        # 節点座標
        self.coords = {1: np.array([0.0, 0.0, 0.0]), 2: np.array([1.0, 0.0, 0.0])}
        # 材料特性
        self.mat = Material()
        self.mat.materials = {1: type("dummy", (), {"E": 210e9, "G": 80e9, "density": 7850})()}
        # 断面特性
        self.bar_param = BarParameter(0.01, 1e-6, 2e-6)
        self.bar_param.J = 0.5e-6
        # 要素生成
        self.elem = BEBarElement(1, [1, 2], 1, 1)
        self.elem.set_node_coordinates(self.coords)
        self.elem.set_material_properties(self.mat, self.bar_param)

    def test_invalid_node_count(self):
        """2節点以外で例外"""
        with self.assertRaises(ValueError):
            BarElement(1, [1], 1, 1)
        with self.assertRaises(ValueError):
            BarElement(1, [1, 2, 3], 1, 1)

    def test_axial_stiffness_and_end_forces(self):
        """単純な梁の剛性行列計算"""
        K = self.elem.get_stiffness_matrix()
        self.assertEqual(K.shape, (12, 12))
        # 軸剛性の一部を検証
        self.assertAlmostEqual(K[0, 0], 210e9 * 0.01 / 1.0)
        self.assertAlmostEqual(K[0, 6], -210e9 * 0.01 / 1.0)
        # 変位ベクトル（軸方向引張り）
        u = np.zeros(12)
        u[0] = 0.001  # 節点1のx方向変位
        u[6] = 0.002  # 節点2のx方向変位

        # 内力計算
        f = self.elem.get_stiffness_matrix() @ u

        # 軸力の検証
        expected_force = 210e9 * 0.01 * (0.002 - 0.001) / 1.0  # E * A * strain
        self.assertAlmostEqual(f[0], -expected_force)  # 節点1の軸力
        self.assertAlmostEqual(f[6], expected_force)  # 節点2の軸力

    def test_mass_matrix(self):
        """質量行列の計算"""
        M = self.elem.get_mass_matrix()
        self.assertEqual(M.shape, (12, 12))
        # 主対角成分が全て非負
        self.assertTrue(np.all(np.diag(M) >= 0))

    def test_rotated_stiffness_is_symmetric(self):
        """45度回転後も剛性行列が対称である。"""
        # 回転角を設定
        elem = BEBarElement(1, [1, 2], 1, 1, angle=45.0)
        elem.set_node_coordinates(self.coords)
        elem.set_material_properties(self.mat, self.bar_param)
        K = elem.get_stiffness_matrix()
        # 回転後の剛性行列は対称性を保持
        self.assertTrue(np.allclose(K, K.T))


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("shear", [False, True])
@pytest.mark.parametrize("cls", [BEBarElement, TBarElement, NonlinearBarElement])
def test_elastic_limit_matches_closed_form_matrix(cls, shear):
    e = beam(cls, dofs=RIGIDITIES if cls is NonlinearBarElement else (), shear=shear)
    expected = elastic_matrix(2.0, shear and cls is not BEBarElement)
    u = np.linspace(-0.0001, 0.0002, 12)
    np.testing.assert_allclose(force(e, u), expected @ u, atol=1e-12)
    k = e.get_tangent_stiffness_matrix(u) if cls is NonlinearBarElement else e.get_stiffness_matrix()
    np.testing.assert_allclose(k, expected, atol=2e-12)


@pytest.mark.material_nonlinear
def test_bernoulli_euler_force_output_uses_right_handed_moments():
    e = beam(BEBarElement)
    u = np.linspace(-0.003, 0.004, 12)
    result = e.calculate_forces(u)
    np.testing.assert_allclose(
        np.r_[result["i_end"], result["j_end"]], elastic_matrix(2, False) @ u, atol=1e-12
    )
