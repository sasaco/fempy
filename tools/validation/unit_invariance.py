"""Reproducible PQ-10 unit-invariance benchmarks.

The three systems describe the same structures with different numeric units.
Run ``python -m tools.validation.unit_invariance`` to write normalized physical
responses and convergence histories as JSON.
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass
import json

from fem.material import BarParameter
from fem.model import FemModel


@dataclass(frozen=True)
class UnitSystem:
    name: str
    length_scale: float
    force_scale: float
    length_unit: str
    force_unit: str
    mass_unit: str

    def length(self, value: float) -> float:
        return value * self.length_scale

    def force(self, value: float) -> float:
        return value * self.force_scale

    def moment(self, value: float) -> float:
        return value * self.force_scale * self.length_scale

    def stress(self, value: float) -> float:
        return value * self.force_scale / self.length_scale**2

    def area(self, value: float) -> float:
        return value * self.length_scale**2

    def inertia(self, value: float) -> float:
        return value * self.length_scale**4

    def density(self, value: float) -> float:
        # K and M must both scale as force/length for unchanged omega**2.
        return value * self.force_scale / self.length_scale**4

    def translational_spring(self, value: float) -> float:
        return value * self.force_scale / self.length_scale

    def rotational_spring(self, value: float) -> float:
        return value * self.force_scale * self.length_scale


UNIT_SYSTEMS = (
    UnitSystem("N-m", 1.0, 1.0, "m", "N", "kg"),
    UnitSystem("N-mm", 1000.0, 1.0, "mm", "N", "N*s^2/mm"),
    UnitSystem("kN-m", 1.0, 0.001, "m", "kN", "t"),
)


def _metadata(model: FemModel, units: UnitSystem) -> None:
    model.model_metadata["units"].update(
        length=units.length_unit,
        force=units.force_unit,
        mass=units.mass_unit,
        time="s",
    )


def _nonlinear_bar_model(
    units: UnitSystem,
    *,
    mode: str,
    nodes: int,
    load: float,
    tolerance: float,
    load_factors=None,
    spring: float | None = None,
    prescribed: float | None = None,
    displacement_target: float | None = None,
    softening: bool = False,
) -> FemModel:
    model = FemModel()
    _metadata(model, units)
    length = 2.0
    for index in range(nodes):
        model.add_node(10 + 20 * index, units.length(length * index / (nodes - 1)), 0, 0)

    if mode == "axial":
        delta = (0.001, 0.004, 0.010)
        resistance = tuple(units.force(value) for value in (10.0, 16.0, 22.0))
        unloading_floor = units.force(100.0)
        delta_4 = P_4 = None
    else:
        delta = tuple(value / units.length_scale for value in (0.001, 0.004, 0.010))
        resistance = tuple(units.moment(value) for value in (10.0, 16.0, 22.0))
        unloading_floor = 100.0 * units.force_scale * units.length_scale**2
        delta_4 = P_4 = None
    if softening:
        delta = (0.001, 0.002, 0.003)
        resistance = tuple(units.force(value) for value in (1.0, 2.0, 3.0))
        unloading_floor = units.force(10.0)
        delta_4, P_4 = 0.004, units.force(2.0)

    model.add_nonlinear_material(
        1,
        "unit-invariance material",
        E=units.stress(10000.0 if not softening else 1000.0),
        delta_1=delta[0],
        delta_2=delta[1],
        delta_3=delta[2],
        P_1=resistance[0],
        P_2=resistance[1],
        P_3=resistance[2],
        delta_4=delta_4,
        P_4=P_4,
        beta=0,
        K_min=unloading_floor,
        nu=0.25,
        density=units.density(7850.0),
        shear_modulus=units.stress(4000.0),
    )
    model.material.add_bar_parameter(
        1,
        BarParameter(
            units.area(1.0),
            units.inertia(1.0),
            units.inertia(1.0),
            units.inertia(1.0),
        ),
    )
    for index in range(nodes - 1):
        model.add_nonlinear_bar_element(
            7 + index,
            [10 + 20 * index, 30 + 20 * index],
            1,
            1,
            [mode],
        )
    model.add_restraint(10, True, True, True, True, True, True)
    tip = 10 + 20 * (nodes - 1)
    if prescribed is not None:
        model.add_restraint(
            tip,
            True,
            False,
            False,
            False,
            False,
            False,
            values=[units.length(prescribed), 0, 0, 0, 0, 0],
        )
    elif mode == "axial":
        model.add_load(tip, fx=units.force(load))
    else:
        model.add_load(tip, mz=units.moment(load))
    if spring is not None:
        model.add_spring_support(tip, "x", units.translational_spring(spring))

    model.analysis_params.update(
        n_load_steps=1,
        max_iterations=50,
        tolerance=tolerance,
    )
    if load_factors is not None:
        model.analysis_params["load_factors"] = list(load_factors)
    if displacement_target is not None:
        model.analysis_params["displacement_control"] = {
            "node": tip,
            "dof": "dx",
            "target": units.length(displacement_target),
        }
    return model


def _history(result: dict) -> dict:
    return {
        "iterations": [step["iterations"] for step in result["step_results"]],
        "relative_residual": [
            record["relative_residual"] for record in result["convergence_history"]
        ],
        "relative_increment": [
            record["relative_du"] for record in result["convergence_history"]
        ],
    }


def bending_load_control(units: UnitSystem) -> dict:
    model = _nonlinear_bar_model(
        units,
        mode="moment_z",
        nodes=5,
        load=18.0,
        tolerance=0.03,
    )
    result = model.run("material_nonlinear")
    tip = result["node_displacements"][90]
    measure = result["metadata"]["solver"]["convergence_measure"]
    return {
        "tip_dy_m": tip["dy"] / units.length_scale,
        "tip_rz_rad": tip["rz"],
        "curvature_per_m": result["curvature"][10]["z"] * units.length_scale,
        "reaction_moment_Nm": result["reaction_forces"][10]["mz"]
        / (units.force_scale * units.length_scale),
        "characteristic_length_m": measure["characteristic_length"]
        / units.length_scale,
        "characteristic_length_source": measure["characteristic_length_source"],
        "convergence_measure": measure["type"],
        **_history(result),
    }


def axial_cyclic_with_spring(units: UnitSystem) -> dict:
    factors = (0.5, 1.0, 0.0, -0.5, -1.0, 0.0, 1.0)
    model = _nonlinear_bar_model(
        units,
        mode="axial",
        nodes=2,
        load=12.0,
        tolerance=1e-9,
        load_factors=factors,
        spring=200.0,
    )
    result = model.run("material_nonlinear")
    return {
        "tip_dx_m": [
            step["node_displacements"][30]["dx"] / units.length_scale
            for step in result["step_results"]
        ],
        "reaction_force_N": [
            step["reaction_forces"][10]["fx"] / units.force_scale
            for step in result["step_results"]
        ],
        "spring_stiffness_N_per_m": units.translational_spring(200.0)
        * units.length_scale
        / units.force_scale,
        **_history(result),
    }


def prescribed_displacement(units: UnitSystem) -> dict:
    model = _nonlinear_bar_model(
        units,
        mode="axial",
        nodes=2,
        load=0.0,
        tolerance=1e-9,
        prescribed=0.004,
    )
    result = model.run("material_nonlinear")
    return {
        "tip_dx_m": result["node_displacements"][30]["dx"] / units.length_scale,
        "reaction_force_N": result["reaction_forces"][10]["fx"] / units.force_scale,
        **_history(result),
    }


def displacement_control(units: UnitSystem) -> dict:
    model = _nonlinear_bar_model(
        units,
        mode="axial",
        nodes=2,
        load=1.0,
        tolerance=1e-9,
        displacement_target=0.007,
        softening=True,
    )
    result = model.run("material_nonlinear")
    return {
        "tip_dx_m": result["node_displacements"][30]["dx"] / units.length_scale,
        "load_factor": result["lambda"],
        **_history(result),
    }


def modal_with_springs(units: UnitSystem) -> dict:
    model = FemModel()
    _metadata(model, units)
    model.add_node(1, 0, 0, 0)
    model.add_node(2, units.length(2.0), 0, 0)
    model.add_material(
        1,
        "modal unit-invariance material",
        E=units.stress(200e9),
        nu=0.3,
        density=units.density(7850.0),
    )
    model.material.add_bar_parameter(
        1,
        BarParameter(
            units.area(0.01),
            units.inertia(1e-5),
            units.inertia(1e-5),
            units.inertia(2e-5),
        ),
    )
    model.add_element(
        1, "bar", [1, 2], 1, section_id=1, shear_correction=False
    )
    for node in (1, 2):
        for direction, stiffness in (
            ("x", units.translational_spring(1e5)),
            ("y", units.translational_spring(1e6)),
            ("z", units.translational_spring(1e6)),
            ("rx", units.rotational_spring(1e4)),
            ("ry", units.rotational_spring(1e4)),
            ("rz", units.rotational_spring(1e4)),
        ):
            model.add_spring_support(node, direction, stiffness)
    result = model.run_modal_analysis(1)
    return {
        "frequency_hz": result["frequencies"][0],
        "density_base": model.material.materials[1].density
        * units.length_scale**4
        / units.force_scale,
        "relative_residual": max(result["eigenpair_residuals"]),
    }


def run_benchmarks() -> dict:
    return {
        "scaling": {
            units.name: {
                "length_numeric_per_m": units.length_scale,
                "force_numeric_per_N": units.force_scale,
                "density_numeric": units.density(7850.0),
            }
            for units in UNIT_SYSTEMS
        },
        "bending_load_control": {
            units.name: bending_load_control(units) for units in UNIT_SYSTEMS
        },
        "axial_cyclic_with_spring": {
            units.name: axial_cyclic_with_spring(units) for units in UNIT_SYSTEMS
        },
        "prescribed_displacement": {
            units.name: prescribed_displacement(units) for units in UNIT_SYSTEMS
        },
        "displacement_control": {
            units.name: displacement_control(units) for units in UNIT_SYSTEMS
        },
        "modal_with_springs": {
            units.name: modal_with_springs(units) for units in UNIT_SYSTEMS
        },
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output")
    args = parser.parse_args(argv)
    result = run_benchmarks()
    rendered = json.dumps(result, indent=2, ensure_ascii=False, allow_nan=False)
    if args.output:
        with open(args.output, "w", encoding="utf-8", newline="\n") as stream:
            stream.write(rendered + "\n")
    else:
        print(rendered)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
