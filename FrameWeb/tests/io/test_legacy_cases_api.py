"""HTTP contract for the explicit FrameWebforJS legacy case representation."""

from __future__ import annotations

import copy
import json

import pytest
from flask import Response
from main import app

from fem import legacy_results
from tests.support.builders.shell_input import data as shell_data
from tests.support.builders.slip_support import slip_model_data

pytestmark = pytest.mark.integration

LEGACY_CASES_MEDIA_TYPE = "application/vnd.frameweb.legacy-cases-v1+json"
CASE_FIELDS = ["disg", "reac", "fsec", "shell_fsec", "size"]
VECTOR_FIELDS = ["dx", "dy", "dz", "rx", "ry", "rz"]
REACTION_FIELDS = ["tx", "ty", "tz", "mx", "my", "mz"]
SECTION_FIELDS = [
    "fxi",
    "fyi",
    "fzi",
    "mxi",
    "myi",
    "mzi",
    "fxj",
    "fyj",
    "fzj",
    "mxj",
    "myj",
    "mzj",
    "L",
]


def _restraints() -> list[dict[str, int]]:
    return [
        {"n": 10, "tx": 1, "ty": 1, "tz": 1, "rx": 1, "ry": 1, "rz": 1},
        {"n": 30, "tx": 0, "ty": 1, "tz": 1, "rx": 1, "ry": 1, "rz": 1},
    ]


def _legacy_two_case_beam() -> dict[str, object]:
    """Return a hand-checkable beam with ordered cases and distinct references."""
    material_1 = {"E": 10_000, "G": 4_000, "A": 1, "Iy": 1, "Iz": 1, "J": 1}
    material_2 = {**material_1, "E": 20_000}
    return {
        "node": {
            "30": {"x": 2, "y": 0, "z": 0},
            "10": {"x": 0, "y": 0, "z": 0},
        },
        "member": {"7": {"ni": 10, "nj": 30, "e": 1, "cg": 0}},
        "notice_points": [{"m": 7, "Points": [0.5]}],
        "element": {"1": {"1": material_1}, "2": {"1": material_2}},
        "fix_node": {"1": _restraints(), "2": _restraints()},
        "load": {
            "positive": {
                "element": 1,
                "fix_node": 1,
                "rate": 1,
                "load_node": [{"n": 30, "tx": 4}],
            },
            "negative-scaled": {
                "element": 2,
                "fix_node": 2,
                "rate": 2.5,
                "load_node": [{"n": 30, "tx": -6}],
            },
        },
    }


def _post(data: dict[str, object], accept: str | None = None) -> Response:
    """Send raw JSON so Flask's test helper cannot reorder legacy case keys."""
    headers = {"Accept": accept} if accept is not None else {}
    return app.test_client().post(
        "/",
        data=json.dumps(data),
        content_type="application/json",
        headers=headers,
    )


def _with_case_count(data: dict[str, object], case_count: int) -> dict[str, object]:
    case = next(iter(data["load"].values()))
    data["load"] = {
        f"case-{index}": copy.deepcopy(case) for index in range(case_count)
    }
    return data


def _assert_six_components(
    actual: dict[str, float],
    fields: list[str],
    **expected: float,
) -> None:
    assert list(actual) == fields
    values = {field: expected.get(field, 0.0) for field in fields}
    assert actual == pytest.approx(values, abs=1e-12)


def _assert_axial_case(
    case: dict,
    *,
    end_displacement: float,
    reaction: float,
    section_force: float,
) -> None:
    assert list(case) == CASE_FIELDS
    assert list(case["disg"]) == ["10", "30", "7n1"]
    _assert_six_components(case["disg"]["10"], VECTOR_FIELDS)
    _assert_six_components(
        case["disg"]["7n1"],
        VECTOR_FIELDS,
        dx=end_displacement / 4,
    )
    _assert_six_components(
        case["disg"]["30"],
        VECTOR_FIELDS,
        dx=end_displacement,
    )

    assert list(case["reac"]) == ["10", "30"]
    _assert_six_components(case["reac"]["10"], REACTION_FIELDS, tx=reaction)
    _assert_six_components(case["reac"]["30"], REACTION_FIELDS)

    assert list(case["fsec"]) == ["7"]
    assert list(case["fsec"]["7"]) == ["P1", "P2"]
    for point, length in (("P1", 0.5), ("P2", 1.5)):
        section = case["fsec"]["7"][point]
        assert list(section) == SECTION_FIELDS
        expected = {field: 0.0 for field in SECTION_FIELDS}
        expected.update(fxi=section_force, fxj=section_force, L=length)
        assert section == pytest.approx(expected, abs=1e-12)

    assert case["shell_fsec"] == {}
    assert case["size"] == 3


