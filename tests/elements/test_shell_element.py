import unittest
import numpy as np
from src.fem.elements.shell_element import ShellElement
from src.fem.material import Material, ShellParameter

class TestShellElement(unittest.TestCase):
    def setUp(self):
        # 三角形要素
        self.node_ids = [1, 2, 3]
        self.coords = {
            1: np.array([0.0, 0.0, 0.0]),
            2: np.array([1.0, 0.0, 0.0]),
            3: np.array([0.0, 1.0, 0.0])
        }
        self.thickness = 0.1
        self.material_id = 1
        self.elem = ShellElement(1, self.node_ids, self.material_id, self.thickness)
        self.elem.set_node_coordinates(self.coords)
        self.mat = Material()
        self.mat.materials = {1: type('dummy', (), {'E': 210e9, 'G': 80e9, 'density': 7850, 'nu': 0.3})()}
        self.shell_param = ShellParameter(self.thickness, self.material_id)
        self.elem.set_material_properties(self.mat, self.shell_param)

    def test_initialization(self):
        self.assertEqual(self.elem.get_node_count(), 3)
        self.assertEqual(self.elem.get_name(), "TriElement1")
        self.assertEqual(self.elem.get_matrix_size(), 18)

    def test_shape_functions(self):
        N = self.elem.get_shape_functions(np.array([1/3, 1/3]))
        self.assertAlmostEqual(np.sum(N), 1.0)
        self.assertTrue(np.all(N >= 0))

    def test_shape_derivatives(self):
        dN = self.elem.get_shape_derivatives(np.array([1/3, 1/3]))
        self.assertEqual(dN.shape, (2, 3))
        self.assertAlmostEqual(np.sum(dN[0]), 0.0)
        self.assertAlmostEqual(np.sum(dN[1]), 0.0)

    def test_gauss_points(self):
        xi, w = self.elem.get_gauss_points()
        self.assertEqual(xi.shape, (1, 2))
        self.assertEqual(w.shape, (1,))
        self.assertAlmostEqual(np.sum(w), 0.5)

    def test_jacobian_determinant(self):
        jac = self.elem.get_jacobian_determinant(np.array([1/3, 1/3]))
        self.assertTrue(jac > 0)

    def test_stiffness_matrix(self):
        K = self.elem.get_stiffness_matrix()
        self.assertEqual(K.shape, (18, 18))
        self.assertTrue(np.allclose(K, K.T))

    def test_mass_matrix(self):
        M = self.elem.get_mass_matrix()
        self.assertEqual(M.shape, (18, 18))
        self.assertTrue(np.all(np.diag(M) >= 0))

class TestQuadShellElement(unittest.TestCase):
    def setUp(self):
        # 四角形要素
        self.node_ids = [1, 2, 3, 4]
        self.coords = {
            1: np.array([0.0, 0.0, 0.0]),
            2: np.array([1.0, 0.0, 0.0]),
            3: np.array([1.0, 1.0, 0.0]),
            4: np.array([0.0, 1.0, 0.0])
        }
        self.thickness = 0.1
        self.material_id = 1
        self.elem = ShellElement(1, self.node_ids, self.material_id, self.thickness)
        self.elem.set_node_coordinates(self.coords)
        self.mat = Material()
        self.mat.materials = {1: type('dummy', (), {'E': 210e9, 'G': 80e9, 'density': 7850, 'nu': 0.3})()}
        self.shell_param = ShellParameter(self.thickness, self.material_id)
        self.elem.set_material_properties(self.mat, self.shell_param)

    def test_initialization(self):
        self.assertEqual(self.elem.get_node_count(), 4)
        self.assertEqual(self.elem.get_name(), "QuadElement1")
        self.assertEqual(self.elem.get_matrix_size(), 24)

    def test_shape_functions(self):
        N = self.elem.get_shape_functions(np.array([0.0, 0.0]))
        self.assertAlmostEqual(np.sum(N), 1.0)
        self.assertTrue(np.all(N >= 0))

    def test_shape_derivatives(self):
        dN = self.elem.get_shape_derivatives(np.array([0.0, 0.0]))
        self.assertEqual(dN.shape, (2, 4))
        self.assertAlmostEqual(np.sum(dN[0]), 0.0)
        self.assertAlmostEqual(np.sum(dN[1]), 0.0)

    def test_gauss_points(self):
        xi, w = self.elem.get_gauss_points()
        self.assertEqual(xi.shape, (4, 2))
        self.assertEqual(w.shape, (4,))
        self.assertAlmostEqual(np.sum(w), 4.0)

    def test_jacobian_determinant(self):
        jac = self.elem.get_jacobian_determinant(np.array([0.0, 0.0]))
        self.assertTrue(jac > 0)

    def test_stiffness_matrix(self):
        K = self.elem.get_stiffness_matrix()
        self.assertEqual(K.shape, (24, 24))
        self.assertTrue(np.allclose(K, K.T))

    def test_mass_matrix(self):
        M = self.elem.get_mass_matrix()
        self.assertEqual(M.shape, (24, 24))
        self.assertTrue(np.all(np.diag(M) >= 0))

if __name__ == '__main__':
    unittest.main() 