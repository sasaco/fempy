"""integration / beam foundation contracts."""

import numpy as np
import pytest

from tests.support.builders.linear_frame import cantilever, run

pytestmark = pytest.mark.integration


@pytest.mark.material_nonlinear
def test_distributed_axial_foundation_exact_solution():
    d = cantilever()
    d["load"]["1"] = dict(load_node=[dict(n=2, tx=3)])
    d["fix_member"] = {"1": [dict(m=1, tx=500)]}
    _, r = run(d)
    # EA u''=k u; u(0)=0; EA u'(L)=F.
    a = np.sqrt(500 / 2000)
    assert r["node_displacements"][2]["dx"] == pytest.approx(3 * np.tanh(2 * a) / (2000 * a), abs=1e-12)


@pytest.mark.material_nonlinear
def test_foundation_constant_patch_and_linear_nonlinear_route():
    d = cantilever()
    d["fix_node"]["1"].append(dict(n=2, tx=1, ty=1, tz=1, rx=1, ry=1, rz=1))
    d["boundary_conditions"] = {
        "restraints": {str(n): dict(dof=[True] * 6, values=[0, 0.002, 0, 0, 0, 0]) for n in (1, 2)}
    }
    d["fix_member"] = {"1": [dict(m=1, ty=500)]}
    d["load"]["1"] = dict(load_member=[dict(m=1, mark=2, direction="y", P1=1, P2=1)])
    for analysis_type in ("static", "material_nonlinear"):
        d["analysis_type"] = analysis_type
        _, r = run(d)
        steps = r.get("step_results", [r])
        for step in steps:
            for forces in step["element_stresses"].values():
                np.testing.assert_allclose(forces["i_end"], 0, atol=1e-9)
                np.testing.assert_allclose(forces["j_end"], 0, atol=1e-9)
