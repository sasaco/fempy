import argparse
import hashlib
import json

from tests.support.paths import ROOT
from tests.support.repairs.shell_section_output import (
    FIELD,
    remove_field,
    without_retired,
)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true")
    args = parser.parse_args()
    records = []
    for path in sorted((ROOT / "tests/data").rglob("*.json")):
        raw = path.read_bytes()
        if ('"' + FIELD + '"').encode() not in raw:
            continue
        new = remove_field(raw.decode("utf8")).encode("utf8")
        assert json.loads(new) == without_retired(json.loads(raw))
        record = dict(
            sample=path.relative_to(ROOT).as_posix(),
            before_sha256=hashlib.sha256(raw).hexdigest(),
            after_sha256=hashlib.sha256(new).hexdigest(),
        )
        records.append(record)
        if args.write:
            path.write_bytes(new)
    if args.write and records:
        report = ROOT / "docs/report"
        (report / "material-nonlinear-phase4-schema-removal.json").write_text(
            json.dumps(
                dict(
                    reason="User retired virtual-beam shell section forces; all other fields and numeric values preserved",
                    removed_field=FIELD,
                    files=records,
                ),
                indent=2,
            )
            + "\n",
            encoding="utf8",
        )
        # Keep the earlier source-input and independent-reaction provenance
        # chains valid, explicitly recording this purely structural migration.
        for name in ("support-repairs", "tetra1-repair"):
            path = report / f"material-nonlinear-phase4-{name}.json"
            data = json.loads(path.read_text(encoding="utf8"))
            for item in data if isinstance(data, list) else [data]:
                change = next(r for r in records if r["sample"] == item["sample"])
                assert item["after_sha256"] == change["before_sha256"]
                item["before_schema_removal_sha256"] = item["after_sha256"]
                item["after_sha256"] = change["after_sha256"]
            path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf8")
    print(f"{len(records)} fixtures require schema migration")


if __name__ == "__main__":
    main()
