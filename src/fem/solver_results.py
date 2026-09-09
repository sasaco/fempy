"""Accepted snapshots and explicit compatibility projections for static APIs."""

from copy import deepcopy


def snapshot(solver, mesh, boundary, elements, solution, force, step, factor, nonlinear):
    u, internal, correction, iterations = solution
    reaction = internal - force
    if not nonlinear:
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
    return deepcopy(result)


def final_result(solver, nonlinear):
    last = deepcopy(solver.step_results[-1])
    if nonlinear:
        # The historical low-level result has no final element_stresses key;
        # FemModel consumes the accepted end-force snapshot during postprocess.
        keys = ('displacement', 'node_displacements', 'reaction_forces', 'curvature', 'converged')
        result = {key: last[key] for key in keys}
        result.update(step_results=deepcopy(solver.step_results),
                      convergence_history=deepcopy(solver.convergence_history),
                      analysis_type='material_nonlinear')
    else:
        keys = ('displacement', 'node_displacements', 'reaction_forces', 'displacement_correction')
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
