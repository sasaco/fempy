"""Reject invalid geometry with panel/load context, before assembly exists."""
from copy import deepcopy

import numpy as np
import pytest

from fem.spatial_loads import SpatialLoad, SpatialLoadDefinitions, SpatialLoadPath
from fem.spatial_loads.validation import prepare_geometry, prepare_panel
from tests.support.builders.spatial_loads import geometry_model

pytestmark = pytest.mark.unit


@pytest.mark.parametrize('points,cells,shell', [
    ([(0, 0), (1, 0), (2, 0)], [(1, 2, 3)], False),
    ([(0, 0), (2, 0), (2, 2), (0, 2)], [(1, 2, 3), (1, 4, 3)], False),
    ([(0, 0), (2, 2), (2, 0), (0, 2)], [(1, 2, 3, 4)], True),
    ([(0, 0), (2, 0), (.5, .5), (0, 2)], [(1, 2, 3, 4)], True),
    ([(0, 0), (1, 0), (0, 1), (3, 0), (4, 0), (3, 1)], [(1, 2, 3), (4, 5, 6)], False),
    ([(0, 0), (1, 0), (0, 1), (.2, .2)], [(1, 2, 3)], False),
    ([(0, 0), (1, 0), (0, 1), (0, 0)], [(1, 2, 3), (4, 2, 3)], False),
    ([(0, 0, 0), (2, 0, 0), (2, 2, .1), (0, 2, 0)], [(1, 2, 3, 4)], True),
    ([(0, 0, 0), (2, 0, 1), (2, 2, 1), (0, 2, 0)], [(1, 2, 3, 4)], True),
    ([(0, 0), (1, 0), (0, np.inf)], [(1, 2, 3)], False),
    # A hole must be declared explicitly, even when connectivity contains it.
    ([(0, 0), (3, 0), (3, 3), (0, 3), (1, 1), (2, 1), (2, 2), (1, 2)],
     [(1, 2, 6, 5), (2, 3, 7, 6), (3, 4, 8, 7), (4, 1, 5, 8)], True),
])
def test_invalid_panel_is_rejected(points, cells, shell):
    mesh, definition = geometry_model(points=points, cells=cells, shell=shell)
    with pytest.raises(ValueError, match='panel 7'):
        prepare_panel(definition, mesh)


@pytest.mark.parametrize('points', [
    [(0, 0, 0), (2, 0, 0), (1, 0, 0)],
    [(0, 0, 0), (2, 2, 0), (0, 2, 0), (2, 0, 0)],
    [(0, 1, 0), (2.1, 1, 0)],
    [(0, 1, 0), (2, 1, .01)],
    [(0, 1, 0), (1e-12, 1, 0)],
])
def test_invalid_line_is_rejected_with_load_panel_and_path_ids(points):
    mesh, panel = geometry_model()
    path = SpatialLoadPath(3, points)
    load = SpatialLoad(9, 7, (3,), ((0, 0),))
    with pytest.raises(ValueError, match='load 9 panel 7 path 3'):
        prepare_geometry(SpatialLoadDefinitions((panel,), (path,), (load,)), mesh)


@pytest.mark.parametrize('first,second', [
    ([(0, 1), (2, 1)], [(1, 0), (1, 2)]),  # ambiguous endpoint pairing
    ([(0, 0), (2, 0)], [(0, 0), (2, 1)]),  # zero-width end
    ([(0, .3), (1, 1.5), (2, .3)], [(0, 1), (1, .5), (2, 1)]),
])
def test_invalid_strip_is_rejected_even_for_zero_intensity(first, second):
    mesh, panel = geometry_model()
    paths = tuple(SpatialLoadPath(i, [(*p, 0) for p in points]) for i, points in enumerate((first, second), 1))
    load = SpatialLoad(9, 7, (1, 2), ((0, 0), (0, 0)))
    with pytest.raises(ValueError, match='load 9 panel 7'):
        prepare_geometry(SpatialLoadDefinitions((panel,), paths, (load,)), mesh)


def test_preparation_is_repeatable_and_does_not_mutate_inputs():
    mesh, panel = geometry_model()
    paths = (SpatialLoadPath(1, [(0, .5, 0), (2, .5, 0)]),
             SpatialLoadPath(2, [(2, 1.5, 0), (0, 1.5, 0)]))
    definitions = SpatialLoadDefinitions((panel,), paths, (SpatialLoad(9, 7, (1, 2), ((1, 2), (4, 3))),))
    original, elements, nodes = deepcopy(definitions), deepcopy(mesh.elements), deepcopy(mesh.nodes)
    assert prepare_geometry(definitions, mesh) == prepare_geometry(definitions, mesh)
    assert definitions == original and mesh.elements == elements
    for n in nodes:
        np.testing.assert_array_equal(mesh.nodes[n], nodes[n])
