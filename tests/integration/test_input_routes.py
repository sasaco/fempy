"""integration / input routes contracts."""

import json

import pytest

from fem.model import FemModel
from main import app
from tests.support.assertions import assert_axial, assert_dict_almost_equal
from tests.support.builders.input_routes import axial_json, json_model, python_axial
from tests.support.builders.linear_frame import cantilever
from tests.support.builders.nonlinear_reference import solve
from tests.support.paths import DATA
from tests.support.serialization import wire

pytestmark = pytest.mark.integration


def without_input_hash(result):
    """Compare responses while retaining each route's truthful input provenance."""
    comparable = wire(result)
    comparable["metadata"]["input_sha256"] = "<route-specific>"
    return comparable


@pytest.mark.parametrize('shell', [False, True])
@pytest.mark.parametrize('area', [False, True])
@pytest.mark.parametrize('fixed', [False, True])
def test_spatial_python_legacy_modern_file_and_http_all_outputs(tmp_path, shell, area, fixed):
    from copy import deepcopy
    import base64
    import gzip
    from fem import BarParameter
    from fem.spatial_loads import SpatialLoad, SpatialLoadDefinitions, SpatialLoadPanel, SpatialLoadPath
    from tests.support.builders.spatial_loads import legacy_panel

    data = legacy_panel(area=area)
    supports = list(range(1, 5)) if fixed else [1]
    data['fix_node'] = {'1': [dict(n=n, tx=1, ty=1, tz=1, rx=1, ry=1, rz=1) for n in supports]}
    data['element']['1']['1']['nu'] = .25
    if shell:
        del data['member']
        data['shell'] = {'1': dict(nodes=[1, 2, 3, 4], e=1, t=.1)}
        data['element']['1']['1']['thickness'] = .1
        data['inf_panel']['7'] = dict(nodes=[1, 2, 3, 4], elements=[1])
    # The second case is deliberately different and must never enter the solve.
    data['load']['2'] = dict(inf_panel=7, load_inf=[dict(L1=1, P11=999, P12=999)])
    original = deepcopy(data)
    python = FemModel()
    for node, xy in enumerate(((0, 0), (2, 0), (2, 2), (0, 2)), 1):
        python.add_node(node, *xy, 0.)
    python.add_material(1, 'route', E=1000., nu=.25)
    python.material.add_bar_parameter(1, BarParameter(1., 1., 1., 1.))
    if shell:
        python.add_element(1, 'shell', [1, 2, 3, 4], 1, thickness=.1)
    else:
        for node in range(1, 5):
            python.add_element(node, 'bar', [node, node % 4+1], 1, section_id=1, shear_correction=False)
    for node in supports:
        python.add_restraint(node, True, True, True, True, True, True)
    python.add_load(2, fz=-3.)
    paths = (SpatialLoadPath(1, ((0, .5, 0), (2, .5, 0))),)
    values = ((10., 20.),)
    if area:
        paths += (SpatialLoadPath(2, ((0, 1.5, 0), (2, 1.5, 0))),)
        values += ((30., 40.),)
    panel = SpatialLoadPanel(7, (1, 2, 3, 4), elements=(1,) if shell else (),
                             triangles=() if shell else ((1, 2, 3), (1, 3, 4)))
    python.set_spatial_loads(SpatialLoadDefinitions((panel,), paths,
        (SpatialLoad(1, 7, tuple(p.id for p in paths), values),)))
    results = [python.run()]
    modern_path = tmp_path / 'modern.json'
    python.save_model(str(modern_path))
    modern = json.loads(modern_path.read_text(encoding='utf8'))
    client = app.test_client()
    for name, payload in (('legacy', data), ('modern', modern)):
        results.append(json_model(payload).run())
        path = tmp_path / f'{name}.json'
        path.write_text(json.dumps(payload), encoding='utf8')
        file_model = FemModel()
        file_model.load_model(str(path))
        results.append(file_model.run())
        response = client.post('/', json=payload)
        assert response.status_code == 200, response.get_data(as_text=True)
        results.append(response.get_json())
        packed = base64.b64encode(json.dumps(list(gzip.compress(json.dumps(payload).encode()))).encode())
        response = client.post('/', data=packed, headers={'Content-Encoding': 'gzip'})
        assert response.status_code == 200, response.get_data(as_text=True)
        results.append(json.loads(gzip.decompress(base64.b64decode(response.data))))
    for result in results:
        assert_dict_almost_equal(without_input_hash(result), without_input_hash(results[0]))
        assert len(result['metadata']['input_sha256']) == 64
    assert data == original


