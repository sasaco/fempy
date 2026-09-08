"""Source-guarded exact pressure reference; --write explicitly applies it."""
import argparse
import hashlib
import json
from pathlib import Path
import re

from pressure_reference import reference

ROOT = Path(__file__).resolve().parents[1]


def completed_data(data):
    assert data['nodes'] == {'1':[0,0,0], '2':[1,0,0], '3':[1,1,0], '4':[0,1,0]}
    assert data['elements'] == {'1':dict(type='shell',nodes=[1,2,3,4],material_id=1,thickness=.01)}
    assert data['materials'] == {'1':dict(name='Steel',E=205e9,nu=.3,density=7850.)}
    assert data['boundary_conditions'] == dict(restraints={'1':dict(dof=[True]*6,values=None)},
        pressures=[dict(element_id=1,face='F1',pressure=1000.)])
    assert set(data) == {'nodes','elements','materials','boundary_conditions','result'}
    return {'1':reference()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', action='store_true'); args = parser.parse_args()
    path = ROOT/'tests/data/shell/shellPressureTest1.json'
    raw = path.read_bytes(); data = json.loads(raw); expected = completed_data(data)
    text = raw.decode('utf8'); start = re.search(r'"result"\s*:\s*',text).end()
    _, size = json.JSONDecoder().raw_decode(text[start:])
    new = (text[:start]+json.dumps(expected,indent=4)+text[start+size:]).encode('utf8')
    manifest = dict(sample=path.relative_to(ROOT).as_posix(),
        before_sha256=hashlib.sha256(raw).hexdigest(), after_sha256=hashlib.sha256(new).hexdigest(),
        method='SymPy exact rational polynomial integration and elimination; no FEM solver imports',
        hashes={f:hashlib.sha256((ROOT/f).read_bytes()).hexdigest() for f in
                ['tests/pressure_reference.py','tests/data/shell/shellPressureTest1.fem']},
        conditions='Unit square, thickness .01, E 205e9, nu .3, F1 pressure 1000, node 1 all six DOFs fixed')
    if args.write and data['result'] != expected:
        assert path.read_bytes() == raw
        path.write_bytes(new)
        (ROOT/'docs/report/material-nonlinear-phase4-pressure-repair.json').write_text(
            json.dumps(manifest,indent=2)+'\n',encoding='utf8')
    print(json.dumps(manifest,indent=2))


if __name__ == '__main__': main()
