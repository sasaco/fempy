"""Nd-dependent bending/shear coupling for the central beam section."""

import copy

import numpy as np
import pytest

from fem.elements.nonlinear_bar_element import NonlinearBarElement
from fem.material import BarParameter, Material, MaterialProperty
from fem.nonlinear.axial_force_table import AxialForceRow, AxialForceTable, SkeletonPoints
from fem.nonlinear.hysteresis import JRStiffnessReductionParams


def table():
    def row(nd, stiffness):
        points = SkeletonPoints((.001, .003, .005),
                                (stiffness*.001, stiffness*.002, stiffness*.0025))
        return AxialForceRow(nd, points, points)

    return AxialForceTable((row(-1., 50.), row(0., 100.), row(1., 200.)), beta=0.)


def test_coupled_local_kernel_integrates_changing_shear_coefficient():
    from fem.nonlinear.axial_bending_response import (
        AxialBendingPlaneState, evaluate_axial_bending_plane,
    )

    law = table()
    committed = AxialBendingPlaneState.initial(law)
    response = evaluate_axial_bending_plane(
        law, committed, .0005, .001, 1., length=1., young_modulus=1000.,
        shear_rigidity=None,
    )

    assert response.state.moment == pytest.approx(.1)
    # C=12B varies linearly from 1200 to 2400 along this path.
    assert response.state.shear_force == pytest.approx(1.8)
    assert response.bending_tangent == pytest.approx(200.)
    assert response.effective_inertia == pytest.approx(.2)
    # Rows [M,V], columns [Nd, curvature, shear deformation].
    np.testing.assert_allclose(
        response.tangent,
        [[.05, 200., 0.], [.6, 0., 1800.]],
        rtol=2e-6,
        atol=2e-8,
    )


def element(axis="moment_z"):
    material = Material()
    material.add_material(1, MaterialProperty("reference", E=1000., nu=.25))
    section = BarParameter(area=1., Iy=1., Iz=1., J=1.)
    material.add_bar_parameter(1, section)
    result = NonlinearBarElement(7, [1, 2], 1, 1, shear_correction=False)
    result.set_material_properties(material, section)
    result.set_node_coordinates({1: np.array([0., 0., 0.]), 2: np.array([1., 0., 0.])})
    result.set_axial_force_table(axis, table())
    return result


def displacement(epsilon=-.0005, curvature=.0005, shear=.001):
    q = np.zeros(12)
    q[6] = epsilon
    q[1], q[7] = 0., shear
    q[5], q[11] = -curvature/2, curvature/2
    return q


def test_element_uses_total_axial_force_and_preserves_nonsymmetric_tangent():
    beam = element()
    q = displacement()
    force = beam.get_internal_force(q)
    tangent = beam.get_tangent_stiffness_matrix(q)

    local, _, _, bending, section = beam._evaluate(q)
    assert local[6] == pytest.approx(-.5)
    assert section["z"]["N"] == pytest.approx(-.5)
    assert section["z"]["Nd"] == pytest.approx(.5)
    assert section["z"]["moment"] == pytest.approx(.075)
    assert section["z"]["shear_force"] == pytest.approx(1.5)
    assert section["z"]["interpolation"] == {"lower_Nd": 0., "upper_Nd": 1., "fraction": .5}
    assert bending["moment_z"].axial_history.Nd == pytest.approx(.5)

    # The global tangent is the derivative of the same force update and is not
    # symmetrized: axial strain changes M, while curvature does not change N.
    h = 1e-7
    numeric = np.column_stack([
        (beam._evaluate(q + h*np.eye(12)[j])[0]
         - beam._evaluate(q - h*np.eye(12)[j])[0])/(2*h)
        for j in range(12)
    ])
    np.testing.assert_allclose(tangent, numeric, rtol=2e-5, atol=2e-5)
    assert not np.allclose(tangent, tangent.T)
    assert np.array_equal(force, beam.get_internal_force(q))


def test_coupled_trials_do_not_mutate_committed_state():
    beam = element()
    before = copy.deepcopy(beam.committed_bending_states)
    first = beam._evaluate(displacement(-.0005, .0002, .0003))
    beam._evaluate(displacement(-.001, -.0003, -.0004))
    repeated = beam._evaluate(displacement(-.0005, .0002, .0003))
    assert beam.committed_bending_states == before
    np.testing.assert_array_equal(first[0], repeated[0])
    assert first[3] == repeated[3]


