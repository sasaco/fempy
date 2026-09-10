"""Input contracts that must hold before any geometry compiler or solver runs."""
from dataclasses import FrozenInstanceError

import numpy as np
import pytest

from fem.spatial_loads import (
    GeometryTolerance, SpatialLoad, SpatialLoadDefinitions, SpatialLoadPanel,
    SpatialLoadPath,
)

pytestmark = pytest.mark.unit


def test_definitions_copy_nested_sequences_and_are_immutable():
    nodes = [1, 2, 3]
    triangles = [[1, 2, 3]]
    panel = SpatialLoadPanel("7", nodes, triangles=triangles)
    points = [[0, 0, 0], [3, 0, 0], [3, 4, 0]]
    path = SpatialLoadPath("2", points)
    intensities = [[10, 20]]
    load = SpatialLoad("1", "7", ["2"], intensities)
    definitions = SpatialLoadDefinitions([panel], [path], [load])
    nodes[0] = 99
    triangles[0][0] = 99
    points[1][0] = 100
    intensities[0][0] = 999
    assert definitions.panels[0].nodes == (1, 2, 3)
    assert panel.triangles == ((1, 2, 3),)
    assert path.cumulative_lengths == (0., 3., 7.)
    assert load.end_intensities == ((10., 20.),)
    with pytest.raises(FrozenInstanceError):
        panel.id = 9


@pytest.mark.parametrize("bad", [True, 1.5, None, "1.5", "", float("nan")])
def test_non_integer_ids_are_not_silently_truncated(bad):
    with pytest.raises(ValueError, match="ID"):
        SpatialLoadPath(bad, [(0, 0, 0), (1, 0, 0)])


@pytest.mark.parametrize("points", [
    [], [(0, 0, 0)], [(0, 0, 0), (0, 0, 0)],
    [(0, 0, 0), (1, 0, 0), (0, 0, 0)],
    [(0, 0), (1, 0)], [(0, 0, 0), (np.inf, 0, 0)],
])
def test_malformed_paths_are_rejected(points):
    with pytest.raises(ValueError, match="path 5"):
        SpatialLoadPath(5, points)


@pytest.mark.parametrize("kwargs", [
    {}, dict(elements=[1], triangles=[[1, 2, 3]]),
    dict(triangles=[[1, 1, 2]]), dict(triangles=[[1, 2, 4]]),
    dict(triangles=[[1, 2, 3], [3, 2, 1]]), dict(elements=[1, 1]),
    dict(triangles=[[1, 2, 3]], holes=[[1, 2, 3]]),
])
def test_invalid_topology_contracts_are_rejected(kwargs):
    with pytest.raises(ValueError, match="panel 7"):
        SpatialLoadPanel(7, [1, 2, 3], **kwargs)


@pytest.mark.parametrize("paths,intensities", [
    ([], []), ([1, 2, 3], [[1, 2]] * 3), ([1, 1], [[1, 2]] * 2),
    ([1, 2], [[1, 2]]), ([1], [[1]]), ([1], [[1, np.nan]]),
])
def test_invalid_load_endpoints_are_rejected(paths, intensities):
    with pytest.raises(ValueError, match="load 3"):
        SpatialLoad(3, 7, paths, intensities)


def test_reference_and_normalized_duplicate_checks():
    panel = SpatialLoadPanel(7, [1, 2, 3], triangles=[[1, 2, 3]])
    path = SpatialLoadPath(1, [(0, 0, 0), (1, 0, 0)])
    with pytest.raises(ValueError, match="duplicate.*path.*1"):
        SpatialLoadDefinitions([panel], [path, path], [])
    with pytest.raises(ValueError, match="load 3.*path 2"):
        SpatialLoadDefinitions([panel], [path], [SpatialLoad(3, 7, [2], [[1, 2]])])
    with pytest.raises(ValueError, match="load 3.*panel 8"):
        SpatialLoadDefinitions([panel], [path], [SpatialLoad(3, 8, [1], [[1, 2]])])


def test_tolerance_is_dimensioned_and_not_a_load_rounding_rule():
    tolerance = GeometryTolerance(absolute_length=1e-8, relative_length=1e-6)
    assert tolerance.length(1e-4) == 1e-8
    assert tolerance.length(1000) == pytest.approx(1e-3)
    for absolute, relative in [(0, 0), (-1, 1), (np.nan, 1), (1, np.inf)]:
        with pytest.raises(ValueError, match="tolerance"):
            GeometryTolerance(absolute, relative)


def test_unordered_path_points_cannot_define_an_arbitrary_path():
    with pytest.raises(ValueError, match='path 5.*sequence'):
        SpatialLoadPath(5, {(0, 0, 0), (1, 0, 0)})
