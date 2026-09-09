"""Keep repaired input bytes, reference bytes and independent sources linked."""

import hashlib
import json
import re

import pytest

from tests.support.paths import ROOT

pytestmark = pytest.mark.unit


@pytest.mark.parametrize(
    "report",
    [
        "general-fem-plane-reference-repair.json",
        "general-fem-space-reference-repairs.json",
        "general-fem-triangle-reference-repairs.json",
        "general-fem-shell-reference-repairs.json",
    ],
)
def test_repaired_reference_input_and_source_hashes_are_current(report):
    document = json.loads((ROOT / "docs/report" / report).read_text(encoding="utf8"))
    records = document.values() if isinstance(document, dict) else document
    for record in records:
        text = (ROOT / record["sample"]).read_bytes().decode("utf8")
        assert hashlib.sha256(text.encode("utf8")).hexdigest() == record["after_sha256"]
        match = re.search(r'"result"\s*:\s*', text)
        assert match
        _, size = json.JSONDecoder().raw_decode(text[match.end() :])
        input_bytes = (text[: match.end()] + text[match.end() + size :]).encode("utf8")
        assert hashlib.sha256(input_bytes).hexdigest() == record["input_bytes_sha256"]
        sources = {
            **record.get("sources", {}),
            **record.get("auxiliary_sources", {}),
            **record.get("source_reference", {}).get("hashes", {}),
        }
        if "source_file" in record:
            sources[record["source_file"]] = record["source_sha256"]
        if "source_input" in record:
            sources[record["source_input"]] = record["source_input_sha256"]
        assert sources
        for path, digest in sources.items():
            assert hashlib.sha256((ROOT / path).read_bytes()).hexdigest() == digest, (
                path
            )
