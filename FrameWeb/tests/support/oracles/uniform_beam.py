import json

import numpy as np

MODES = {
    "axial": (0, "tx", "dx"),
    "torsion": (3, "rx", "rx"),
    "moment_y": (4, "ry", "ry"),
    "moment_z": (5, "rz", "rz"),
}
DISP = ["dx", "dy", "dz", "rx", "ry", "rz"]
FORCE = ["fx", "fy", "fz", "mx", "my", "mz"]


def assert_uniform(r, mode, n, p, e):
    j, _, name = MODES[mode]
    errors = dict(displacement=0.0, end_force=0.0, reaction=0.0)
    for i in range(n + 1):
        x = 2 * i / n
        expected = np.zeros(6)
        expected[j] = x * e
        if mode == "moment_y":
            expected[2] = -x * x * e / 2
        if mode == "moment_z":
            expected[1] = x * x * e / 2
        np.testing.assert_allclose(
            [r["node_displacements"][str(10 + 20 * i)][k] for k in DISP], expected, rtol=1e-8, atol=1e-10
        )
        errors["displacement"] = max(
            errors["displacement"],
            float(
                np.max(
                    np.abs(np.array([r["node_displacements"][str(10 + 20 * i)][k] for k in DISP]) - expected)
                )
            ),
        )
    for k in range(n):
        expected = np.zeros(6)
        expected[j] = p
        np.testing.assert_allclose(r["element_stresses"][str(7 + k)]["j_end"], expected, atol=1e-8)
        np.testing.assert_allclose(r["element_stresses"][str(7 + k)]["i_end"], -expected, atol=1e-8)
        errors["end_force"] = max(
            errors["end_force"],
            float(np.max(np.abs(np.array(r["element_stresses"][str(7 + k)]["j_end"]) - expected))),
            float(np.max(np.abs(np.array(r["element_stresses"][str(7 + k)]["i_end"]) + expected))),
        )
    expected = np.zeros(6)
    expected[j] = -p
    np.testing.assert_allclose([r["reaction_forces"]["10"][k] for k in FORCE], expected, atol=1e-8)
    errors["reaction"] = float(
        np.max(np.abs(np.array([r["reaction_forces"]["10"][k] for k in FORCE]) - expected))
    )
    print("NONLINEAR_METRIC " + json.dumps(dict(mode=mode, **errors)))
    # Nodal balance, including r cross F, follows from these absolute end forces.
    assert r["converged"] is True
    assert all(s["converged"] for s in r["step_results"])
    for s in r["step_results"]:
        c = [c for c in r["convergence_history"] if c["step"] == s["step"]][-1]
        assert c["relative_residual"] < 1e-8
    return errors
