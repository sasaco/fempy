"""Strict structural subset of docs/v0/src/FileIO.js input records.

No missing material/parameter defaults, inferred restraints or skipped bad rows.
Unsupported coordinate systems, thermal analysis and element formulations fail.
"""
import numpy as np
from .mesh import MeshModel
from .boundary_condition import BoundaryCondition
from .material import Material, MaterialProperty
from .section import Section


def read_v0_model(lines):
    mesh, boundary, materials = MeshModel(), BoundaryCondition(), Material()
    thicknesses, pending, used = {}, [], set()
    solids = {'tetraelement1': ('tetra', 4), 'hexaelement1': ('hexa', 8),
              'wedgeelement1': ('wedge', 6), 'tetraelement2': ('tetra2', 10),
              'hexaelement2': ('hexa2', 20), 'wedgeelement2': ('wedge2', 15)}
    shells = {'trielement1': 3, 'quadelement1': 4}

    def numbers(parts):
        values = list(map(float, parts))
        if not np.isfinite(values).all():
            raise ValueError('Non-finite value')
        return values

    for line_number, line in enumerate(lines, 1):
        fields = line.lstrip('\ufeff').split('#', 1)[0].split('//', 1)[0].split()
        if not fields:
            continue
        kind = fields[0].lower()
        try:
            key = int(fields[1])
            if kind in ('node', 'material', 'shellparameter') or kind in solids or kind in shells:
                category = 'element' if kind in solids or kind in shells else kind
                if (category, key) in used:
                    raise ValueError(f'Duplicate {category} {key}')
                used.add((category, key))
            if kind == 'node' and len(fields) == 5:
                mesh.add_node(key, numbers(fields[2:]))
            elif kind == 'material' and len(fields) == 8:
                E, nu, G, density, conductivity, specific_heat = numbers(fields[2:])
                if E <= 0 or not -1 < nu < .5 or density < 0:
                    raise ValueError('Invalid elastic material')
                materials.add_material(key, MaterialProperty(
                    str(key), E, nu, density=density, k=conductivity,
                    c=specific_heat, shear_modulus=G))
            elif kind == 'shellparameter' and len(fields) == 3:
                t = numbers(fields[2:])[0]
                if t <= 0:
                    raise ValueError('Shell thickness must be positive')
                thicknesses[key] = t
            elif kind in shells or kind in solids:
                start = 4 if kind in shells else 3
                count = shells[kind] if kind in shells else solids[kind][1]
                if len(fields) != start+count:
                    raise ValueError(f'{fields[0]} requires {count} nodes')
                nodes = list(map(int, fields[start:]))
                if len(set(nodes)) != count:
                    raise ValueError('Duplicate element node')
                params = (dict(param_id=int(fields[3]), formulation='dkt' if kind == 'trielement1' else 'mindlin')
                          if kind in shells else {})
                mesh.add_element(key, 'shell' if kind in shells else solids[kind][0],
                                 nodes, int(fields[2]), **params)
                pending.append((line_number, 'element', key))
            elif kind == 'restraint' and len(fields) in (8, 14):
                values = numbers(fields[2:])
                if any(v not in (0, 1) for v in values[::2]):
                    raise ValueError('Restraint flags must be 0 or 1')
                if key in boundary.restraints:
                    raise ValueError('Duplicate restraint')
                flags, prescribed = list(map(bool, values[::2])), values[1::2]
                if any(v != 0 and not f for f, v in zip(flags, prescribed)):
                    raise ValueError('Prescribed displacement on a free DOF')
                boundary.add_restraint(key, flags, prescribed)
                pending.append((line_number, 'node', key))
            elif kind == 'load' and len(fields) in (5, 8):
                values = numbers(fields[2:]); values += [0.]*(6-len(values))
                boundary.add_load(key, values)
                pending.append((line_number, 'node', key))
            elif kind == 'pressure' and len(fields) == 4:
                face = fields[2].upper()
                if face not in ('F1', 'F2'):
                    raise ValueError('Supported shell pressure faces are F1/F2')
                boundary.add_pressure(key, face, numbers(fields[3:])[0])
                pending.append((line_number, 'pressure', key))
            else:
                raise ValueError(f'Unsupported or malformed {fields[0]} record')
        except (ValueError, IndexError) as error:
            raise ValueError(f'V0 line {line_number}: {error}') from error
    for line_number, kind, key in pending:
        try:
            if kind == 'node':
                if key not in mesh.nodes:
                    raise ValueError(f'Unknown node {key}')
            elif kind == 'pressure':
                if key not in mesh.elements or mesh.elements[key]['type'] != 'shell':
                    raise ValueError('Pressure requires a supported shell element')
            else:
                element = mesh.elements[key]
                if element['material_id'] not in materials.materials:
                    raise ValueError(f'Unknown material {element["material_id"]}')
                missing = set(element['nodes'])-mesh.nodes.keys()
                if missing:
                    raise ValueError(f'Unknown nodes {sorted(missing)}')
                if 'param_id' in element:
                    param = element.pop('param_id')
                    if param not in thicknesses:
                        raise ValueError(f'Unknown shell parameter {param}')
                    element['thickness'] = thicknesses[param]
        except ValueError as error:
            raise ValueError(f'V0 line {line_number}: {error}') from error
    return dict(mesh=mesh, boundary=boundary, material=materials, section=Section())
