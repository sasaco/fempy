import argparse
import hashlib
import json

from tests.support.oracles.source_solid import solve_source
from tests.support.paths import ROOT
from tests.support.provenance import read_source_records
from tests.support.repairs.support_reactions import STEMS, completed_reference


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true")
    args = parser.parse_args()
    changes, records = [], []
    output = ROOT / "docs/report/material-nonlinear-phase4-support-repairs.json"
    prior = json.loads(output.read_text(encoding="utf8")) if output.exists() else []
    for stem in STEMS:
        source_path = ROOT / "docs/v0/testdata/bend" / (stem + ".out")
        source = read_source_records(source_path)
        independent = solve_source(source_path, decimal_stiffness=stem.endswith("2"))
        path = ROOT / "tests/data/bend" / (stem + ".json")
        raw = path.read_bytes()
        fixed = completed_reference(json.loads(raw), source, independent)
        changes.append((path, raw, fixed))
        records.append(
            dict(
                sample=path.relative_to(ROOT).as_posix(),
                before_sha256=hashlib.sha256(raw).hexdigest(),
                source_reference=independent,
                fields_added=["reac", "size", "fsec", "shell_results"],
                empty_field_reason="Source/input identity proves a solid-only model; size is source node count",
            )
        )
        previous = next(
            (
                item
                for item in prior
                if item["sample"] == records[-1]["sample"]
                and item["after_sha256"] == records[-1]["before_sha256"]
            ),
            None,
        )
        if previous and json.loads(raw) == fixed:
            records[-1]["before_sha256"] = previous["before_sha256"]
    if args.write:
        assert all(path.read_bytes() == raw for path, raw, _ in changes), (
            "Input changed during reference generation"
        )
        for path, raw, fixed in changes:
            # Replace only the result object. Input text, displacement values,
            # and line endings are preserved.
            original = raw.decode("utf8")
            data = json.loads(original)
            if data == fixed:
                continue
            decoder = json.JSONDecoder()
            import re

            match = re.search(r'"result"\s*:\s*', original)
            start = match.end()
            _, length = decoder.raw_decode(original[start:])
            newline = "\r\n" if "\r\n" in original else "\n"
            replacement = json.dumps(fixed["result"], ensure_ascii=False, indent=2).replace("\n", newline)
            path.write_bytes((original[:start] + replacement + original[start + length :]).encode("utf8"))
        for record, (path, _, _) in zip(records, changes):
            record["after_sha256"] = hashlib.sha256(path.read_bytes()).hexdigest()
        output.write_text(json.dumps(records, ensure_ascii=False, indent=2) + "\n", encoding="utf8")
    print(
        json.dumps(
            [{k: v for k, v in record.items() if k != "source_reference"} for record in records], indent=2
        )
    )


if __name__ == "__main__":
    main()
