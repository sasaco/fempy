import numpy as np

from fem.file_io import result_to_jsonable


def section_cut_result_view(result, model, data):
    """Map IDs and section-cut signs using geometry and src/app/result.py.

    This maps actual output only. It neither computes nor alters reference values.
    Unmapped nodes/fields stay visible as mismatches.
    """
    result = result_to_jsonable(result)
    nodes = data.get("node", {k: dict(zip(("x", "y", "z"), v)) for k, v in data.get("nodes", {}).items()})
    labels = {int(k): k for k in nodes}
    labels.update(getattr(model, "node_labels", {}))
    member_points = {str(p["m"]): sorted(set(p.get("Points", []))) for p in data.get("notice_points", [])}
    sections = {}
    for member_id, member in data.get("member", {}).items():
        start = np.array([nodes[str(member["ni"])][k] for k in ("x", "y", "z")])
        end = np.array([nodes[str(member["nj"])][k] for k in ("x", "y", "z")])
        length = np.linalg.norm(end - start)
        axis = (end - start) / length
        rigid_points = [
            p
            for r in data.get("rigid", [])
            if str(r["m"]) == member_id
            for p in (float(r.get("Ilength", 0)), length - float(r.get("Jlength", 0)))
            if 0 < p < length
        ]
        points = []
        for point in sorted(
            [0.0, length] + rigid_points + [p for p in member_points.get(member_id, []) if 0 < p < length]
        ):
            if not points or point - points[-1] > 1e-10 * max(1.0, length):
                points.append(point)
        candidates = []
        for node, coord in model.mesh.nodes.items():
            t = np.dot(coord - start, axis)
            if (
                node not in labels
                and 0 < t < length
                and np.linalg.norm(coord - start - t * axis) < 1e-8 * max(1.0, length)
            ):
                candidates.append((t, node))
        n_count = l_count = 0
        for t, node in sorted(candidates):
            if any(abs(t - p) < 1e-8 * max(1.0, length) for p in points[1:-1]):
                n_count += 1
                labels[node] = f"{member_id}n{n_count}"
            else:
                l_count += 1
                labels[node] = f"{member_id}l{l_count}"
        elements = []
        for key, element in model.elements.items():
            mesh_data = model.mesh.elements[key]
            if str(mesh_data.get("original_id", key)) != member_id or not hasattr(
                element, "calculate_forces"
            ):
                continue
            ti = np.dot(model.mesh.nodes[element.node_ids[0]] - start, axis)
            tj = np.dot(model.mesh.nodes[element.node_ids[1]] - start, axis)
            elements.append((ti, tj, str(key)))
        segments = {}
        for i, (a, b) in enumerate(zip(points[:-1], points[1:]), 1):
            inside = sorted(e for e in elements if e[0] >= a - 1e-8 and e[1] <= b + 1e-8)
            if not inside:
                continue  # missing segment remains a key mismatch, never a pass
            fi = result["element_stresses"][inside[0][2]]["i_end"]
            fj = result["element_stresses"][inside[-1][2]]["j_end"]
            signs = [-1, 1, 1, -1, -1, 1]
            values = {k + "i": v * s for k, v, s in zip(("fx", "fy", "fz", "mx", "my", "mz"), fi, signs)}
            values.update(
                {k + "j": -v * s for k, v, s in zip(("fx", "fy", "fz", "mx", "my", "mz"), fj, signs)}
            )
            values["L"] = b - a
            segments[f"P{i}"] = values
        sections[member_id] = segments
    displacement = {
        labels.get(int(k), f"unmapped:{k}"): v
        for k, v in result["node_displacements"].items()
        if labels.get(int(k), f"unmapped:{k}") is not None
    }
    reactions = {
        k: {
            out: result["reaction_forces"].get(k, {}).get(src, 0.0)
            for out, src in zip(("tx", "ty", "tz", "mx", "my", "mz"), ("fx", "fy", "fz", "mx", "my", "mz"))
        }
        for k in nodes
        if int(k) in model.boundary.restraints
        and int(k) not in getattr(model.boundary, "auxiliary_restraint_nodes", set())
        or int(k) in getattr(model.boundary, "spring_supports", {})
    }
    if data.get("dimension") == 2:
        generated = sorted(set(model.mesh.nodes) - {int(k) for k in nodes})
        if generated:
            reaction = result["reaction_forces"].get(str(generated[-1]), {})
            reactions["0"] = dict(tx=0.0, ty=0.0, tz=0.0, mx=0.0, my=0.0, mz=reaction.get("mz", 0.0))
    return dict(
        disg=displacement,
        reac=reactions,
        fsec=sections,
        size=len(model.mesh.nodes),
        shell_results=result.get("legacy_shell_results", result.get("shell_results", {})),
    )
