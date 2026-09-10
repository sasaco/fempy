"""Topology-defined nonconvex boundaries and holes never become convex hulls."""
from dataclasses import replace

import numpy as np
import pytest

from fem.spatial_loads import SpatialLoad, SpatialLoadDefinitions, SpatialLoadPath, compile_spatial_loads
from fem.spatial_loads.geometry import signed_area, triangulate_simple
from fem.spatial_loads.validation import prepare_panel
from tests.support.builders.spatial_loads import geometry_model

pytestmark = pytest.mark.unit


def ring_model(*, reverse=False, shell=False):
    points = [(0, 0), (4, 0), (4, 3), (0, 3), (1, 1), (2, 1), (2, 2), (1, 2)]
    quads = [(1, 2, 6, 5), (2, 3, 7, 6), (3, 4, 8, 7), (4, 1, 5, 8)]
    cells = quads if shell else [t for a, b, c, d in quads for t in [(a, b, c), (a, c, d)]]
    mesh, panel = geometry_model(points, cells, shell=shell, reverse=reverse)
    return mesh, replace(panel, holes=((5, 6, 7, 8) if reverse else (5, 8, 7, 6),))


def strip(panel, first, second=None, values=(1, 1)):
    paths = tuple(SpatialLoadPath(i, [(*p, 0) for p in points])
                  for i, points in enumerate((first,) if second is None else (first, second), 1))
    return SpatialLoadDefinitions((panel,), paths, (SpatialLoad(9, 7, tuple(p.id for p in paths),
                                                              (values,)*len(paths)),))


@pytest.mark.parametrize('reverse', [False, True])
@pytest.mark.parametrize('shell', [False, True])
@pytest.mark.parametrize('partial', [False, True])
def test_area_subtracts_declared_hole_with_exact_force_and_first_moment(reverse, shell, partial):
    mesh, panel = ring_model(reverse=reverse, shell=shell)
    # Whole rectangle minus an off-centre 1x1 hole; partial clips half the hole.
    end = 1.5 if partial else 4
    definitions = strip(panel, [(0, 0), (end, 0)], [(0, 3), (end, 3)])
    result = compile_spatial_loads(definitions, mesh)
    hole_area, hole_x = (.5, 1.25) if partial else (1., 1.5)
    area = 3*end-hole_area
    np.testing.assert_allclose(result.resultant, [0, 0, area], atol=1e-11)
    np.testing.assert_allclose(result.moment, [1.5*area, -(3*end*end/2-hole_area*hole_x), 0], atol=1e-11)
    assert result.loads[0].clipped_area == pytest.approx(area)
    assert result.force_error < 1e-11 and result.moment_error < 1e-11


@pytest.mark.parametrize('first,second', [([(1.1, 1.1), (1.9, 1.1)], [(1.1, 1.9), (1.9, 1.9)]),
    ([(0, .5), (4, .5)], [(0, 3.1), (4, 3.1)])])
def test_empty_or_outside_area_is_rejected_with_load_and_panel_ids(first, second):
    mesh, panel = ring_model()
    with pytest.raises(ValueError, match='load 9 panel 7'):
        compile_spatial_loads(strip(panel, first, second), mesh)


def test_line_cannot_cross_a_hole_but_can_follow_its_boundary():
    mesh, panel = ring_model()
    with pytest.raises(ValueError, match='load 9 panel 7.*extrapolation'):
        compile_spatial_loads(strip(panel, [(0, 1.5), (4, 1.5)]), mesh)
    result = compile_spatial_loads(strip(panel, [(0, 1), (4, 1)]), mesh)
    np.testing.assert_allclose(result.resultant, [0, 0, 4], atol=1e-12)
    np.testing.assert_allclose(result.moment, [4, -8, 0], atol=1e-12)


@pytest.mark.parametrize('holes', [(), ((5, 6, 7, 8),), ((1, 2, 3, 4),), ((5, 8, 6, 7),)])
def test_holes_must_match_actual_topology_and_opposite_winding(holes):
    mesh, panel = ring_model()
    with pytest.raises(ValueError, match='panel 7.*hole'):
        prepare_panel(replace(panel, holes=holes), mesh)