def test_nonlinear_axial_law_supplies_its_piecewise_Nd_path():
    beam = element()
    beam.set_hysteresis_model(
        "axial",
        JRStiffnessReductionParams.symmetric(
            .0004, .001, .002, .4, .7, .9, beta=0.,
        ),
    )
    q = displacement(epsilon=-.0008)
    _, _, axial, _, section = beam._evaluate(q)

    assert axial["axial"]["center"].current_P == pytest.approx(-.6)
    assert section["z"]["Nd"] == pytest.approx(.6)
    assert section["z"]["moment"] == pytest.approx(.08)
    # Nd is 0->.4 over the first half and .4->.6 over the second half.
    # Thus mean B=.5*(100+140)/2 + .5*(140+160)/2 = 135,
    # not the endpoint-linear approximation (100+160)/2 = 130.
    assert section["z"]["shear_force"] == pytest.approx(1.62)


@pytest.mark.parametrize("axis", ["moment_y", "moment_z"])
@pytest.mark.parametrize("epsilon, moment, shear_force", [
    (-.0005, .075, 1.5),
    (.0005, .0375, 1.05),
])
def test_both_axes_and_compression_tension_signs(axis, epsilon, moment, shear_force):
    beam = element(axis)
    q = np.zeros(12)
    q[6] = epsilon
    if axis == "moment_z":
        q[1], q[7] = 0., .001
        q[5], q[11] = -.00025, .00025
    else:
        q[2], q[8] = 0., .001
        q[4], q[10] = -.00025, .00025
    section = beam._evaluate(q)[4][axis[-1]]
    assert section["Nd"] == pytest.approx(-1000*epsilon)
    assert section["moment"] == pytest.approx(moment)
    assert section["shear_force"] == pytest.approx(shear_force)


@pytest.mark.parametrize("parts", [2, 4, 8])
def test_local_integral_is_invariant_when_the_same_path_is_committed_in_parts(parts):
    from fem.nonlinear.axial_bending_response import (
        AxialBendingPlaneState, evaluate_axial_bending_plane,
    )

    law = table()
    initial = AxialBendingPlaneState.initial(law)
    whole = evaluate_axial_bending_plane(
        law, initial, .0005, .001, 1., length=1., young_modulus=1000.,
        shear_rigidity=None,
    ).state
    split = initial
    for index in range(1, parts+1):
        fraction = index/parts
        split = evaluate_axial_bending_plane(
            law, split, fraction*.0005, fraction*.001, fraction,
            length=1., young_modulus=1000., shear_rigidity=None,
        ).state
    assert split.moment == pytest.approx(whole.moment, abs=2e-13)
    assert split.shear_force == pytest.approx(whole.shear_force, abs=2e-12)
    assert split.history.current_K == pytest.approx(whole.history.current_K)
    assert split.axial_history.contact_side == whole.axial_history.contact_side


def test_identical_Nd_rows_reduce_to_the_fixed_skeleton_kernel():
    from fem.nonlinear.axial_bending_response import (
        AxialBendingPlaneState, evaluate_axial_bending_plane,
    )
    from fem.nonlinear.beam_section_response import BendingPlaneState, evaluate_bending_plane
    from fem.nonlinear.hysteresis import JRStiffnessReductionModel

    points = SkeletonPoints((.001, .003, .005), (.1, .2, .25))
    law = AxialForceTable(tuple(AxialForceRow(nd, points, points) for nd in (-2., 0., 2.)), beta=0.)
    fixed_model = JRStiffnessReductionModel(law.interpolate(0.).to_jr_params())
    dependent = evaluate_axial_bending_plane(
        law, AxialBendingPlaneState.initial(law), .002, .001, 1.5,
        length=1., young_modulus=1000., shear_rigidity=None,
    )
    fixed = evaluate_bending_plane(
        fixed_model, BendingPlaneState.initial(fixed_model), .002, .001,
        length=1., young_modulus=1000., shear_rigidity=None,
    )
    assert dependent.state.moment == fixed.state.moment
    assert dependent.state.shear_force == fixed.state.shear_force
    assert dependent.state.history == fixed.state.history
    np.testing.assert_allclose(dependent.tangent[:, 1:], fixed.tangent, rtol=2e-7, atol=2e-8)
    np.testing.assert_allclose(dependent.tangent[:, 0], 0., atol=2e-9)


def test_shared_table_does_not_share_history_between_elements():
    shared = table()
    first, second = element(), element()
    # Definitions may be shared; section states may not.
    first.axial_force_tables["moment_z"] = shared
    second.axial_force_tables["moment_z"] = shared
    first.get_internal_force(displacement())
    first.commit_state()
    assert first.committed_bending_states["moment_z"].curvature == pytest.approx(.0005)
    assert second.committed_bending_states["moment_z"].curvature == 0.
    assert shared.to_dict() == table().to_dict()


