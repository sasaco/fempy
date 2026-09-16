import argparse
import hashlib
import json
import re

from tests.support.paths import ROOT
from tests.support.repairs.triangle_reference import SAMPLE, SOURCE, completed_data


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true")
    args = parser.parse_args()
    path = ROOT / SAMPLE
    raw = path.read_bytes()
    data = json.loads(raw)
    result, proof = completed_data(data)
    text = raw.decode("utf8")
    start = re.search(r'"result"\s*:\s*', text).end()
    _, length = json.JSONDecoder().raw_decode(text[start:])
    new = (text[:start] + json.dumps(result, indent=2) + text[start + length :]).encode("utf8")
    record = dict(
        sample=SAMPLE,
        source=SOURCE,
        source_reference=proof,
        before_sha256=hashlib.sha256(raw).hexdigest(),
        after_sha256=hashlib.sha256(new).hexdigest(),
        previous_result_sha256=hashlib.sha256(
            json.dumps(data["result"], sort_keys=True).encode()
        ).hexdigest(),
    )
    if args.write and data["result"] != result:
        assert path.read_bytes() == raw
        path.write_bytes(new)
        (ROOT / "docs/report/material-nonlinear-phase4-tri1-reference-repair.json").write_text(
            json.dumps(record, indent=2) + "\n", encoding="utf8"
        )
    print(json.dumps({k: v for k, v in record.items() if k != "source_reference"}, indent=2))


if __name__ == "__main__":
    main()
