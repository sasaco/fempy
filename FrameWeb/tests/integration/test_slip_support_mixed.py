import numpy as np
import pytest

from fem.file_io import _read_json_model
from fem.model import FemModel
from tests.support.builders.slip_support import slip_model_data, slip_definition
from tests.support.builders.input_routes import python_axial

pytestmark = pytest.mark.integration


@pytest.mark.parametrize('kind, kb', [('tetra', 200.), ('shell', 1000/.9375*.1)])
@pytest.mark.parametrize('with_beam', [False, True])
def test_linear_shell_solid_and_beam_mix(kind, kb, with_beam):
    # Right unit tetra: V=1/6, Dxxxx=1200, dN2/dx=1 => kxx=200.
    # Unit right plane-stress triangle: t*A=.1, Dxxxx=1000/(1-.25^2).
    nodes = {'10': [0, 0, 0], '30': [1, 0, 0], '50': [0, 1, 0]}
    if kind == 'tetra':
        nodes['70'] = [0, 0, 1]
    elements = {'5': dict(type=kind, nodes=[int(n) for n in nodes], material_id=1, thickness=.2)}
    stride = 6 if kind == 'shell' or with_beam else 3
    restraints = {n: {'dof': [True]*stride} for n in nodes}
    restraints['30']['dof'][0] = False
    raw = dict(nodes=nodes, elements=elements,
               materials={'1': dict(name='reference', E=1000, nu=.25)},
               boundary_conditions=dict(restraints=restraints, loads={'30': [1, 0, 0, 0, 0, 0]},
                   nonlinear_spring_supports={'30': {'x': slip_definition()}}),
               analysis_params={'displacement_control': {'node': 30, 'dof': 'dx', 'targets': [.03, .024, .01]}})
    if with_beam:
        raw['elements']['6'] = dict(type='bar', nodes=[10, 30], material_id=1, section_id=1)
        raw['bar_parameters'] = {'1': dict(area=3, Iy=1, Iz=1, J=1)}
        kb += 3000.
    model = FemModel()
    model.read_json_model(_read_json_model(raw))
    result = model.run()
    assert model.solver.layout.stride == stride
    assert [s['lambda'] for s in result['step_results']] == pytest.approx(kb*np.array([.03, .024, .01])+[12, 6, 0])
    assert result['reaction_forces'][30]['fx'] == pytest.approx(0., abs=1e-9)


def test_three_dof_model_rejects_rotational_support():
    raw = slip_model_data()
    raw['nodes'].update({'50': [0, 1, 0], '70': [0, 0, 1]})
    raw['elements'] = {'5': dict(type='tetra', nodes=[10, 30, 50, 70], material_id=1)}
    raw['boundary_conditions']['nonlinear_spring_supports'] = {'30': {'rz': slip_definition()}}
    with pytest.raises(ValueError, match='30.*rz.*3 DOFs'):
        _read_json_model(raw)


def test_jr_axial_beam_and_slip_support_use_independent_histories():
    model = python_axial(force=1)
    model.add_slip_spring_support(30, 'x', 1000, 100, .001)
    model.analysis_params['displacement_control'] = dict(node=30, dof='dx', targets=[.001, .004, .012])
    result = model.run()
    # JR N(.0005,.002,.006) = 5,12,18. Slip F(.001,.004,.012) = 1,1.3,2.1.
    assert [s['lambda'] for s in result['step_results']] == pytest.approx([6, 13.3, 20.1])
    assert result['element_stresses'][7]['j_end'][0] == pytest.approx(18)
    assert result['support_response'][30]['x']['force'] == pytest.approx(2.1)
