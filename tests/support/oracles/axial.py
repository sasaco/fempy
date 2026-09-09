def axial_acceptance_reference():
    """L=2, N=12: inverse skeleton e=.001+(12-10)/2000=.002.

    Four load levels 3,6,9,12 yield u=.0006,.0012,.0018,.004.
    Reaction=-N, local i=-N and j=N; all other components zero.
    """

    def response(force, u):
        zero = dict.fromkeys(("dx", "dy", "dz", "rx", "ry", "rz"), 0.0)
        return dict(
            node_displacements={"10": zero, "30": dict(zero, dx=u)},
            reaction_forces={"10": dict(fx=-force, fy=0.0, fz=0.0, mx=0.0, my=0.0, mz=0.0)},
            element_stresses={
                "7": {"i_end": [-force, 0.0, 0.0, 0.0, 0.0, 0.0], "j_end": [force, 0.0, 0.0, 0.0, 0.0, 0.0]}
            },
        )

    final = response(12.0, 0.004)
    final.update(analysis_type="material_nonlinear", converged=True)
    final["step_results"] = [
        dict(response(p, u), converged=True, step=i, **{"lambda": i / 4})
        for i, (p, u) in enumerate(zip([3.0, 6.0, 9.0, 12.0], [0.0006, 0.0012, 0.0018, 0.004]), 1)
    ]
    return final
