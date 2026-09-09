"""Source-guarded completion of solid reaction references.

No production FEM imports. Ordinary execution is read-only; --write adds the
independent reactions and the source-model-derived empty beam/shell fields.
Quadratic solids use original original source shapes at 50 digits and two-part stiffness.
"""

import copy

from tests.support.repairs.solid_sources import repaired_data

STEMS = ("sampleBendHexa1", "sampleBendWedge1", "sampleBendHexa2", "sampleBendWedge2", "sampleBendTetra2")


def completed_reference(data, source, independent):
    repaired_data(data, source)  # Full source/input identity, including DOFs.
    assert independent["maximum_free_force_residual"] <= 1e-10
    assert independent["hashes"][source["path"]] == source["sha256"]
    assert set(independent["node_ids"]) == set(source["nodes"])
    assert set(independent["reac"]) == set(source["restraints"])
    fields = dict(reac=independent["reac"], size=len(source["nodes"]), fsec={}, shell_results={})
    fixed = copy.deepcopy(data)
    for key, value in fields.items():
        old = fixed["result"]["1"]
        assert key not in old or old[key] == value, f"Unknown existing reference: {key}"
        old[key] = copy.deepcopy(value)
    return fixed
