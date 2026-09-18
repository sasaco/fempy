"""Canonical JSON and actual browser CSV compressed-input contracts."""

import base64
import gzip
import json
from pathlib import Path

import main
import pytest
from fem.diagnostics import InputValidationError
from main import Compressor, app

FIXTURE_PATH = Path(__file__).parents[1] / "data/transport/legacy-browser-envelope.json"
BROWSER = json.loads(FIXTURE_PATH.read_text(encoding="utf-8"))
ENCODINGS = ["gzip", "gzip,base64"]
FORMATS = ["json-array", "legacy-csv"]


def outer_body(text: str) -> bytes:
    return base64.b64encode(text.encode("utf-8"))


def envelope(compressed: bytes, wire_format: str) -> bytes:
    values = list(compressed)
    text = (
        json.dumps(values)
        if wire_format == "json-array"
        else ",".join(str(value) for value in values)
    )
    return outer_body(text)


def compressed_json(value: object) -> bytes:
    return gzip.compress(json.dumps(value, ensure_ascii=False).encode("utf-8"), mtime=0)


def test_decompress_accepts_legacy_browser_fixture() -> None:
    assert (
        BROWSER["generator"]["expression"] == "btoa(pako.gzip(JSON.stringify(payload)))"
    )
    assert Compressor.decompress(BROWSER["body"].encode("ascii")) == BROWSER["payload"]


@pytest.mark.parametrize("wire_format", FORMATS)
def test_decompress_accepts_same_gzip_bytes(wire_format: str) -> None:
    data = {"message": "日本語", "values": [0, 255], "flag": True}
    assert Compressor.decompress(envelope(compressed_json(data), wire_format)) == data


def test_decompress_accepts_canonical_json_byte_array() -> None:
    assert Compressor.decompress(envelope(compressed_json({}), "json-array")) == {}


def test_legacy_decimal_tokens_allow_leading_zeroes() -> None:
    text = ",".join(f"{value:03d}" for value in compressed_json({}))
    assert Compressor.decompress(outer_body(text)) == {}


@pytest.mark.parametrize("encoding", ENCODINGS)
@pytest.mark.parametrize("wire_format", FORMATS)
def test_http_accepts_both_envelopes(encoding: str, wire_format: str) -> None:
    client = app.test_client()
    reference = client.post("/", json=BROWSER["payload"])
    assert reference.status_code == 200
    # The CSV branch consumes the saved Node/pako producer output directly.
    csv = base64.b64decode(BROWSER["body"], validate=True)
    body = (
        BROWSER["body"]
        if wire_format == "legacy-csv"
        else outer_body(f"[{csv.decode('ascii')}]")
    )
    response = client.post("/", data=body, headers={"Content-Encoding": encoding})
    assert response.status_code == 200, response.get_data(as_text=True)
    result = json.loads(gzip.decompress(base64.b64decode(response.data, validate=True)))
    assert result == reference.get_json()
    assert result["analysis_type"] == "static"
    assert result["node_displacements"]["2"]["dy"] == pytest.approx(0.002)
    assert result["reaction_forces"]["1"]["fy"] == pytest.approx(-3)
    assert result["element_stresses"]


@pytest.mark.parametrize("encoding", [None, "json"])
def test_http_plain_json_remains_supported(encoding: str | None) -> None:
    headers = {} if encoding is None else {"Content-Encoding": encoding}
    response = app.test_client().post("/", json=BROWSER["payload"], headers=headers)
    assert response.status_code == 200
    assert response.get_json()["node_displacements"]["2"]["dy"] == pytest.approx(0.002)


INVALID_OUTER = [
    pytest.param(text, id=name)
    for name, text in [
        ("object", "{}"),
        ("number-no-csv-fallback", "31"),
        ("string", '"31,139"'),
        ("null", "null"),
        ("boolean", "true"),
        ("bool-element", "[true]"),
        ("float", "[31.0]"),
        ("string-element", '["31"]'),
        ("nested", "[[31]]"),
        ("negative", "[-1]"),
        ("too-large", "[256]"),
        ("empty-array", "[]"),
        ("empty", ""),
        ("leading-comma", ",31"),
        ("trailing-comma", "31,"),
        ("empty-token", "31,,139"),
        ("only-comma", ","),
        ("space", "31, 139"),
        ("newline", "31,139\n"),
        ("plus", "31,+139"),
        ("minus", "31,-1"),
        ("decimal", "31,139.0"),
        ("exponent", "31,1e2"),
        ("unicode-digit", "31,１３９"),
        ("non-ascii", "31,é"),
        ("four-digits", "31,0139"),
        ("huge-token", "31," + "9" * 5000),
        ("csv-too-large", "31,256"),
        ("expression", "1+1"),
        ("python-import", "__import__('os').getcwd()"),
    ]
]


