"""Pointwise Nd interpolation and exact whole-interval admissibility references."""
from dataclasses import FrozenInstanceError

import numpy as np
import pytest

from fem.diagnostics import InputValidationError

pytestmark = [pytest.mark.unit, pytest.mark.material_nonlinear]


def definition():
    return dict(type="jr_stiffness_reduction", symmetric=True, beta=.4, axial_force_points=[
        dict(Nd=0., delta_1=.0002, P_1=40., delta_2=.002, P_2=120.,
             delta_3=.01, P_3=160., delta_4=.02, P_4=120.),
        dict(Nd=1000., delta_1=.0003, P_1=60., delta_2=.0025, P_2=150.,
             delta_3=.008, P_3=180., delta_4=.016, P_4=120.),
        dict(Nd=2000., delta_1=.0004, P_1=80., delta_2=.003, P_2=140.,
             delta_3=.006, P_3=150., delta_4=.012, P_4=70.),
    ])


def table(data=None):
    from fem.nonlinear.axial_force_table import AxialForceTable
    return AxialForceTable.from_dict(definition() if data is None else data)


def test_manual_interpolation_points_slopes_and_first_150_moment():
    material = table()
    curve = material.interpolate(1500.)
    assert curve.positive.curvatures == pytest.approx([.00035, .00275, .007, .014], rel=1e-12)
    assert curve.positive.moments == (70., 145., 165., 95.)
    assert curve.positive.slopes == pytest.approx([200000., 31250., 4705.88235294118, -10000.])
    assert curve.negative == curve.positive
    assert (curve.lower_Nd, curve.upper_Nd, curve.fraction) == (1000., 2000., .5)
    assert curve.positive_derivative.curvatures == pytest.approx([1e-7, 5e-7, -2e-6, -4e-6])
    assert curve.positive_derivative.moments == pytest.approx([.02, -.01, -.03, -.05])
    r = material.evaluate_skeleton(.0038125, 1500.)
    assert r.moment == pytest.approx(150., rel=1e-12)
    assert r.bending_tangent == pytest.approx(80000./17.)
    # K3' = (-.02 + K3*.0000025)/.00425 = -560/289.
    assert r.moment_Nd_derivative == pytest.approx(-.01-(80000./17.)*5e-7-(560./289.)*.0010625)


@pytest.mark.parametrize("curvature", [-.03, -.005, -.001, -.0001, .0001, .001, .005, .03])
def test_point_and_skeleton_derivatives_match_independent_differences(curvature):
    material = table()
    response = material.evaluate_skeleton(curvature, 1500.)
    for h in [.01, .001]:
        left = material.evaluate_skeleton(curvature, 1500.-h)
        right = material.evaluate_skeleton(curvature, 1500.+h)
        assert (right.moment-left.moment)/(2*h) == pytest.approx(response.moment_Nd_derivative, rel=1e-7, abs=1e-10)
        before, after = material.interpolate(1500.-h), material.interpolate(1500.+h)
        # A constant K1=200000 loses roughly one ULP in subtractive FD; the
        # error bound follows the magnitudes and probe width, not the result.
        rounding = 4*np.finfo(float).eps*max(after.positive.slopes)/h
        np.testing.assert_allclose((np.array(after.positive.slopes)-before.positive.slopes)/(2*h),
                                   material.interpolate(1500.).positive_slope_derivatives, rtol=1e-7, atol=rounding)


@pytest.mark.parametrize("Nd", [0., 1000., 2000.])
def test_exact_rows_and_one_sided_derivative_convention(Nd):
    curve = table().interpolate(Nd)
    row = definition()["axial_force_points"][int(Nd/1000)]
    assert curve.positive.curvatures == tuple(row[f"delta_{i}"] for i in (1, 2, 3, 4))
    assert curve.positive.moments == tuple(row[f"P_{i}"] for i in (1, 2, 3, 4))
    assert curve.lower_Nd == (0. if Nd == 0 else 1000.)


def test_definition_is_immutable_and_parameter_adapters_are_owned():
    data = definition()
    material = table(data)
    data["axial_force_points"][0]["P_1"] = 999.
    assert material.rows[0].positive.moments[0] == 40.
    with pytest.raises(FrozenInstanceError):
        material.beta = 2.
    with pytest.raises(FrozenInstanceError):
        material.rows[0].Nd = -10.
    params = material.interpolate(1500.).to_jr_params()
    params.P_1_pos = 999.
    assert material.interpolate(1500.).to_jr_params().P_1_pos == 70.
    assert table(material.to_dict()) == material


