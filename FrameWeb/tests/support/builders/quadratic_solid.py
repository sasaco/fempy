"""Polynomial patch, work, rigid modes and public quadratic-solid paths."""

from fem.model import FemModel

TET = [
    [0, 0, 0],
    [1, 0, 0],
    [0, 1, 0],
    [0, 0, 1],
    [0.5, 0, 0],
    [0.5, 0.5, 0],
    [0, 0.5, 0],
    [0, 0, 0.5],
    [0.5, 0, 0.5],
    [0, 0.5, 0.5],
]

WEDGE = [
    [0, 0, -1],
    [1, 0, -1],
    [0, 1, -1],
    [0, 0, 1],
    [1, 0, 1],
    [0, 1, 1],
    [0.5, 0, -1],
    [0.5, 0.5, -1],
    [0, 0.5, -1],
    [0.5, 0, 1],
    [0.5, 0.5, 1],
    [0, 0.5, 1],
    [0, 0, 0],
    [1, 0, 0],
    [0, 1, 0],
]

HEX = [
    [-1, -1, -1],
    [1, -1, -1],
    [1, 1, -1],
    [-1, 1, -1],
    [-1, -1, 1],
    [1, -1, 1],
    [1, 1, 1],
    [-1, 1, 1],
    [0, -1, -1],
    [1, 0, -1],
    [0, 1, -1],
    [-1, 0, -1],
    [0, -1, 1],
    [1, 0, 1],
    [0, 1, 1],
    [-1, 0, 1],
    [-1, -1, 0],
    [1, -1, 0],
    [1, 1, 0],
    [-1, 1, 0],
]

CASES = [("tetra2", TET, 1 / 6), ("wedge2", WEDGE, 1.0), ("hexa2", HEX, 8.0)]


def make_model(kind, coords):
    m = FemModel()
    for i, xyz in enumerate(coords):
        m.add_node(10 + i * 3, *xyz)
    m.add_material(1, "test", 1000, 0.25, density=2)
    m.add_element(8, kind, list(m.mesh.nodes), 1)
    m._create_elements()
    return m, m.elements[8]
