"""Public transport and saved-model history against the manual's hand table."""
import base64
import gzip
import json
from copy import deepcopy
from pathlib import Path

import numpy as np
import pytest

from fem.file_io import _read_json_model, result_to_jsonable
from fem.model import FemModel
from main import app
from tests.support.builders.slip_support import slip_model_data, legacy_slip_data

pytestmark = pytest.mark.regression

FORCES = [5., 12., 6., 0., 0., 7., 0., -5., -12., -6., 0., 0., 6., 13.]


def assert_history(result):
    for force, step in zip(FORCES, result['step_results'], strict=True):
        response = step['support_response']['30']['x']
        assert response['force'] == pytest.approx(force, abs=1e-9)
        assert step['reaction_forces']['30']['fx'] == pytest.approx(-force, abs=1e-9)
        assert step['lambda'] == pytest.approx(3000*response['deformation']+force, abs=1e-9)
    assert result['support_response'] == result['step_results'][-1]['support_response']


@pytest.mark.parametrize('legacy', [False, True])
@pytest.mark.parametrize('compressed', [False, True])
def test_http_history_including_compressed_transport(legacy, compressed):
    raw = legacy_slip_data() if legacy else slip_model_data()
    client = app.test_client()
    if compressed:
        data = base64.b64encode(json.dumps(list(gzip.compress(json.dumps(raw).encode()))).encode())
        reply = client.post('/', data=data, headers={'Content-Encoding': 'gzip'})
        assert reply.status_code == 200, reply.data
        result = json.loads(gzip.decompress(base64.b64decode(reply.data)))
    else:
        reply = client.post('/', json=raw)
        assert reply.status_code == 200, reply.data
        result = reply.get_json()
    assert_history(result)


def test_python_file_result_roundtrip_reanalysis_and_hash(tmp_path):
    raw = slip_model_data()
    raw['boundary_conditions'].pop('nonlinear_spring_supports')
    model = FemModel()
    model.read_json_model(_read_json_model(raw))
    model.add_slip_spring_support(30, 'x', 1000, 100, .01)
    expected = result_to_jsonable(model.run())
    assert_history(expected)
    model.save_model(str(tmp_path/'model.json'))
    model.save_results(str(tmp_path/'result.json'))
    assert_history(json.loads((tmp_path/'result.json').read_text(encoding='utf8')))
    restored = FemModel()
    restored.load_model(str(tmp_path/'model.json'))
    actual = result_to_jsonable(restored.run())
    assert actual['support_response'] == expected['support_response']
    assert actual['metadata']['input_sha256'] == expected['metadata']['input_sha256']
    assert_history(actual)
    actual['support_response']['30']['x']['zero_pos'] = 99
    assert_history(result_to_jsonable(restored.run()))
    from fem.nonlinear.hysteresis.slip import SlipSpringParams
    restored.boundary.nonlinear_spring_supports[30]['x'] = SlipSpringParams(1000, 200, .01)
    assert restored.run()['metadata']['input_sha256'] != expected['metadata']['input_sha256']


@pytest.mark.parametrize('kind', ['static', 'modal'])
def test_http_rejects_explicit_incompatible_analysis(kind):
    data = slip_model_data()
    data['analysis_type'] = kind
    reply = app.test_client().post('/', json=data)
    assert reply.status_code == 400
    assert reply.get_json()['error_code'] == 'unsupported_analysis'
    assert reply.get_json()['details']['support_nodes'] == [30]


def test_documented_complete_example():
    path = Path(__file__).resolve().parents[2]/'docs/examples/slip-support-history.json'
    model = FemModel()
    model.load_model(str(path))
    assert_history(result_to_jsonable(model.run()))
