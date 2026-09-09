"""Accepted snapshots and explicit compatibility projections for static APIs."""

from copy import deepcopy
import hashlib
import json

import numpy as np

from ._version import __version__
from .convergence import generalized_force_norm, relative_measure


RESULT_SCHEMA_VERSION = "1.0"
DEFAULT_MODEL_METADATA = {
    "coordinate_system": {
        "name": "global_cartesian",
        "axes": ["x", "y", "z"],
    },
    "units": {
        "system": "consistent_user_defined",
        "length": "unspecified",
        "force": "unspecified",
        "mass": "unspecified",
        "time": "unspecified",
    },
}


def normalize_model_metadata(metadata):
    """Merge user declarations onto the non-SI-assuming public defaults."""
    result = deepcopy(DEFAULT_MODEL_METADATA)
    if metadata:
        if not isinstance(metadata, dict):
            raise ValueError("model_metadata must be an object")
        unknown = set(metadata) - set(DEFAULT_MODEL_METADATA)
        if unknown:
            raise ValueError(f"Unknown model_metadata fields: {sorted(unknown)}")
        for key, value in metadata.items():
            if not isinstance(value, dict):
                raise ValueError(f"model_metadata.{key} must be an object")
            result[key].update(deepcopy(value))
    return result


def _effective_analysis_parameters(analysis_type, parameters):
    names = {
        "static": (),
        "modal": ("n_modes",),
        "material_nonlinear": (
            "n_load_steps", "max_iterations", "tolerance",
            "load_factors", "displacement_control",
        ),
    }[analysis_type]
    return {
        name: deepcopy(parameters[name])
        for name in names
        if name in parameters and parameters[name] is not None
    }


def build_result_metadata(model, analysis_type):
    """Build deterministic provenance and final solver diagnostics."""
    from .file_io import model_to_jsonable

    declarations = normalize_model_metadata(model.model_metadata)
    model_data = {
        "mesh": model.mesh,
        "boundary": model.boundary,
        "material": model.material,
        "section": model.section,
        "analysis_type": analysis_type,
        "analysis_params": model.analysis_params,
        "model_metadata": declarations,
    }
    canonical = json.dumps(
        model_to_jsonable(model_data), sort_keys=True, separators=(",", ":"),
        ensure_ascii=False, allow_nan=False,
    ).encode("utf-8")

    solver = model.solver
    history = solver.convergence_history
    step_iterations = [int(step.get("iterations", 0)) for step in solver.step_results]
    residual_norm = residual_scale = relative_residual = None
    if history:
        residual_norm = float(history[-1]["residual_norm"])
        residual_scale = float(history[-1]["residual_scale"])
        relative_residual = float(history[-1]["relative_residual"])
    elif analysis_type == "static" and solver.displacement is not None:
        applied = solver.load_factor * solver.load_vector
        residual = solver._equilibrium_residual(
            applied - solver._last_internal_force,
            solver.displacement,
            model.boundary,
            solver.layout.stride,
        )
        residual_norm = generalized_force_norm(
            residual,
            stride=solver.layout.stride,
            length=solver.characteristic_length,
        )
        free_load = solver._apply_bc_to_residual(
            applied, model.boundary, solver.layout.stride
        )
        _, springs = solver._get_boundary_dofs(
            model.boundary, len(solver.displacement), solver.layout.stride
        )
        restoring = solver._apply_bc_to_residual(
            solver._last_internal_force + solver._spring_force(
                solver.displacement, springs
            ),
            model.boundary,
            solver.layout.stride,
        )
        residual_scale = max(
            generalized_force_norm(
                free_load,
                stride=solver.layout.stride,
                length=solver.characteristic_length,
            ),
            generalized_force_norm(
                restoring,
                stride=solver.layout.stride,
                length=solver.characteristic_length,
            ),
        )
        relative_residual = relative_measure(residual_norm, residual_scale)
    elif analysis_type == "modal" and model.results.get("eigenpair_residuals"):
        relative_residual = float(max(model.results["eigenpair_residuals"]))

    return {
        "schema_version": RESULT_SCHEMA_VERSION,
        "product": {"name": "FEMPython", "version": __version__},
        "input_sha256": hashlib.sha256(canonical).hexdigest(),
        "analysis": {
            "type": analysis_type,
            "parameters": _effective_analysis_parameters(
                analysis_type, model.analysis_params
            ),
        },
        **declarations,
        "solver": {
            "converged": True,
            "iterations": sum(step_iterations),
            "step_iterations": step_iterations,
            "residual_norm": residual_norm,
            "residual_scale": residual_scale,
            "relative_residual": relative_residual,
            "convergence_measure": {
                "type": "dimensionless_generalized_l2",
                "characteristic_length": solver.characteristic_length,
                "characteristic_length_source": solver.characteristic_length_source,
                "force_components": "[Fx,Fy,Fz,Mx/L,My/L,Mz/L]",
                "displacement_components": "[dx/L,dy/L,dz/L,rx,ry,rz]",
                "residual_absolute_floor": None,
                "displacement_reference_floor": 1.0,
            },
            "warnings": deepcopy(solver.analysis_warnings),
            "high_precision": solver.precise_end_forces is not None,
        },
    }


