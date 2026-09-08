"""Read-only provenance checks. Never generate references from the solver.

V0 .out files may contain a full input echo, or just a partial printed result.
File names alone do not establish equivalent models or complete references.
"""
from collections import Counter
from decimal import Decimal, ROUND_HALF_UP
import hashlib
import json
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parents[1]
DOFS = ('dx', 'dy', 'dz', 'rx', 'ry', 'rz')


def read_v0(path):
    path = Path(path).resolve()
    records = {key: {} for key in ('nodes', 'elements', 'materials', 'restraints', 'loads', 'displacements')}
    records['sha256'] = hashlib.sha256(path.read_bytes()).hexdigest()
    records['path'] = path.relative_to(ROOT).as_posix()
    for number, line in enumerate(path.read_text(encoding='utf-8-sig').splitlines(), 1):
        fields = line.split()
        if not fields or fields[0].startswith('#'):
            continue
        kind = fields[0].lower()
        if kind == 'node':
            records['nodes'][fields[1]] = list(map(float, fields[2:5]))
        elif kind == 'material':
            records['materials'][fields[1]] = dict(zip(('E', 'nu', 'G'), map(float, fields[2:5])))
        elif 'element' in kind:
            is_shell = kind.startswith(('tri', 'quad'))
            records['elements'][fields[1]] = dict(type=fields[0], material=fields[2],
                nodes=fields[4:] if is_shell else fields[3:], line=number)
        elif kind == 'restraint':
            values = list(map(float, fields[2:]))
            records['restraints'][fields[1]] = values+[0.]*(12-len(values))
        elif kind == 'load':
            previous = records['loads'].setdefault(fields[1], [0.]*6)
            values = list(map(float, fields[2:8]))
            values += [0.]*(6-len(values))
            records['loads'][fields[1]] = [a+b for a, b in zip(previous, values)]
        elif kind == 'displacement':
            if len(fields) != 8:
                raise ValueError(f'Invalid displacement at {path}:{number}')
            records['displacements'][fields[1]] = dict(zip(DOFS, map(float, fields[2:8])))
    return records


def input_findings(data):
    """Evidence about the SELECTED case, rather than always material case 1."""
    findings = []
    connectivity = [v for key in ('member', 'shell', 'solid', 'elements') for v in data.get(key, {}).values()]
    if not connectivity:
        findings.append(dict(category='input', code='missing_element_topology', nodes=len(data.get('node', data.get('nodes', {})))))
    materials = next(iter(data.get('element', {}).values()), {})
    zeros = []
    for mid, member in data.get('member', {}).items():
        material = materials.get(str(member.get('e', 1)), {})
        fields = [k for k in ('E', 'G', 'A', 'Iy', 'Iz', 'J') if material.get(k) == 0]
        if fields:
            zeros.append(dict(member=mid, material=str(member.get('e', 1)), fields=fields))
    if zeros:
        findings.append(dict(category='input', code='zero_member_properties', members=zeros,
            implication='Identify supported/loaded modes; zero alone does not prove an invalid model. No tiny-stiffness replacement.',
            source='src/app/inputDataUtils.py::get_secMatValues'))
    case = next(iter(data.get('load', {}).values()), {})
    unsupported = [v for v in case.get('load_member', []) if v.get('mark') not in (1, 2, 9, 11)
                   and (v.get('P1') or v.get('P2'))]
    if unsupported:
        findings.append(dict(category='input', code='unsupported_member_load', rows=unsupported))
    rounded = []
    nodes = data.get('node', {})
    points = {str(v['m']): set(map(float, v.get('Points', []))) for v in data.get('notice_points', [])}
    for zone in data.get('rigid', []):
        mid = str(zone['m'])
        if mid not in data.get('member', {}):
            findings.append(dict(category='input', code='unknown_rigid_member', member=mid))
            continue
        member = data['member'][mid]
        start, end = [np.array([nodes[str(member[k])][axis] for axis in ('x', 'y', 'z')]) for k in ('ni', 'nj')]
        points.setdefault(mid, set()).update([float(zone.get('Ilength', 0)), np.linalg.norm(end-start)-float(zone.get('Jlength', 0))])
    for load in data.get('_all_member_loads', case.get('load_member', [])):
        mid = str(load.get('m'))
        if mid not in data.get('member', {}):
            continue
        member = data['member'][mid]
        start, end = [np.array([nodes[str(member[k])][axis] for axis in ('x', 'y', 'z')]) for k in ('ni', 'nj')]
        length = float(np.linalg.norm(end-start))
        a, b = float(load.get('L1') or 0), float(load.get('L2') or 0)
        if load.get('mark') == 2:
            b = a-b if b < 0 else length-b
        if load.get('mark') in (1, 2, 11):
            points.setdefault(mid, set()).update([a, b])
    for mid, distances in points.items():
        if mid not in data.get('member', {}):
            findings.append(dict(category='input', code='unknown_notice_member', member=mid))
            continue
        member = data['member'][mid]
        start, end = [np.array([nodes[str(member[k])][axis] for axis in ('x', 'y', 'z')]) for k in ('ni', 'nj')]
        length = float(np.linalg.norm(end-start))
        for distance in sorted(distances):
            if not 0 < distance < length:
                continue
            position = start+(end-start)*(distance/length)
            old = np.array([float(Decimal(str(v)).quantize(Decimal('.001'), ROUND_HALF_UP)) for v in position])
            shift = float(np.linalg.norm(old-position))
            if shift > 1e-10:
                rounded.append(dict(member=mid, distance=distance, exact=position.tolist(), legacy=old.tolist(), shift=shift))
    if rounded:
        findings.append(dict(category='input_model_difference', code='legacy_millimetre_rounding', count=len(rounded),
            max_position_shift=max(v['shift'] for v in rounded), examples=rounded[:5],
            source='src/app/components/member.py::get_coordinateByLength -> components/node.py::get_coordinate',
            implication='Old code rounds generated coordinates. This proves different geometry, not provenance of every embedded result.'))
    return findings


