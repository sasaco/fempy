"""Repair explicitly verified DKT references; writing requires --write."""

import argparse
import hashlib
import json
import re

from tests.support.oracles.triangle_sample import independent_sample
from tests.support.paths import ROOT

ORIGINAL = {
    "shellRibTri1": "9297095c5819e6d3daf717d7c0218c3099bb748cfbf3094db74549e68d614b04",
    "shellTensTorTri1": "b7c161aa2cbd94cf2fc1ba795849c07756cec3fc689f18a2859ae2447eeb462b",
    "shellBeamTri1": "b83b14e84e3bfb717f93bf424e8746afa5a01f7c33069ca72c9b79114cb66a4d",
    "shellThickBeamTri1": "4105d48e95418810b61554517288bffe683998f2e2536771146d693d6a1744c9",
}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true")
    args = parser.parse_args()
    manifest_path = ROOT / "tests/data/manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf8"))
    records = []
    pending = []
    for name, original_hash in ORIGINAL.items():
        path = ROOT / f"tests/data/shell/{name}.json"
        raw = path.read_bytes()
        text = raw.decode("utf8")
        data = json.loads(text)
        reference, proof = independent_sample(
            data,
            ROOT / f"docs/v0/testdata/shell/{name}.out",
            spin=name in ("shellRibTri1", "shellTensTorTri1"),
        )
        before = hashlib.sha256(raw).hexdigest()
        assert before == original_hash or data["result"] == {"1": reference}, (
            "Unknown reference revision"
        )
        match = re.search(r'"result"\s*:\s*', text)
        assert match
        _, length = json.JSONDecoder().raw_decode(text[match.end() :])
        prefix, suffix = text[: match.end()], text[match.end() + length :]
        content = (
            prefix + json.dumps({"1": reference}, ensure_ascii=False, indent=2) + suffix
        ).encode("utf8")
        after = hashlib.sha256(content).hexdigest()
        # Byte-level preservation of every input field, not merely equal floats.
        input_hash = hashlib.sha256((prefix + suffix).encode("utf8")).hexdigest()
        pending.append((path, content))
        for item in manifest["samples"]:
            if item["id"] == f"shell/{name}":
                item["sha256"] = after
        records.append(
            dict(
                sample=path.relative_to(ROOT).as_posix(),
                original_sha256=original_hash,
                after_sha256=after,
                input_bytes_sha256=input_hash,
                reason="Stale embedded results disagree with original-input independent DKT solve; all required fields retained.",
                source_reference=proof,
            )
        )
    if args.write:
        for path, content in pending:
            path.write_bytes(content)
        manifest_path.write_text(
            json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf8"
        )
        (ROOT / "docs/report/general-fem-triangle-reference-repairs.json").write_text(
            json.dumps(records, ensure_ascii=False, indent=2) + "\n", encoding="utf8"
        )
    print(f"{len(records)} independent references verified; write={args.write}")


if __name__ == "__main__":
    main()
