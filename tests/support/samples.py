"""Explicit saved-sample contracts; load cases and history steps are distinct."""

import json

from tests.support.paths import DATA


def registered_samples():
    manifest = json.loads((DATA / "manifest.json").read_text(encoding="utf8"))
    return manifest["samples"]


def sample_cases():
    for sample in registered_samples():
        if sample["contract"] == "section_cut_cases":
            for case in sample["cases"]:
                yield f"{sample['id']}:{case}", DATA / sample["file"], case


def history_samples():
    return [
        DATA / sample["file"] for sample in registered_samples() if sample["contract"] == "cantilever_history"
    ]
