import numpy as np


def cantilever_reference(data, factor=1.0):
    """Three-member y-axis cantilever, tip Fx, base at y=0 (manual 7.21).

    At x measured upward from the loaded end: Vy=F, Mz=-F*x.
    For each element, invert the monotonic skeleton at Mmid, then integrate
    tipward from the fixed end:
    theta_i=theta_j-L*kappa;
    ux_i=ux_j+L*(theta_i+theta_j)/2 + F*L/(G*k*A)+F*L**3/(12*E*Iz).
    Omit F*L/(G*k*A) when G is absent or shear_correction is false.
    Last term is the reference elastic moment-gradient flexibility retained
    by the central-section formulation. Only monotonic symmetric branches with
    positive slope are covered; no cyclic history or plateau inversion.
    """
    assert set(data["node"]) == {"1", "2", "3", "4"}
    assert set(data["member"]) == {"1", "2", "3"}
    load = data["load"]["1"]["load_node"][0]
    assert int(load["n"]) == 1
    force = factor * load["tx"]
    tip_y = data["node"]["1"]["y"]
    disps = {"4": dict.fromkeys(("dx", "dy", "dz", "rx", "ry", "rz"), 0.0)}
    end_forces = {}
    for member_id in ("3", "2", "1"):
        member = data["member"][member_id]
        ni, nj = str(member["ni"]), str(member["nj"])
        yi, yj = data["node"][ni]["y"], data["node"][nj]["y"]
        length = yj - yi
        assert length > 0
        mat = data["element"]["1"][str(member["e"])]
        ei = mat["E"] * mat["Iz"]
        g = mat.get("G", mat["E"] / (2 * (1 + mat.get("nu", 0.3))))
        moment = -force * ((yi + yj) / 2 - tip_y)
        if "nonlinear" in mat:
            nl = mat["nonlinear"]
            assert nl["symmetric"]
            points = [(0.0, 0.0)] + [(nl[f"P_{i}"], nl[f"delta_{i}"]) for i in (1, 2, 3)]
            assert abs(moment) < nl["P_3"], "No unique force-controlled inverse at capacity"
            for (pa, ea), (pb, eb) in zip(points[:-1], points[1:]):
                assert pb > pa and eb > ea
                if abs(moment) <= pb:
                    curvature = np.sign(moment) * (ea + (abs(moment) - pa) * (eb - ea) / (pb - pa))
                    break
        else:
            curvature = moment / ei
        rotation = disps[nj]["rz"] - length * curvature
        shear = (
            force * length / (g * (5 / 6) * mat["A"])
            if "G" in mat and member.get("shear_correction", True)
            else 0.0
        )
        u = (
            disps[nj]["dx"]
            + length * (rotation + disps[nj]["rz"]) / 2
            + shear
            + force * length**3 / (12 * ei)
        )
        disps[ni] = dict(dx=u, dy=0.0, dz=0.0, rx=0.0, ry=0.0, rz=rotation)
        end_forces[member_id] = {
            "i_end": [0.0, -force, 0.0, 0.0, 0.0, -length * force / 2 - moment],
            "j_end": [0.0, force, 0.0, 0.0, 0.0, -length * force / 2 + moment],
        }
    return {
        "node_displacements": disps,
        "element_stresses": end_forces,
        "reaction_forces": {"4": dict(fx=-force, fy=0.0, fz=0.0, mx=0.0, my=0.0, mz=force * tip_y)},
    }
