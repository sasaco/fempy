"""Independent statics oracle for the saved JR K4 pure-bending history."""

from decimal import Decimal


DISPLACEMENT_KEYS = ("dx", "dy", "dz", "rx", "ry", "rz")
REACTION_KEYS = ("tx", "ty", "tz", "mx", "my", "mz")
SECTION_KEYS = ("fxi", "fyi", "fzi", "mxi", "myi", "mzi", "fxj", "fyj", "fzj", "mxj", "myj", "mzj")


def _decimal(value):
    return Decimal(str(value))


def _skeleton_moment(curvature, nonlinear):
    """Evaluate the positive monotonic JR polygon without product imports."""
    points = [(Decimal(0), Decimal(0))] + [
        (_decimal(nonlinear[f"delta_{i}"]), _decimal(nonlinear[f"P_{i}"]))
        for i in range(1, 5)
    ]
    for (delta_i, moment_i), (delta_j, moment_j) in zip(points, points[1:]):
        if curvature <= delta_j:
            return moment_i + (moment_j - moment_i) * (curvature - delta_i) / (delta_j - delta_i)
    delta_i, moment_i = points[-2]
    delta_j, moment_j = points[-1]
    return moment_j + (moment_j - moment_i) * (curvature - delta_j) / (delta_j - delta_i)


def jr_k4_bending_history(data):
    """Derive every stored field from the JR polygon and cantilever statics."""
    member_id, member = next(iter(data["member"].items()))
    node_i, node_j = str(member["ni"]), str(member["nj"])
    start, end = data["node"][node_i], data["node"][node_j]
    length = sum((_decimal(end[k]) - _decimal(start[k])) ** 2 for k in ("x", "y", "z")).sqrt()
    nonlinear = data["element"]["1"][str(member["e"])]["nonlinear"]
    load = next(iter(data["load"].values()))
    base_moment = _decimal(load["load_node"][0]["rz"])
    targets = load["displacement_control"]["targets"]
    history = {}

    for index, target_value in enumerate(targets, 1):
        target = _decimal(target_value)
        curvature = target / length
        moment = _skeleton_moment(curvature, nonlinear)
        displacement = {key: 0.0 for key in DISPLACEMENT_KEYS}
        displacement["dy"] = float(curvature * length * length / 2)
        displacement["rz"] = float(target)
        reaction = {key: 0.0 for key in REACTION_KEYS}
        reaction["mz"] = float(-moment)
        section = {key: 0.0 for key in SECTION_KEYS}
        section["mzi"] = section["mzj"] = float(-moment)
        section["L"] = float(length)
        history[str(index)] = {
            "lambda": float(moment / base_moment),
            "control_displacement": float(target),
            "curvature": {member_id: {"y": 0.0, "z": float(curvature)}},
            "disg": {
                node_i: {key: 0.0 for key in DISPLACEMENT_KEYS},
                node_j: displacement,
            },
            "reac": {node_i: reaction},
            "fsec": {member_id: {"P1": section}},
        }
    return history
