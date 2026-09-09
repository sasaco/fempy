"""Source-guarded exact pressure reference; --write explicitly applies it."""

from tests.support.oracles.pressure import reference


def completed_data(data):
    assert data["nodes"] == {"1": [0, 0, 0], "2": [1, 0, 0], "3": [1, 1, 0], "4": [0, 1, 0]}
    assert data["elements"] == {"1": dict(type="shell", nodes=[1, 2, 3, 4], material_id=1, thickness=0.01)}
    assert data["materials"] == {"1": dict(name="Steel", E=205e9, nu=0.3, density=7850.0)}
    assert data["boundary_conditions"] == dict(
        restraints={"1": dict(dof=[True] * 6, values=None)},
        pressures=[dict(element_id=1, face="F1", pressure=1000.0)],
    )
    assert set(data) == {"nodes", "elements", "materials", "boundary_conditions", "result"}
    return {"1": reference()}
