"""harness / comparison contracts."""

import pytest

from tests.support.builders.linear_frame import cantilever, run

pytestmark = pytest.mark.unit


@pytest.mark.material_nonlinear
def test_comparator_rejects_missing_extra_wrong_sign_and_number():
    from tests.support.assertions import assert_dict_almost_equal

    expected = {"u": {"dx": 0.004}, "reaction": [-12.0, 0.0], "converged": True}
    for actual in [
        dict(expected, u={}),
        dict(expected, extra=0),
        dict(expected, u={"dx": 0.005}),
        dict(expected, reaction=[12.0, 0.0]),
        dict(expected, converged=False),
        dict(expected, reaction=[-12.0]),
    ]:
        with pytest.raises(AssertionError):
            assert_dict_almost_equal(actual, expected)


@pytest.mark.material_nonlinear
def test_legacy_cut_signs_and_labels_have_independent_physical_values():
    from tests.support.assertions import comparison_errors
    from tests.support.section_cut_view import section_cut_result_view

    d = cantilever()
    d["notice_points"] = [dict(m=1, Points=[1])]
    m, r = run(d)
    view = section_cut_result_view(r, m, d)
    assert view["size"] == 3
    assert set(view["disg"]) == {"1", "2", "1n1"}
    assert view["disg"]["1n1"]["dy"] == pytest.approx(3 * 1**2 * (6 - 1) / (6 * 4000), abs=1e-12)
    for segment, mi, mj in [("P1", -6, -3), ("P2", -3, 0)]:
        expected = dict.fromkeys(("fxi", "fzi", "mxi", "myi", "fxj", "fzj", "mxj", "myj"), 0.0)
        expected.update(fyi=-3, fyj=-3, mzi=mi, mzj=mj, L=1.0)
        assert not comparison_errors(view["fsec"]["1"][segment], expected)
        broken = dict(expected, mzi=99)
        assert comparison_errors(view["fsec"]["1"][segment], broken)