def test_constant_Nd_knee_crossing_keeps_the_shear_curvature_tangent():
    """Review regression: a nonconstant table must retain the fixed-Nd limit."""
    from fem.nonlinear.axial_bending_response import (
        AxialBendingPlaneState, evaluate_axial_bending_plane,
    )

    law = table()
    response = evaluate_axial_bending_plane(
        law, AxialBendingPlaneState.initial(law), .002, .001, 0.,
        length=1., young_modulus=1000., shear_rigidity=None,
    )
    # First half C=1200, second half C=600: V=.9 and
    # dV/dphi = .001/.002 * (600-900) = -150. The endpoint is smooth.
    assert response.state.shear_force == pytest.approx(.9, abs=1e-12)
    assert response.tangent[1, 1] == pytest.approx(-150., rel=1e-6, abs=1e-6)


@pytest.mark.parametrize('shear', [0., .001])
def test_Nd_path_rejects_an_interior_shear_condensation_pole(shear):
    """Both endpoint coefficients are finite, but the path crosses a pole."""
    from fem.diagnostics import NumericalConditionError
    from fem.nonlinear.axial_bending_response import (
        AxialBendingPlaneState, evaluate_axial_bending_plane,
    )

    rows = []
    for nd in (0., 1.):
        points = SkeletonPoints((.001, .003, .005, .007),
                                tuple((1+nd)*p for p in (.1, .2, .25, .2)))
        rows.append(AxialForceRow(nd, points, points))
    law = AxialForceTable(tuple(rows), beta=0.)
    options = dict(length=1., young_modulus=1000., shear_rigidity=337.0370367,
                   compute_tangent=False)
    committed = evaluate_axial_bending_plane(
        law, AxialBendingPlaneState.initial(law), .006, 0., 0., **options,
    ).state
    # K4=-25*(1+Nd), so 12*K4+GA=0 at Nd=.123456789.
    # A terminal-only check or a quadrature warning cannot diagnose this path.
    with pytest.raises(NumericalConditionError) as failure:
        evaluate_axial_bending_plane(law, committed, .006, shear, 1., **options)
    assert failure.value.details['reason'] == 'singular_shear_condensation'
    assert failure.value.details['path_fraction'] == pytest.approx(.123456789, abs=1e-12)
    assert failure.value.details['Nd'] == pytest.approx(.123456789, abs=1e-12)


@pytest.mark.parametrize('sign', [-1., 1.])
@pytest.mark.parametrize('phi', [.0037, .006, .02])
def test_constant_Nd_multiple_knees_have_independent_integral_and_tangent(sign, phi):
    from fem.nonlinear.axial_bending_response import AxialBendingPlaneState, evaluate_axial_bending_plane

    law = table()
    moment = .2+25*(phi-.003) if phi < .005 else .25
    terminal_c = 300. if phi < .005 else 0.
    mean_c = 12*moment/phi
    response = evaluate_axial_bending_plane(
        law, AxialBendingPlaneState.initial(law), sign*phi, .001, 0.,
        length=1., young_modulus=1000.,
    )
    assert response.state.shear_force == pytest.approx(.001*mean_c, rel=1e-12)
    assert response.tangent[1, 1] == pytest.approx(.001/(sign*phi)*(terminal_c-mean_c), rel=2e-6)


@pytest.mark.parametrize('sign', [-1., 1.])
@pytest.mark.parametrize('ga', [None, 300.])
def test_constant_Nd_unload_zero_reload_integrates_the_actual_branches(sign, ga):
    from fem.nonlinear.axial_bending_response import AxialBendingPlaneState, evaluate_axial_bending_plane

    law = table()
    options = dict(length=1., young_modulus=1000., shear_rigidity=ga)
    initial = evaluate_axial_bending_plane(
        law, AxialBendingPlaneState.initial(law), sign*.004, .001, .5, **options,
    ).state
    saved = copy.deepcopy(initial)
    response = evaluate_axial_bending_plane(law, initial, -sign*.0025, .003, .5, **options)
    # M=.3375, Kd=75: zero at phi=-.0005; reload to (-.003,-.3) has K=120.
    c_unload, c_reload = (900., 1440.) if ga is None else (225., 7200/29)
    mean_c = (4.5*c_unload+2*c_reload)/6.5
    assert response.state.moment == pytest.approx(-sign*.24, abs=1e-12)
    assert response.state.shear_force-initial.shear_force == pytest.approx(.002*mean_c, rel=1e-12)
    assert response.tangent[1, 1] == pytest.approx(.002/(-sign*.0065)*(c_reload-mean_c), rel=2e-6)
    assert initial == saved


def test_discontinuous_stiffness_jump_across_pole_value_is_allowed():
    from fem.nonlinear.axial_bending_response import AxialBendingPlaneState, evaluate_axial_bending_plane

    rows = []
    for nd in (0., 1.):
        points = SkeletonPoints((.001, .003, .005, .007),
                                tuple((1+nd)*p for p in (.1, .2, .25, .2)))
        rows.append(AxialForceRow(nd, points, points))
    law = AxialForceTable(tuple(rows), beta=0.)
    response = evaluate_axial_bending_plane(
        law, AxialBendingPlaneState.initial(law), .006, .001, 0.,
        length=1., young_modulus=1000., shear_rigidity=200.,
    )
    # At phi=.005 B jumps 25 -> -25. It never passes through -GA/12.
    expected = .001*(1200/7+2*150+2*120+600)/6
    assert response.state.shear_force == pytest.approx(expected, rel=1e-12)
    assert response.shear_coefficient == pytest.approx(600.)