def test_nonconvex_panel_allows_bent_line_but_rejects_shortcut_across_notch():
    points = [(0, 0), (1, 0), (2, 0), (2, 1), (1, 1), (1, 2), (0, 2), (0, 1)]
    cells = [(1, 2, 5), (1, 5, 8), (2, 3, 4), (2, 4, 5), (8, 5, 6), (8, 6, 7)]
    mesh, panel = geometry_model(points, cells)
    result = compile_spatial_loads(strip(panel, [(1.5, .5), (.5, .5), (.5, 1.5)]), mesh)
    np.testing.assert_allclose(result.resultant, [0, 0, 2], atol=1e-12)
    np.testing.assert_allclose(result.moment, [1.5, -1.5, 0], atol=1e-12)
    with pytest.raises(ValueError, match='load 9 panel 7.*extrapolation'):
        compile_spatial_loads(strip(panel, [(2, .75), (.75, 2)]), mesh)
    with pytest.raises(ValueError, match='load 9 panel 7'):
        compile_spatial_loads(strip(panel, [(0, 0), (2, 0)], [(0, 2), (2, 2)]), mesh)


def test_nonconvex_strip_preserves_its_bends_and_moments():
    mesh, panel = geometry_model()
    result = compile_spatial_loads(strip(panel, [(0, 0), (1, .5), (2, 0)],
                                         [(0, 1), (1, 1.5), (2, 1)]), mesh)
    np.testing.assert_allclose(result.resultant, [0, 0, 2], atol=1e-12)
    np.testing.assert_allclose(result.moment, [1.5, -2, 0], atol=1e-12)


@pytest.mark.parametrize('reverse', [False, True])
def test_nonconvex_ring_triangulation_preserves_exact_area_and_first_moments(reverse):
    polygon = [(0, 0), (1, 0), (2, 0), (2, 1), (1, 1), (1, 2), (0, 2)]
    if reverse:
        polygon.reverse()
    triangles = triangulate_simple(polygon, 1e-9)
    assert sum(abs(signed_area(t)) for t in triangles) == pytest.approx(3.)
    moment = sum(abs(signed_area(t)) * np.mean(t, axis=0) for t in triangles)
    np.testing.assert_allclose(moment, [2.5, 2.5], atol=1e-12)


@pytest.mark.parametrize('concave_hole', [False, True])
def test_multiple_or_nonconvex_holes_have_conservative_linear_area_loading(concave_hole):
    # A connected unit grid with either two holes or a single L-shaped hole.
    excluded = {(1, 1), (2, 1), (1, 2)} if concave_hole else {(1, 1), (3, 1)}
    nx, ny = 5, 4
    coords = [(x, y) for y in range(ny+1) for x in range(nx+1)]
    def node(x, y):
        return y*(nx+1) + x+1
    cells = []
    for y in range(ny):
        for x in range(nx):
            if (x, y) not in excluded:
                a, b, c, d = node(x, y), node(x+1, y), node(x+1, y+1), node(x, y+1)
                cells.extend(((a, b, c), (a, c, d)))
    mesh, panel = geometry_model(coords, cells)
    rings = [[(1, 1), (1, 2), (1, 3), (2, 3), (2, 2), (3, 2), (3, 1), (2, 1)]] if concave_hole else [
        [(x, 1), (x, 2), (x+1, 2), (x+1, 1)] for x in (1, 3)]
    panel = replace(panel, holes=tuple(tuple(node(x, y) for x, y in ring) for ring in rings))
    result = compile_spatial_loads(strip(panel, [(0, 0), (nx, 0)], [(0, ny), (nx, ny)], values=(0, nx)), mesh)
    # p=x. Integrate the full rectangle and subtract each unit square analytically.
    total = ny*nx**2/2 - sum(x+.5 for x, y in excluded)
    mx = ny**2*nx**2/4 - sum((x+.5)*(y+.5) for x, y in excluded)
    my = -(ny*nx**3/3 - sum(x*x+x+1/3 for x, y in excluded))
    np.testing.assert_allclose(result.resultant, [0, 0, total], atol=2e-11)
    np.testing.assert_allclose(result.moment, [mx, my, 0], atol=2e-10)
