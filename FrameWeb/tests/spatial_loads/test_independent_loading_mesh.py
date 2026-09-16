"""An independent loading mesh defines the domain; structural cells receive it."""
from dataclasses import replace

import numpy as np
import pytest

from fem.spatial_loads import (
    SpatialLoad, SpatialLoadDefinitions, SpatialLoadMeshNode, SpatialLoadPanel,
    SpatialLoadPath, compile_spatial_loads,
)
from fem.spatial_loads.serialization import from_json, to_json
from tests.support.builders.spatial_loads import geometry_model

pytestmark = pytest.mark.unit


def independent_panel(panel, *, outside=False):
    end = 2.25 if outside else 1.75
    records = tuple(SpatialLoadMeshNode(i+101, point) for i, point in enumerate((
        (.25, .25, 0), (end, .25, 0), (end, 1.75, 0), (.25, 1.75, 0))))
    return replace(panel, loading_nodes=records,
                   loading_triangles=((101, 102, 103), (101, 103, 104)))


@pytest.mark.parametrize('shell', [False, True])
@pytest.mark.parametrize('area', [False, True])
def test_independent_nodes_map_to_structural_dofs_and_preserve_force_moment(shell, area):
    mesh, panel = geometry_model(shell=shell)
    panel = independent_panel(panel)
    paths = (SpatialLoadPath(1, ((.25, 1, 0), (1.75, 1, 0))),)
    if area:
        paths = (SpatialLoadPath(1, ((.25, .25, 0), (1.75, .25, 0))),
                 SpatialLoadPath(2, ((.25, 1.75, 0), (1.75, 1.75, 0))))
    definitions = SpatialLoadDefinitions((panel,), paths, (SpatialLoad(9, 7,
        tuple(p.id for p in paths), ((2, 2),)*len(paths)),))
    result = compile_spatial_loads(definitions, mesh)
    measure = 2.25 if area else 1.5
    np.testing.assert_allclose(result.resultant, [0, 0, 2*measure], atol=1e-12)
    np.testing.assert_allclose(result.moment, [2*measure, -2*measure, 0], atol=1e-12)
    assert result.loads[0].clipped_area == pytest.approx(measure if area else 0)
    assert result.loads[0].integrated_length == pytest.approx(measure if not area else 0)
    assert result.force_error < 1e-11 and result.moment_error < 1e-11
    restored = from_json(to_json(definitions), mesh)
    assert restored == definitions
    assert {n.id for n in restored.panels[0].loading_nodes} == {101, 102, 103, 104}


def test_independent_domain_not_structural_convex_hull_controls_line_coverage():
    mesh, panel = geometry_model(shell=True)
    panel = independent_panel(panel)
    inside = SpatialLoadDefinitions((panel,), (SpatialLoadPath(1, ((.25, .5, 0), (1.75, .5, 0))),),
                                     (SpatialLoad(9, 7, (1,), ((1, 1),)),))
    assert compile_spatial_loads(inside, mesh).loads[0].integrated_length == pytest.approx(1.5)
    crossing = replace(inside, paths=(SpatialLoadPath(1, ((0, .5, 0), (2, .5, 0))),))
    with pytest.raises(ValueError, match='load 9 panel 7.*extrapolation'):
        compile_spatial_loads(crossing, mesh)


def test_independent_domain_outside_structural_cells_is_rejected():
    mesh, panel = geometry_model(shell=True)
    panel = independent_panel(panel, outside=True)
    paths = (SpatialLoadPath(1, ((.25, .25, 0), (2.25, .25, 0))),
             SpatialLoadPath(2, ((.25, 1.75, 0), (2.25, 1.75, 0))))
    definitions = SpatialLoadDefinitions((panel,), paths, (SpatialLoad(9, 7, (1, 2), ((1, 1), (1, 1))),))
    with pytest.raises(ValueError, match='panel 7.*outside structural'):
        compile_spatial_loads(definitions, mesh)


