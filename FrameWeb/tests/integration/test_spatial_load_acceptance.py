"""Public preflight and independent continuum acceptance for spatial loads."""
from copy import deepcopy
from dataclasses import replace

import numpy as np
import pytest

from fem import model as model_module, solver as solver_module
from fem.spatial_loads import SpatialLoadDefinitions, SpatialLoadPath
from tests.support.builders.spatial_loads import solver_panel, uniform_strip_model
from tests.support.oracles.spatial_loads import cantilever_uniform, simply_supported_uniform, uniform_cylindrical_plate

pytestmark = pytest.mark.integration


def test_public_preflight_coordinates_compile_and_stiffness_order(monkeypatch):
    model = solver_panel()
    order = []
    for owner, name, label in (
        (model_module, 'validate_analysis_capabilities', 'capabilities'),
        (model, '_set_element_coordinates', 'coordinates'),
        (solver_module, 'compile_spatial_loads', 'compile'),
        (model.solver, 'create_stiffness_matrix', 'stiffness'),
    ):
        original = getattr(owner, name)
        def record(*args, _original=original, _label=label, **kwargs):
            order.append(_label)
            return _original(*args, **kwargs)
        monkeypatch.setattr(owner, name, record)
    model.run()
    assert order == ['capabilities', 'coordinates', 'compile', 'stiffness']
    model.run()
    assert order[4:] == order[:4]


@pytest.mark.parametrize('invalid', ['outside', 'tilted', 'missing_node', 'crossed', 'nonconvex'])
def test_public_geometry_failure_precedes_stiffness_and_retry_uses_current_mesh(monkeypatch, invalid):
    model = solver_panel(shell=True)
    reference = model.run()['spatial_load_contribution']
    definitions = model.boundary.spatial_loads
    nodes = deepcopy(model.mesh.nodes)
    if invalid == 'outside':
        model.set_spatial_loads(replace(definitions, paths=(
            SpatialLoadPath(1, ((-1, .5, 0), (2, .5, 0))), definitions.paths[1])))
    elif invalid == 'missing_node':
        del model.mesh.nodes[3]
    elif invalid == 'tilted':
        model.mesh.nodes[3] = np.array([2., 2., 1.])
    elif invalid == 'crossed':
        model.set_spatial_loads(replace(definitions, paths=(
            definitions.paths[0], SpatialLoadPath(2, ((0, 1.5, 0), (2, 0, 0))))))
    else:
        model.mesh.nodes[3] = np.array([.5, .5, 0.])
    with monkeypatch.context() as patch:
        patch.setattr(model.solver, 'create_stiffness_matrix', lambda *a: pytest.fail('no K assembly'))
        with pytest.raises(ValueError, match='panel 7'):
            model.run()
    assert model.results is None
    assert model.solver.spatial_load_contribution is None
    model.mesh.nodes = nodes
    model.set_spatial_loads(definitions)
    assert model.run()['spatial_load_contribution'] == reference


@pytest.mark.parametrize('shell,triangular', [(False, False), (True, False), (True, True)])
@pytest.mark.parametrize('simply_supported', [False, True])
@pytest.mark.parametrize('sign', [-1., 1.])
def test_uniform_area_beam_and_cylindrical_plate_converge_to_closed_form(shell, triangular, simply_supported, sign):
    intensity = sign*3.
    rigidity = 1000.*.2**3/12 if shell else 1000.
    if simply_supported:
        _, exact = simply_supported_uniform(2., intensity, rigidity)
    else:
        _, _, exact = cantilever_uniform(2., intensity, rigidity)
    if shell:
        exact = uniform_cylindrical_plate(2., intensity, 1000., .2,
                                         simply_supported=simply_supported, mindlin=not triangular)
    errors = []
    for segments in (2, 4, 8):
        model = uniform_strip_model(segments, shell=shell, triangular_shell=triangular,
                                    simply_supported=simply_supported, intensity=intensity)
        result = model.run()
        station = segments//2 if simply_supported else segments
        # Average the two edges: the T3 diagonal biases edge loads oppositely.
        observed = sum(result['node_displacements'][2*station+s]['dz'] for s in (1, 2))/2
        errors.append(abs(observed/exact - 1))
        reactions = result['reaction_forces']
        assert sum(r.get('fz', 0.) for r in reactions.values()) == pytest.approx(-4*intensity, abs=1e-8)
        moment = sum(r.get('my', 0.)-model.mesh.nodes[n][0]*r.get('fz', 0.) for n, r in reactions.items())
        assert moment == pytest.approx(4*intensity, abs=1e-8)
        contribution = result['spatial_load_contribution']
        assert contribution['resultant'][2] == pytest.approx(4*intensity, abs=1e-12)
        assert contribution['moment'][1] == pytest.approx(-4*intensity, abs=1e-12)
    if shell and not triangular and not simply_supported:
        # Q4 reproduces the cylindrical cantilever tip exactly on these meshes.
        assert max(errors) < 1e-9
    else:
        assert errors[1] < errors[0]*.6
        assert errors[2] < errors[1]*.6
    assert errors[-1] < .025


@pytest.mark.parametrize('simply_supported', [False, True])
def test_uniform_line_on_beam_converges_to_closed_form(simply_supported):
    exact = (simply_supported_uniform(2., 3., 1000.)[-1] if simply_supported
             else cantilever_uniform(2., 3., 1000.)[-1])
    errors = []
    for segments in (2, 4, 8):
        model = uniform_strip_model(segments, area=False, simply_supported=simply_supported)
        result = model.run()
        station = segments//2 if simply_supported else segments
        errors.append(abs(result['node_displacements'][2*station+1]['dz']/exact-1))
        assert result['node_displacements'][2*station+2]['dz'] == pytest.approx(0., abs=1e-13)
        assert sum(r['fz'] for r in result['reaction_forces'].values()) == pytest.approx(-6., abs=1e-10)
    assert errors[1] < errors[0]*.3
    assert errors[2] < errors[1]*.3
    assert errors[-1] < .025