@pytest.mark.parametrize("text", INVALID_OUTER)
def test_rejects_invalid_outer_byte_sequences(text: str) -> None:
    with pytest.raises(InputValidationError):
        Compressor.decompress(outer_body(text))


@pytest.mark.parametrize("text", INVALID_OUTER)
def test_http_rejects_invalid_outer_byte_sequences(text: str) -> None:
    assert_invalid_http(outer_body(text))


def assert_invalid_http(body: bytes) -> None:
    response = app.test_client().post(
        "/", data=body, headers={"Content-Encoding": "gzip,base64"}
    )
    assert response.status_code == 400, response.get_data(as_text=True)
    result = response.get_json()
    assert result["error_code"] == "invalid_input"
    assert result["error_category"] == "input"
    assert result["converged"] is False


@pytest.mark.parametrize("replacement", [False, True, 0.0, "0", [0], -1, 256])
def test_rejects_non_integer_bytes_in_otherwise_valid_gzip(replacement: object) -> None:
    values = list(compressed_json({}))
    values[3] = replacement
    with pytest.raises(InputValidationError):
        Compressor.decompress(outer_body(json.dumps(values)))


BAD_GZIP = [
    pytest.param(b"not gzip", id="not-gzip"),
    pytest.param(compressed_json({})[:-4], id="truncated"),
    pytest.param(compressed_json({})[:-8] + bytes(8), id="bad-checksum"),
    pytest.param(bytes.fromhex("1f8b0800000000000003ff"), id="bad-deflate"),
]


@pytest.mark.parametrize("wire_format", FORMATS)
@pytest.mark.parametrize("compressed", BAD_GZIP)
def test_invalid_gzip_is_input_error(compressed: bytes, wire_format: str) -> None:
    body = envelope(compressed, wire_format)
    with pytest.raises(InputValidationError):
        Compressor.decompress(body)
    assert_invalid_http(body)


@pytest.mark.parametrize("wire_format", FORMATS)
@pytest.mark.parametrize(
    "content", [b"{", b"\xff", b"[]", b'"text"', b"1", b"true", b"null"]
)
def test_invalid_inner_json_is_input_error(content: bytes, wire_format: str) -> None:
    body = envelope(gzip.compress(content, mtime=0), wire_format)
    with pytest.raises(InputValidationError):
        Compressor.decompress(body)
    assert_invalid_http(body)


@pytest.mark.parametrize("suffix", [b"!", b"\n", b" ", b"\xff"])
def test_base64_rejects_non_alphabet_bytes(suffix: bytes) -> None:
    body = envelope(compressed_json({}), "json-array") + suffix
    with pytest.raises(InputValidationError):
        Compressor.decompress(body)
    assert_invalid_http(body)


def test_base64_rejects_invalid_padding() -> None:
    with pytest.raises(InputValidationError):
        Compressor.decompress(b"A")
    assert_invalid_http(b"A")


def test_outer_json_integer_digit_limit_is_input_error_without_csv_fallback() -> None:
    body = outer_body("9" * 5000)
    with pytest.raises(
        InputValidationError, match="Invalid compressed byte sequence JSON"
    ):
        Compressor.decompress(body)
    assert_invalid_http(body)


@pytest.mark.parametrize("wire_format", FORMATS)
def test_inner_json_integer_digit_limit_is_input_error(wire_format: str) -> None:
    content = ('{"value":' + "9" * 5000 + "}").encode("ascii")
    body = envelope(gzip.compress(content, mtime=0), wire_format)
    with pytest.raises(InputValidationError, match="Invalid compressed input JSON"):
        Compressor.decompress(body)
    assert_invalid_http(body)


def test_code_shaped_input_is_never_executed(monkeypatch: pytest.MonkeyPatch) -> None:
    calls = []
    monkeypatch.setattr(main.os, "getcwd", lambda: calls.append("executed"))
    assert_invalid_http(outer_body("__import__('os').getcwd()"))
    assert calls == []


def test_unexpected_decompression_error_is_not_reclassified(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    body = envelope(compressed_json({}), "json-array")

    def fail(_data: bytes) -> bytes:
        raise RuntimeError("unexpected decompressor defect")

    monkeypatch.setattr(main.gzip, "decompress", fail)
    with pytest.raises(RuntimeError, match="unexpected decompressor defect"):
        Compressor.decompress(body)
    response = app.test_client().post(
        "/", data=body, headers={"Content-Encoding": "gzip"}
    )
    assert response.status_code == 500
    assert response.get_json()["error_code"] == "analysis_failure"
