"""io / structural input contracts."""

import copy

import numpy as np
import pytest

from fem.file_io import _read_json_model
from fem.model import FemModel
from main import app
from tests.support.builders.input_routes import axial_json, json_model
from tests.support.builders.linear_frame import cantilever, run
from tests.support.builders.shell_input import data

pytestmark = pytest.mark.integration


@pytest.mark.material_nonlinear
def test_json_does_not_add_unrequested_rotational_restraint():
    d = axial_json(0)
    d["fix_node"]["1"][0]["rz"] = 0
    m = json_model(d)
    assert m.boundary.restraints[10].dof_restraints[5] is False
    response = app.test_client().post("/", json=d)
    assert response.status_code == 422


@pytest.mark.parametrize("nonlinear", [False, True])
@pytest.mark.parametrize("flag", [None, False, True])
@pytest.mark.parametrize("has_g", [False, True])
def test_g_omission_and_explicit_shear_flag(nonlinear, flag, has_g):
    data = cantilever()
    mat = data["element"]["1"]["2"]
    if has_g:
        mat["G"] = 100
    else:
        del mat["G"]
    if nonlinear:
        mat["nonlinear"] = dict(
            type="jr_stiffness_reduction",
            delta_1=0.01,
            delta_2=0.02,
            delta_3=0.03,
            P_1=20,
            P_2=30,
            P_3=35,
            hysteresis_dofs=["axial"],
        )
    if flag is not None:
        data["member"]["1"]["shear_correction"] = flag
    model, result = run(data)
    enabled = has_g and (nonlinear if flag is None else flag)
    assert model.mesh.elements[1]["shear_correction"] is enabled
    # Bending is elastic in both models: FL^3/(3EI) + enabled*FL/(kGA).
    assert result["node_displacements"][2]["dy"] == pytest.approx(
        0.002 + (0.036 if enabled else 0), abs=1e-11
    )
    assert result["reaction_forces"][1]["fy"] == pytest.approx(-3, abs=1e-9)


def test_selected_case_and_rigid_zone_use_their_own_g():
    data = cantilever()
    data["member"]["1"]["shear_correction"] = True
    data["element"]["2"] = copy.deepcopy(data["element"]["1"])
    del data["element"]["2"]["2"]["G"]
    data["load"]["1"]["element"] = 2
    data["rigid"] = [dict(m=1, e=1, Ilength=1, Jlength=0)]
    model = _read_json_model(data)
    assert [(e["material_id"], e["shear_correction"]) for e in model["mesh"].elements.values()] == [
        (1, True),
        (2, False),
    ]


@pytest.mark.parametrize("kind", ["shell", "solid"])
@pytest.mark.parametrize("sections", [{}, dict(A=0, Iy=0, Iz=0, J=0)])
def test_nonbeam_elements_require_no_beam_section(kind, sections):
    coords = [[0, 0, 0], [1, 0, 0], [0, 1, 0], [0, 0, 1]]
    data = dict(
        node={str(i + 1): dict(zip("xyz", p)) for i, p in enumerate(coords)},
        element={"1": {"1": dict(E=1000, nu=0.25, thickness=0.2, **sections)}},
    )
    data[kind] = {"1": dict(e=1, nodes=[1, 2, 3] if kind == "shell" else [1, 2, 3, 4])}
    parsed = _read_json_model(data)
    assert parsed["material"].bar_params == {}
    m = FemModel()
    m.read_json_model(parsed)
    k = m.elements[1].get_stiffness_matrix()
    assert np.isfinite(k).all() and np.linalg.norm(k) > 0
    if kind == "shell":
        assert m.elements[1].thickness == 0.2


@pytest.mark.material_nonlinear
@pytest.mark.parametrize("mark", [0, "0"])
def test_disabled_member_load_ignores_stale_values_and_does_not_split(mark):
    d = cantilever()
    _, expected = run(d)
    d["load"]["1"]["load_member"] = [
        dict(m=1, mark=mark, P1=99, P2=-23, L1="unused", L2=-10, direction="unused")
    ]
    model, actual = run(d)
    assert len(model.mesh.nodes) == 2
    assert actual["node_displacements"] == expected["node_displacements"]
    assert actual["reaction_forces"] == expected["reaction_forces"]


@pytest.mark.material_nonlinear
def test_member_and_shell_number_namespaces_do_not_overwrite_each_other():
    d = cantilever()
    d["node"]["3"] = dict(x=0, y=1, z=0)
    d["shell"] = {"1": dict(nodes=[1, 2, 3], e=1)}
    m = FemModel()
    m.read_json_model(_read_json_model(d))
    assert m.mesh.elements[1]["nodes"] == [1, 2]
    shells = [e for e in m.mesh.elements.values() if e["type"] == "shell"]
    assert len(shells) == 1 and shells[0]["nodes"] == [1, 2, 3]


def test_explicit_shell_thickness_and_mindlin_override_are_preserved():
    raw = data()
    raw["element"]["2"]["1"]["thickness"] = 0.4
    raw["shell"]["7"]["formulation"] = "mindlin"
    result = _read_json_model(raw)
    assert result["mesh"].elements[7]["thickness"] == 0.4
    assert result["mesh"].elements[7]["formulation"] == "mindlin"


@pytest.mark.parametrize("value", [None, 0.0, -1.0, float("nan")])
def test_missing_or_invalid_legacy_shell_thickness_is_rejected(value):
    raw = data()
    if value is None:
        del raw["element"]["2"]["1"]["A"]
    else:
        raw["element"]["2"]["1"]["A"] = value
    with pytest.raises(ValueError, match="thickness"):
        _read_json_model(raw)


@pytest.mark.parametrize("selected", [False, True])
def test_shell_area_field_supplies_thickness(selected):
    raw = data()
    if not selected:
        raw.pop("load")
        raw["shell"] = {"1": dict(e=1, nodes=[1, 2, 3])}
        raw["element"].pop("2")
    original = copy.deepcopy(raw)
    result = _read_json_model(raw)
    element = result["mesh"].elements[7 if selected else 1]
    assert element["thickness"] == (0.3 if selected else 0.2)
    assert element["formulation"] == "dkt"
    assert raw == original
    raw["element"]["2" if selected else "1"]["1"]["A"] = 0
    with pytest.raises(ValueError, match="thickness"):
        _read_json_model(raw)
