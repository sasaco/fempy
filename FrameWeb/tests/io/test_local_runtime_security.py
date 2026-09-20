"""Security contract for the authenticated, bounded desktop-local runtime."""

from __future__ import annotations

import base64
import gzip
import json

import main
import pytest
from flask import Request
from main import app


TOKEN = "test-local-runtime-token"
AUTH_HEADERS = {main.LOCAL_AUTH_HEADER: TOKEN}


@pytest.fixture(autouse=True)
def local_runtime_token(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv(main.LOCAL_AUTH_ENV, TOKEN)


def _compressed_body(content: bytes) -> bytes:
    compressed = gzip.compress(content, mtime=0)
    return base64.b64encode(json.dumps(list(compressed)).encode("ascii"))


def _post_compressed(compressed: bytes):
    body = base64.b64encode(json.dumps(list(compressed)).encode("ascii"))
    return app.test_client().post(
        "/",
        data=body,
        headers={**AUTH_HEADERS, "Content-Encoding": "gzip"},
    )


def test_authenticated_get_returns_exact_readiness_marker() -> None:
    response = app.test_client().get("/", headers=AUTH_HEADERS)

    assert response.status_code == 200
    assert response.get_json() == main.LOCAL_READINESS_MARKER


def test_authenticated_options_is_allowed() -> None:
    response = app.test_client().options("/", headers=AUTH_HEADERS)

    assert response.status_code == 204
    assert main.LOCAL_AUTH_HEADER in response.headers["Access-Control-Allow-Headers"]


def test_authenticated_post_reaches_json_processing(monkeypatch: pytest.MonkeyPatch) -> None:
    sentinel = {"kind": "analysis_result_set", "version": 1}
    monkeypatch.setattr(main, "build_analysis_result_set", lambda value: sentinel)

    response = app.test_client().post("/", json={"input": True}, headers=AUTH_HEADERS)

    assert response.status_code == 200
    assert response.get_json() == sentinel


@pytest.mark.parametrize(
    "content_type",
    [
        "application/json",
        "application/json; charset=utf-8",
        "application/json; charset=UTF-8",
        "application/json; charset=utf8",
    ],
)
def test_local_json_accepts_strict_utf8_with_supported_content_type(
    content_type: str,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    sentinel = {"kind": "analysis_result_set", "version": 1}
    monkeypatch.setattr(main, "build_analysis_result_set", lambda value: sentinel)
    body = json.dumps({"message": "日本語"}, ensure_ascii=False).encode("utf-8")

    response = app.test_client().post(
        "/",
        data=body,
        headers={**AUTH_HEADERS, "Content-Type": content_type},
    )

    assert response.status_code == 200
    assert response.get_json() == sentinel


def test_local_json_rejects_declared_utf16_before_analysis(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    def fail(_value):
        raise AssertionError("UTF-16 request reached analysis")

    monkeypatch.setattr(main, "build_analysis_result_set", fail)
    response = app.test_client().post(
        "/",
        data=json.dumps({"input": True}).encode("utf-16"),
        headers={**AUTH_HEADERS, "Content-Type": "application/json; charset=utf-16"},
    )

    assert response.status_code == 415
    assert response.get_json()["error_code"] == "unsupported_media_type"


def test_local_json_rejects_raw_utf16_without_charset_before_analysis(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    def fail(_value):
        raise AssertionError("UTF-16 request reached analysis")

    monkeypatch.setattr(main, "build_analysis_result_set", fail)
    response = app.test_client().post(
        "/",
        data=json.dumps({"input": True}).encode("utf-16-le"),
        headers={**AUTH_HEADERS, "Content-Type": "application/json"},
    )

    assert response.status_code == 400
    assert response.get_json()["error_code"] == "invalid_input"


def test_local_json_rejects_malformed_utf8_before_analysis(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    def fail(_value):
        raise AssertionError("malformed UTF-8 request reached analysis")

    monkeypatch.setattr(main, "build_analysis_result_set", fail)
    response = app.test_client().post(
        "/",
        data=b'{"message":"\xff"}',
        headers={**AUTH_HEADERS, "Content-Type": "application/json"},
    )

    assert response.status_code == 400
    assert response.get_json()["error_code"] == "invalid_input"


@pytest.mark.parametrize(
    "content_type",
    [
        "text/plain",
        "application/problem+json",
        "application/json; profile=unsupported",
    ],
)
def test_local_json_rejects_noncanonical_media_type(
    content_type: str,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    def fail(_value):
        raise AssertionError("unsupported media type reached analysis")

    monkeypatch.setattr(main, "build_analysis_result_set", fail)
    response = app.test_client().post(
        "/",
        data=b"{}",
        headers={**AUTH_HEADERS, "Content-Type": content_type},
    )

    assert response.status_code == 415
    assert response.get_json()["error_code"] == "unsupported_media_type"


def test_absent_local_token_preserves_flask_utf16_json_behavior(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.delenv(main.LOCAL_AUTH_ENV)
    sentinel = {"kind": "analysis_result_set", "version": 1}
    monkeypatch.setattr(main, "build_analysis_result_set", lambda value: sentinel)

    response = app.test_client().post(
        "/",
        data=json.dumps({"input": True}).encode("utf-16"),
        content_type="application/json",
    )

    assert response.status_code == 200
    assert response.get_json() == sentinel


@pytest.mark.parametrize("method", ["GET", "POST", "OPTIONS"])
@pytest.mark.parametrize("provided", [None, "wrong-token"])
def test_missing_or_wrong_token_is_rejected_for_every_method(
    method: str,
    provided: str | None,
) -> None:
    headers = {} if provided is None else {main.LOCAL_AUTH_HEADER: provided}
    response = app.test_client().open("/", method=method, data=b"{}", headers=headers)

    assert response.status_code == 401
    assert response.get_json() == {"error": "unauthorized"}


def test_unauthorized_post_does_not_read_or_parse_body(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    def fail(*_args, **_kwargs):
        raise AssertionError("unauthorized request body was processed")

    monkeypatch.setattr(Request, "get_data", fail)
    monkeypatch.setattr(Request, "get_json", fail)
    monkeypatch.setattr(main, "build_analysis_result_set", fail)
    monkeypatch.setattr(main.Compressor, "decompress", fail)

    response = app.test_client().post(
        "/",
        data=b"body must remain unread",
        headers={"Content-Encoding": "gzip"},
    )

    assert response.status_code == 401


def test_token_comparison_uses_constant_time_primitive(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    calls: list[tuple[str, str]] = []

    def compare(left: str, right: str) -> bool:
        calls.append((left, right))
        return True

    monkeypatch.setattr(main.hmac, "compare_digest", compare)

    response = app.test_client().get(
        "/", headers={main.LOCAL_AUTH_HEADER: "supplied-token"}
    )

    assert response.status_code == 200
    assert calls == [("supplied-token", TOKEN)]


def test_empty_configured_token_fails_closed(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv(main.LOCAL_AUTH_ENV, "")

    response = app.test_client().get("/", headers={main.LOCAL_AUTH_HEADER: ""})

    assert response.status_code == 503
    assert response.get_json() == {"error": "local_runtime_misconfigured"}


def test_absent_local_token_preserves_legacy_get(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.delenv(main.LOCAL_AUTH_ENV)

    response = app.test_client().get("/")

    assert response.status_code == 200
    assert response.get_json() == {"results": "Hello World!"}


def test_local_post_requires_content_length() -> None:
    response = app.test_client().open(
        "/",
        method="POST",
        headers=AUTH_HEADERS,
    )

    assert response.status_code == 411
    assert response.get_json()["error_code"] == "length_required"


def test_local_post_rejects_oversized_body_before_json_parsing(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    def fail(*_args, **_kwargs):
        raise AssertionError("oversized request body was parsed")

    monkeypatch.setattr(Request, "get_data", fail)
    monkeypatch.setattr(Request, "get_json", fail)
    response = app.test_client().post(
        "/",
        data=b"x" * (main.LOCAL_MAX_REQUEST_BODY_BYTES + 1),
        headers=AUTH_HEADERS,
    )

    assert response.status_code == 413
    assert response.get_json()["error_code"] == "request_too_large"


def test_local_gzip_decompression_is_bounded() -> None:
    content = b'{"padding":"' + b"a" * main.LOCAL_MAX_DECOMPRESSED_JSON_BYTES + b'"}'

    response = app.test_client().post(
        "/",
        data=_compressed_body(content),
        headers={**AUTH_HEADERS, "Content-Encoding": "gzip"},
    )

    assert response.status_code == 413
    assert response.get_json()["error_code"] == "request_too_large"


@pytest.mark.parametrize(
    "compressed",
    [
        pytest.param(gzip.compress(b"{}", mtime=0) + b"trailing", id="trailing"),
        pytest.param(b"not-gzip", id="invalid"),
        pytest.param(gzip.compress(b"{}", mtime=0)[:-2], id="truncated"),
    ],
)
def test_local_gzip_rejects_trailing_or_invalid_data(compressed: bytes) -> None:
    response = _post_compressed(compressed)

    assert response.status_code == 400
    assert response.get_json()["error_code"] == "invalid_input"
