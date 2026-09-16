"""Distribution metadata and public entry-point contracts."""

from importlib import metadata
from pathlib import Path
import tomllib

import fem
import main


def test_distribution_and_runtime_versions_have_one_source():
    root = Path(__file__).resolve().parents[2]
    project = tomllib.loads((root / "pyproject.toml").read_text(encoding="utf-8"))["project"]

    assert "version" not in project
    assert project["dynamic"] == ["version"]
    assert metadata.version("FEMPython") == fem.__version__
    assert not (root / "setup.py").exists()


def test_legacy_http_function_name_is_a_compatibility_alias():
    assert main.FrameWeb3 is main.FEMPython
