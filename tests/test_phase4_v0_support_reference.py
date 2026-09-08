"""Validate the independent original-JS reference evaluator using a hand patch."""
import json
from pathlib import Path
import subprocess

import pytest


def test_original_v0_tetra_uniform_axial_stress_supports(tmp_path):
    # Unit tetra, nu=0, exx=.003, E=1000: sxx=3; nodal force = V*B.T*s.
    path = tmp_path/'patch.out'
    path.write_text('''Node 1 0 0 0
Node 2 1 0 0
Node 3 0 1 0
Node 4 0 0 1
Material 1 1000 0 500 1 0 0
TetraElement1 1 1 1 2 3 4
Restraint 1 1 0 1 0 1 0
Restraint 2 1 .003 1 0 1 0
Restraint 3 1 0 1 0 1 0
Restraint 4 1 0 1 0 1 0
Displacement 1 0 0 0 0 0 0
Displacement 2 .003 0 0 0 0 0
Displacement 3 0 0 0 0 0 0
Displacement 4 0 0 0 0 0 0
''', encoding='utf8')
    raw = subprocess.check_output(['node',str(Path(__file__).with_name('v0_support_reference.cjs')),str(path)],text=True,encoding='utf8')
    reference = json.loads(raw)
    for node, force in [('1',-.5),('2',.5),('3',0.),('4',0.)]:
        assert reference['reac'][node] == pytest.approx(dict(tx=force,ty=0.,tz=0.,mx=0.,my=0.,mz=0.),abs=1e-13)
    assert reference['maximum_free_force_residual'] == 0.
    assert len(reference['hashes']) == 9


def test_original_v0_rejects_partial_output_without_echo():
    result = subprocess.run(['node',str(Path(__file__).with_name('v0_support_reference.cjs')),
        'docs/v0/testdata/bend/sampleBendTetra1.out'],capture_output=True,text=True)
    assert result.returncode != 0
    assert 'Complete source input echo' in result.stderr


@pytest.mark.parametrize('axis', range(3))
@pytest.mark.parametrize('force', [.5, -2.])
def test_refined_source_solves_load_not_inaccurate_output_displacement(tmp_path, axis, force):
    from v0_refined_reference import solve_source
    text = ['Node 1 0 0 0', 'Node 2 1 0 0', 'Node 3 0 1 0', 'Node 4 0 0 1',
            'Material 1 1000 0 500 1 0 0', 'TetraElement1 1 1 1 2 3 4']
    # Each selected axis has a free vertex; all other DOFs are prescribed.
    tip = axis+2
    for n in range(1,5):
        rest = [1,0]*3
        if n == tip: rest[2*axis] = 0
        text += [f'Restraint {n} '+ ' '.join(map(str,rest)), f'Displacement {n} .123 .456 .789 0 0 0']
    load = [0.,0.,0.]; load[axis] = force
    text += [f'Load {tip} '+ ' '.join(map(str,load))]
    source = tmp_path/'patch.out'; source.write_text('\n'.join(text),encoding='utf8')
    before = source.read_bytes(); ref = solve_source(source)
    # For nu=0, axial stress gives the opposite reaction at the origin.
    key = ('tx','ty','tz')[axis]
    assert ref['reac']['1'][key] == pytest.approx(-force, rel=1e-13, abs=1e-13)
    assert ref['reac'][str(tip)][key] == 0.
    assert ref['maximum_free_force_residual'] < 1e-13
    assert ref['refinement_iterations'] > 0
    assert source.read_bytes() == before