@pytest.mark.parametrize("accept", [None, "application/json"])
def test_default_response_remains_flat(accept: str | None) -> None:
    response = _post(_legacy_two_case_beam(), accept)

    assert response.status_code == 200
    body = response.get_json()
    assert {"node_displacements", "reaction_forces", "element_stresses"} <= body.keys()
    assert "positive" not in body
    assert "negative-scaled" not in body


def test_exact_accept_returns_ordered_projected_cases() -> None:
    response = _post(_legacy_two_case_beam(), LEGACY_CASES_MEDIA_TYPE)

    assert response.status_code == 200
    assert response.mimetype == LEGACY_CASES_MEDIA_TYPE
    body = response.get_json()
    assert list(body) == ["positive", "negative-scaled"]
    assert body["positive"] != body["negative-scaled"]
    _assert_axial_case(
        body["positive"],
        end_displacement=0.0008,
        reaction=-4,
        section_force=4,
    )
    _assert_axial_case(
        body["negative-scaled"],
        end_displacement=-0.0015,
        reaction=15,
        section_force=-15,
    )


def test_case_solver_is_repeatable_and_does_not_mutate_input() -> None:
    from fem.legacy_results import solve_legacy_cases

    data = _legacy_two_case_beam()
    original = copy.deepcopy(data)

    first = solve_legacy_cases(data)
    second = solve_legacy_cases(data)

    assert data == original
    assert first == second
    assert first["positive"] != first["negative-scaled"]


def test_unknown_legacy_vendor_media_type_is_rejected() -> None:
    response = _post(
        _legacy_two_case_beam(),
        "application/vnd.frameweb.legacy-cases-v2+json",
    )

    assert response.status_code == 406
    body = response.get_json()
    assert body["converged"] is False
    assert "positive" not in body


@pytest.mark.parametrize("data", [slip_model_data(), shell_data()])
def test_selector_rejects_unsupported_input_shapes(data: dict) -> None:
    response = _post(data, LEGACY_CASES_MEDIA_TYPE)

    assert response.status_code == 400
    body = response.get_json()
    assert body["error_code"] == "invalid_input"
    assert body["converged"] is False


def test_later_invalid_case_is_atomic() -> None:
    data = _legacy_two_case_beam()
    data["load"]["broken-later"] = {
        "element": 999,
        "fix_node": 1,
        "load_node": [{"n": 30, "tx": 1}],
    }

    response = _post(data, LEGACY_CASES_MEDIA_TYPE)

    assert response.status_code != 200
    body = response.get_json()
    assert body["converged"] is False
    assert "positive" not in body
    assert "negative-scaled" not in body


@pytest.mark.parametrize(
    "case_count",
    [legacy_results.MAX_LEGACY_CASES - 1, legacy_results.MAX_LEGACY_CASES],
)
def test_case_count_guard_accepts_supported_boundary(case_count: int) -> None:
    data = _with_case_count(_legacy_two_case_beam(), case_count)

    legacy_results._validate_legacy_beam_input(data)


def test_case_count_guard_rejects_before_model_creation(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    data = _with_case_count(
        _legacy_two_case_beam(), legacy_results.MAX_LEGACY_CASES + 1
    )
    model_created = False

    class UnexpectedFemModel:
        def __init__(self) -> None:
            nonlocal model_created
            model_created = True

    monkeypatch.setattr(legacy_results, "FemModel", UnexpectedFemModel)

    response = _post(data, LEGACY_CASES_MEDIA_TYPE)

    assert response.status_code == 400
    body = response.get_json()
    assert body["error_code"] == "invalid_input"
    assert body["converged"] is False
    assert str(legacy_results.MAX_LEGACY_CASES) in body["error"]
    assert model_created is False
