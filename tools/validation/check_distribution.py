"""Validate built distributions and record their immutable release provenance."""
from __future__ import annotations

import argparse
import ast
from email.parser import BytesParser
from email.policy import default
import hashlib
import json
from pathlib import Path
import tarfile
import zipfile

ROOT = Path(__file__).resolve().parents[2]
PROJECT_NAME = "FEMPython"
NORMALIZED_NAME = "fempython"


def _project_version() -> str:
    tree = ast.parse((ROOT / "src" / "fem" / "_version.py").read_text(encoding="utf-8"))
    for node in tree.body:
        if isinstance(node, ast.Assign) and any(
            isinstance(target, ast.Name) and target.id == "__version__" for target in node.targets
        ):
            value = ast.literal_eval(node.value)
            if isinstance(value, str) and value:
                return value
    raise ValueError("src/fem/_version.py must define a non-empty literal __version__")


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _artifact(path: Path) -> dict[str, object]:
    return {"filename": path.name, "sha256": _sha256(path), "size": path.stat().st_size}


def _distribution_files(packages_dir: Path, version: str) -> tuple[Path, Path]:
    wheels = sorted(packages_dir.glob("*.whl"))
    sdists = sorted(packages_dir.glob("*.tar.gz"))
    if len(wheels) != 1 or len(sdists) != 1:
        raise ValueError(
            f"expected exactly one wheel and one sdist in {packages_dir}, "
            f"found {len(wheels)} wheel(s) and {len(sdists)} sdist(s)"
        )
    expected_wheel = f"{NORMALIZED_NAME}-{version}-py3-none-any.whl"
    expected_sdist = f"{NORMALIZED_NAME}-{version}.tar.gz"
    if wheels[0].name != expected_wheel or sdists[0].name != expected_sdist:
        raise ValueError(
            f"unexpected distribution names: {wheels[0].name!r}, {sdists[0].name!r}; "
            f"expected {expected_wheel!r}, {expected_sdist!r}"
        )
    return wheels[0], sdists[0]


def _check_wheel(wheel: Path, version: str) -> None:
    with zipfile.ZipFile(wheel) as archive:
        names = archive.namelist()
        metadata_names = [name for name in names if name.endswith(".dist-info/METADATA")]
        if len(metadata_names) != 1:
            raise ValueError(f"{wheel.name}: expected one METADATA file")
        metadata = BytesParser(policy=default).parsebytes(archive.read(metadata_names[0]))
    expected = {
        "Name": PROJECT_NAME,
        "Version": version,
        "Requires-Python": ">=3.11",
        "Description-Content-Type": "text/markdown",
    }
    for field, value in expected.items():
        if metadata[field] != value:
            raise ValueError(f"{wheel.name}: {field} is {metadata[field]!r}, expected {value!r}")
    for required in ("fem/__init__.py", "app/__init__.py"):
        if required not in names:
            raise ValueError(f"{wheel.name}: missing public package {required}")
    if "main.py" in names:
        raise ValueError(f"{wheel.name}: repository-only HTTP entry point main.py must not be packaged")


def _check_sdist(sdist: Path, version: str) -> None:
    prefix = f"{NORMALIZED_NAME}-{version}/"
    required = {prefix + "pyproject.toml", prefix + "src/fem/_version.py"}
    with tarfile.open(sdist, "r:gz") as archive:
        names = set(archive.getnames())
    missing = sorted(required - names)
    if missing:
        raise ValueError(f"{sdist.name}: missing {missing}")


def build_manifest(packages_dir: Path, commit: str) -> dict[str, object]:
    version = _project_version()
    wheel, sdist = _distribution_files(packages_dir, version)
    _check_wheel(wheel, version)
    _check_sdist(sdist, version)
    return {
        "schema": 1,
        "commit": commit,
        "project": {"name": PROJECT_NAME, "version": version},
        "wheel": _artifact(wheel),
        "sdist": _artifact(sdist),
    }


def verify_manifest(packages_dir: Path, manifest_path: Path, commit: str) -> dict[str, object]:
    recorded = json.loads(manifest_path.read_text(encoding="utf-8"))
    actual = build_manifest(packages_dir, commit)
    if recorded != actual:
        raise ValueError(
            "distribution provenance mismatch\n"
            f"recorded={json.dumps(recorded, ensure_ascii=False, sort_keys=True)}\n"
            f"actual={json.dumps(actual, ensure_ascii=False, sort_keys=True)}"
        )
    return actual


def _append_github_outputs(path: Path, manifest: dict[str, object], artifact_name: str) -> None:
    wheel = manifest["wheel"]
    project = manifest["project"]
    assert isinstance(wheel, dict) and isinstance(project, dict)
    lines = {
        "artifact_name": artifact_name,
        "version": project["version"],
        "wheel_filename": wheel["filename"],
        "wheel_sha256": wheel["sha256"],
    }
    with path.open("a", encoding="utf-8", newline="\n") as output:
        for key, value in lines.items():
            output.write(f"{key}={value}\n")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--packages-dir", type=Path, required=True)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--write-manifest", type=Path)
    mode.add_argument("--verify-manifest", type=Path)
    parser.add_argument("--commit", required=True)
    parser.add_argument("--github-output", type=Path)
    parser.add_argument("--artifact-name")
    args = parser.parse_args()

    if args.write_manifest:
        manifest = build_manifest(args.packages_dir, args.commit)
        args.write_manifest.parent.mkdir(parents=True, exist_ok=True)
        args.write_manifest.write_text(
            json.dumps(manifest, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
            newline="\n",
        )
    else:
        manifest = verify_manifest(args.packages_dir, args.verify_manifest, args.commit)

    if args.github_output:
        if not args.artifact_name:
            parser.error("--github-output requires --artifact-name")
        _append_github_outputs(args.github_output, manifest, args.artifact_name)
    print(json.dumps(manifest, ensure_ascii=False, indent=2, sort_keys=True))


if __name__ == "__main__":
    main()
