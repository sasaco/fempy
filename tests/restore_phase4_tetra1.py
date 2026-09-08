"""Explicit replacement of incomplete Tetra1 gold by a source-input V0 solve.

Default is read-only. --write preserves all input bytes. Neither this module
nor its reference dependencies import the production FEM solver.
"""
import argparse
import copy
import hashlib
import json
import re

from restore_phase4_sources import ROOT, read_v0, assert_source_input
from v0_refined_reference import solve_source

SAMPLE = 'tests/data/bend/sampleBendTetra1.json'
SOURCE = 'docs/v0/testdata/bend/sampleBendTetra1.fem'
ORIGINAL_SHA256 = 'e208edfae118c637fb858593495791bc1596c840e26b884e32d5d7bfc1348e16'
SOURCE_SHA256 = 'c27e8c496dfb09cb50ab261869bd212c325086ad67f668c82f57e835bd1608a6'
MANIFEST = ROOT/'docs/report/material-nonlinear-phase4-tetra1-repair.json'


def completed_data(data, source, reference):
    topology = assert_source_input(data, source)
    assert source['sha256'] == SOURCE_SHA256
    assert data.get('solid') and len(topology) == 2160
    assert reference.get('input_only') is True
    assert reference['hashes'][source['path']] == source['sha256']
    assert reference['maximum_free_force_residual'] <= 1e-10
    assert set(reference['disg']) == set(reference['node_ids']) == set(source['nodes'])
    assert set(reference['reac']) == set(source['restraints'])
    result = dict(disg=reference['disg'], reac=reference['reac'], size=len(source['nodes']),
                  fsec={}, shell_fsec={}, shell_results={})
    fixed = copy.deepcopy(data)
    fixed['result'] = {'1':result}
    return fixed


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', action='store_true')
    args = parser.parse_args()
    path = ROOT/SAMPLE
    raw = path.read_bytes(); before = hashlib.sha256(raw).hexdigest()
    previous = json.loads(MANIFEST.read_text(encoding='utf8')) if MANIFEST.exists() else None
    assert before == ORIGINAL_SHA256 or (previous and before == previous['after_sha256']), 'Unknown fixture revision'
    source = read_v0(ROOT/SOURCE)
    reference = solve_source(ROOT/SOURCE, input_only=True)
    data = json.loads(raw); fixed = completed_data(data, source, reference)
    if before != ORIGINAL_SHA256:
        assert data == fixed, 'Independent reference has changed'
    original = raw.decode('utf8')
    start = re.search(r'"result"\s*:\s*', original).end()
    _, length = json.JSONDecoder().raw_decode(original[start:])
    newline = '\r\n' if '\r\n' in original else '\n'
    replacement = json.dumps(fixed['result'], ensure_ascii=False, indent=2).replace('\n', newline)
    updated = raw if data == fixed else (original[:start]+replacement+original[start+length:]).encode('utf8')
    record = dict(sample=SAMPLE, source=SOURCE, before_sha256=ORIGINAL_SHA256,
        after_sha256=hashlib.sha256(updated).hexdigest(), source_reference=reference,
        previous_result_sha256=(previous['previous_result_sha256'] if previous else
            hashlib.sha256(json.dumps(data['result'],sort_keys=True).encode()).hexdigest()),
        reason='Complete identical .fem input solved independently with V0; incomplete .out not used as a gold source')
    if args.write:
        assert path.read_bytes() == raw, 'Input changed during reference generation'
        path.write_bytes(updated)
        MANIFEST.write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    print(json.dumps({k:v for k,v in record.items() if k != 'source_reference'},indent=2))


if __name__ == '__main__': main()
