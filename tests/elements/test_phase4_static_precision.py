"""Force precision must survive stiff, short segments and rigid translations."""
import json
from pathlib import Path

import numpy as np
import pytest

from src.fem.file_io import _read_json_model
from src.fem.legacy_beam import select_case
from src.fem.model import FemModel
from test_phase4_beam_precision import beam


def test_beam_keeps_displacement_below_the_rigid_translation_ulp():
    e = beam()
    high = np.zeros(12); high[[0, 6]] = 1.
    low = np.zeros(12); low[6] = 1e-18
    expected = e.material.materials[1].E/e.length*1e-18
    force = e.calculate_forces(high, displacement_correction=low)
    assert force['j_end'][0] == pytest.approx(expected, rel=1e-13, abs=1e-20)
    assert force['i_end'][0] == -force['j_end'][0]


@pytest.mark.parametrize('direction', [(1.,0.,0.), (.6,.8,0.), (0.,.6,.8)])
def test_public_axial_force_survives_prescribed_rigid_translation(direction):
    from test_phase4_legacy import cantilever
    direction = np.array(direction)
    data = cantilever()
    for n, x in [('1',0.),('2',2.)]:
        data['node'][n] = dict(zip(('x','y','z'), direction*x))
    data['load']['1']['load_node'] = [dict(n=2, **dict(zip(('tx','ty','tz'), 1e-8*direction)))]
    data['element']['1']['2'].update(E=2.65e10, A=1., Iy=1., Iz=1., J=1., G=1e10)
    m = FemModel(); m.read_json_model(_read_json_model(data))
    m.add_forced_displacement(1, dx=.75, dy=.5, dz=.25)
    result = m.run()
    raw = result['constitutive_element_stresses'][1]
    assert raw['j_end'][0] == pytest.approx(1e-8, rel=1e-8, abs=1e-16)
    assert np.any(result['displacement_correction'])
    for name, value in zip(('fx','fy','fz'), -1e-8*direction):
        assert result['reaction_forces'][1][name] == pytest.approx(value, abs=1e-16)


def test_sparse_product_retains_real_sub_ulp_force_and_python311_fallback(monkeypatch):
    import math
    from scipy.sparse import csr_matrix
    from src.fem.precision import sparse_product
    high = np.array([1., 1., 1.]); low = np.array([0.,1e-18,0.])
    matrix = csr_matrix([[1e10,-1e10,3e-13]])
    assert sparse_product(matrix, high, low)[0] == pytest.approx(-1e-8+3e-13, abs=1e-23)
    monkeypatch.delattr(math, 'fma', raising=False)
    assert sparse_product(matrix, high, low)[0] == pytest.approx(-1e-8+3e-13, abs=1e-23)


@pytest.mark.parametrize('case', ['1', '2'])
def test_short_segment_sample_reaches_constitutive_equilibrium(case):
    data = json.loads(Path('tests/data/bar/2D_Sample04.json').read_text(encoding='utf8'))
    m = FemModel(); m.read_json_model(_read_json_model(select_case(data, case)))
    result = m.run()
    # No embedded legacy numbers: independently require nodal equilibrium
    # of the unmodified constitutive forces, before free-branch recovery.
    force = np.zeros_like(result['displacement'])
    raw = result['constitutive_element_stresses']
    for key, element in m.elements.items():
        indices = [m.solver._node_dof_start(n, 6)+i for n in element.node_ids for i in range(6)]
        section = np.r_[raw[key]['i_end'], raw[key]['j_end']]
        force[indices] += element.get_transformation_matrix(12).T@section
    prescribed, springs = m.solver._get_boundary_dofs(m.boundary, len(force), 6)
    for i, stiffness in springs.items():
        force[i] += stiffness*result['displacement'][i]
    nodal = np.zeros_like(force)
    for n, load in m.boundary.loads.items():
        start = m.solver._node_dof_start(n, 6)
        nodal[start:start+6] += load.forces
    free = [i for i in range(len(force)) if i not in prescribed]
    np.testing.assert_allclose(force[free], nodal[free], rtol=0, atol=1e-8)