def source_evidence(path, data, actual_displacement=None):
    """Compare available source records, retaining all coverage gaps and diffs."""
    from run_sample import comparison_errors
    path = Path(path)
    result = []
    for extension in ('.fem', '.out'):
        source = ROOT/'docs/v0/testdata'/path.parent.name/(path.stem+extension)
        if not source.exists():
            continue
        records = read_v0(source)
        evidence = {k: records[k] for k in ('path', 'sha256')}
        evidence['counts'] = {k: len(v) for k, v in records.items() if isinstance(v, dict)}
        evidence['element_types'] = dict(Counter(v['type'] for v in records['elements'].values()))
        model_differences = {}
        current_nodes = {k: [v[a] for a in ('x', 'y', 'z')] for k, v in data.get('node', {}).items()}
        if records['nodes']:
            model_differences['nodes'] = comparison_errors(current_nodes, records['nodes'])
        if records['elements']:
            current_elements = {k: dict(material=str(v.get('e', 1)), nodes=list(map(str, v['nodes'])))
                                for field in ('shell', 'solid') for k, v in data.get(field, {}).items()}
            source_elements = {k: {f: v[f] for f in ('material', 'nodes')} for k, v in records['elements'].items()}
            model_differences['topology'] = comparison_errors(current_elements, source_elements)
        materials = next(iter(data.get('element', {}).values()), {})
        if records['materials']:
            # Static isotropic solids use E and nu; G does not enter their 3D constitutive matrix.
            fields = ('E', 'nu') if data.get('solid') else ('E', 'nu', 'G')
            current = {k: {f: v.get(f) for f in fields} for k, v in materials.items()}
            original = {k: {f: v.get(f) for f in fields} for k, v in records['materials'].items()}
            model_differences['static_material'] = comparison_errors(current, original)
        case = next(iter(data.get('load', {}).values()), {})
        if records['loads']:
            current = {}
            for load in case.get('load_node', []):
                n = str(load['n'])
                current[n] = [a+float(load.get(k, 0)) for a, k in zip(current.get(n, [0.]*6), ('tx', 'ty', 'tz', 'rx', 'ry', 'rz'))]
            model_differences['nodal_loads'] = comparison_errors(current, records['loads'])
        if records['restraints']:
            current = {}
            for restraint in next(iter(data.get('fix_node', {}).values()), []):
                current[str(restraint['n'])] = [v for k in ('tx', 'ty', 'tz', 'rx', 'ry', 'rz') for v in (restraint.get(k, 0), 0.)]
            model_differences['restraints'] = comparison_errors(current, records['restraints'])
        evidence['input_comparison'] = {k: dict(mismatches=len(v), examples=[s[:500] for s in v[:3]]) for k, v in model_differences.items()}
        evidence['limitations'] = 'Static input records only. Missing echoes are unverified; no external reaction reference is present. Shell formulation and thickness require separate verification.'
        displacements = records['displacements']
        if displacements:
            saved = next(iter(data.get('result', {}).values()), {}).get('disg', {})
            evidence['reference_coverage'] = dict(source_nodes=len(displacements), input_nodes=len(current_nodes),
                                                  missing_source_nodes=sorted(current_nodes.keys()-displacements.keys(), key=int))
            scales = {}
            for factor in (1, 1000):
                converted = {k: {d: x/factor for d, x in v.items()} for k, v in saved.items()}
                errors = comparison_errors(converted, displacements)
                scales[str(factor)] = dict(mismatches=len(errors), examples=[s[:500] for s in errors[:3]])
            evidence['embedded_displacement_divided_by'] = scales
            if actual_displacement is not None:
                errors = comparison_errors(actual_displacement, displacements)
                evidence['solver_vs_source_displacement'] = dict(mismatches=len(errors), examples=[s[:500] for s in errors[:5]])
                if actual_displacement.keys() == displacements.keys():
                    evidence['solver_vs_source_displacement']['max_absolute_difference'] = max(
                        abs(actual_displacement[n][d]-value)
                        for n, row in displacements.items() for d, value in row.items())
        result.append(evidence)
    return result