def test_independent_loading_mesh_can_define_and_subtract_its_own_hole():
    mesh, panel = geometry_model(points=[(0, 0), (4, 0), (4, 4), (0, 4)], shell=True)
    points = ((.25, .25, 0), (3.75, .25, 0), (3.75, 3.75, 0), (.25, 3.75, 0),
              (1, 1, 0), (2, 1, 0), (2, 2, 0), (1, 2, 0))
    nodes = tuple(SpatialLoadMeshNode(i+101, point) for i, point in enumerate(points))
    quads = ((101, 102, 106, 105), (102, 103, 107, 106),
             (103, 104, 108, 107), (104, 101, 105, 108))
    triangles = tuple(t for a, b, c, d in quads for t in ((a, b, c), (a, c, d)))
    panel = replace(panel, loading_nodes=nodes, loading_triangles=triangles,
                    holes=((105, 108, 107, 106),))
    definitions = SpatialLoadDefinitions((panel,), (
        SpatialLoadPath(1, ((.25, .25, 0), (3.75, .25, 0))),
        SpatialLoadPath(2, ((.25, 3.75, 0), (3.75, 3.75, 0)))),
        (SpatialLoad(9, 7, (1, 2), ((1, 1), (1, 1))),))
    result = compile_spatial_loads(definitions, mesh)
    np.testing.assert_allclose(result.resultant, [0, 0, 11.25], atol=1e-11)
    np.testing.assert_allclose(result.moment, [23, -23, 0], atol=1e-11)
    assert result.loads[0].clipped_area == pytest.approx(11.25)
    assert from_json(to_json(definitions), mesh) == definitions


def test_independent_domain_maps_into_structural_target_with_its_own_hole():
    points = [(0, 0), (3, 0), (3, 3), (0, 3), (1, 1), (2, 1), (2, 2), (1, 2)]
    quads = [(1, 2, 6, 5), (2, 3, 7, 6), (3, 4, 8, 7), (4, 1, 5, 8)]
    cells = [t for a, b, c, d in quads for t in ((a, b, c), (a, c, d))]
    mesh, panel = geometry_model(points, cells)
    nodes = tuple(SpatialLoadMeshNode(i+101, point) for i, point in enumerate(
        ((.25, .25, 0), (2.75, .25, 0), (2.75, .75, 0), (.25, .75, 0))))
    panel = replace(panel, loading_nodes=nodes,
                    loading_triangles=((101, 102, 103), (101, 103, 104)))
    definitions = SpatialLoadDefinitions((panel,), (
        SpatialLoadPath(1, ((.25, .25, 0), (2.75, .25, 0))),
        SpatialLoadPath(2, ((.25, .75, 0), (2.75, .75, 0)))),
        (SpatialLoad(9, 7, (1, 2), ((1, 1), (1, 1))),))
    result = compile_spatial_loads(definitions, mesh)
    np.testing.assert_allclose(result.resultant, [0, 0, 1.25], atol=1e-12)
    np.testing.assert_allclose(result.moment, [.625, -1.875, 0], atol=1e-12)


@pytest.mark.parametrize('nodes,triangles', [
    ((), ((101, 102, 103),)),
    ((SpatialLoadMeshNode(101, (0, 0, 0)),), ()),
    ((SpatialLoadMeshNode(101, (0, 0, 0)), SpatialLoadMeshNode(101, (1, 0, 0)),
      SpatialLoadMeshNode(103, (0, 1, 0))), ((101, 102, 103),)),
    ((SpatialLoadMeshNode(101, (0, 0, 0)), SpatialLoadMeshNode(102, (1, 0, 0)),
      SpatialLoadMeshNode(103, (0, 1, 0))), ((101, 102, 999),)),
    ((SpatialLoadMeshNode(101, (0, 0, 0)), SpatialLoadMeshNode(102, (1, 0, 0)),
      SpatialLoadMeshNode(103, (0, 1, 0))), ((101, 102, 103), (103, 102, 101))),
])
def test_independent_loading_mesh_contract_rejects_incomplete_or_bad_references(nodes, triangles):
    _, panel = geometry_model()
    with pytest.raises(ValueError, match='panel 7.*loading'):
        replace(panel, loading_nodes=nodes, loading_triangles=triangles)
