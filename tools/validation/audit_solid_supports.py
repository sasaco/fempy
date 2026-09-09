"""Audit independent source-derived reaction candidates, never overwrite fixtures."""

import argparse
import contextlib
import hashlib
import io
import json
from pathlib import Path

from fem.model import FemModel
from tests.support.assertions import comparison_errors
from tests.support.oracles.source_solid import solve_source
from tests.support.paths import ROOT
from tests.support.provenance import read_source_records
from tests.support.repairs.solid_sources import STEMS, repaired_data
from tests.support.section_cut_view import section_cut_result_view


def main(output):
    records = []
    for stem in STEMS:
        source = ROOT / "docs/v0/testdata/bend" / (stem + ".out")
        path = ROOT / "tests/data/bend" / (stem + ".json")
        raw = path.read_bytes()
        data = json.loads(raw)
        repaired_data(data, read_source_records(source))  # Exact source/input equivalence guard.
        reference = solve_source(source, decimal_stiffness=stem.endswith("2"))
        with contextlib.redirect_stdout(io.StringIO()):
            model = FemModel()
            model.load_model(str(path))
            result = model.run()
        actual = section_cut_result_view(result, model, data)["reac"]
        errors = comparison_errors(actual, reference["reac"])
        records.append(
            dict(
                sample=path.relative_to(ROOT).as_posix(),
                input_sha256=hashlib.sha256(raw).hexdigest(),
                source_reference=reference,
                strict_mismatches=len(errors),
                examples=errors[:8],
                maximum_absolute_difference=max(
                    abs(v - reference["reac"][n][k]) for n, row in actual.items() for k, v in row.items()
                ),
                status="match" if not errors else "cross_implementation_precision_unresolved",
            )
        )
        assert path.read_bytes() == raw
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(records, ensure_ascii=False, indent=2) + "\n", encoding="utf8")
    for r in records:
        print(
            r["sample"],
            r["strict_mismatches"],
            r["maximum_absolute_difference"],
            r["source_reference"]["maximum_free_force_residual"],
        )


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=Path("tmp/solid-support-audit.json"))
    main(parser.parse_args().output)