@pytest.mark.material_nonlinear
def test_python_json_file_http_equivalence(tmp_path):
    data = axial_json()
    path = tmp_path / "axial.json"
    path.write_text(json.dumps(data), encoding="utf-8")
    models = [python_axial(), json_model(data), FemModel()]
    models[-1].load_model(str(path))
    results = [m.run() for m in models]
    response = app.test_client().post("/", json=data)
    assert response.status_code == 200
    results.append(json.loads(response.data))
    for r in results:
        assert_axial(r)
        assert_dict_almost_equal(without_input_hash(r), without_input_hash(results[0]))
        # Construction routes intentionally retain distinct input provenance.
        assert without_input_hash(r) == without_input_hash(results[0])


@pytest.mark.material_nonlinear
def test_python_json_and_http_use_saved_jr_k4_displacement_history():
    data = json.loads((DATA / "snap/jr_k4_displacement_control.json").read_text(encoding="utf8"))
    expected = list(data["result"].values())
    results = [solve(data, route) for route in ("python", "json", "http")]
    for result in results:
        assert [step["lambda"] for step in result["step_results"]] == pytest.approx(
            [step["lambda"] for step in expected], abs=1e-9
        )
        assert [step["control_displacement"] for step in result["step_results"]] == pytest.approx(
            [step["control_displacement"] for step in expected], abs=1e-12
        )
        assert [step["curvature"]["7"]["z"] for step in result["step_results"]] == pytest.approx(
            [step["curvature"]["7"]["z"] for step in expected], abs=1e-12
        )
        assert result["lambda"] == pytest.approx(12.0)
        assert result["node_displacements"]["30"]["dy"] == pytest.approx(0.040)
        assert result["node_displacements"]["30"]["rz"] == pytest.approx(0.040)
        assert result["reaction_forces"]["10"]["mz"] == pytest.approx(-12.0)
    assert_dict_almost_equal(without_input_hash(results[1]), without_input_hash(results[0]))
    assert_dict_almost_equal(without_input_hash(results[2]), without_input_hash(results[0]))


@pytest.mark.material_nonlinear
def test_omitted_nu_has_same_nonlinear_default_in_python_json_and_http():
    d = axial_json(0)
    del d["element"]["1"]["1"]["nu"]
    # G omission now selects Bernoulli; this test isolates the nu default.
    d["element"]["1"]["1"]["G"] = 10000 / 2.4
    d["load"]["1"]["load_node"][0]["ty"] = 1
    m = python_axial(0)
    m.add_nonlinear_material(1, "reference", 10000, 0.001, 0.004, 0.010, 10, 16, 22, beta=0)
    m.add_load(30, fy=1)
    results = [wire(m.run()), wire(json_model(d).run())]
    response = app.test_client().post("/", json=d)
    assert response.status_code == 200
    results.append(json.loads(response.data))
    # Existing Python nonlinear default nu=.2 -> G=10000/2.4.
    expected = 8 / 30000 + 2 / ((10000 / 2.4) * 5 / 6)
    for r in results:
        assert r["node_displacements"]["30"]["dy"] == pytest.approx(expected, abs=1e-10)


def test_omitted_g_file_http_and_saved_model_agree(tmp_path):
    from main import app

    data = cantilever()
    del data["element"]["1"]["2"]["G"]
    data["member"]["1"]["shear_correction"] = True
    path = tmp_path / "beam.json"
    path.write_text(json.dumps(data), encoding="utf-8")
    model = FemModel()
    model.load_model(str(path))
    result = model.run()
    assert result["node_displacements"][2]["dy"] == pytest.approx(0.002, abs=1e-12)
    saved = tmp_path / "saved.json"
    model.save_model(str(saved))
    restored = FemModel()
    restored.load_model(str(saved))
    assert restored.run()["node_displacements"][2]["dy"] == pytest.approx(0.002, abs=1e-12)
    response = app.test_client().post("/", json=data)
    assert response.status_code == 200
    assert json.loads(response.data)["node_displacements"]["2"]["dy"] == pytest.approx(0.002, abs=1e-12)
