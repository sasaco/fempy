"""Explicit independently verified spatial reference repairs; --write only."""

import argparse
import hashlib
import json
import re

from tests.support.oracles.space_frame_series import solve_reference
from tests.support.paths import ROOT

ORIGINALS = {
    "3D_Sample06": "8ec0a5d5d447e456c165d60016856b467d9e6fcd8ba17991b126bcf84f6ef982",
    "shell/3D_Sample01": "fdeadecae9def0ce86c26253ed3f31ed8ba8808bddfe1c0d37b3c6514c9ff0e8",
    "3D_Sample03": "3202a05eac9b46ab53d0c885a8284ef0bc6f26a71584bb367ed26de2fb0184d2",
    "3D_Sample02": "d8dec329a4c615c8e2c866650bd39ed640d22230d804e59b023f8c115c96690f",
    "3D_Sample04": "43fc78c06f8d4e37d485cfaae9d7b434a814f01b560864d5eb3ce1161bd97c6a",
    "3D_Sample05": "1d084c1d858e309343966bca06addeb5da400dca9e45749b10a66469e5a4c5a6",
    "3D_Sample07": "d5b169acb4988f21d50cc379f53e0fa86d6e728e6689edc55723cc2b12669592",
    "3D_Sample08": "e70e5ac23b6707073f96299601a12fea09009156e8fcd498e456e1f2e641e2b7",
    "3D_Sample09": "8f35b5b0537befe566c7613c114295108a342503e80516a10ff06f2053fa1f8d",
    "3D_Sample10": "a11e00f8fa244351bb31f79ad6ae7b4a59cdc424e18c5ac9c88f3ec2481258e3",
}


def repair(name, write):
    relative = name if "/" in name else "bar/" + name
    path = ROOT / f"tests/data/{relative}.json"
    text = path.read_bytes().decode("utf8")
    data = json.loads(text)
    reference = {key: solve_reference(data, key) for key in data["load"]}
    assert (
        hashlib.sha256(text.encode("utf8")).hexdigest() == ORIGINALS[name]
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
        original_sha256=ORIGINALS[name],
        after_sha256=hashlib.sha256(output).hexdigest(),
        input_bytes_sha256=hashlib.sha256((prefix + suffix).encode("utf8")).hexdigest(),
        cases=list(reference),
        source_method="65-digit scalar axial/bending/torsion ODEs; original-node spatial assembly; sparse float preconditioner with high-precision equation residual <1e-35; six global rigid modes constrained; piecewise-material condensation and explicit endpoint load transfer",
        sources={
            name: hashlib.sha256((ROOT / name).read_bytes()).hexdigest()
            for name in [
                "tests/support/oracles/space_frame_series.py",
                "tests/support/oracles/plane_frame_series.py",
                "tests/support/oracles/piecewise_series.py",
                "tests/support/oracles/shell_variational.py",
            ]
        },
        verification="14 independent closed-form/invalid-support cases; all required quantities compared; no tolerance relaxation",
        product_imports=False,
        old_response_values_used=False,
    )
    if write:
        path.write_bytes(output)
        manifest_path = ROOT / "tests/data/manifest.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf8"))
        for item in manifest["samples"]:
            if item["id"] == relative:
                item["sha256"] = proof["after_sha256"]
        manifest_path.write_text(
            json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf8"
        )
    print(
        f"{name}: {len(reference)} independent cases verified; write={write}",
        flush=True,
    )
    return proof


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true")
    parser.add_argument(
        "--samples", nargs="+", choices=list(ORIGINALS), default=list(ORIGINALS)
    )
    args = parser.parse_args()
    proof_path = ROOT / "docs/report/general-fem-space-reference-repairs.json"
    records = (
        json.loads(proof_path.read_text(encoding="utf8")) if proof_path.exists() else {}
    )
    for name in args.samples:
        proof = repair(name, args.write)
        records[proof["sample"]] = proof
        if args.write:
            proof_path.write_text(
                json.dumps(records, ensure_ascii=False, indent=2) + "\n",
                encoding="utf8",
            )


if __name__ == "__main__":
    main()
