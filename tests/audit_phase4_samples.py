"""Read-only audit of the all reference and load cases of each legacy sample.

Writes diagnostics, NEVER expected results. Run from the repository root:
  .venv/Scripts/python.exe tests/audit_phase4_samples.py
"""
import contextlib
import copy
import io
import json
import hashlib
from pathlib import Path

from run_sample import FemModel, _read_json_model, legacy_result_view, comparison_errors
from src.fem.legacy_beam import select_case
from phase4_source_evidence import input_findings, source_evidence


def audit_case(path, case_id):
    data = json.loads(path.read_text(encoding='utf-8'))
    reference = data.get('result', {}).get(case_id, {})
    entry = dict(sample=path.as_posix(), case=case_id, scope='all load and reference cases',
                 input_sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                 reference=dict(location=f'{path.as_posix()}#/result/{case_id}',
                     provenance='Embedded result; original generating program/version not recorded',
                     units='Legacy contract: kN, m, rad; fixture origin unverified',
                     signs='src/app/result.py section-cut convention',
                     positions='node labels; notice-point segment i/j ends'))
    actual = None
    try:
        data = select_case(data, case_id)
        entry['input_findings'] = input_findings(data)
        with contextlib.redirect_stdout(io.StringIO()):
            model = FemModel()
            model.read_json_model(_read_json_model(copy.deepcopy(data)))
            result = model.run()
        actual = legacy_result_view(result, model, data)
        entry['fields'] = {}
        for field in dict.fromkeys([*actual, *reference]):
            if field not in actual:
                entry['fields'][field] = dict(status='missing output')
                continue
            values = actual[field]
            if field not in reference:
                entry['fields'][field] = dict(status='missing reference')
                continue
            errors = comparison_errors(values, reference[field], field)
            entry['fields'][field] = dict(mismatches=len(errors), examples=errors[:5])
        entry['status'] = 'mismatch' if any(
            v.get('mismatches', 0) or 'status' in v for v in entry['fields'].values()) else 'match'
    except Exception as error:
        entry.update(status='error', error_type=type(error).__name__, message=str(error))
    entry['source_evidence'] = source_evidence(path, data, actual['disg'] if actual else None)
    return entry


def audit(path):
    data = json.loads(path.read_text(encoding='utf-8'))
    cases = dict.fromkeys([*data.get('load', {}), *data.get('result', {})] or ['1'])
    return [audit_case(path, case_id) for case_id in cases]


if __name__ == '__main__':
    entries = [entry for folder in ('bar','shell','bend')
               for path in sorted((Path('tests/data')/folder).glob('*.json')) for entry in audit(path)]
    output = Path('docs/report/material-nonlinear-phase4-sample-audit.json')
    output.write_text(json.dumps(entries, ensure_ascii=False, indent=2), encoding='utf-8')
    print(f'{len(entries)} legacy cases audited: '+str({s:sum(e['status']==s for e in entries)
                                                      for s in ('match','mismatch','error')}))