@pytest.mark.parametrize('shell', [False, True])
@pytest.mark.parametrize('area', [False, True])
def test_public_line_and_area_units_scale_with_force_and_length(shell, area):
    reference = uniform_strip_model(2, shell=shell, area=area)
    converted = uniform_strip_model(2, shell=shell, area=area)
    length, force = 1000., .001  # N, m -> kN, mm
    for node in converted.mesh.nodes:
        converted.mesh.nodes[node] = np.asarray(converted.mesh.nodes[node])*length
    material = converted.material.materials[1]
    converted.material.materials[1] = replace(material, E=material.E*force/length**2)
    section = converted.material.get_bar_parameter(1)
    converted.material.add_bar_parameter(1, replace(section, area=section.area*length**2,
        Iy=section.Iy*length**4, Iz=section.Iz*length**4, J=section.J*length**4))
    if shell:
        for element in converted.mesh.elements.values():
            element['thickness'] *= length
    definitions = converted.boundary.spatial_loads
    converted.set_spatial_loads(replace(definitions,
        paths=tuple(replace(path, points=tuple(tuple(v*length for v in p) for p in path.points))
                    for path in definitions.paths),
        loads=tuple(replace(load, end_intensities=tuple(tuple(v*force/length**(2 if area else 1)
                    for v in pair) for pair in load.end_intensities)) for load in definitions.loads)))
    expected, result = reference.run(), converted.run()
    for node, values in expected['node_displacements'].items():
        for key, value in values.items():
            assert result['node_displacements'][node][key] == pytest.approx(
                value*(length if key.startswith('d') else 1.), rel=1e-8, abs=1e-8)
    for node, values in expected['reaction_forces'].items():
        for key, value in values.items():
            assert result['reaction_forces'][node][key] == pytest.approx(
                value*force*(length if key.startswith('m') else 1.), rel=1e-8, abs=1e-8)


@pytest.mark.parametrize('area', [False, True])
@pytest.mark.parametrize('invalid', ['outside', 'crossed', 'hole', 'topology', 'missing_path'])
def test_http_geometry_and_input_errors_have_ids_and_recover(area, invalid):
    from main import app
    from tests.support.builders.spatial_loads import legacy_panel

    data = legacy_panel(area=area)
    data['fix_node'] = {'1': [dict(n=1, tx=1, ty=1, tz=1, rx=1, ry=1, rz=1)]}
    good = deepcopy(data)
    if invalid == 'outside':
        data['line']['1']['position'][0]['x'] = -1
    elif invalid == 'crossed':
        data['line']['1']['position'] = [dict(x=0, y=0), dict(x=2, y=2),
                                        dict(x=0, y=2), dict(x=2, y=0)]
    elif invalid == 'hole':
        data['inf_panel']['7']['holes'] = [[1, 2, 3]]
    elif invalid == 'topology':
        del data['inf_panel']['7']['triangles']
    else:
        data['load']['1']['load_inf'][0]['L1'] = 99
    client = app.test_client()
    response = client.post('/', json=data)
    assert response.status_code == 400
    result = response.get_json()
    assert result['error_code'] == 'invalid_input'
    assert result['converged'] is False
    assert 'node_displacements' not in result
    assert any(identifier in result['error'] for identifier in ('7', '99', 'load_inf[0]'))
    assert client.post('/', json=good).status_code == 200


@pytest.mark.parametrize('shell', [False, True])
@pytest.mark.parametrize('area', [False, True])
def test_public_load_superposition_zero_and_reversed_paths(shell, area):
    from tests.support.assertions import assert_dict_almost_equal
    from tests.support.serialization import wire

    model = solver_panel(shell=shell, area=area, fixed=False)
    model.add_restraint(1, True, True, True, True, True, True)
    model.boundary.loads.clear()
    definitions = model.boundary.spatial_loads
    load = definitions.loads[0]
    baseline = model.run()
    first = replace(load, end_intensities=tuple(tuple(v*3 for v in p) for p in load.end_intensities))
    second = replace(load, id=23, end_intensities=tuple(tuple(v*-2 for v in p) for p in load.end_intensities))
    model.set_spatial_loads(replace(definitions, loads=(first, second)))
    combined = model.run()
    model.set_spatial_loads(replace(definitions,
        paths=tuple(replace(path, points=path.points[::-1]) for path in definitions.paths),
        loads=(replace(load, end_intensities=tuple(p[::-1] for p in load.end_intensities)),)))
    reversed_result = model.run()
    for field in ('node_displacements', 'reaction_forces', 'element_stresses',
                  'shell_results', 'legacy_shell_results', 'element_nodal_equilibrium_forces'):
        if field in baseline:
            for result in (combined, reversed_result):
                assert_dict_almost_equal(wire(result[field]), wire(baseline[field]))
    zero = replace(load, end_intensities=((0., 0.),)*len(load.path_ids))
    model.set_spatial_loads(replace(definitions, loads=(zero,)))
    result = model.run()
    np.testing.assert_allclose(result['displacement'], 0., atol=1e-13)
    np.testing.assert_allclose(result['spatial_load_contribution']['resultant'], 0., atol=1e-13)
