"""Strict normalization of component-specific nonlinear-law definitions."""
from .axial_force_table import AxialForceTable
from .hysteresis import JRStiffnessReductionParams


DOFS = ('axial', 'torsion', 'moment_y', 'moment_z')


def law_from_dict(dof, definition):
    if dof not in DOFS:
        raise ValueError(f'Unknown nonlinear component: {dof}')
    if isinstance(definition, (AxialForceTable, JRStiffnessReductionParams)):
        law = definition
    else:
        if not isinstance(definition, dict):
            raise ValueError(f'{dof} law must be an object')
        if 'axial_force_points' in definition:
            law = AxialForceTable.from_dict(definition)
        else:
            allowed = {'type', 'symmetric', 'beta', 'K_min'} | {
                f'{name}_{index}{suffix}'
                for name in ('delta', 'P') for index in (1, 2, 3, 4)
                for suffix in ('', '_neg')
            }
            unknown = set(definition)-allowed
            if unknown:
                raise ValueError(f'Unknown {dof} law fields: {sorted(unknown)}')
            if definition.get('type', 'jr_stiffness_reduction') != 'jr_stiffness_reduction':
                raise ValueError(f'Unknown nonlinear material type: {definition.get("type")}')
            symmetric = definition.get('symmetric', True)
            if not isinstance(symmetric, bool):
                raise ValueError('symmetric must be boolean')
            try:
                positive = {
                    f'{name}_{index}_pos': definition[f'{name}_{index}']
                    for name in ('delta', 'P') for index in (1, 2, 3)
                }
            except KeyError as error:
                raise ValueError(f'Missing {dof} law field: {error.args[0]}') from error
            values = dict(positive)
            for name in ('delta', 'P'):
                fourth = definition.get(f'{name}_4')
                values[f'{name}_4_pos'] = fourth
            for name in ('delta', 'P'):
                for index in (1, 2, 3, 4):
                    source = f'{name}_{index}' if symmetric else f'{name}_{index}_neg'
                    fallback = definition.get(f'{name}_{index}')
                    values[f'{name}_{index}_neg'] = definition.get(source, fallback)
            values.update(beta=definition.get('beta', .4), K_min=definition.get('K_min'))
            law = JRStiffnessReductionParams(**values)
    if isinstance(law, AxialForceTable) and dof not in ('moment_y', 'moment_z'):
        raise ValueError('Nd tables apply only to moment_y or moment_z')
    return law


def laws_from_dict(definitions):
    if not isinstance(definitions, dict) or not definitions:
        raise ValueError('laws must be a nonempty object')
    return {dof: law_from_dict(dof, definition) for dof, definition in definitions.items()}


def law_to_dict(law):
    if isinstance(law, AxialForceTable):
        return law.to_dict()
    if not isinstance(law, JRStiffnessReductionParams):
        raise TypeError('Unsupported nonlinear component law')
    symmetric = all(
        getattr(law, f'{name}_{index}_pos') == getattr(law, f'{name}_{index}_neg')
        for name in ('delta', 'P') for index in (1, 2, 3, 4)
    )
    result = dict(type='jr_stiffness_reduction', symmetric=symmetric,
                  beta=law.beta, K_min=law.K_min)
    for suffix, output_suffix in [('pos', ''), *([] if symmetric else [('neg', '_neg')])]:
        for name in ('delta', 'P'):
            for index in (1, 2, 3, 4):
                value = getattr(law, f'{name}_{index}_{suffix}')
                if value is not None:
                    result[f'{name}_{index}{output_suffix}'] = value
    return result
