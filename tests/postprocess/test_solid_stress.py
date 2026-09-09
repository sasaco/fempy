"""postprocess / solid stress contracts."""

import numpy as np
import pytest

from fem.elements.advanced_element import WedgeElement as AdvancedWedge
from fem.elements.solid_element import TetraElement, WedgeElement
from fem.material import Material, MaterialProperty

pytestmark = pytest.mark.unit


@pytest.mark.parametrize("cls", [TetraElement, WedgeElement, AdvancedWedge])
def test_solid_affine_stress_patch(cls):
    coords = np.array([[0, 0, 0], [2, 0, 0], [0, 3, 0], [0, 0, 4]], dtype=float)
    if cls != TetraElement:
        coords = np.array([[0, 0, -1], [2, 0, -1], [0, 3, -1], [0, 0, 1], [2, 0, 1], [0, 3, 1]], dtype=float)
    coords = coords @ np.array([[1, 0.2, 0.1], [0, 1, 0.3], [0, 0, 1]])
    m = Material()
    m.add_material(1, MaterialProperty("patch", 1000, 0.25))
    e = cls(1, list(range(len(coords))), 1)
    e.set_material_properties(m)
    e.set_node_coordinates(dict(enumerate(coords)))
    strain = np.array([0.001, 0.002, -0.001, 0.003, 0.004, -0.002])
    gradient = np.array([[0.001, 0.003, 0], [0, 0.002, 0.004], [-0.002, 0, -0.001]])
    u = (coords @ gradient.T + [0.1, 0.2, 0.3]).ravel()
    r = e.calculate_stress_strain(u)
    # lambda=mu=400; sigma_normal=2mu*epsilon+lambda*trace.
    stress = np.r_[800 * strain[:3] + 400 * sum(strain[:3]), 400 * strain[3:]]
    np.testing.assert_allclose(r["strain"], np.tile(strain, (len(r["strain"]), 1)), atol=1e-14)
    np.testing.assert_allclose(r["stress"], np.tile(stress, (len(r["stress"]), 1)), atol=1e-10)