def snapshot(solver, mesh, boundary, elements, solution, force, step, factor, nonlinear):
    u, internal, correction, iterations = solution
    reaction = internal - force
    if not nonlinear:
        if getattr(solver,'precise_reactions',None) is not None:
            reaction=solver.precise_reactions.copy()
        _, springs = solver._get_boundary_dofs(boundary, len(u), solver.layout.stride)
        # Preserve the direct solve's compensated spring reaction convention.
        for dof, stiffness in springs.items():
            reaction[dof] = -stiffness * (u[dof] + correction[dof])
    result = dict(
        step=step, **{'lambda': factor}, displacement=u.copy(),
        node_displacements=solver._format_node_displacements(u, mesh),
        reaction_forces=solver._format_reactions(reaction, boundary, solver.layout.stride),
        converged=True, iterations=iterations,
    )
    if nonlinear:
        result['element_stresses'] = solver._element_end_forces(elements, u, solver.layout.stride)
        result['curvature'] = solver._element_curvatures(elements, u, solver.layout.stride)
    elif correction is not None:
        result['displacement_correction'] = correction.copy()
        if getattr(solver, 'interpolated_displacements', {}):
            result['interpolated_displacements'] = deepcopy(solver.interpolated_displacements)
        if getattr(solver, 'precise_end_forces', None) is not None:
            result['precise_end_forces'] = deepcopy(solver.precise_end_forces)
    return deepcopy(result)


def final_result(solver, nonlinear):
    last = deepcopy(solver.step_results[-1])
    if nonlinear:
        # The historical low-level result has no final element_stresses key;
        # FemModel consumes the accepted end-force snapshot during postprocess.
        keys = ('displacement', 'node_displacements', 'reaction_forces', 'curvature', 'converged')
        if last.get('control_mode') == 'displacement':
            keys += ('lambda', 'control_mode', 'control_node', 'control_dof',
                     'control_displacement')
        result = {key: last[key] for key in keys if key in last}
        result.update(step_results=deepcopy(solver.step_results),
                      convergence_history=deepcopy(solver.convergence_history),
                      analysis_type='material_nonlinear')
    else:
        keys = ('displacement', 'node_displacements', 'reaction_forces', 'displacement_correction',
                'interpolated_displacements', 'precise_end_forces')
        result = {key: last[key] for key in keys if key in last}
    return result


def legacy_nonlinear_result(result, stride):
    """Keep the existing HTTP/FemModel/NonlinearSolver 3DOF output schema."""
    if stride == 3:
        for item in [result, *result['step_results']]:
            item['node_displacements'] = {
                node: {key: values[key] for key in ('dx', 'dy', 'dz')}
                for node, values in item['node_displacements'].items()
            }
    return result
