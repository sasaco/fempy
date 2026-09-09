"""Independent algebraic/virtual-work references, not calls to constitutive code.

Synthetic generalized deformations are small (strain or rad/length).
beam001 is also checked mathematically; its large shear deformation does not
establish physical applicability of the small-displacement model.
The cyclic polygons below are derived in the material nonlinear validation report.
"""

import json

from main import app
from tests.support.builders.input_routes import axial_json, json_model, python_axial
from tests.support.serialization import wire

MODES = {
    "axial": (0, "tx", "dx"),
    "torsion": (3, "rx", "rx"),
    "moment_y": (4, "ry", "ry"),
    "moment_z": (5, "rz", "rz"),
}

DISP = ["dx", "dy", "dz", "rx", "ry", "rz"]

FORCE = ["fx", "fy", "fz", "mx", "my", "mz"]


def configuration(mode="axial", n=1, force=12, asymmetric=False):
    d = axial_json(force)
    # These references include Timoshenko shear; request it with explicit G.
    d["element"]["1"]["1"]["G"] = 4000
    d["node"] = {str(10 + 20 * i): dict(x=2 * i / n, y=0, z=0) for i in range(n + 1)}
    d["member"] = {str(7 + i): dict(ni=10 + 20 * i, nj=30 + 20 * i, e=1, cg=0) for i in range(n)}
    nl = d["element"]["1"]["1"]["nonlinear"]
    nl["hysteresis_dofs"] = [mode]
    if asymmetric:
        nl.update(
            symmetric=False,
            delta_1_neg=0.002,
            delta_2_neg=0.006,
            delta_3_neg=0.012,
            P_1_neg=12,
            P_2_neg=20,
            P_3_neg=26,
        )
    d["load"]["1"]["load_node"] = [dict(n=10 + 20 * n, **{MODES[mode][1]: force})]
    return d


def solve(data, route):
    if route == "http":
        response = app.test_client().post("/", json=data)
        assert response.status_code == 200, response.data
        return json.loads(response.data)
    if route == "python":
        # Construct directly, independently of JSON deserialization.
        m = python_axial()
        m.mesh.nodes.clear()
        m.mesh.elements.clear()
        m.boundary.loads.clear()
        for key, xyz in data["node"].items():
            m.add_node(int(key), **xyz)
        nl = data["element"]["1"]["1"]["nonlinear"]
        params = {k: v for k, v in nl.items() if k not in ("type", "hysteresis_dofs")}
        m.add_nonlinear_material(
            1,
            "reference",
            data["element"]["1"]["1"]["E"],
            nu=0.25,
            **({"shear_modulus": data["element"]["1"]["1"]["G"]} if "G" in data["element"]["1"]["1"] else {}),
            **params,
        )
        for key, elem in data["member"].items():
            m.add_nonlinear_bar_element(int(key), [elem["ni"], elem["nj"]], 1, 1, nl["hysteresis_dofs"])
        for load in data["load"]["1"]["load_node"]:
            m.boundary.add_load(
                int(load["n"]), [load.get(k, 0) for k in ("tx", "ty", "tz", "rx", "ry", "rz")]
            )
        for node, bc in data.get("boundary_conditions", {}).get("restraints", {}).items():
            m.boundary.add_restraint(int(node), bc["dof"], bc.get("values"))
        for node, supports in data.get("boundary_conditions", {}).get("spring_supports", {}).items():
            for direction, stiffness in supports.items():
                m.add_spring_support(int(node), direction, stiffness)
        m.analysis_params.update({k: v for k, v in data["load"]["1"].items() if k != "load_node"})
        m.analysis_params.update(data.get("analysis_params", {}))
    else:
        m = json_model(data)
    return wire(m.run())
