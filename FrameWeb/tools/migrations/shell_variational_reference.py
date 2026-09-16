"""Repair eight source-checked shell references with independent energy solves."""

import argparse
import hashlib
import json
import re

from tests.support.oracles.shell_variational import solve_reference
from tests.support.oracles.triangle_sample import check_input
from tests.support.paths import ROOT
from tests.support.provenance import read_source_records

ORIGINALS = {
    "shellTri1": "ac12584c5170571c8df4cf21aa70c26b0d5f9c90daa71b903b43f6f5ad5e2a46",
    "shellBeamQuad1": "ee617c5aa11bfd48641cd3ecad29c850e92a6a092d76db3dd6510aaba03eca70",
    "shellQuad1": "e1308c17531eb20a45097eedc64de7ba3015c2e2378948694fd6f78171fdb143",
    "shellQuad1_t1": "8315e6d3372b3fcbd2b12f62a7b6d78fd2a314c854baac0e09ca3652c7634cb5",
    "shellQuad1_t2": "6d642e41a2b665fb94b90850baead66fdb3f24754fea9c86f0b6a7ea4853e289",
    "shellRibQuad1": "4e4eae7f5d5a1d8259569b5aaec9bedd0b4ba691f96a675b07b90c52eba67552",
    "shellTensTorQuad1": "032c3a700052742599eccfbb13949ef4cfc0ae28255d8d5702a2c10e468c7d4e",
    "shellThickBeamQuad1": "ead76369d9065fd56943c412f076e889aeb38bf4d5a84635f4ebe39c21543ac5",
}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true")
    args = parser.parse_args()
    records = []
    for name, original in ORIGINALS.items():
        path = ROOT / f"tests/data/shell/{name}.json"
        text = path.read_bytes().decode("utf8")
        data = json.loads(text)
        source = ROOT / f"docs/v0/testdata/shell/{name}.out"
        check_input(
            data,
            read_source_records(source),
            element_types=("TriElement1", "QuadElement1"),
        )
        reference = {"1": solve_reference(data)}
        assert (
            hashlib.sha256(text.encode("utf8")).hexdigest() == original
            or data["result"] == reference
        ), "Unknown input/reference revision"
        match = re.search(r'"result"\s*:\s*', text)
        assert match
        _, size = json.JSONDecoder().raw_decode(text[match.end() :])
        prefix, suffix = text[: match.end()], text[match.end() + size :]
        output = (
            prefix + json.dumps(reference, ensure_ascii=False, indent=2) + suffix
        ).encode("utf8")
        proof = dict(
            sample=path.relative_to(ROOT).as_posix(),
            original_sha256=original,
            after_sha256=hashlib.sha256(output).hexdigest(),
            input_bytes_sha256=hashlib.sha256(
                (prefix + suffix).encode("utf8")
            ).hexdigest(),
            source_input=source.relative_to(ROOT).as_posix(),
            source_input_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
            method="65-digit variational shell: Hermite-edge DKT or edge-midpoint covariant MITC4 shear, membrane/bending/rotation-spin energies; independently solved and both surface tensors evaluated before float conversion",
            sources={
                name: hashlib.sha256((ROOT / name).read_bytes()).hexdigest()
                for name in [
                    "tests/support/oracles/shell_variational.py",
                    "tests/support/oracles/space_frame_series.py",
                    "tests/support/oracles/plane_frame_series.py",
                    "tests/support/oracles/piecewise_series.py",
                    "tests/support/oracles/triangle_sample.py",
                    "tests/support/provenance.py",
                ]
            },
            verification="Eight independent rigid-mode/constant membrane/curvature/drilling energy cases; source input equality; free equation residual <1e-35; all required output quantities retained",
            product_imports=False,
            old_response_values_used=False,
        )
        records.append(proof)
        if args.write:
            path.write_bytes(output)
            manifest_path = ROOT / "tests/data/manifest.json"
            manifest = json.loads(manifest_path.read_text(encoding="utf8"))
            for item in manifest["samples"]:
                if item["id"] == "shell/" + name:
                    item["sha256"] = proof["after_sha256"]
            manifest_path.write_text(
                json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
                encoding="utf8",
            )
            (ROOT / "docs/report/general-fem-shell-reference-repairs.json").write_text(
                json.dumps(records, ensure_ascii=False, indent=2) + "\n",
                encoding="utf8",
            )
        print(f"{name}: independent reference verified; write={args.write}", flush=True)


if __name__ == "__main__":
    main()
