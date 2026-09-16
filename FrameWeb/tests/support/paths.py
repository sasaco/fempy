"""Stable repository paths, independent of the calling directory."""

from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "tests/data"
SOURCE_ARCHIVE = ROOT / "docs/v0"
