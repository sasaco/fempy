"""Explicit replacement of incomplete Tetra1 gold by a source-input original source solve.

Default is read-only. --write preserves all input bytes. Neither this module
nor its reference dependencies import the production FEM solver.
"""

import copy

from tests.support.paths import ROOT
from tests.support.repairs.solid_sources import assert_source_input

SAMPLE = "tests/data/bend/sampleBendTetra1.json"

SOURCE = "docs/v0/testdata/bend/sampleBendTetra1.fem"

ORIGINAL_SHA256 = "e208edfae118c637fb858593495791bc1596c840e26b884e32d5d7bfc1348e16"

SOURCE_SHA256 = "c27e8c496dfb09cb50ab261869bd212c325086ad67f668c82f57e835bd1608a6"

MANIFEST = ROOT / "docs/report/material-nonlinear-phase4-tetra1-repair.json"


def completed_data(data, source, reference):
    topology = assert_source_input(data, source)
    assert source["sha256"] == SOURCE_SHA256
    assert data.get("solid") and len(topology) == 2160
    assert reference.get("input_only") is True
    assert reference["hashes"][source["path"]] == source["sha256"]
    assert reference["maximum_free_force_residual"] <= 1e-10
    assert set(reference["disg"]) == set(reference["node_ids"]) == set(source["nodes"])
    assert set(reference["reac"]) == set(source["restraints"])
    result = dict(
        disg=reference["disg"], reac=reference["reac"], size=len(source["nodes"]), fsec={}, shell_results={}
    )
    fixed = copy.deepcopy(data)
    fixed["result"] = {"1": result}
    return fixed
