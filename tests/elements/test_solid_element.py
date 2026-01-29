import unittest
import numpy as np
from src.fem.elements.solid_element import TetraElement, HexaElement, WedgeElement
from src.fem.material import Material

class TestTetraElement(unittest.TestCase):
    def setUp(self):
        # 四面体要素
        self.node_ids = [1, 2, 3, 4]
        self.coords = {
            1: np.array([0.0, 0.0, 0.0]),
            2: np.array([1.0, 0.0, 0.0]),
            3: np.array([0.0, 1.0, 0.0]),
            4: np.array([0.0, 0.0, 1.0])
        }
        self.material_id = 1
        self.elem = TetraElement(1, self.node_ids, self.material_id)
        self.elem.set_node_coordinates(self.coords)
        self.mat = Material()
        self.mat.materials = {1: type('dummy', (), {'E': 210e9, 'G': 80e9, 'density': 7850, 'nu': 0.3})()}
        self.elem.set_material_properties(self.mat)

    def test_initialization(self):
        self.assertEqual(self.elem.get_name(), "tetra")

    def test_shape_functions(self):
        N = self.elem.get_shape_functions(np.array([0.25, 0.25, 0.25]))
        self.assertAlmostEqual(np.sum(N), 1.0)
        self.assertTrue(np.all(N >= 0))

    def test_shape_derivatives(self):
        dN = self.elem.get_shape_derivatives(np.array([0.25, 0.25, 0.25]))
        self.assertEqual(dN.shape, (3, 4))
        self.assertAlmostEqual(np.sum(dN[0]), 0.0)
        self.assertAlmostEqual(np.sum(dN[1]), 0.0)
        self.assertAlmostEqual(np.sum(dN[2]), 0.0)

    def test_volume(self):
        volume = self.elem.get_volume()
        self.assertAlmostEqual(volume, 1.0/6.0)

    def test_stiffness_matrix(self):
        K = self.elem.get_stiffness_matrix()
        self.assertEqual(K.shape, (12, 12))
        self.assertTrue(np.allclose(K, K.T))

    def test_mass_matrix(self):
        M = self.elem.get_mass_matrix()
        self.assertEqual(M.shape, (12, 12))
        self.assertTrue(np.all(np.diag(M) >= 0))

class TestHexaElement(unittest.TestCase):
    def setUp(self):
        # 六面体要素
        self.node_ids = [1, 2, 3, 4, 5, 6, 7, 8]
        self.coords = {
            1: np.array([0.0, 0.0, 0.0]),
            2: np.array([1.0, 0.0, 0.0]),
            3: np.array([1.0, 1.0, 0.0]),
            4: np.array([0.0, 1.0, 0.0]),
            5: np.array([0.0, 0.0, 1.0]),
            6: np.array([1.0, 0.0, 1.0]),
            7: np.array([1.0, 1.0, 1.0]),
            8: np.array([0.0, 1.0, 1.0])
        }
        self.material_id = 1
        self.elem = HexaElement(1, self.node_ids, self.material_id)
        self.elem.set_node_coordinates(self.coords)
        self.mat = Material()
        self.mat.materials = {1: type('dummy', (), {'E': 210e9, 'G': 80e9, 'density': 7850, 'nu': 0.3})()}
        self.elem.set_material_properties(self.mat)

    def test_initialization(self):
        self.assertEqual(self.elem.get_name(), "hexa")

    def test_shape_functions(self):
        N = self.elem.get_shape_functions(np.array([0.0, 0.0, 0.0]))
        self.assertAlmostEqual(np.sum(N), 1.0)
        self.assertTrue(np.all(N >= 0))

    def test_shape_derivatives(self):
        dN = self.elem.get_shape_derivatives(np.array([0.0, 0.0, 0.0]))
        self.assertEqual(dN.shape, (3, 8))
        self.assertAlmostEqual(np.sum(dN[0]), 0.0)
        self.assertAlmostEqual(np.sum(dN[1]), 0.0)
        self.assertAlmostEqual(np.sum(dN[2]), 0.0)

    def test_gauss_points(self):
        xi, w = self.elem.get_gauss_points()
        self.assertEqual(xi.shape, (8, 3))
        self.assertEqual(w.shape, (8,))
        self.assertAlmostEqual(np.sum(w), 8.0)

    def test_stiffness_matrix(self):
        K = self.elem.get_stiffness_matrix()
        self.assertEqual(K.shape, (24, 24))
        self.assertTrue(np.allclose(K, K.T))

    def test_mass_matrix(self):
        M = self.elem.get_mass_matrix()
        self.assertEqual(M.shape, (24, 24))
        self.assertTrue(np.all(np.diag(M) >= 0))

class TestWedgeElement(unittest.TestCase):
    def setUp(self):
        # くさび要素
        self.node_ids = [1, 2, 3, 4, 5, 6]
        self.coords = {
            1: np.array([0.0, 0.0, 0.0]),
            2: np.array([1.0, 0.0, 0.0]),
            3: np.array([0.0, 1.0, 0.0]),
            4: np.array([0.0, 0.0, 1.0]),
            5: np.array([1.0, 0.0, 1.0]),
            6: np.array([0.0, 1.0, 1.0])
        }
        self.material_id = 1
        self.elem = WedgeElement(1, self.node_ids, self.material_id)
        self.elem.set_node_coordinates(self.coords)
        self.mat = Material()
        self.mat.materials = {1: type('dummy', (), {'E': 210e9, 'G': 80e9, 'density': 7850, 'nu': 0.3})()}
        self.elem.set_material_properties(self.mat)

    def test_initialization(self):
        self.assertEqual(self.elem.get_name(), "wedge")

    def test_shape_functions(self):
        N = self.elem.get_shape_functions(np.array([0.0, 0.0, 0.0]))
        self.assertAlmostEqual(np.sum(N), 1.0)
        self.assertTrue(np.all(N >= 0))

    def test_shape_derivatives(self):
        dN = self.elem.get_shape_derivatives(np.array([0.0, 0.0, 0.0]))
        self.assertEqual(dN.shape, (3, 6))
        self.assertAlmostEqual(np.sum(dN[0]), 0.0)
        self.assertAlmostEqual(np.sum(dN[1]), 0.0)
        self.assertAlmostEqual(np.sum(dN[2]), 0.0)

    def test_gauss_points(self):
        xi, w = self.elem.get_gauss_points()
        self.assertEqual(xi.shape, (6, 3))
        self.assertEqual(w.shape, (6,))
        self.assertAlmostEqual(np.sum(w), 6.0)

    def test_stiffness_matrix(self):
        K = self.elem.get_stiffness_matrix()
        self.assertEqual(K.shape, (18, 18))
        self.assertTrue(np.allclose(K, K.T))

    def test_mass_matrix(self):
        M = self.elem.get_mass_matrix()
        self.assertEqual(M.shape, (18, 18))
        self.assertTrue(np.all(np.diag(M) >= 0))

if __name__ == '__main__':
    unittest.main() 