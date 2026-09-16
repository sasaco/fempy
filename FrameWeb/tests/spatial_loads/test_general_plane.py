"""General planar loads use physical lengths, oriented normals and 3D moments."""
from dataclasses import replace

import numpy as np
import pytest

from fem.spatial_loads import (
    LoadDirection, LocalPlane, SpatialLoad, SpatialLoadDefinitions, SpatialLoadPath,
    compile_spatial_loads,
)
from fem.spatial_loads.serialization import from_json, to_json
from fem.spatial_loads.validation import prepare_panel
from tests.support.builders.spatial_loads import geometry_model, square_definitions

pytestmark = pytest.mark.unit


@pytest.mark.parametrize('shell', [False, True])
@pytest.mark.parametrize('area', [False, True])
@pytest.mark.parametrize('normal', [False, True])
@pytest.mark.parametrize('reverse', [False, True])
def test_inclined_loads_rotate_nodal_forces_and_translate_moments(shell, area, normal, reverse):
    mesh, panel = geometry_model(shell=shell, reverse=reverse)
    definitions = square_definitions(panel, area=area, coefficients=(2, 1, 3, 0))
    original = compile_spatial_loads(definitions, mesh)
    # Orthogonal proper rotation, including a vertical component in both axes.
    rotation = np.array([[1, 2, 2], [2, 1, -2], [-2, 2, -1]], dtype=float) / 3
    offset = np.array([5., -7., 11.])
    for node, point in mesh.nodes.items():
        mesh.nodes[node] = rotation @ point + offset
    panel = replace(panel, plane=LocalPlane(offset, rotation[:, 0], rotation[:, 1]))
    paths = tuple(replace(p, points=tuple(rotation @ point + offset for point in p.points))
                  for p in definitions.paths)
    direction = LoadDirection('normal') if normal else LoadDirection(vector=7*rotation[:, 2])
    load = replace(definitions.loads[0], direction=direction)
    result = compile_spatial_loads(SpatialLoadDefinitions((panel,), paths, (load,)), mesh)
    sign = -1 if normal and reverse else 1
    force = sign * rotation @ original.resultant
    moment = sign * rotation @ original.moment + np.cross(offset, force)
    np.testing.assert_allclose(result.resultant, force, atol=1e-11)
    np.testing.assert_allclose(result.moment, moment, atol=1e-10)
    for old, new in zip(original.cells, result.cells):
        np.testing.assert_allclose(new.nodal_forces, sign*np.array(old.nodal_forces) @ rotation.T, atol=1e-11)
    assert result.force_scale == pytest.approx(original.force_scale)
    assert result.loads[0].integrated_length == pytest.approx(original.loads[0].integrated_length)
    assert result.loads[0].clipped_area == pytest.approx(original.loads[0].clipped_area)
    assert result.force_error < 1e-10 and result.moment_error < 1e-10
    assert from_json(to_json(SpatialLoadDefinitions((panel,), paths, (load,))), mesh) == SpatialLoadDefinitions((panel,), paths, (load,))


@pytest.mark.parametrize('vector', [(3, 4, 0), (1e300, -1e300, 1e300), (1e-300, 0, 0)])
def test_direction_is_normalized_without_overflow_or_unit_change(vector):
    direction = LoadDirection(vector=vector)
    assert np.linalg.norm(direction.vector) == pytest.approx(1.)


@pytest.mark.parametrize('kwargs', [dict(vector=(0, 0, 0)), dict(vector=(1, 2)),
    dict(vector=(np.nan, 0, 1)), dict(vector=(True, 0, 1)), dict(mode='local'),
    dict(mode='normal', vector=(0, 0, 1))])
def test_invalid_load_directions_are_rejected(kwargs):
    with pytest.raises(ValueError, match='direction'):
        LoadDirection(**kwargs)


@pytest.mark.parametrize('origin,u,v', [((0, 0), (1, 0, 0), (0, 1, 0)),
    ((0, 0, np.inf), (1, 0, 0), (0, 1, 0)), ((0, 0, 0), (0, 0, 0), (0, 1, 0)),
    ((0, 0, 0), (1, 0, 0), (1, 1, 0)), ((0, 0, 0), (1, 0, 0), (1, 0, 0))])
def test_invalid_explicit_planes_are_rejected(origin, u, v):
    with pytest.raises(ValueError, match='plane'):
        LocalPlane(origin, u, v)


def test_plane_axes_scale_and_origin_do_not_change_physical_integrals():
    mesh, panel = geometry_model()
    expected = compile_spatial_loads(square_definitions(panel), mesh)
    changed = replace(panel, plane=LocalPlane((100, -200, 0), (0, 5, 0), (-3, 0, 0)))
    actual = compile_spatial_loads(square_definitions(changed), mesh)
    np.testing.assert_allclose(actual.dof_loads, expected.dof_loads, atol=1e-12)
    np.testing.assert_allclose(actual.moment, expected.moment, atol=1e-11)


def test_mismatched_explicit_plane_rejects_without_projection():
    mesh, panel = geometry_model()
    panel = replace(panel, plane=LocalPlane((0, 0, .1), (1, 0, 0), (0, 1, 0)))
    with pytest.raises(ValueError, match='panel 7.*plane'):
        prepare_panel(panel, mesh)


def test_zero_resultant_still_has_nonzero_force_scale_and_moment():
    mesh, panel = geometry_model()
    direction = LoadDirection(vector=(1, 2, 3))
    definitions = square_definitions(panel, coefficients=(-1, 1, 0, 0))
    scalar_scale = compile_spatial_loads(definitions, mesh).force_scale
    definitions = replace(definitions, loads=(replace(definitions.loads[0], direction=direction),))
    result = compile_spatial_loads(definitions, mesh)
    np.testing.assert_allclose(result.resultant, 0, atol=1e-13)
    np.testing.assert_allclose(result.moment, np.cross([4/3, 0, 0], direction.vector), atol=1e-12)
    assert result.force_scale > 0
    assert result.force_scale == pytest.approx(scalar_scale)


@pytest.mark.parametrize('target,value', [
    ('plane', None), ('plane', []), ('plane', {'origin': [0, 0, 0]}),
    ('plane', {'origin': [0, 0, 0], 'axis_u': [1, 0, 0], 'axis_v': [0, 1, 0], 'normal': [0, 0, 1]}),
    ('direction', [0, 0, -1]), ('direction', {}), ('direction', {'mode': 'local'}),
    ('direction', {'mode': 'normal', 'vector': [0, 0, 1]}),
    ('direction', {'mode': 'global', 'vector': [0, 0, 0]}),
    ('direction', {'mode': 'global', 'vector': [0, 0, 1], 'scale': 2}),
])
def test_modern_plane_and_direction_records_reject_malformed_or_unknown_fields(target, value):
    mesh, panel = geometry_model()
    payload = to_json(square_definitions(panel))
    payload['panels' if target == 'plane' else 'loads'][0][target] = value
    with pytest.raises(ValueError, match='plane|direction'):
        from_json(payload, mesh)
