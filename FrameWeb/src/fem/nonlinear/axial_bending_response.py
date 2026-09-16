"""Nd-dependent bending and condensed-shear response for a section plane.

The history graph is advanced by :mod:`axial_force_path`.  This layer adds
the accumulated ``dV = C ds`` response and the endpoint algorithmic tangent.
Inputs are owned committed snapshots; no evaluation mutates them.
"""
from dataclasses import dataclass
from math import fsum, isfinite

import numpy as np
from numpy.polynomial import Polynomial
from scipy.integrate import quad

from ..diagnostics import InputValidationError, NumericalConditionError
from .axial_force_history import AxialForceHistoryState, _affine, _has_constant_skeleton
from .axial_force_path import (
    _lerp, _reload_stiffness_rational, _side_polynomials, evaluate_axial_force_path,
)
from .axial_force_reload_hold import _interval_roots
from .axial_force_table import AxialForceTable, _number
from .beam_section_response import (
    BendingPlaneState, _finite, _shear_coefficient, evaluate_bending_plane,
)
from .hysteresis import JRStiffnessReductionModel


@dataclass(frozen=True)
class AxialBendingPlaneState:
    """Owned Nd-dependent state of one central bending plane."""

    curvature: float
    shear_deformation: float
    moment: float
    shear_force: float
    axial_history: AxialForceHistoryState

    @property
    def history(self):
        """Compatibility view used by existing section-result code."""
        return self.axial_history.history

    @classmethod
    def initial(cls, table: AxialForceTable) -> "AxialBendingPlaneState":
        curve = table.interpolate(0.)
        model = JRStiffnessReductionModel(curve.to_jr_params())
        history = model.create_initial_state()
        return cls(0., 0., 0., 0., AxialForceHistoryState(0., history))


@dataclass(frozen=True)
class AxialBendingPlaneResponse:
    state: AxialBendingPlaneState
    # Rows [M, V], columns [Nd, curvature, shear deformation].
    tangent: np.ndarray
    bending_tangent: float
    effective_inertia: float
    shear_coefficient: float
    events: tuple


def _validate(table, committed, curvature, shear_deformation, Nd, length,
              young_modulus, shear_rigidity):
    if not isinstance(table, AxialForceTable):
        raise InputValidationError('Expected an AxialForceTable')
    curvature = _number(curvature, 'curvature')
    shear_deformation = _number(shear_deformation, 'shear_deformation')
    Nd = _number(Nd, 'Nd')
    table.interpolate(committed.axial_history.Nd)
    table.interpolate(Nd)
    for name, value in (
        ('committed.curvature', committed.curvature),
        ('committed.shear_deformation', committed.shear_deformation),
        ('committed.moment', committed.moment),
        ('committed.shear_force', committed.shear_force),
    ):
        _finite(name, value)
    _finite('length', length, positive=True)
    _finite('young_modulus', young_modulus, positive=True)
    if shear_rigidity is not None:
        _finite('shear_rigidity', shear_rigidity, positive=True)
    history = committed.axial_history.history
    if (history.current_delta != committed.curvature
            or history.current_P != committed.moment):
        raise InputValidationError('Committed moment/curvature must match Nd history')
    return curvature, shear_deformation, Nd


def _validate_shear_interval(numerator, denominator, length, shear_rigidity,
                             left, right, start_nd, end_nd):
    """Check every pole and near-pole extremum of B=U/W on one real branch.

    After clearing W, the cancellation ratio is |b+g|/(|b|+|g|).
    Its minima occur at endpoints, zeros of b+g, or stationary points
    of b/g. Derivative-root isolation includes touching/even roots.
    Adjacent branches are checked separately: a jump across zero is not a pole.
    """
    if shear_rigidity is None:
        return
    scale = max(np.max(np.abs(numerator.coef)), np.max(np.abs(denominator.coef)))
    b = (12.*(numerator/scale/length))/length
    g = shear_rigidity*(denominator/scale)
    scale = max(np.max(np.abs(b.coef)), np.max(np.abs(g.coef)))
    if not isfinite(scale):
        raise NumericalConditionError('Nonfinite condensed bending coefficient',
                                      reason='nonfinite_shear_condensation')
    b, g = b/scale, g/scale
    candidates = {0., 1., *_interval_roots(b+g),
                  *_interval_roots(b.deriv()*g-b*g.deriv())}
    for position in sorted(candidates):
        width = float(denominator(position))
        if width == 0:
            # A target/anchor coincidence at an event can give a removable
            # U=W=0. It is not a zero of the shear-condensation denominator.
            continue
        bending = float(numerator(position)/width)
        try:
            _shear_coefficient(bending, length, shear_rigidity)
        except NumericalConditionError as error:
            fraction = _lerp(left, right, position)
            error.details.update(path_fraction=fraction, Nd=_lerp(start_nd, end_nd, fraction))
            raise


