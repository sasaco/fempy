"""integration / nonlinear reference contracts."""

import json

import numpy as np
import pytest

from tests.support.builders.nonlinear_reference import (
    DISP,
    FORCE,
    MODES,
    configuration,
    solve,
)
from tests.support.oracles.uniform_beam import assert_uniform

pytestmark = pytest.mark.integration


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("route", ["python", "json", "http"])
@pytest.mark.parametrize("mode", MODES)
@pytest.mark.parametrize("n", [1, 4])
@pytest.mark.parametrize(
    "p,e,asymmetric", [(5, 0.0005, False), (12, 0.002, False), (-16, -0.004, True), (18, 0.006, False)]
)
def test_uniform_modes_independent_inverse_skeleton(route, mode, n, p, e, asymmetric):
    assert_uniform(solve(configuration(mode, n, p, asymmetric), route), mode, n, p, e)


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("route", ["python", "json", "http"])
@pytest.mark.parametrize("mode", MODES)
@pytest.mark.parametrize("control", ["load", "displacement"])
@pytest.mark.parametrize("asymmetric", [False, True])
@pytest.mark.parametrize("subdivisions", [1, 7])
def test_cyclic_polygon_residual_deformation_and_work(route, mode, control, asymmetric, subdivisions):
    # Kd+=10000; Kd-=6000 for asymmetric. Every event has a hand-derived ordinate.
    if asymmetric:
        strains = np.array([0, 1, 2, 0.8, -2, -4, -4 / 3, 2]) * 1e-3
        forces = np.array([0, 10, 12, 0, -12, -16, 0, 12])
        loop_work = 2 * (544 / 15) * 1e-3
    else:
        strains = np.array([0, 1, 2, 0.8, -1, -2, -0.8, 2]) * 1e-3
        forces = np.array([0, 10, 12, 0, -10, -12, 0, 12])
        loop_work = 2 * 22.4 * 1e-3
    if subdivisions > 1:
        strains = np.r_[
            strains[0],
            np.concatenate(
                [np.linspace(a, b, subdivisions + 1)[1:] for a, b in zip(strains[:-1], strains[1:])]
            ),
        ]
        forces = np.r_[
            forces[0],
            np.concatenate(
                [np.linspace(a, b, subdivisions + 1)[1:] for a, b in zip(forces[:-1], forces[1:])]
            ),
        ]
    d = configuration(mode, force=1, asymmetric=asymmetric)
    if control == "load":
        factors = forces.tolist()
    else:
        # Unit target generalized deformation .001, hence tip motion .002.
        j = MODES[mode][0]
        values = [0.0] * 6
        values[j] = 0.002
        flags = [False] * 6
        flags[j] = True
        d["boundary_conditions"] = {"restraints": {"30": {"dof": flags, "values": values}}}
        d["load"]["1"]["load_node"] = []
        factors = (strains / 0.001).tolist()
    d["analysis_params"] = {"load_factors": factors}
    r = solve(d, route)
    assert len(r["step_results"]) == len(strains)
    observed_e, observed_p = [], []
    for step, e, p in zip(r["step_results"], strains, forces):
        assert_uniform(
            dict(step, convergence_history=r["convergence_history"], step_results=[step]), mode, 1, p, e
        )
        observed_e.append(step["node_displacements"]["30"][MODES[mode][2]] / 2)
        observed_p.append(step["element_stresses"]["7"]["j_end"][MODES[mode][0]])
    # Closed deformation-force loop +2 -> ... -> +2. No claim that all internal
    # history variables return to initial values; signed external work is tested.
    start = 2 * subdivisions
    work = 2 * np.sum(
        np.diff(observed_e[start:]) * (np.array(observed_p[start:-1]) + observed_p[start + 1 :]) / 2
    )
    assert work == pytest.approx(loop_work, rel=1e-8, abs=1e-10)
    assert work > 0
    print("NONLINEAR_METRIC " + json.dumps(dict(mode=mode, work=abs(work - loop_work))))


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("route", ["python", "json", "http"])
def test_public_spring_api_participates_in_equilibrium(route):
    d = configuration(force=20)
    # .004 displacement -> N=12, spring=2000*.004=8, total=20.
    d["boundary_conditions"] = {"spring_supports": {"30": {"x": 2000}}}
    r = solve(d, route)
    assert r["node_displacements"]["30"]["dx"] == pytest.approx(0.004, abs=1e-10)
    assert r["reaction_forces"]["10"]["fx"] == pytest.approx(-12, abs=1e-8)
    assert r["reaction_forces"]["30"]["fx"] == pytest.approx(-8, abs=1e-8)
    assert r["reaction_forces"]["10"]["fx"] + r["reaction_forces"]["30"]["fx"] + 20 == pytest.approx(
        0, abs=1e-8
    )


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("route", ["python", "json", "http"])
@pytest.mark.parametrize("mode", MODES)
@pytest.mark.parametrize("control", ["load", "displacement"])
@pytest.mark.parametrize("beta", [0, 1])
@pytest.mark.parametrize("sign", [1, -1])
@pytest.mark.parametrize("subdivisions", [1, 3])
def test_nested_reversal_and_damage_independent_polygon(route, mode, control, beta, sign, subdivisions):
    # Hand-derived branch intersections, with every zero/return point included.
    # beta=1: Kd+=10000*(2)^-1=5000, outer zero=-.4e-3;
    # A=(-.7,-5), virgin negative Kd=10000, zero=-.2;
    # B=(.9,6), retained positive Kd=5000, zero=-.3;
    # reload to A has slope 12500, then resumes outer slope 50000/3.
    if beta:
        vertices = [
            (0, 0),
            (1, 10),
            (2, 12),
            (-0.4, 0),
            (-0.7, -5),
            (-0.2, 0),
            (0.9, 6),
            (-0.3, 0),
            (-0.5, -2.5),
            (-0.7, -5),
            (-1, -10),
            (-2, -12),
        ]
    else:
        vertices = [
            (0, 0),
            (1, 10),
            (2, 12),
            (0.8, 0),
            (-0.1, -5),
            (0.4, 0),
            (1.2, 6),
            (0.6, 0),
            (0.25, -2.5),
            (0.5, 0),
            (0.85, 3),
            (0.55, 0),
            (0.25, -2.5),
            (-0.1, -5),
            (-1, -10),
            (-2, -12),
        ]
    base = np.array(vertices) * [sign * 0.001, sign]
    # Signed external work includes the initial loading; no closed-cycle claim.
    expected_work = 2 * np.sum(np.diff(base[:, 0]) * (base[:-1, 1] + base[1:, 1]) / 2)
    path = np.vstack(
        [base[0], *[np.linspace(a, b, subdivisions + 1)[1:] for a, b in zip(base[:-1], base[1:])]]
    )
    d = configuration(mode, force=1)
    d["element"]["1"]["1"]["nonlinear"]["beta"] = beta
    if control == "load":
        factors = path[:, 1].tolist()
    else:
        j = MODES[mode][0]
        flags, values = [False] * 6, [0.0] * 6
        flags[j], values[j] = True, 0.002
        d["boundary_conditions"] = {"restraints": {"30": dict(dof=flags, values=values)}}
        d["load"]["1"]["load_node"] = []
        factors = (path[:, 0] / 0.001).tolist()
    d["analysis_params"] = dict(load_factors=factors)
    r = solve(d, route)
    observed = []
    max_errors = dict(displacement=0.0, end_force=0.0, reaction=0.0)
    for step, (e, p) in zip(r["step_results"], path):
        errors = assert_uniform(
            dict(step, convergence_history=r["convergence_history"], step_results=[step]), mode, 1, p, e
        )
        max_errors = {key: max(value, errors[key]) for key, value in max_errors.items()}
        observed.append(
            (
                step["node_displacements"]["30"][MODES[mode][2]] / 2,
                step["element_stresses"]["7"]["j_end"][MODES[mode][0]],
            )
        )
    observed = np.array(observed)
    work = 2 * np.sum(np.diff(observed[:, 0]) * (observed[:-1, 1] + observed[1:, 1]) / 2)
    assert work == pytest.approx(expected_work, rel=1e-8, abs=1e-10)
    print(
        "PHASE4_NESTED_METRIC "
        + json.dumps(
            dict(
                route=route,
                mode=mode,
                control=control,
                beta=beta,
                sign=sign,
                subdivisions=subdivisions,
                work_error=abs(work - expected_work),
                **max_errors,
            )
        )
    )


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("route", ["python", "json", "http"])
@pytest.mark.parametrize("direction", ["ty", "tz"])
def test_elastic_cantilever_force_moment_balance(route, direction):
    d = configuration("moment_z" if direction == "ty" else "moment_y", force=0)
    d["load"]["1"]["load_node"] = [dict(n=30, **{direction: 1})]
    r = solve(d, route)
    # E I=10000, G k A=4000*5/6, L=2.
    v = 8 / 30000 + 2 / (4000 * 5 / 6)
    disp = r["node_displacements"]["30"]
    assert disp["dy" if direction == "ty" else "dz"] == pytest.approx(v, abs=1e-10)
    assert disp["rz" if direction == "ty" else "ry"] == pytest.approx(
        (1 if direction == "ty" else -1) * 0.0002, abs=1e-10
    )
    fixed = r["reaction_forces"]["10"]
    assert fixed["fy" if direction == "ty" else "fz"] == pytest.approx(-1, abs=1e-10)
    assert fixed["mz" if direction == "ty" else "my"] == pytest.approx(
        -2 if direction == "ty" else 2, abs=1e-10
    )


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("route", ["python", "json", "http"])
def test_explicit_G_elastic_torsion(route):
    d = configuration("torsion", force=1)
    d["element"]["1"]["1"]["G"] = 2500
    nl = d["element"]["1"]["1"]["nonlinear"]
    nl.update(P_1=2.5, P_2=4, P_3=5.5)
    assert_uniform(solve(d, route), "torsion", 1, 1, 1 / 2500)


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("route", ["python", "json", "http"])
def test_two_members_with_intermediate_load_have_independent_equilibrium(route):
    d = configuration(n=2)
    d["node"]["30"]["x"] = 0.5
    d["load"]["1"]["load_node"].append(dict(n=30, tx=4))
    r = solve(d, route)
    assert r["node_displacements"]["30"]["dx"] == pytest.approx(0.5 * 0.004, abs=1e-10)
    assert r["node_displacements"]["50"]["dx"] == pytest.approx(0.5 * 0.004 + 1.5 * 0.002, abs=1e-10)
    assert r["reaction_forces"]["10"]["fx"] == pytest.approx(-16, abs=1e-9)
    assert r["element_stresses"]["7"]["j_end"][0] == pytest.approx(16, abs=1e-9)
    assert r["element_stresses"]["8"]["i_end"][0] == pytest.approx(-12, abs=1e-9)
    assert r["element_stresses"]["7"]["j_end"][0] + r["element_stresses"]["8"]["i_end"][
        0
    ] - 4 == pytest.approx(0, abs=1e-9)


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("mode", MODES)
@pytest.mark.parametrize("route", ["python", "json", "http"])
@pytest.mark.parametrize("vertical", [False, True])
def test_rotated_member_global_displacements_and_equilibrium(mode, route, vertical):
    # Columns are local unit vectors in global coordinates, independently fixed.
    c = 1 / np.sqrt(2)
    q = (
        np.array([[0, 1, 0], [0, 0, 1], [1, 0, 0]])
        if vertical
        else np.array([[c, -c, 0], [c, c, 0], [0, 0, 1]])
    )
    d = configuration(mode)
    d["node"]["30"] = dict(zip(("x", "y", "z"), (q @ [2, 0, 0]).astype(float).tolist()))
    force = np.zeros(6)
    force[MODES[mode][0]] = 12
    global_force = np.r_[q @ force[:3], q @ force[3:]]
    d["load"]["1"]["load_node"] = [
        dict(n=30, **dict(zip(("tx", "ty", "tz", "rx", "ry", "rz"), global_force)))
    ]
    r = solve(d, route)
    local_u = np.zeros(6)
    local_u[MODES[mode][0]] = 0.004
    if mode == "moment_y":
        local_u[2] = -0.004
    if mode == "moment_z":
        local_u[1] = 0.004
    expected_u = np.r_[q @ local_u[:3], q @ local_u[3:]]
    np.testing.assert_allclose([r["node_displacements"]["30"][k] for k in DISP], expected_u, atol=1e-10)
    reaction = np.array([r["reaction_forces"]["10"][k] for k in FORCE])
    np.testing.assert_allclose(reaction, -global_force, atol=1e-8)
    np.testing.assert_allclose(reaction[:3] + global_force[:3], 0, atol=1e-8)
    np.testing.assert_allclose(
        reaction[3:] + global_force[3:] + np.cross(q @ [2, 0, 0], global_force[:3]), 0, atol=1e-8
    )
    np.testing.assert_allclose(r["element_stresses"]["7"]["j_end"], force, atol=1e-8)
    np.testing.assert_allclose(r["element_stresses"]["7"]["i_end"], -force, atol=1e-8)
