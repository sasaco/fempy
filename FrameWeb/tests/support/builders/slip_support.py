"""Small public models with an independently known parallel beam stiffness."""
from copy import deepcopy


def slip_definition():
    return dict(type='slip', K1=1000., K2=100., delta_1=.01)


def slip_model_data(direction='x'):
    names = ['x', 'y', 'z', 'rx', 'ry', 'rz']
    free = names.index(direction)
    fixed = [i != free for i in range(6)]
    load = [1. if i == free else 0. for i in range(6)]
    # L=2, EA/L=3000, 12EI/L^3=3000, GJ/L=400, 4EI/L=4000.
    return deepcopy({
        'nodes': {'10': [0., 0., 0.], '30': [2., 0., 0.]},
        'elements': {'5': {'type': 'bar', 'nodes': [10, 30], 'material_id': 1,
                           'section_id': 1, 'shear_correction': False}},
        'materials': {'1': {'name': 'reference', 'E': 2000., 'nu': .25}},
        'bar_parameters': {'1': {'area': 3., 'Iy': 1., 'Iz': 1., 'J': 1.}},
        'boundary_conditions': {
            'restraints': {'10': {'dof': [True]*6}, '30': {'dof': fixed}},
            'loads': {'30': load},
            'nonlinear_spring_supports': {'30': {direction: slip_definition()}},
        },
        'analysis_params': {'displacement_control': {
            'node': 30, 'dof': 'd'+direction if len(direction) == 1 else direction,
            'targets': [.005, .03, .024, .018, .01, .025, 0., -.005, -.03, -.024, -.018, .01, .024, .04],
        }},
    })


def legacy_slip_data():
    return {
        'node': {'10': {'x': 0, 'y': 0, 'z': 0}, '30': {'x': 2, 'y': 0, 'z': 0}},
        'member': {'5': {'ni': 10, 'nj': 30, 'e': 1}},
        'element': {'1': {'1': {'E': 2000., 'A': 3., 'Iy': 1., 'Iz': 1., 'J': 1., 'nu': .25}}},
        'fix_node': {'1': [dict(n=10, tx=1, ty=1, tz=1, rx=1, ry=1, rz=1),
                           dict(n=30, tx=slip_definition(), ty=1, tz=1, rx=1, ry=1, rz=1)]},
        'load': {'1': {'fix_node': 1, 'load_node': [dict(n=30, tx=1.)]}},
        'analysis_params': slip_model_data()['analysis_params'],
    }
