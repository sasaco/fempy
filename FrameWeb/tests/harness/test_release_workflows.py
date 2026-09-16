"""Release workflows must preserve the tested-artifact publication contract."""

from io import BytesIO
from pathlib import Path
import tarfile
import zipfile

import pytest

from tools.validation import check_distribution

ROOT = Path(__file__).resolve().parents[2]
TESTS = ROOT / ".github" / "workflows" / "tests.yml"
PUBLISH = ROOT / ".github" / "workflows" / "publish-pypi.yml"


def _text(path: Path) -> str:
    return path.read_text(encoding="utf-8").replace("\r\n", "\n")


def test_verification_builds_once_and_gates_every_required_check():
    workflow = _text(TESTS)

    assert "workflow_call:" in workflow
    assert workflow.count("uv build ") == 1
    assert "python -m pytest tests -q" in workflow
    assert "-m material_nonlinear -q" in workflow
    assert "python -m tools.validation.check_wiki" in workflow
    assert "tests/io/test_package_metadata.py" not in workflow  # Collected by the full suite.
    assert "check_distribution.py" in workflow
    assert "check_wheel_install.py" in workflow
    assert "needs: build_distribution" in workflow
    assert "python-package-distributions-${{ github.sha }}" in workflow
    assert "actions/upload-artifact@043fb46d" in workflow
    assert "actions/download-artifact@3e5f45b" in workflow
    for python in ("'3.11'", "'3.12'", "'3.13'"):
        assert python in workflow
    assert "ubuntu-latest" in workflow
    assert "windows-latest" in workflow


def test_publish_uses_only_the_successful_reusable_workflow_artifact():
    workflow = _text(PUBLISH)

    assert "branches:" not in workflow
    assert "'v[0-9]*'" in workflow
    assert "uses: ./.github/workflows/tests.yml" in workflow
    assert "needs: verification" in workflow
    assert "needs.verification.outputs.artifact_name" in workflow
    assert "needs.verification.outputs.wheel_sha256" in workflow
    assert "uv build" not in workflow
    assert "PYPI_TOKEN" not in workflow
    assert "TEST_PYPI_TOKEN" not in workflow
    assert workflow.count("id-token: write") == 1
    assert "environment:\n      name: pypi" in workflow
    assert "already exists on PyPI; refusing to skip or overwrite" in workflow
    assert "skip-existing" not in workflow
    assert "(.wheel, .sdist)" in workflow
    assert "pypa/gh-action-pypi-publish@dc37677b" in workflow
    assert "packages-dir: release/packages" in workflow


def _fake_distributions(root: Path, version: str = "1.0.2") -> Path:
    source = root / "src" / "fem"
    source.mkdir(parents=True)
    (source / "_version.py").write_text(f'__version__ = "{version}"\n', encoding="utf-8")
    packages = root / "packages"
    packages.mkdir()
    wheel = packages / f"fempython-{version}-py3-none-any.whl"
    metadata = (
        "Metadata-Version: 2.4\n"
        "Name: FEMPython\n"
        f"Version: {version}\n"
        "Requires-Python: >=3.11\n"
        "Description-Content-Type: text/markdown\n\n"
    )
    with zipfile.ZipFile(wheel, "w") as archive:
        archive.writestr(f"fempython-{version}.dist-info/METADATA", metadata)
        archive.writestr("fem/__init__.py", "")
        archive.writestr("app/__init__.py", "")

    sdist = packages / f"fempython-{version}.tar.gz"
    with tarfile.open(sdist, "w:gz") as archive:
        for name, content in {
            f"fempython-{version}/pyproject.toml": b"[project]\n",
            f"fempython-{version}/src/fem/_version.py": f'__version__ = "{version}"\n'.encode(),
        }.items():
            info = tarfile.TarInfo(name)
            info.size = len(content)
            archive.addfile(info, BytesIO(content))
    return packages


def test_distribution_manifest_rejects_changed_bytes_or_commit(tmp_path, monkeypatch):
    packages = _fake_distributions(tmp_path)
    monkeypatch.setattr(check_distribution, "ROOT", tmp_path)
    manifest_path = tmp_path / "release-manifest.json"
    manifest = check_distribution.build_manifest(packages, "abc123")
    manifest_path.write_text(check_distribution.json.dumps(manifest), encoding="utf-8")

    assert check_distribution.verify_manifest(packages, manifest_path, "abc123") == manifest
    with pytest.raises(ValueError, match="provenance mismatch"):
        check_distribution.verify_manifest(packages, manifest_path, "different-commit")

    wheel = packages / str(manifest["wheel"]["filename"])
    with zipfile.ZipFile(wheel, "a") as archive:
        archive.writestr("tampered.txt", "changed")
    with pytest.raises(ValueError, match="provenance mismatch"):
        check_distribution.verify_manifest(packages, manifest_path, "abc123")