def _shear_intervals(table, committed, curvature, Nd, response, length, shear_rigidity):
    """Recover rational branch stiffness on the already-resolved history path."""
    start_phi, start_nd = committed.curvature, committed.axial_history.Nd
    cuts = sorted({0., 1., *(e.fraction for e in response.events if 0 < e.fraction < 1)})
    for left, right in zip(cuts, cuts[1:]):
        middle = (left+right)/2
        phi, trial_nd = _lerp(start_phi, curvature, middle), _lerp(start_nd, Nd, middle)
        trial = evaluate_axial_force_path(table, committed.axial_history, phi, trial_nd)
        history = trial.state.history
        segment = history.active_segment
        n = _affine(_lerp(start_nd, Nd, left), _lerp(start_nd, Nd, right))
        if segment is None:
            side = trial.state.contact_side or (1 if phi >= 0 else -1)
            d, p = _side_polynomials(table, n, side)
            index = table.evaluate_skeleton(phi, trial_nd, side=side).segment-1
            if index == 3 and len(d) == 4:
                numerator, denominator = Polynomial((0.,)), Polynomial((1.,))
            else:
                numerator, denominator = p[index+1]-p[index], d[index+1]-d[index]
        elif segment.branch in ('reloading', 'inner_reloading') or (
                segment.target is not None and segment.target.kind == 'forward'):
            numerator, denominator, _, _ = _reload_stiffness_rational(table, segment, n, .5)
        else:
            numerator, denominator = Polynomial((trial.bending_tangent,)), Polynomial((1.,))
        _validate_shear_interval(numerator, denominator, length, shear_rigidity,
                                 left, right, start_nd, Nd)
        yield left, right, numerator, denominator


def _mean_shear_coefficient(intervals, length, shear_rigidity):
    integrals = []
    for left, right, numerator, denominator in intervals:
        def coefficient(fraction):
            bending = float(numerator(fraction)/denominator(fraction))
            return _shear_coefficient(bending, length, shear_rigidity)

        if numerator.trim().degree() == denominator.trim().degree() == 0:
            mean = coefficient(.5)
        else:
            integral = quad(coefficient, 0., 1., epsabs=0., epsrel=2e-11,
                            limit=50, full_output=1)
            if len(integral) != 3 or not all(isfinite(value) for value in integral[:2]):
                raise NumericalConditionError(
                    'Nd-dependent shear integral did not converge',
                    reason='axial_force_shear_integration',
                    path_interval=[left, right], error_estimate=float(integral[1]),
                    integration_message=integral[3] if len(integral) > 3 else 'Nonfinite integral',
                )
            mean = integral[0]
        integrals.append((right-left)*mean)
    return fsum(integrals)


def _core(table, committed, curvature, shear_deformation, Nd, *, length,
          young_modulus, shear_rigidity):
    curvature, shear_deformation, Nd = _validate(
        table, committed, curvature, shear_deformation, Nd, length,
        young_modulus, shear_rigidity,
    )
    start_phi = committed.curvature
    start_nd = committed.axial_history.Nd
    response = evaluate_axial_force_path(
        table, committed.axial_history, curvature, Nd,
    )
    terminal_c = _shear_coefficient(
        response.bending_tangent, length, shear_rigidity,
    )
    delta_shear = shear_deformation-committed.shear_deformation
    moving = curvature != start_phi or Nd != start_nd
    # Validate the whole path even when ds=0; an undefined intermediate C
    # must not become an accepted material state or a later Newton tangent.
    intervals = (tuple(_shear_intervals(table, committed, curvature, Nd, response,
                                       length, shear_rigidity)) if moving else ())

    if delta_shear == 0:
        mean_c = terminal_c
        shear_force = committed.shear_force
    elif not moving:
        mean_c = terminal_c
        shear_force = committed.shear_force+mean_c*delta_shear
    else:
        mean_c = _mean_shear_coefficient(intervals, length, shear_rigidity)
        shear_force = committed.shear_force+mean_c*delta_shear

    inertia = response.bending_tangent/young_modulus
    values = (response.moment, response.bending_tangent, terminal_c,
              mean_c, shear_force, inertia)
    if not all(isfinite(value) for value in values):
        raise NumericalConditionError(
            'Nonfinite Nd-dependent bending-plane response',
            reason='nonfinite_section_response',
        )
    state = AxialBendingPlaneState(
        curvature, shear_deformation, float(response.moment),
        float(shear_force), response.state,
    )
    return state, float(response.bending_tangent), float(inertia), float(terminal_c), response.events


