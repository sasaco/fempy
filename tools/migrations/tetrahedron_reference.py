import argparse
import hashlib
import json
import re

from tests.support.oracles.source_solid import solve_source
from tests.support.paths import ROOT
from tests.support.provenance import read_source_records
from tests.support.repairs.tetrahedron_reference import (
    MANIFEST,
    ORIGINAL_SHA256,
    SAMPLE,
    SOURCE,
    completed_data,
)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true")
    args = parser.parse_args()
    path = ROOT / SAMPLE
    raw = path.read_bytes()
    before = hashlib.sha256(raw).hexdigest()
    previous = json.loads(MANIFEST.read_text(encoding="utf8")) if MANIFEST.exists() else None
    assert before == ORIGINAL_SHA256 or (previous and before == previous["after_sha256"]), (
        "Unknown fixture revision"
    )
    source = read_source_records(ROOT / SOURCE)
    reference = solve_source(ROOT / SOURCE, input_only=True)
    data = json.loads(raw)
    fixed = completed_data(data, source, reference)
    if before != ORIGINAL_SHA256:
        assert data == fixed, "Independent reference has changed"
    original = raw.decode("utf8")
    start = re.search(r'"result"\s*:\s*', original).end()
    _, length = json.JSONDecoder().raw_decode(original[start:])
    newline = "\r\n" if "\r\n" in original else "\n"
    replacement = json.dumps(fixed["result"], ensure_ascii=False, indent=2).replace("\n", newline)
    updated = (
        raw if data == fixed else (original[:start] + replacement + original[start + length :]).encode("utf8")
    )
    record = dict(
        sample=SAMPLE,
        source=SOURCE,
        before_sha256=ORIGINAL_SHA256,
        after_sha256=hashlib.sha256(updated).hexdigest(),
        source_reference=reference,
        previous_result_sha256=(
            previous["previous_result_sha256"]
            if previous
            else hashlib.sha256(json.dumps(data["result"], sort_keys=True).encode()).hexdigest()
        ),
        reason="Complete identical .fem input solved independently with original source; incomplete .out not used as a gold source",
    )
    if args.write:
        assert path.read_bytes() == raw, "Input changed during reference generation"
        path.write_bytes(updated)
        MANIFEST.write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf8")
    print(json.dumps({k: v for k, v in record.items() if k != "source_reference"}, indent=2))


if __name__ == "__main__":
    main()
