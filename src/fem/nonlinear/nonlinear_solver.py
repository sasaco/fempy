"""Compatibility facade for the shared static solver (no independent engine)."""
from ..solver import Solver
from ..equilibrium import NonlinearConvergenceError
from ..solver_results import legacy_nonlinear_result


class NonlinearSolver:
    DEFAULT_N_STEPS = Solver.DEFAULT_N_STEPS
    DEFAULT_MAX_ITER = Solver.DEFAULT_MAX_ITER
    DEFAULT_TOL = Solver.DEFAULT_TOL

    def __init__(self, solver=None):
        object.__setattr__(self, '_solver', solver if solver is not None else Solver())

    def __getattr__(self, name):
        return getattr(self._solver, name)

    def __setattr__(self, name, value):
        setattr(self._solver, name, value)

    def solve(self, mesh, material, boundary, elements, **kwargs):
        kwargs.setdefault('analysis_type', 'material_nonlinear')
        result = self._solver.solve(mesh, material, boundary, elements, **kwargs)
        if kwargs['analysis_type'] == 'material_nonlinear':
            return legacy_nonlinear_result(result, self._solver.layout.stride)
        return result

    def solve_nonlinear(self, mesh, material, boundary, elements,
                        n_steps=DEFAULT_N_STEPS, max_iter=DEFAULT_MAX_ITER,
                        tol=DEFAULT_TOL, callback=None, load_factors=None):
        return self.solve(mesh, material, boundary, elements,
                          analysis_type='material_nonlinear', n_steps=n_steps,
                          max_iter=max_iter, tol=tol, callback=callback,
                          load_factors=load_factors)