def _endpoint_tangent(table, committed, target, base, *, length,
                      young_modulus, shear_rigidity):
    target = np.asarray(target, dtype=float)
    ranges = np.array([
        table.rows[-1].Nd-table.rows[0].Nd,
        max(point for row in table.rows for side in (row.positive, row.negative)
            for point in side.curvatures),
        max(abs(target[2]), 1e-3),
    ])
    result = np.empty((2, 3))

    def values(candidate):
        state = _core(
            table, committed, candidate[1], candidate[2], candidate[0],
            length=length, young_modulus=young_modulus,
            shear_rigidity=shear_rigidity,
        )[0]
        return np.array([state.moment, state.shear_force])

    base_values = np.array([base.moment, base.shear_force])
    for column in range(3):
        step = 1e-7*max(abs(target[column]), ranges[column])
        plus, minus = target.copy(), target.copy()
        plus[column] += step
        minus[column] -= step
        try:
            upper = values(plus)
        except InputValidationError as error:
            if error.details.get('reason') != 'axial_force_out_of_range':
                raise
            upper = None
        try:
            lower = values(minus)
        except InputValidationError as error:
            if error.details.get('reason') != 'axial_force_out_of_range':
                raise
            lower = None
        if upper is not None and lower is not None:
            result[:, column] = (upper-lower)/(2*step)
        elif upper is not None:
            result[:, column] = (upper-base_values)/step
        elif lower is not None:
            result[:, column] = (base_values-lower)/step
        else:
            raise NumericalConditionError(
                'Cannot evaluate Nd-dependent section tangent',
                reason='axial_force_tangent_domain', Nd=float(target[0]),
            )
    return result


def evaluate_axial_bending_plane(
    table: AxialForceTable,
    committed: AxialBendingPlaneState,
    curvature: float,
    shear_deformation: float,
    Nd: float,
    *,
    length: float,
    young_modulus: float,
    shear_rigidity: float | None = None,
    compute_tangent: bool = True,
) -> AxialBendingPlaneResponse:
    """Advance a straight ``(curvature, Nd, shear)`` section trial.

    The integral is split at every history/table event.  The returned tangent
    differentiates the complete endpoint update from the same committed state;
    at a table boundary the available in-domain one-sided derivative is used.
    """
    if _has_constant_skeleton(table, table.rows[0].Nd, table.rows[-1].Nd):
        _validate(table, committed, curvature, shear_deformation, Nd, length,
                  young_modulus, shear_rigidity)
        model = JRStiffnessReductionModel(table.interpolate(Nd).to_jr_params())
        fixed_state = BendingPlaneState(
            committed.curvature, committed.shear_deformation,
            committed.moment, committed.shear_force,
            committed.axial_history.history.copy(),
        )
        fixed = evaluate_bending_plane(
            model, fixed_state, curvature, shear_deformation,
            length=length, young_modulus=young_modulus,
            shear_rigidity=shear_rigidity,
        )
        axial_history = AxialForceHistoryState(
            Nd, fixed.state.history,
            committed.axial_history.contact_side,
            committed.axial_history.contact_segment,
        )
        state = AxialBendingPlaneState(
            fixed.state.curvature, fixed.state.shear_deformation,
            fixed.state.moment, fixed.state.shear_force, axial_history,
        )
        tangent = (np.column_stack((np.zeros(2), fixed.tangent))
                   if compute_tangent else np.empty((0, 0)))
        return AxialBendingPlaneResponse(
            state, tangent, fixed.bending_tangent, fixed.effective_inertia,
            fixed.shear_coefficient, (),
        )
    state, bending, inertia, coefficient, events = _core(
        table, committed, curvature, shear_deformation, Nd,
        length=length, young_modulus=young_modulus,
        shear_rigidity=shear_rigidity,
    )
    tangent = (_endpoint_tangent(
        table, committed, (Nd, curvature, shear_deformation), state,
        length=length, young_modulus=young_modulus,
        shear_rigidity=shear_rigidity,
    ) if compute_tangent else np.empty((0, 0)))
    return AxialBendingPlaneResponse(
        state, tangent, bending, inertia, coefficient, tuple(events),
    )
