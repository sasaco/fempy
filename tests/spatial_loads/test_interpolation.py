"""Arc-length correspondence and load-field invariants on ruled strips."""
import numpy as np
import pytest

from fem.spatial_loads import SpatialLoad, SpatialLoadDefinitions, SpatialLoadPath
from fem.spatial_loads.geometry import signed_area
from fem.spatial_loads.interpolation import ProjectedPath, build_strip
from fem.spatial_loads.validation import prepare_geometry
from tests.support.builders.spatial_loads import geometry_model

pytestmark = pytest.mark.unit


@pytest.mark.parametrize('reverse_first,reverse_second', [(False, False), (False, True), (True, False), (True, True)])
def test_both_path_orientations_preserve_the_same_physical_field(reverse_first, reverse_second):
    mesh, panel = geometry_model()
    paths = [SpatialLoadPath(1, [(0, 0, 0), (2, 0, 0)]),
             SpatialLoadPath(2, [(0, 2, 0), (2, 2, 0)])]
    values = [(1, 5), (7, 19)]
    for i, reverse in enumerate([reverse_first, reverse_second]):
        if reverse:
            paths[i] = SpatialLoadPath(paths[i].id, paths[i].points[::-1])
            values[i] = values[i][::-1]
    load = SpatialLoad(9, 7, (1, 2), values)
    prepared, = prepare_geometry(SpatialLoadDefinitions((panel,), paths, (load,)), mesh)
    strip, = prepared.strip_cells
    for x, y in [(0, 0), (2, 2), (1.1, .7), (.1, 1.9)]:
        assert strip.intensity((x, y), 1e-10) == pytest.approx(1+2*x+3*y+2*x*y)
    assert sum(abs(signed_area(p.triangle)) for p in prepared.area_pieces) == pytest.approx(4)


def test_different_path_knots_are_merged_in_normalized_arc_length():
    mesh, panel = geometry_model()
    paths = (SpatialLoadPath(1, [(0, 0, 0), (.5, 0, 0), (2, 0, 0)]),
             SpatialLoadPath(2, [(0, 2, 0), (1.5, 2, 0), (2, 2, 0)]))
    load = SpatialLoad(9, 7, (1, 2), ((1, 5), (7, 19)))
    prepared, = prepare_geometry(SpatialLoadDefinitions((panel,), paths, (load,)), mesh)
    assert [(s.s0, s.s1) for s in prepared.strip_cells] == [(0, .25), (.25, .75), (.75, 1)]
    for strip in prepared.strip_cells:
        x = strip.s0 + strip.s1
        assert strip.intensity((x, 1), 1e-10) == pytest.approx(4+4*x)
    assert sum(abs(signed_area(p.triangle)) for p in prepared.area_pieces) == pytest.approx(4)


def test_polyline_correspondence_uses_length_not_vertex_index():
    # Convex strip with different segment lengths on its two sides.
    mesh, panel = geometry_model(points=[(0, 0), (4, 0), (4, 3), (0, 3)])
    paths = (SpatialLoadPath(1, [(0, 1, 0), (1, 0, 0), (4, 0, 0)]),
             SpatialLoadPath(2, [(0, 3, 0), (4, 3, 0)]))
    load = SpatialLoad(9, 7, (1, 2), ((0, 10), (20, 30)))
    prepared, = prepare_geometry(SpatialLoadDefinitions((panel,), paths, (load,)), mesh)
    knot = np.sqrt(2)/(np.sqrt(2)+3)
    assert prepared.strip_cells[0].s1 == pytest.approx(knot)
    np.testing.assert_allclose(prepared.strip_cells[0].points[2], [4*knot, 3])
    for strip in prepared.strip_cells:
        center = np.mean(strip.points, axis=0)
        assert strip.intensity(center, 1e-10) == pytest.approx(10+5*(strip.s0+strip.s1))
    assert sum(abs(signed_area(p.triangle)) for p in prepared.area_pieces) == pytest.approx(11.5)


def test_load_field_is_independent_of_loading_mesh_diagonal():
    values = []
    for triangles in [[(1, 2, 3), (1, 3, 4)], [(1, 2, 4), (2, 3, 4)]]:
        mesh, panel = geometry_model(cells=triangles)
        paths = (SpatialLoadPath(1, [(0, 0, 0), (2, 0, 0)]),
                 SpatialLoadPath(2, [(0, 2, 0), (2, 2, 0)]))
        load = SpatialLoad(9, 7, (1, 2), ((0, 0), (0, 4)))
        prepared, = prepare_geometry(SpatialLoadDefinitions((panel,), paths, (load,)), mesh)
        values.append(prepared.strip_cells[0].intensity((.4, .7), 1e-10))
    np.testing.assert_allclose(values, [.28, .28])


def test_field_rejects_points_outside_its_cell():
    first = ProjectedPath(1, ((0, 0), (1, 0)), (0., 1.))
    second = ProjectedPath(2, ((0, 1), (1, 1)), (0., 1.))
    strip, = build_strip(first, second, ((1, 1), (1, 1)), 1e-10)
    with pytest.raises(ValueError, match='outside'):
        strip.intensity((1.1, .5), 1e-10)
