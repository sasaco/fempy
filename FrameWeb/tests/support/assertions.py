import math
from numbers import Real

import numpy as np
import pytest

from tests.support.oracles.cantilever import cantilever_reference
from tests.support.serialization import wire


def assert_dict_almost_equal(actual, expected, path=""):
    """Compare every key and sequence entry, including finite values and signs."""
    if isinstance(expected, dict):
        assert isinstance(actual, dict), f"Type mismatch at {path}"
        assert actual.keys() == expected.keys(), (
            f"Keys differ at {path}: {actual.keys()} != {expected.keys()}"
        )
        for key in expected:
            assert_dict_almost_equal(actual[key], expected[key], f"{path}/{key}")
    elif isinstance(expected, (list, tuple)):
        assert isinstance(actual, (list, tuple)), f"Type mismatch at {path}"
        assert len(actual) == len(expected), f"Length differs at {path}"
        for i, (a, e) in enumerate(zip(actual, expected)):
            assert_dict_almost_equal(a, e, f"{path}/{i}")
    elif isinstance(expected, bool) or expected is None:
        assert actual is expected, f"Value differs at {path}: {actual} != {expected}"
    elif isinstance(expected, Real):
        assert isinstance(actual, Real) and not isinstance(actual, bool), f"Type mismatch at {path}"
        assert math.isfinite(actual) and math.isfinite(expected), f"Non-finite at {path}"
        assert actual == pytest.approx(expected, rel=1e-8, abs=1e-10), (
            f"Value differs at {path}: {actual} != {expected}"
        )
    else:
        assert actual == expected, f"Value differs at {path}: {actual} != {expected}"


def comparison_errors(actual, expected, path=""):
    """Visit all common numeric leaves even when another key/quantity fails."""
    errors = []
    if isinstance(actual, dict) and isinstance(expected, dict):
        missing, extra = expected.keys() - actual.keys(), actual.keys() - expected.keys()
        if missing or extra:
            errors.append(f"{path}: missing={sorted(missing)}, extra={sorted(extra)}")
        for key in sorted(actual.keys() & expected.keys(), key=str):
            errors.extend(comparison_errors(actual[key], expected[key], f"{path}/{key}"))
    else:
        try:
            assert_dict_almost_equal(actual, expected, path)
        except AssertionError as error:
            errors.append(str(error))
    return errors


def assert_acceptance_result(result, reference, controls):
    """Independent physical reference schema; iteration counts use criteria.

    Every displacement, reaction and end force is compared at every step.
    Raw displacement is checked against the nodal view, and convergence records
    must terminate within the requested tolerance and iteration budget.
    """
    fields = ("node_displacements", "reaction_forces", "element_stresses", "converged")
    assert set(reference) == set(fields) | {"analysis_type", "step_results"}
    actual = {k: result[k] for k in fields}
    actual["analysis_type"] = result["analysis_type"]
    actual["step_results"] = [{k: s[k] for k in fields + ("step", "lambda")} for s in result["step_results"]]
    assert_dict_almost_equal(actual, reference)
    for s in result["step_results"]:
        flattened = [
            v
            for node in sorted(s["node_displacements"], key=int)
            for v in s["node_displacements"][node].values()
        ]
        assert_dict_almost_equal(s["displacement"], flattened, "raw displacement")
        history = [c for c in result["convergence_history"] if c["step"] == s["step"]]
        assert len(history) == s["iterations"]
        assert 1 <= s["iterations"] <= controls.get("max_iterations", 50)
        assert history[-1]["relative_residual"] < controls.get("tolerance", 1e-6)
        if history[-1]["relative_du"] is not None:
            assert history[-1]["relative_du"] < controls.get("tolerance", 1e-6)


def assert_cantilever_history(result, data):
    """All nodal, reaction and section components; every converged load step."""

    def compare(r, factor):
        expected = cantilever_reference(data, factor)
        for field, entities in expected.items():
            assert r[field].keys() == entities.keys()
            for key, values in entities.items():
                assert r[field][key].keys() == values.keys()
                for component, value in values.items():
                    np.testing.assert_allclose(
                        r[field][key][component],
                        value,
                        rtol=1e-6,
                        atol=1e-9,
                        err_msg=f"{field}/{key}/{component}",
                    )

    compare(result, 1.0)
    assert result["converged"] is True
    assert result["analysis_type"] == "material_nonlinear"
    assert len(result["step_results"]) == data["load"]["1"]["n_load_steps"]
    for step in result["step_results"]:
        assert step["converged"] is True
        compare(step, step["lambda"])
        record = [c for c in result["convergence_history"] if c["step"] == step["step"]][-1]
        assert record["relative_residual"] < data["load"]["1"]["tolerance"]


def assert_axial(result, force=12, displacement=0.004):
    r = wire(result)
    assert r["converged"] is True
    assert r["analysis_type"] == "material_nonlinear"
    assert r["node_displacements"]["30"]["dx"] == pytest.approx(displacement, abs=1e-10)
    np.testing.assert_allclose(list(r["node_displacements"]["10"].values()), 0, atol=1e-12)
    assert r["reaction_forces"]["10"]["fx"] == pytest.approx(-force, abs=1e-9)
    np.testing.assert_allclose(r["element_stresses"]["7"]["i_end"], [-force, 0, 0, 0, 0, 0], atol=1e-9)
    np.testing.assert_allclose(r["element_stresses"]["7"]["j_end"], [force, 0, 0, 0, 0, 0], atol=1e-9)
    assert len(r["step_results"]) == 4
    assert all(s["converged"] for s in r["step_results"])
    for step in r["step_results"]:
        last = [c for c in r["convergence_history"] if c["step"] == step["step"]][-1]
        assert last["relative_residual"] < 1e-10