@pytest.mark.parametrize('roots', [(.2, .8), (.37, .37)])
@pytest.mark.parametrize('scale', [1e-12, 1., 1e12])
def test_rational_shear_domain_detects_interior_poles_with_equal_endpoint_signs(roots, scale):
    from numpy.polynomial import Polynomial
    from fem.diagnostics import NumericalConditionError
    from fem.nonlinear.axial_bending_response import _validate_shear_interval

    # B/scale=-25+(t-r1)(t-r2), GA=300*scale. The sum 12B+GA
    # has two interior zeros, or one touching zero. Endpoints alone are safe.
    numerator = scale*(Polynomial((-25.,))+Polynomial.fromroots(roots))
    with pytest.raises(NumericalConditionError) as failure:
        _validate_shear_interval(numerator, Polynomial((1.,)), 1., 300*scale, 0., 1., 0., 1.)
    assert failure.value.details['reason'] == 'singular_shear_condensation'
    assert failure.value.details['path_fraction'] == pytest.approx(roots[0], abs=1e-10)


def test_unconverged_shear_quadrature_cannot_return_a_successful_state(monkeypatch):
    from fem.diagnostics import NumericalConditionError
    from fem.nonlinear import axial_bending_response as kernel

    law = table()
    initial = kernel.AxialBendingPlaneState.initial(law)
    saved = copy.deepcopy(initial)
    monkeypatch.setattr(kernel, 'quad', lambda *args, **kwargs: (.5, .1, {}, 'subdivision limit'))
    with pytest.raises(NumericalConditionError) as failure:
        kernel.evaluate_axial_bending_plane(
            law, initial, .0005, .001, .5,
            length=1., young_modulus=1000., compute_tangent=False,
        )
    assert failure.value.details['reason'] == 'axial_force_shear_integration'
    assert failure.value.details['integration_message'] == 'subdivision limit'
    assert initial == saved


@pytest.mark.parametrize('sign', [-1., 1.])
@pytest.mark.parametrize('ga', [None, 40000.])
def test_moving_reload_shear_matches_a_logarithmic_primitive(sign, ga):
    from math import log
    from fem.nonlinear.axial_bending_response import AxialBendingPlaneState, evaluate_axial_bending_plane

    rows = []
    for nd in (0., 1.):
        points = SkeletonPoints(tuple(d*(1+.5*nd) for d in (.001, .002, .003)),
                                tuple(p*(1-.5*nd) for p in (10., 12., 13.)))
        rows.append(AxialForceRow(nd, points, points))
    law = AxialForceTable(tuple(rows), beta=0.)
    options = dict(length=1., young_modulus=10000., shear_rigidity=ga, compute_tangent=False)
    state = AxialBendingPlaneState.initial(law)
    for phi in (sign*.002, 0.):
        state = evaluate_axial_bending_plane(law, state, phi, 0., 0., **options).state
    # Zero anchor=sign*.0008, target=-sign*.001*(1+.5Nd),
    # target M=-sign*10*(1-.5Nd): B=10000*(1-.5Nd)/(1.8+.5Nd).
    mean_c = (120000*(-1+5.6*log(23/18)) if ga is None
              else 60000+168000*log(19/24))
    result = evaluate_axial_bending_plane(law, state, 0., .001, 1., **options)
    assert result.state.shear_force == pytest.approx(.001*mean_c, rel=1e-12)
    assert result.state.moment == pytest.approx(-sign*4/2.3, rel=1e-12)
    reverse = evaluate_axial_bending_plane(law, result.state, 0., 0., 0., **options)
    assert reverse.state.shear_force == pytest.approx(0., abs=1e-12)


@pytest.mark.parametrize('offset,stops', [(1e-11, True), (1e-8, False)])
def test_shear_domain_checks_near_pole_stationary_point_without_a_zero(offset, stops):
    from numpy.polynomial import Polynomial
    from fem.diagnostics import NumericalConditionError
    from fem.nonlinear.axial_bending_response import _validate_shear_interval

    # The minimum cancellation ratio is offset/(50-offset), at t=.37.
    numerator = Polynomial((-25.+offset,))+Polynomial.fromroots((.37, .37))
    options = (numerator, Polynomial((1.,)), 1., 300., 0., 1., 0., 1.)
    if stops:
        with pytest.raises(NumericalConditionError):
            _validate_shear_interval(*options)
    else:
        _validate_shear_interval(*options)
