"""Audit every registered structural sample case without updating references."""

import argparse
import json
from pathlib import Path

from tests.support.sample_audit import audit_case
from tests.support.samples import sample_cases


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=Path("tmp/sample-audit.json"))
    args = parser.parse_args()
    entries = [audit_case(path, case) for _, path, case in sample_cases()]
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(entries, ensure_ascii=False, indent=2) + "\n", encoding="utf8")
    print({status: sum(e["status"] == status for e in entries) for status in ("match", "mismatch", "error")})


if __name__ == "__main__":
    main()
