"""validation / source solid reference contracts."""

import json
import subprocess
from decimal import Decimal as D
from decimal import localcontext
from subprocess import CalledProcessError

import numpy as np
import pytest

from tests.support.builders.quadratic_solid import CASES
from tests.support.oracles.source_solid import solve_source
from tests.support.oracles.source_solid_decimal import inverse, stiffness
from tests.support.paths import ROOT

pytestmark = [pytest.mark.oracle, pytest.mark.requires_node]


@pytest.mark.parametrize("kind,coords,volume", CASES)
def test_original_shape_stiffness_has_exact_affine_work_and_rigid_modes(kind, coords, volume):
    source_kind = {"tetra2": "TetraElement2", "wedge2": "WedgeElement2", "hexa2": "HexaElement2"}[kind]
    k = stiffness(source_kind, tuple(tuple(float(v) for v in r) for r in coords), 1000.0, 0.25)
    with localcontext() as ctx:
        ctx.prec = 50
        # exx=1 gives sxx=1200 and energy=600*V, independently of interpolation.
        u = [D(str(v)) for row in coords for v in (row[0], 0, 0)]
        energy = sum(u[i] * sum(a * b for a, b in zip(row, u)) for i, row in enumerate(k)) / 2
        exact_volume = {"tetra2": D(1) / 6, "wedge2": D(1), "hexa2": D(8)}[kind]
        assert abs(energy - 600 * exact_volume) < D("1e-35")
        for axis in np.eye(3):
            for field in (np.tile(axis, (len(coords), 1)), np.cross(axis, coords)):
                rigid = [D(str(float(v))) for v in field.ravel()]
                assert max(abs(sum(a * b for a, b in zip(row, rigid))) for row in k) < D("1e-35")


def test_decimal_reference_inverse_on_skew_jacobian():
    with localcontext() as ctx:
        ctx.prec = 50
        j = [[D(v) for v in r] for r in [[2, 1, 3], [1, 4, 2], [0, 2, 5]]]
        inv, det = inverse(j)
        assert det == 33
        for i in range(3):
            for c in range(3):
                assert abs(sum(j[i][a] * inv[a][c] for a in range(3)) - int(i == c)) < D("1e-45")


def test_source_input_only_tetra_solves_hand_axial_patch(tmp_path):
    source = tmp_path / "patch.fem"
    source.write_text(
        """Material 1 1000 0 500 1 0 0
Node 1 0 0 0
Node 2 1 0 0
Node 3 0 1 0
Node 4 0 0 1
TetraElement1 1 1 1 2 3 4
Restraint 1 1 0 1 0 1 0
Restraint 2 0 0 1 0 1 0
Restraint 3 1 0 1 0 1 0
Restraint 4 1 0 1 0 1 0
Load 2 .5 0 0
""",
        encoding="utf8",
    )
    before = source.read_bytes()
    ref = solve_source(source, input_only=True)
    assert ref["disg"]["2"]["dx"] == pytest.approx(0.003, rel=1e-13)
    assert ref["reac"]["1"]["tx"] == pytest.approx(-0.5, abs=1e-13)
    assert ref["maximum_free_force_residual"] < 1e-13
    assert "original_reac" not in ref
    assert source.read_bytes() == before


@pytest.mark.parametrize("record", ["Unknown 1 2", "Load 9 1 0 0", "Node 1 0 0 0"])
def test_source_input_rejects_unsupported_dangling_and_duplicate_records(tmp_path, record):
    source = tmp_path / "invalid.fem"
    source.write_text(
        """Material 1 1000 0 500 1 0 0
Node 1 0 0 0
Node 2 1 0 0
Node 3 0 1 0
Node 4 0 0 1
TetraElement1 1 1 1 2 3 4
Restraint 1 1 0 1 0 1 0
"""
        + record
        + "\n",
        encoding="utf8",
    )
    with pytest.raises(CalledProcessError):
        solve_source(source, input_only=True)


def test_original_source_tetra_uniform_axial_stress_supports(tmp_path):
    # Unit tetra, nu=0, exx=.003, E=1000: sxx=3; nodal force = V*B.T*s.
    path = tmp_path / "patch.out"
    path.write_text(
        """Node 1 0 0 0
Node 2 1 0 0
Node 3 0 1 0
Node 4 0 0 1
Material 1 1000 0 500 1 0 0
TetraElement1 1 1 1 2 3 4
Restraint 1 1 0 1 0 1 0
Restraint 2 1 .003 1 0 1 0
Restraint 3 1 0 1 0 1 0
Restraint 4 1 0 1 0 1 0
Displacement 1 0 0 0 0 0 0
Displacement 2 .003 0 0 0 0 0
Displacement 3 0 0 0 0 0 0
Displacement 4 0 0 0 0 0 0
""",
        encoding="utf8",
    )
    raw = subprocess.check_output(
        ["node", str((ROOT / "tests/support/oracles/source_solid.cjs")), str(path)],
        text=True,
        encoding="utf8",
    )
    reference = json.loads(raw)
    for node, force in [("1", -0.5), ("2", 0.5), ("3", 0.0), ("4", 0.0)]:
        assert reference["reac"][node] == pytest.approx(
            dict(tx=force, ty=0.0, tz=0.0, mx=0.0, my=0.0, mz=0.0), abs=1e-13
        )
    assert reference["maximum_free_force_residual"] == 0.0
    assert len(reference["hashes"]) == 9


def test_original_source_rejects_partial_output_without_echo():
    result = subprocess.run(
        [
            "node",
            str((ROOT / "tests/support/oracles/source_solid.cjs")),
            "docs/v0/testdata/bend/sampleBendTetra1.out",
        ],
        capture_output=True,
        text=True,
    )
    assert result.returncode != 0
    assert "Complete source input echo" in result.stderr


@pytest.mark.parametrize("axis", range(3))
@pytest.mark.parametrize("force", [0.5, -2.0])
def test_refined_source_solves_load_not_inaccurate_output_displacement(tmp_path, axis, force):
    from tests.support.oracles.source_solid import solve_source

    text = [
        "Node 1 0 0 0",
        "Node 2 1 0 0",
        "Node 3 0 1 0",
        "Node 4 0 0 1",
        "Material 1 1000 0 500 1 0 0",
        "TetraElement1 1 1 1 2 3 4",
    ]
    # Each selected axis has a free vertex; all other DOFs are prescribed.
    tip = axis + 2
    for n in range(1, 5):
        rest = [1, 0] * 3
        if n == tip:
            rest[2 * axis] = 0
        text += [f"Restraint {n} " + " ".join(map(str, rest)), f"Displacement {n} .123 .456 .789 0 0 0"]
    load = [0.0, 0.0, 0.0]
    load[axis] = force
    text += [f"Load {tip} " + " ".join(map(str, load))]
    source = tmp_path / "patch.out"
    source.write_text("\n".join(text), encoding="utf8")
    before = source.read_bytes()
    ref = solve_source(source)
    # For nu=0, axial stress gives the opposite reaction at the origin.
    key = ("tx", "ty", "tz")[axis]
    assert ref["reac"]["1"][key] == pytest.approx(-force, rel=1e-13, abs=1e-13)
    assert ref["reac"][str(tip)][key] == 0.0
    assert ref["maximum_free_force_residual"] < 1e-13
    assert ref["refinement_iterations"] > 0
    assert source.read_bytes() == before
