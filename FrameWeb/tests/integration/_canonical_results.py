"""Assertions and selectors for the sole public AnalysisResultSet contract."""

from __future__ import annotations

import numpy as np


DISPLACEMENTS = ("dx", "dy", "dz", "rx", "ry", "rz")
FORCES = ("fx", "fy", "fz", "mx", "my", "mz")


def load_step_results(result_set):
    assert result_set["kind"] == "analysis_result_set"
    rows = result_set["results"]
    assert rows
    assert all(row["state"]["kind"] == "load_step" for row in rows)
    assert [row["state"]["index"] for row in rows] == list(range(len(rows)))
    assert [row["state"]["is_final"] for row in rows] == [False] * (len(rows) - 1) + [True]
    assert all("step_results" not in row for row in rows)
    return rows


def static_result(result_set):
    assert result_set["kind"] == "analysis_result_set"
    assert len(result_set["results"]) == 1
    row = result_set["results"][0]
    assert row["state"] == {"kind": "static", "index": 0}
    return row


def node_components(result):
    return {row["node_id"]: row["components"] for row in result["node_displacements"]}


def reaction_components(result):
    return {row["node_id"]: row["components"] for row in result["support_reactions"]}


def member_results(result):
    return {row["member_id"]: row for row in result["member_section_forces"]}


def canonical_end_forces(raw_values, end):
    fx, fy, fz, mx, my, mz = map(float, raw_values)
    if end == "i_end":
        values = (-fx, fy, fz, -mx, -my, mz)
    else:
        values = (fx, -fy, -fz, mx, my, -mz)
    return dict(zip(FORCES, values, strict=True))


def assert_uniform_result(result, mode, member_count, force, deformation):
    mode_index = {"axial": 0, "torsion": 3, "moment_y": 4, "moment_z": 5}[mode]
    component = DISPLACEMENTS[mode_index]
    nodes = node_components(result)
    for index in range(member_count + 1):
        position = 2 * index / member_count
        expected = np.zeros(6)
        expected[mode_index] = position * deformation
        if mode == "moment_y":
            expected[2] = -position * position * deformation / 2
        if mode == "moment_z":
            expected[1] = position * position * deformation / 2
        np.testing.assert_allclose(
            [nodes[str(10 + 20 * index)][name] for name in DISPLACEMENTS],
            expected,
            rtol=1e-8,
            atol=1e-10,
        )
    raw = np.zeros(6)
    raw[mode_index] = force
    members = member_results(result)
    for index in range(member_count):
        segment = members[str(7 + index)]["segments"][0]
        np.testing.assert_allclose(
            list(segment["i_end"].values()),
            list(canonical_end_forces(-raw, "i_end").values()),
            atol=1e-8,
        )
        np.testing.assert_allclose(
            list(segment["j_end"].values()),
            list(canonical_end_forces(raw, "j_end").values()),
            atol=1e-8,
        )
    expected_reaction = np.zeros(6)
    expected_reaction[mode_index] = -force
    reactions = reaction_components(result)
    np.testing.assert_allclose(
        [reactions["10"][name] for name in FORCES], expected_reaction, atol=1e-8
    )
    assert component in nodes[str(10 + 20 * member_count)]


def assert_step_diagnostics(result):
    iterations = result["diagnostics"]["iterations"]
    assert iterations
    assert iterations[-1]["converged"] is True
    assert [row["index"] for row in iterations] == list(range(len(iterations)))
