"""integration / linear beam contracts."""

import numpy as np
import pytest

from tests.support.builders.linear_frame import cantilever, run

pytestmark = pytest.mark.integration


@pytest.mark.material_nonlinear
def test_legacy_bernoulli_section_and_cg():
    d = cantilever()
    _, r = run(d)
    assert r["node_displacements"][2]["dy"] == pytest.approx(3 * 8 / (3 * 1000 * 4), abs=1e-12)
    d["member"]["1"]["cg"] = 90
    _, r = run(d)
    assert r["node_displacements"][2]["dy"] == pytest.approx(3 * 8 / (3 * 1000 * 3), abs=1e-12)


@pytest.mark.material_nonlinear
def test_generated_point_coordinates_keep_submillimetre_precision():
    d = cantilever()
    x = 0.123456789012345
    d["notice_points"] = [dict(m=1, Points=[x])]
    model, result = run(d)
    generated = next(n for n in model.mesh.nodes if n not in (1, 2))
    assert model.mesh.nodes[generated][0] == x
    assert result["node_displacements"][generated]["dy"] == pytest.approx(
        3 * x * x * (6 - x) / (6 * 4000), rel=1e-8, abs=1e-10
    )


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("q0,q1", [(3, 3), (0, 6), (6, 0)])
def test_uniform_and_triangular_load_consistent_forces(q0, q1):
    d = cantilever()
    d["load"]["1"] = dict(load_member=[dict(m=1, mark=2, direction="y", P1=q0, P2=q1)])
    _, r = run(d)
    # Unit-load integration of M(x)*(L-x)/EI; triangle rising towards tip.
    expected = 2**4 * (4 * q0 + 11 * q1) / (120 * 1000 * 4)
    assert r["node_displacements"][2]["dy"] == pytest.approx(expected, abs=1e-12)
    f = r["element_stresses"][1]
    np.testing.assert_allclose(f["j_end"], 0, atol=1e-10)
    assert f["i_end"][1] == pytest.approx(-(q0 + q1), abs=1e-10)
    assert f["i_end"][5] == pytest.approx(-4 * (q0 + 2 * q1) / 6, abs=1e-10)


@pytest.mark.material_nonlinear
def test_released_tip_propped_beam():
    d = cantilever()
    d["fix_node"]["1"].append(dict(n=2, ty=1, rz=1))
    d["joint"] = {"1": [dict(m=1, zi=1, zj=0)]}
    d["load"]["1"] = dict(load_member=[dict(m=1, mark=2, direction="y", P1=8, P2=8)])
    _, r = run(d)
    assert r["reaction_forces"][1]["fy"] == pytest.approx(-10, abs=1e-9)
    assert r["reaction_forces"][2]["fy"] == pytest.approx(-6, abs=1e-9)
    assert r["element_stresses"][1]["j_end"][5] == pytest.approx(0, abs=1e-9)
    assert r["element_stresses"][1]["i_end"][5] == pytest.approx(-4, abs=1e-9)


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("k", [0.5, 20, 2000])
def test_legacy_support_spring_is_not_prescribed_motion(k):
    d = cantilever()
    d["fix_node"]["1"].append(dict(n=2, ty=k))
    _, r = run(d)
    u = 3 / (3 * 1000 * 4 / 8 + k)
    assert r["node_displacements"][2]["dy"] == pytest.approx(u, abs=1e-12)
    assert r["reaction_forces"][2]["fy"] == pytest.approx(-k * u, abs=1e-9)


@pytest.mark.material_nonlinear
def test_rigid_zone_uses_its_assigned_section():
    d = cantilever()
    d["element"]["1"]["1"].update(E=2000, Iz=4)
    d["rigid"] = [dict(m=1, e=1, Ilength=1, Jlength=0)]
    _, r = run(d)
    # Integral F*(L-x)^2/EI across [0,1] and [1,2].
    assert r["node_displacements"][2]["dy"] == pytest.approx(7 / 8000 + 1 / 4000, abs=1e-12)


