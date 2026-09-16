"""Independent statics for the legacy input and shared linear beam path."""

import copy

from fem.file_io import _read_json_model
from fem.model import FemModel


def cantilever():
    return dict(
        node={"1": dict(x=0, y=0, z=0), "2": dict(x=2, y=0, z=0)},
        member={"1": dict(ni=1, nj=2, e=2, cg=0)},
        element={
            "1": {"1": dict(E=1000, G=1, A=9, Iy=9, Iz=9, J=9), "2": dict(E=1000, G=1, A=2, Iy=3, Iz=4, J=5)}
        },
        fix_node={"1": [dict(n=1, tx=1, ty=1, tz=1, rx=1, ry=1, rz=1)]},
        load={"1": dict(load_node=[dict(n=2, ty=3)])},
    )


def run(data):
    m = FemModel()
    m.read_json_model(_read_json_model(copy.deepcopy(data)))
    return m, m.run()
