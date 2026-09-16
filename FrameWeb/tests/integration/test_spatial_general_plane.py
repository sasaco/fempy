"""General planar spatial input through public analysis, saved models and HTTP."""
import base64
from copy import deepcopy
from dataclasses import replace
import gzip
import json

import numpy as np
import pytest

from fem.model import FemModel
from fem.spatial_loads import (
    LoadDirection, LocalPlane, SpatialLoad, SpatialLoadDefinitions,
    SpatialLoadMeshNode, SpatialLoadPath,
)
from main import app
from tests.support.assertions import assert_dict_almost_equal
from tests.support.builders.input_routes import json_model
from tests.support.builders.spatial_loads import solver_panel
from tests.support.oracles.spatial_loads import square_strip_loads
from tests.support.serialization import wire

pytestmark = pytest.mark.integration


def general_model(shell, area, fixed, normal):
    model = solver_panel(shell=shell, area=area, fixed=fixed)
    if not fixed:
        model.add_restraint(1, True, True, True, True, True, True)
    rotation = np.array([[1, 2, 2], [2, 1, -2], [-2, 2, -1]], dtype=float) / 3
    offset = np.array([5., -7., 11.])
    for node, point in model.mesh.nodes.items():
        model.mesh.nodes[node] = rotation @ point + offset
    definitions = model.boundary.spatial_loads
    panel = replace(definitions.panels[0], plane=LocalPlane(offset, rotation[:, 0], rotation[:, 1]))
    paths = tuple(replace(p, points=tuple(rotation @ point + offset for point in p.points))
                  for p in definitions.paths)
    direction = LoadDirection('normal') if normal else LoadDirection(vector=(2., -1., 3.))
    load = replace(definitions.loads[0], direction=direction)
    model.set_spatial_loads(SpatialLoadDefinitions((panel,), paths, (load,)))
    return model, rotation[:, 2] if normal else np.array(direction.vector)


@pytest.mark.parametrize('shell', [False, True])
@pytest.mark.parametrize('area', [False, True])
@pytest.mark.parametrize('fixed', [False, True])
@pytest.mark.parametrize('normal', [False, True])
def test_public_general_plane_matches_independent_nodal_loads_all_outputs(shell, area, fixed, normal):
    model, direction = general_model(shell, area, fixed, normal)
    result = model.run()
    expected, _ = general_model(shell, area, fixed, normal)
    expected.set_spatial_loads(SpatialLoadDefinitions())
    values = square_strip_loads(shell=shell, area=area)
    forces = values[:, None] * direction
    for node, f in zip(model.mesh.nodes, forces):
        expected.add_load(node, fx=f[0], fy=f[1], fz=f[2])
    direct = expected.run()
    for key in ('node_displacements', 'reaction_forces', 'element_stresses', 'shell_results', 'legacy_shell_results'):
        if key in direct:
            assert_dict_almost_equal(wire(result[key]), wire(direct[key]))
    audit = result['spatial_load_contribution']
    np.testing.assert_allclose(audit['resultant'], sum(forces), atol=1e-11)
    np.testing.assert_allclose(audit['moment'], sum(np.cross(model.mesh.nodes[n], f)
                                                   for n, f in zip(model.mesh.nodes, forces)), atol=1e-10)
    if shell:
        # Ke*u-f_spatial is the direct element equilibrium force, independent of
        # stress/section-result output, which already matches the nodal solve.
        # All fixed cases give a direct independent observation with Ke*u = 0.
        if fixed:
            equilibrium = np.array(result['element_nodal_equilibrium_forces'][1]['forces'])
            np.testing.assert_allclose(equilibrium[:, :3], -forces, atol=1e-11)
            np.testing.assert_allclose(equilibrium[:, 3:], 0, atol=1e-11)
    assert_dict_almost_equal(wire(model.run()), wire(result))


@pytest.mark.parametrize('normal', [False, True])
@pytest.mark.parametrize('shell', [False, True])
def test_general_plane_python_dictionary_file_plain_compressed_http_and_retry(tmp_path, normal, shell):
    model, _ = general_model(shell, True, False, normal)
    expected = model.run()
    path = tmp_path / 'general.json'
    model.save_model(str(path))
    payload = json.loads(path.read_text(encoding='utf8'))
    original = deepcopy(payload)
    restored = FemModel()
    restored.load_model(str(path))
    assert restored.boundary.spatial_loads == model.boundary.spatial_loads
    results = [restored.run(), json_model(payload).run()]
    client = app.test_client()
    invalid = deepcopy(payload)
    invalid['spatial_loads']['panels'][0]['plane']['origin'][0] += 1
    response = client.post('/', json=invalid)
    assert response.status_code == 400
    assert 'panel 7' in response.get_data(as_text=True)
    response = client.post('/', json=payload)
    assert response.status_code == 200, response.get_data(as_text=True)
    results.append(response.get_json())
    packed = base64.b64encode(json.dumps(list(gzip.compress(json.dumps(payload).encode()))).encode())
    response = client.post('/', data=packed, headers={'Content-Encoding': 'gzip'})
    assert response.status_code == 200
    results.append(json.loads(gzip.decompress(base64.b64decode(response.data))))
    def comparable(result):
        value = wire(result)
        value['metadata']['input_sha256'] = '<route-specific>'
        return value
    for result in results:
        assert_dict_almost_equal(comparable(result), comparable(expected))
    assert payload == original


def test_independent_loading_mesh_runs_public_static_and_roundtrips_http(tmp_path):
    model = solver_panel(shell=True, area=True, fixed=True)
    definitions = model.boundary.spatial_loads
    nodes = tuple(SpatialLoadMeshNode(i+101, point) for i, point in enumerate(
        ((.25, .25, 0), (1.75, .25, 0), (1.75, 1.75, 0), (.25, 1.75, 0))))
    panel = replace(definitions.panels[0], loading_nodes=nodes,
                    loading_triangles=((101, 102, 103), (101, 103, 104)))
    paths = (SpatialLoadPath(1, ((.25, .25, 0), (1.75, .25, 0))),
             SpatialLoadPath(2, ((.25, 1.75, 0), (1.75, 1.75, 0))))
    model.set_spatial_loads(SpatialLoadDefinitions(
        (panel,), paths, (SpatialLoad(9, 7, (1, 2), ((2, 2), (2, 2))),)))
    result = model.run()
    np.testing.assert_allclose(result['spatial_load_contribution']['resultant'], [0, 0, 4.5], atol=1e-12)
    equilibrium = np.array(result['element_nodal_equilibrium_forces'][1]['forces'])
    np.testing.assert_allclose(equilibrium[:, 2], -1.125, atol=1e-12)
    np.testing.assert_allclose(equilibrium[:, [0, 1, 3, 4, 5]], 0, atol=1e-12)
    path = tmp_path / 'independent-loading-mesh.json'
    model.save_model(str(path))
    response = app.test_client().post('/', json=json.loads(path.read_text(encoding='utf8')))
    assert response.status_code == 200, response.get_data(as_text=True)
    actual = response.get_json()
    for value in (result, actual):
        value['metadata']['input_sha256'] = '<route-specific>'
    assert_dict_almost_equal(wire(actual), wire(result))
