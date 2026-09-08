"""V0 input records are independent data, not a best-effort default model."""
from pathlib import Path
import numpy as np
import pytest
from src.fem.file_io import _read_fem_model
from src.fem.model import FemModel


def test_v0_shell_material_thickness_restraint_load_records():
    lines = '''Material 7 1200 .2 500 7.8 45 1
ShellParameter 9 .2
Node 10 0 0 0
Node 20 2 0 0
Node 30 0 3 0
TriElement1 17 7 9 10 20 30
Restraint 10 1 .001 0 0 1 0 0 0 1 .002 0 0
Load 20 1 2 3
Load 20 4 5 6 7 8 9
Pressure 17 F2 100'''.splitlines()
    d = _read_fem_model(lines)
    mat = d['material'].materials[7]
    assert (mat.E, mat.nu, mat.G, mat.density, mat.k, mat.c) == (1200, .2, 500, 7.8, 45, 1)
    assert d['mesh'].elements[17]['thickness'] == .2
    rest = d['boundary'].restraints[10]
    assert rest.dof_restraints == [True, False, True, False, True, False]
    assert rest.values == [.001, 0, 0, 0, .002, 0]
    np.testing.assert_array_equal(d['boundary'].loads[20].forces, [5,7,9,7,8,9])


@pytest.mark.parametrize('record', [
    'Material 1 bad .2 500 7.8 45 1', 'Node 2 NaN 0 0',
    'TriElement1 1 7 99 10 20 30', 'TetraElement2 1 7 10 20 30',
    'Restraint 10 2 0 0 0 0 0', 'Load 10 1 2',
    'Coordinates 1 0 0 0 1 0 0 0 1 0', 'MysteryElement 1 7 10 20 30',
    'Node 10 1 2 3', 'Load 99 1 2 3', 'Load 10 1 2 3 4',
])
def test_v0_bad_input_rejected_with_line_number(record):
    with pytest.raises(ValueError, match='line'):
        _read_fem_model(['Node 10 0 0 0', record])


def test_original_hexa_input_preserves_all_records():
    p = Path(__file__).resolve().parents[1]/'docs/v0/testdata/bend/sampleBendHexa1.fem'
    m = FemModel(); m.load_model(str(p))
    assert len(m.mesh.nodes) == 620
    assert len(m.elements) == sum(l.startswith('HexaElement1 ') for l in p.read_text().splitlines())
    assert m.material.materials[1].E == 21000
    assert m.solver._get_max_dof_per_node(m.mesh) == 3
    # Every fixed node is constrained in x/y/z, not x/z only.
    assert all(r.dof_restraints[:3] == [True]*3 for r in m.boundary.restraints.values())
