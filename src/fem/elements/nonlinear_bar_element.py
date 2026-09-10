"""Small-displacement beam with midpoint section hysteresis (manual 7.21).

Section laws act on strain/curvature, not nodal motion or hinge rotation.
Active bending laws update condensed shear increments from their JR tangents.
"""
from typing import Dict, List, Optional
import copy

import numpy as np

from .bar_element import TBarElement
from ..nonlinear.hysteresis import (
    HysteresisState, JRStiffnessReductionParams, JRStiffnessReductionModel,
)
from ..nonlinear.beam_section_response import BendingPlaneState, evaluate_bending_plane
from ..diagnostics import NumericalConditionError


class NonlinearBarElement(TBarElement):
    """One section state per law, indexed by ``center``.

    ``current_states`` is the candidate from the last force evaluation;
    ``committed_states`` is the only source of loading history. Tangent and
    output evaluations never change either. See the phase-2 plan for units.
    """

    DOF_MAPPING = {
        'axial': (0, 6), 'torsion': (3, 9),
        'moment_y': (4, 10), 'moment_z': (5, 11),
    }

    def __init__(self, element_id: int, node_ids: List[int], material_id: int,
                 section_id: int, angle: float = 0.0, shear_correction: bool = True):
        super().__init__(element_id, node_ids, material_id, section_id, angle, shear_correction)
        self.hysteresis_models: Dict[str, JRStiffnessReductionModel] = {}
        self.current_states: Dict[str, Dict[str, HysteresisState]] = {}
        self.committed_states: Dict[str, Dict[str, HysteresisState]] = {}
        self.current_bending_states: Dict[str, BendingPlaneState] = {}
        self.committed_bending_states: Dict[str, BendingPlaneState] = {}
        # (global displacement, local nodal resisting force)
        self._trial_response = None
        self._committed_response = None
        self._trial_section_response = {}
        self._committed_section_response = {}

    def get_name(self) -> str:
        return 'nonlinear_bar'

    def set_hysteresis_model(self, dof: str, params: JRStiffnessReductionParams) -> None:
        """Set N(epsilon), T(twist/length), or M(curvature) for a section mode."""
        if dof not in self.DOF_MAPPING:
            raise ValueError(f'Unknown DOF: {dof}. Valid values: {list(self.DOF_MAPPING)}')
        model = JRStiffnessReductionModel(params)
        self.hysteresis_models[dof] = model
        self.current_states[dof] = {'center': model.create_initial_state()}
        self.committed_states[dof] = {'center': model.create_initial_state()}
        if dof in ('moment_y', 'moment_z'):
            self.current_bending_states[dof] = BendingPlaneState.initial(model)
            self.committed_bending_states[dof] = BendingPlaneState.initial(model)
        self._trial_response = self._committed_response = None
        self._trial_section_response = {}
        self._committed_section_response = {}

    def _section_operators(self):
        """Return constant B and reference rigidities for e = B q_local.

        e = [epsilon, tau, kappa_y, kappa_z, s_y, s_z]. The last two modes
        include the elastic bending/shear flexibility eliminated by the exact
        two-node Timoshenko interpolation; they are not raw shear strains.
        """
        if self.material is None or self.bar_param is None:
            raise ValueError('Material properties not set')
        if self.length is None or not np.isfinite(self.length) or self.length <= 0:
            raise ValueError('Element length must be finite and positive')
        l = self.length
        mat = self.material.materials[self.material_id]
        section = self.bar_param
        e, g, a = mat.E, mat.G, section.area
        if not np.all(np.isfinite([e, g, a])) or min(e, g, a) <= 0:
            raise ValueError('E, G and area must be finite and positive')
        rigidities = np.array([e*a, g*section.J, e*section.Iy, e*section.Iz, 0., 0.])
        if not np.all(np.isfinite(rigidities)) or np.any(rigidities < 0):
            raise ValueError('Section rigidities must be finite and nonnegative')
        b = np.zeros((6, 12))
        for row, (i, j) in enumerate(self.DOF_MAPPING.values()):
            b[row, i], b[row, j] = -1/l, 1/l
        b[4, [1, 7, 5, 11]] = [-1/l, 1/l, -0.5, -0.5]
        b[5, [2, 8, 4, 10]] = [-1/l, 1/l, 0.5, 0.5]
        for row, ei, correction in [(4, e*section.Iz, section.kappa_y),
                                     (5, e*section.Iy, section.kappa_z)]:
            if self.shear_correction:
                if not np.isfinite(correction) or correction <= 0:
                    raise ValueError('Shear correction must be finite and positive')
                ga = correction*g*a
                rigidities[row] = 12*ei*ga / (12*ei + ga*l*l)
            else:
                rigidities[row] = 12*ei/(l*l)
        return b, rigidities

    def _evaluate(self, displacement):
        """Pure evaluation from committed history: f=L B.T p, K=df/dq."""
        u = np.asarray(displacement, dtype=float)
        if u.shape != (12,) or not np.all(np.isfinite(u)):
            raise ValueError('Expected 12 finite element displacements')
        t = self.get_transformation_matrix(12)
        b, rigidities = self._section_operators()
        deformation = b @ (t @ u)
        p = rigidities * deformation
        tangent = np.diag(rigidities)
        states = {}
        bending_states = {}
        section_response = {}
        for row, dof in enumerate(self.DOF_MAPPING):
            if dof not in self.hysteresis_models:
                continue
            model = self.hysteresis_models[dof]
            if dof in ('moment_y', 'moment_z'):
                shear_row = 5 if dof == 'moment_y' else 4
                correction = self.bar_param.kappa_z if shear_row == 5 else self.bar_param.kappa_y
                material = self.material.materials[self.material_id]
                ga = correction*material.G*self.bar_param.area if self.shear_correction else None
                try:
                    response = evaluate_bending_plane(
                        model, self.committed_bending_states[dof],
                        deformation[row], deformation[shear_row], length=self.length,
                        young_modulus=material.E, shear_rigidity=ga)
                except NumericalConditionError as error:
                    error.details.update(element_id=self.element_id, axis=dof[-1])
                    raise
                indices = [row, shear_row]
                p[indices] = [response.state.moment, response.state.shear_force]
                tangent[np.ix_(indices, indices)] = response.tangent
                bending_states[dof] = response.state
                states[dof] = {'center': response.state.history.copy()}
                section_response[dof[-1]] = {
                    'curvature': response.state.curvature, 'moment': response.state.moment,
                    'shear_deformation': response.state.shear_deformation,
                    'shear_force': response.state.shear_force,
                    'bending_tangent': response.bending_tangent,
                    'effective_inertia': response.effective_inertia,
                    'shear_coefficient': response.shear_coefficient,
                    'branch': response.state.history.branch,
                    'skeleton': {
                        side: [[getattr(model.params, f'delta_{i}_{side}'),
                                getattr(model.params, f'P_{i}_{side}')]
                               for i in (1, 2, 3, 4)
                               if getattr(model.params, f'delta_{i}_{side}') is not None]
                        for side in ('pos', 'neg')
                    },
                }
                continue
            state = self.committed_states[dof]['center'].copy()
            p[row], tangent[row, row], info = model.get_force_and_stiffness(deformation[row], state)
            states[dof] = {'center': model.update_state(
                deformation[row], p[row], tangent[row, row], state, info)}
        f_local = self.length * b.T @ p
        k_local = self.length * b.T @ tangent @ b
        for values in section_response.values():
            values.update(N=float(p[0]), Nd=float(-p[0]))
        return f_local, t.T @ k_local @ t, states, bending_states, section_response

    def get_internal_force(self, displacement: np.ndarray) -> np.ndarray:
        f_local, _, states, bending_states, section_response = self._evaluate(displacement)
        self.current_states = states
        self.current_bending_states = bending_states
        self._trial_section_response = section_response
        self._trial_response = (np.array(displacement, copy=True), f_local.copy())
        return self.get_transformation_matrix(12).T @ f_local

    def get_tangent_stiffness_matrix(self, displacement: np.ndarray) -> np.ndarray:
        return self._evaluate(displacement)[1]

    def calculate_forces(self, displacement: np.ndarray) -> Dict[str, np.ndarray]:
        """Local nodal resisting forces [N,Vy,Vz,T,My,Mz], without history changes.

        At the converged displacement use the force accepted by Newton, including
        after commit (re-evaluating a JR branch could change its force).
        """
        response = self._committed_response
        if response is not None and np.array_equal(displacement, response[0]):
            local = response[1]
        else:
            local = self._evaluate(displacement)[0]
        return {'i_end': local[:6].copy(), 'j_end': local[6:].copy()}

    def commit_state(self) -> None:
        self.committed_states = copy.deepcopy(self.current_states)
        self.committed_bending_states = copy.deepcopy(self.current_bending_states)
        self._committed_response = copy.deepcopy(self._trial_response)
        self._committed_section_response = copy.deepcopy(self._trial_section_response)

    def get_section_response(self):
        """Accepted central bending response, without re-evaluating history."""
        return {'center': copy.deepcopy(self._committed_section_response)}

    def calculate_curvature(self, displacement: np.ndarray) -> Dict[str, float]:
        """Midpoint total curvature about local y/z (1/m), without history changes.

        These are the generalized deformations used by the M-curvature laws,
        including elastic bending on an axis without a hysteresis law.
        """
        u = np.asarray(displacement, dtype=float)
        if u.shape != (12,) or not np.all(np.isfinite(u)):
            raise ValueError('Expected 12 finite element displacements')
        b, _ = self._section_operators()
        deformation = b @ (self.get_transformation_matrix(12) @ u)
        return {'y': float(deformation[2]), 'z': float(deformation[3])}

    def rollback_state(self) -> None:
        self.current_states = copy.deepcopy(self.committed_states)
        self.current_bending_states = copy.deepcopy(self.committed_bending_states)
        self._trial_response = copy.deepcopy(self._committed_response)
        self._trial_section_response = copy.deepcopy(self._committed_section_response)

    def reset_states(self) -> None:
        self.committed_states = {
            dof: {'center': model.create_initial_state()}
            for dof, model in self.hysteresis_models.items()
        }
        self.current_states = copy.deepcopy(self.committed_states)
        self.committed_bending_states = {
            dof: BendingPlaneState.initial(model)
            for dof, model in self.hysteresis_models.items()
            if dof in ('moment_y', 'moment_z')
        }
        self.current_bending_states = copy.deepcopy(self.committed_bending_states)
        self._trial_response = self._committed_response = None
        self._trial_section_response = {}
        self._committed_section_response = {}

    def get_hysteresis_state(self, dof: str) -> Optional[Dict[str, HysteresisState]]:
        """Return a snapshot of the candidate central section state."""
        return copy.deepcopy(self.current_states.get(dof))

    def is_nonlinear(self) -> bool:
        return bool(self.hysteresis_models)

    def get_nonlinear_dofs(self) -> List[str]:
        return list(self.hysteresis_models)

    def get_max_displacement(self, dof: str) -> Dict[str, float]:
        """Maximum generalized strain/curvature, not endpoint displacement."""
        if dof not in self.current_states:
            return {}
        state = self.current_states[dof]['center']
        return {'center_pos': state.delta_max_pos, 'center_neg': state.delta_max_neg}
