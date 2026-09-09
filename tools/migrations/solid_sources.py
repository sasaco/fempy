import argparse
import hashlib
import json

from tests.support.paths import ROOT
from tests.support.provenance import read_source_records
from tests.support.repairs.solid_sources import STEMS, repaired_data


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true")
    args = parser.parse_args()
    changes, evidence = [], []
    for stem in STEMS:
        path = ROOT / "tests/data/bend" / (stem + ".json")
        source = read_source_records(ROOT / "docs/v0/testdata/bend" / (stem + ".out"))
        raw = path.read_bytes()
        data = json.loads(raw)
        fixed = repaired_data(data, source)
        changes.append((path, fixed, raw))
        evidence.append(
            dict(
                sample=path.relative_to(ROOT).as_posix(),
                source=source["path"],
                source_sha256=source["sha256"],
                before_sha256=hashlib.sha256(raw).hexdigest(),
                restored_elements=0 if data.get("solid") else len(fixed["solid"]),
                source_displacement_nodes=len(source["displacements"]),
                missing_reference_fields=sorted(
                    {"reac", "fsec", "size", "shell_results"} - fixed["result"]["1"].keys()
                ),
            )
        )
    # All checks must pass before any file is touched.
    if args.write:
        assert all(path.read_bytes() == raw for path, _, raw in changes), "Input changed during repair"
        for path, data, _ in changes:
            path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        for item in evidence:
            item["after_sha256"] = hashlib.sha256((ROOT / item["sample"]).read_bytes()).hexdigest()
        (ROOT / "docs/report/material-nonlinear-phase4-source-repairs.json").write_text(
            json.dumps(evidence, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
        )
    print(json.dumps(evidence, ensure_ascii=True, indent=2))


if __name__ == "__main__":
    main()
