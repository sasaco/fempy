"""Nonlinear acceptance: public input routes and independent axial solution."""

import copy

from fem.file_io import _read_json_model
from fem.material import BarParameter
from fem.model import FemModel


def axial_json(force=12, **controls):
    # L=2; N(e): (0,0),(.001,10),(.004,16),(.010,22).
    # N=12 gives e=.002, u=.004, R=-12 independently.
    return {
        "node": {"10": {"x": 0, "y": 0, "z": 0}, "30": {"x": 2, "y": 0, "z": 0}},
        "member": {"7": {"ni": 10, "nj": 30, "e": 1, "cg": 0}},
        "element": {
            "1": {
                "1": {
                    "E": 10000,
                    "nu": 0.25,
                    "A": 1,
                    "Iy": 1,
                    "Iz": 1,
                    "J": 1,
                    "nonlinear": {
                        "type": "jr_stiffness_reduction",
                        "delta_1": 0.001,
                        "delta_2": 0.004,
                        "delta_3": 0.010,
                        "P_1": 10,
                        "P_2": 16,
                        "P_3": 22,
                        "beta": 0,
                        "hysteresis_dofs": ["axial"],
                    },
                }
            }
        },
        "fix_node": {"1": [dict(n=10, tx=1, ty=1, tz=1, rx=1, ry=1, rz=1)]},
        "load": {
            "1": dict(
                load_node=[dict(n=30, tx=force)],
                n_load_steps=4,
                max_iterations=50,
                tolerance=1e-10,
                **controls,
            )
        },
    }


def python_axial(force=12, *, delta_4=None, P_4=None):
    m = FemModel()
    m.add_node(10, 0, 0, 0)
    m.add_node(30, 2, 0, 0)
    m.add_nonlinear_material(
        1, "reference", 10000, 0.001, 0.004, 0.010, 10, 16, 22,
        beta=0, nu=0.25, delta_4=delta_4, P_4=P_4,
    )
    m.material.add_bar_parameter(1, BarParameter(1, 1, 1, 1))
    m.add_nonlinear_bar_element(7, [10, 30], 1, 1, ["axial"])
    m.add_restraint(10, True, True, True, True, True, True)
    m.add_load(30, fx=force)
    m.analysis_params.update(n_load_steps=4, max_iterations=50, tolerance=1e-10)
    return m


def json_model(data):
    m = FemModel()
    m.read_json_model(_read_json_model(copy.deepcopy(data)))
    return m
