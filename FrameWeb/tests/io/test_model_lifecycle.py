"""io / model lifecycle contracts."""

import pytest

from fem.material import BarParameter
from fem.model import FemModel
from tests.support.assertions import assert_axial
from tests.support.builders.input_routes import python_axial
from tests.support.builders.nonlinear_solver import axial_bar

pytestmark = pytest.mark.integration


@pytest.mark.material_nonlinear
def test_failed_file_reload_does_not_leave_previous_success(tmp_path):
    m = python_axial()
    assert_axial(m.run())
    path = tmp_path / "invalid.json"
    path.write_text("{}", encoding="utf-8")
    with pytest.raises(ValueError):
        m.load_model(str(path))
    assert m.get_results() is None


@pytest.mark.material_nonlinear
def test_failed_fem_run_clears_previous_results():
    model = FemModel()
    model.results = {"converged": True}
    mesh, material, boundary, _ = axial_bar()
    model.mesh, model.material, model.boundary = mesh, material, boundary
    material.add_bar_parameter(1, BarParameter(3, 1, 1, 1))
    model.analysis_params["max_iterations"] = 1
    with pytest.raises(RuntimeError, match="converg"):
        model.run("material_nonlinear")
    assert model.get_results() is None


@pytest.mark.material_nonlinear
def test_programmatic_fem_model_has_analysis_defaults():
    model = FemModel()
    mesh, material, boundary, _ = axial_bar()
    model.mesh, model.material, model.boundary = mesh, material, boundary
    model.material.add_bar_parameter(1, BarParameter(3, 1, 1, 1))
    result = model.run("material_nonlinear")
    assert result["node_displacements"][2]["dx"] == pytest.approx(0.01)


@pytest.mark.parametrize("analysis", ["static", "modal", "material_nonlinear"])
def test_missing_topology_has_an_input_error_before_linear_algebra(analysis):
    from fem.model import FemModel

    m = FemModel()
    m.mesh.add_node(1, [0.0, 0.0, 0.0])
    m.results = {"stale": True}
    with pytest.raises(ValueError, match="structural elements.*topology"):
        m.run(analysis)
    assert m.results is None