@pytest.mark.material_nonlinear
def test_point_load_and_negative_width_split():
    d = cantilever()
    d["load"]["1"] = dict(load_member=[dict(m=1, mark=1, direction="y", P1=3, L1=1)])
    _, r = run(d)
    assert r["node_displacements"][2]["dy"] == pytest.approx(3 * 1**2 * (6 - 1) / (6 * 4000), abs=1e-12)
    d["load"]["1"] = dict(load_member=[dict(m=1, mark=2, direction="y", P1=3, P2=3, L1=0.5, L2=-1)])
    _, r = run(d)
    # Integral q*a^2*(3L-a)/(6EI) da for a=.5..1.5.
    primitive = lambda a: 3 * (2 * a**3 - a**4 / 4) / (6 * 4000)
    assert r["node_displacements"][2]["dy"] == pytest.approx(primitive(1.5) - primitive(0.5), abs=1e-12)


@pytest.mark.material_nonlinear
def test_public_distributed_load_and_joint_apis():
    d = cantilever()
    d["load"]["1"] = {}
    m, _ = run(d)
    m.add_distributed_load(1, "local_y", 8, 8)
    m.add_restraint(2, dy=True, rz=True)
    m.add_joint_condition(1, zj=0)
    r = m.run()
    assert r["reaction_forces"][1]["fy"] == pytest.approx(-10, abs=1e-9)
    assert r["element_stresses"][1]["j_end"][5] == pytest.approx(0, abs=1e-9)
    r2 = m.run()
    np.testing.assert_allclose(r2["displacement"], r["displacement"], atol=1e-14)


@pytest.mark.material_nonlinear
def test_timoshenko_uniform_load_includes_shear_and_fixed_end_forces():
    d = cantilever()
    d["member"]["1"]["shear_correction"] = True
    d["element"]["1"]["2"]["G"] = 100
    d["load"]["1"] = dict(load_member=[dict(m=1, mark=2, direction="y", P1=3, P2=3)])
    _, r = run(d)
    assert r["node_displacements"][2]["dy"] == pytest.approx(
        3 * 16 / (8 * 4000) + 3 * 4 / (2 * 100 * 2 * 5 / 6), abs=1e-12
    )
    np.testing.assert_allclose(r["element_stresses"][1]["j_end"], 0, atol=1e-10)


@pytest.mark.material_nonlinear
def test_fully_released_unloaded_rotation_is_absent_not_stabilized():
    d = cantilever()
    d["fix_node"]["1"].append(dict(n=2, ty=1))
    d["joint"] = {"1": [dict(m=1, zj=0)]}
    d["load"]["1"] = dict(load_member=[dict(m=1, mark=2, direction="y", P1=8, P2=8)])
    _, r = run(d)
    assert r["reaction_forces"][1]["fy"] == pytest.approx(-10, abs=1e-9)
    assert r["reaction_forces"][2]["fy"] == pytest.approx(-6, abs=1e-9)


@pytest.mark.material_nonlinear
def test_split_roundoff_and_other_case_subdivisions_keep_loads_separate():
    d = cantilever()
    d["rigid"] = [dict(m=1, e=2, Jlength=0.2)]
    d["notice_points"] = [dict(m=1, Points=[1.8000000000000003])]
    d["load"]["2"] = dict(load_member=[dict(m=1, mark=1, L1=1, P1=999, direction="y")])
    m, r = run(d)
    assert len(m.mesh.nodes) == 4
    assert min(e.length for e in m.elements.values()) > 0.1
    assert r["node_displacements"][2]["dy"] == pytest.approx(0.002, abs=1e-12)


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("restrained", [False, True])
def test_temperature_load_free_expansion_and_restrained_force(restrained):
    d = cantilever()
    d["element"]["1"]["2"]["Xp"] = 1e-5
    d["load"]["1"] = {}
    m, _ = run(d)
    m.add_temperature_load(1, 20)
    if restrained:
        m.add_restraint(2, dx=True)
    r = m.run()
    assert r["node_displacements"][2]["dx"] == pytest.approx(0 if restrained else 0.0004, abs=1e-12)
    assert r["element_stresses"][1]["j_end"][0] == pytest.approx(-0.4 if restrained else 0, abs=1e-10)
