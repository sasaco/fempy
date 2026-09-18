"""Reproduce the backend/frontend result-schema mismatch through the HTTP route."""

import base64
import gzip
import json
from pathlib import Path
import sys

repo_root = Path(__file__).parents[2]
sys.path.insert(0, str(repo_root / "FrameWeb"))

from main import app  # noqa: E402


fixture_path = (
    repo_root
    / "FrameWeb"
    / "tests"
    / "data"
    / "transport"
    / "legacy-browser-envelope.json"
)
fixture = json.loads(fixture_path.read_text(encoding="utf-8"))

response = app.test_client().post(
    "/",
    data=fixture["body"],
    headers={"Content-Encoding": "gzip,base64"},
)
assert response.status_code == 200, response.get_data(as_text=True)

result = json.loads(gzip.decompress(base64.b64decode(response.data, validate=True)))
required_case_keys = {"disg", "reac", "fsec"}
legacy_cases = [
    value
    for value in result.values()
    if isinstance(value, dict) and required_case_keys <= value.keys()
]
print(
    json.dumps(
        {
            "status": response.status_code,
            "result_keys": sorted(result),
            "required_case_keys": sorted(required_case_keys),
            "legacy_case_count": len(legacy_cases),
        }
    )
)
assert legacy_cases, "backend response has no frontend-compatible result case"
