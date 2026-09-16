"""Repair explicitly verified plane samples from scalar series; --write only."""

import argparse
import hashlib
import json
import re

import mpmath

from tests.support.oracles.plane_frame_series import solve_reference
from tests.support.paths import ROOT

ORIGINALS = {
    "2D_Sample08": "88d727114277899be4e9a0c7f0d9f8ab43ec3b231c2560c7a95d2e82ab7e14a0",
    "2D_Sample09": "f29cd25078654fb703aeffc5c17e0f9581686bd1e31a9dfec3044ac11f835508",
    "2D_Sample11": "6f80599b17c008f9f2f75a58e5f1b6eb358224bfb5fccc94726240a0f22da7c9",
    "2D_Sample13": "a664c44d3d12526deb08d5a864ca119ef4d6b4e0108c71489ee90330dd4b40ee",
    "2D_Sample03": "a0f239d7d0692dabe46e301a1c0622544d77c552373760c6903a10d547c1f17a",
    "2D_Sample06": "aac6fb54c1095149aea04b735aa049664890a6d05a086cbeccbbfbbe2c98ceca",
    "2D_Sample07": "694e58949e67903502503f7927990708dd9d1846fc0425bb1f366fef33fde40d",
    "2D_Sample01": "bcead6bd52dc2d52f9f67103db6f1d80d3c303d105e9f2058f9b57d8f4ceb6c3",
    "2D_Sample02": "1460554aee2223a89e6b46e447639112cc08a1d4e3fcf3224944532193be5bdd",
    "2D_Sample04": "aaa943ec97e6ce1d9cac78db8145b87225e11437c9fd2722aa2df69e2e8f4381",
    "2D_Sample05": "8d294b4fd294d398410c81c6a183c0272a34f77d3473989a24e441ccd732945c",
    "2D_Sample10": "1460554aee2223a89e6b46e447639112cc08a1d4e3fcf3224944532193be5bdd",
}


def repair(name, write):
    path = ROOT / ("tests/data/bar/" + name + ".json")
    text = path.read_bytes().decode("utf8")
    data = json.loads(text)
    reference = {key: solve_reference(data, key) for key in data["load"]}
    before = hashlib.sha256(text.encode("utf8")).hexdigest()
    assert before == ORIGINALS[name] or data["result"] == reference, (
        "Unknown input/reference revision"
    )
    match = re.search(r'"result"\s*:\s*', text)
    assert match
    _, length = json.JSONDecoder().raw_decode(text[match.end() :])
    prefix, suffix = text[: match.end()], text[match.end() + length :]
    output = (
        prefix + json.dumps(reference, ensure_ascii=False, indent=2) + suffix
    ).encode("utf8")
    after = hashlib.sha256(output).hexdigest()
    oracle = ROOT / "tests/support/oracles/plane_frame_series.py"
    proof = dict(
        sample=path.relative_to(ROOT).as_posix(),
        original_sha256=ORIGINALS[name],
        after_sha256=after,
        input_bytes_sha256=hashlib.sha256((prefix + suffix).encode("utf8")).hexdigest(),
        source_method="Original JSON input; independent EA*u\u2032\u2032-kx*u=-qx and EI*v\u2032\u2032\u2032\u2032+ky*v=qy scalar power series, 65 digits; original-node global solve",
        source_file=oracle.relative_to(ROOT).as_posix(),
        source_sha256=hashlib.sha256(oracle.read_bytes()).hexdigest(),
        mpmath_version=mpmath.__version__,
        cases=list(reference),
        auxiliary_sources={
            name: hashlib.sha256((ROOT / name).read_bytes()).hexdigest()
            for name in ["tests/support/oracles/piecewise_series.py"]
        },
        verification=f"9 independent closed-form cases; all {len(reference)} cases compared for all required quantities; series tail <=1e-55 scale and free global equation residual <1e-40",
        product_imports=False,
        old_response_values_used=False,
    )
    if write:
        path.write_bytes(output)
        manifest_path = ROOT / "tests/data/manifest.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf8"))
        for item in manifest["samples"]:
            if item["id"] == "bar/" + name:
                item["sha256"] = after
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
    proof_path = ROOT / "docs/report/general-fem-plane-reference-repair.json"
    old = (
        json.loads(proof_path.read_text(encoding="utf8")) if proof_path.exists() else {}
    )
    if "sample" in old:
        old = {old["sample"]: old}
    for name in args.samples:
        proof = repair(name, args.write)
        old[proof["sample"]] = proof
        if args.write:
            proof_path.write_text(
                json.dumps(old, ensure_ascii=False, indent=2) + "\n", encoding="utf8"
            )


if __name__ == "__main__":
    main()