def test_asymmetric_tension_table_and_global_unloading_floor():
    data = definition()
    data['symmetric'] = False
    for row in data['axial_force_points']:
        row['Nd'] -= 500.
        for i in (1, 2, 3):
            row[f'delta_{i}_neg'] = row[f'delta_{i}']*2.
            row[f'P_{i}_neg'] = row[f'P_{i}']*.5
        # Positive K4 only: omission is consistent independently by side.
    material = table(data)
    assert material.K_min == pytest.approx(500.)
    for Nd in [-500., 0., 750., 1500.]:
        curve = material.interpolate(Nd)
        assert curve.K_min == 500.
        assert curve.negative.slopes[-1] == 0.
        assert material.evaluate_skeleton(-1., Nd).bending_tangent == 0.


@pytest.mark.parametrize("Nd", [-1., 2001., np.nextafter(0., -np.inf), np.nextafter(2000., np.inf)])
def test_outside_range_is_rejected_with_the_trial_and_bounds(Nd):
    with pytest.raises(InputValidationError) as failure:
        table().interpolate(Nd)
    assert failure.value.details['reason'] == 'axial_force_out_of_range'
    assert failure.value.details['Nd'] == Nd
    assert failure.value.details['range'] == [0., 2000.]


@pytest.mark.parametrize("bad", [float('nan'), float('inf'), -float('inf'), True, "1500"])
def test_nonfinite_or_nonnumeric_query_rejected(bad):
    with pytest.raises(InputValidationError):
        table().interpolate(bad)


@pytest.mark.parametrize("change", [
    lambda d: d.update(axial_force_points=d['axial_force_points'][:1]),
    lambda d: d['axial_force_points'][1].update(Nd=0.),
    lambda d: d['axial_force_points'][0].update(Nd=1.),
    lambda d: d['axial_force_points'][1].update(Nd=float('nan')),
    lambda d: d['axial_force_points'][1].update(delta_2=float('inf')),
    lambda d: d['axial_force_points'][1].update(delta_2=.0001),
    lambda d: d['axial_force_points'][1].pop('P_4'),
    lambda d: [d['axial_force_points'][1].pop(k) for k in ('delta_4', 'P_4')],
    lambda d: d.update(symmetric=False),
    lambda d: d.update(beta=-1.),
    lambda d: d.update(K_min=300000.),
    lambda d: d.update(K_min=0.),
    lambda d: d.update(type='unknown'),
    lambda d: d.update(typo=True),
    lambda d: d['axial_force_points'][0].update(beta=.7),
])
def test_invalid_table_definitions_are_rejected(change):
    data = definition()
    change(data)
    with pytest.raises(InputValidationError):
        table(data)


@pytest.mark.parametrize("pair", [1, 2])
def test_valid_rows_can_violate_order_strictly_inside_an_interval(pair):
    # Both endpoint slopes are ordered. At eta=1/2 the two compared
    # widths equal 1.5 but the force increments are 2 and 2.5.
    points = [(1., 3., 4., 2., 6., 6.5), (2., 3., 4., 2., 3., 3.5)] if pair == 1 else [
        (1., 2., 4., 10., 12., 16.), (1., 3., 4., 10., 12., 13.)]
    rows = [dict(Nd=float(i), **dict(zip(('delta_1','delta_2','delta_3','P_1','P_2','P_3'), p)))
            for i, p in enumerate(points)]
    with pytest.raises(InputValidationError) as failure:
        table(dict(symmetric=True, beta=0., axial_force_points=rows))
    assert failure.value.details['reason'] == 'axial_force_slope_order'
    assert 0. < failure.value.details['fraction'] < 1.
    assert failure.value.details['slope_pair'] == [pair, pair+1]


def test_identical_rows_are_exactly_fixed_skeleton_including_fourth_extension():
    data = definition()
    row = data['axial_force_points'][0]
    data['axial_force_points'] = [dict(row, Nd=-100.), dict(row, Nd=100.)]
    material = table(data)
    from fem.nonlinear.hysteresis import JRStiffnessReductionModel
    fixed = JRStiffnessReductionModel(material.interpolate(0.).to_jr_params())
    for Nd in [-100., -96., -20., 0., 100.]:
        assert material.interpolate(Nd).positive == material.rows[0].positive
        for curvature in [-.05, -.01, -.002, -.0002, -.001, 0., .0002, .002, .01, .05]:
            r = material.evaluate_skeleton(curvature, Nd)
            moment, slope = fixed.get_skeleton_force(curvature, 1 if curvature >= 0 else -1)
            assert (r.moment, r.bending_tangent) == pytest.approx((moment, slope))
            assert r.moment_Nd_derivative == 0.
