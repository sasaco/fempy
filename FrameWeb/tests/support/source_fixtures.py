"""Reject unrelated input and unknown gold values before completing references."""

import pytest

from tests.support.oracles.source_solid import solve_source
from tests.support.paths import ROOT
from tests.support.provenance import read_source_records


@pytest.fixture(scope="module")
def source_reference():
    path = ROOT / "docs/v0/testdata/bend/sampleBendHexa1.out"
    return read_source_records(path), solve_source(path)


"""Independent source-input solves must not need or trust stored displacements."""


import json

import pytest


@pytest.fixture(scope="module")
def tetra_reference():
    from tests.support.paths import ROOT
    from tests.support.provenance import read_source_records
    from tests.support.repairs.tetrahedron_reference import SAMPLE, SOURCE

    return (
        json.loads((ROOT / SAMPLE).read_text(encoding="utf8")),
        read_source_records(ROOT / SOURCE),
        solve_source(ROOT / SOURCE, input_only=True),
    )
