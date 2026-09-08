"""Source-guarded completion of solid reaction references.

No production FEM imports. Ordinary execution is read-only; --write adds the
independent reactions and the source-model-derived empty beam/shell fields.
Quadratic solids use original V0 shapes at 50 digits and two-part stiffness.
"""
import argparse
import copy
import hashlib
import json

from restore_phase4_sources import ROOT, read_v0, repaired_data
from v0_refined_reference import solve_source


STEMS = ('sampleBendHexa1', 'sampleBendWedge1', 'sampleBendHexa2',
         'sampleBendWedge2', 'sampleBendTetra2')


def completed_reference(data, source, independent):
    repaired_data(data, source)  # Full source/input identity, including DOFs.
    assert independent['maximum_free_force_residual'] <= 1e-10
    assert independent['hashes'][source['path']] == source['sha256']
    assert set(independent['node_ids']) == set(source['nodes'])
    assert set(independent['reac']) == set(source['restraints'])
    fields = dict(reac=independent['reac'], size=len(source['nodes']),
                  fsec={}, shell_fsec={}, shell_results={})
    fixed = copy.deepcopy(data)
    for key, value in fields.items():
        old = fixed['result']['1']
        assert key not in old or old[key] == value, f'Unknown existing reference: {key}'
        old[key] = copy.deepcopy(value)
    return fixed


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', action='store_true')
    args = parser.parse_args()
    changes, records = [], []
    output = ROOT/'docs/report/material-nonlinear-phase4-support-repairs.json'
    prior = json.loads(output.read_text(encoding='utf8')) if output.exists() else []
    for stem in STEMS:
        source_path = ROOT/'docs/v0/testdata/bend'/(stem+'.out')
        source = read_v0(source_path)
        independent = solve_source(source_path, decimal_stiffness=stem.endswith('2'))
        path = ROOT/'tests/data/bend'/(stem+'.json'); raw = path.read_bytes()
        fixed = completed_reference(json.loads(raw), source, independent)
        changes.append((path, raw, fixed))
        records.append(dict(sample=path.relative_to(ROOT).as_posix(),
            before_sha256=hashlib.sha256(raw).hexdigest(), source_reference=independent,
            fields_added=['reac','size','fsec','shell_fsec','shell_results'],
            empty_field_reason='Source/input identity proves a solid-only model; size is source node count'))
        previous = next((item for item in prior if item['sample'] == records[-1]['sample']
                         and item['after_sha256'] == records[-1]['before_sha256']), None)
        if previous and json.loads(raw) == fixed:
            records[-1]['before_sha256'] = previous['before_sha256']
    if args.write:
        assert all(path.read_bytes() == raw for path,raw,_ in changes), 'Input changed during reference generation'
        for path,raw,fixed in changes:
            # Replace only the result object. Input text, displacement values,
            # and line endings are preserved.
            original = raw.decode('utf8')
            data = json.loads(original)
            if data == fixed: continue
            decoder = json.JSONDecoder()
            import re
            match = re.search(r'"result"\s*:\s*', original)
            start = match.end(); _, length = decoder.raw_decode(original[start:])
            newline = '\r\n' if '\r\n' in original else '\n'
            replacement = json.dumps(fixed['result'], ensure_ascii=False, indent=2).replace('\n', newline)
            path.write_bytes((original[:start]+replacement+original[start+length:]).encode('utf8'))
        for record, (path,_,_) in zip(records,changes):
            record['after_sha256'] = hashlib.sha256(path.read_bytes()).hexdigest()
        output.write_text(json.dumps(records,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    print(json.dumps([{k:v for k,v in record.items() if k != 'source_reference'} for record in records],indent=2))


if __name__ == '__main__': main()
