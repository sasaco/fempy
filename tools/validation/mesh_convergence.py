"""Reproducible PQ-09 mesh-convergence benchmarks.

The reference fields in this module are closed-form continuum solutions.  The
product solver supplies only the finite-element response; it never supplies an
expected value.  Run with ``python -m tools.validation.mesh_convergence`` to
write the measurements as JSON to stdout.
"""

from __future__ import annotations

import argparse
import json
from math import log

import numpy as np

from fem.material import BarParameter
from fem.model import FemModel


def _order(coarse: float, fine: float, ratio: float = 2.0) -> float:
    if coarse <= 0 or fine <= 0:
        return float("nan")
    return log(coarse / fine) / log(ratio)


def _with_orders(rows: list[dict], size: str = "n") -> list[dict]:
    for previous, current in zip(rows, rows[1:]):
        ratio = current[size] / previous[size]
        current["orders"] = {
            name.removesuffix("_error"): _order(previous[name], current[name], ratio)
            for name in ("displacement_error", "stress_error", "energy_error")
        }
    return rows


def beam_benchmark(n: int) -> dict:
    """Euler--Bernoulli cantilever under a uniform line load.

    The quartic displacement, quadratic moment and strain energy follow from
    ``EI w'''' = q`` with a clamp at x=0 and a free end at x=L.
    """
    length, elastic, inertia, load = 2.0, 1000.0, 4.0, 3.0
    model = FemModel()
    model.add_material(1, "beam benchmark", elastic, 0.25)
    model.material.add_bar_parameter(1, BarParameter(2.0, 3.0, inertia))
    for i in range(n + 1):
        model.add_node(i + 1, length * i / n, 0.0, 0.0)
    for i in range(n):
        model.add_element(
            i + 1, "bar", [i + 1, i + 2], 1, section_id=1,
            shear_correction=False,
        )
        model.add_distributed_load(i + 1, "local_y", load, load)
    model.add_restraint(1, True, True, True, True, True, True)
    result = model.run()

    displacement_squared = reference_displacement_squared = 0.0
    moment_squared = reference_moment_squared = 0.0
    finite_energy = 0.0
    points, weights = np.polynomial.legendre.leggauss(8)
    element_length = length / n
    for i in range(n):
        left = i * element_length
        values = np.array(
            [
                result["node_displacements"][i + 1]["dy"],
                result["node_displacements"][i + 1]["rz"],
                result["node_displacements"][i + 2]["dy"],
                result["node_displacements"][i + 2]["rz"],
            ]
        )
        for point, weight in zip(points, weights):
            s = (point + 1.0) / 2.0
            x = left + element_length * s
            shape = np.array(
                [
                    1 - 3 * s**2 + 2 * s**3,
                    element_length * (s - 2 * s**2 + s**3),
                    3 * s**2 - 2 * s**3,
                    element_length * (-s**2 + s**3),
                ]
            )
            curvature_shape = np.array(
                [
                    (-6 + 12 * s) / element_length**2,
                    (-4 + 6 * s) / element_length,
                    (6 - 12 * s) / element_length**2,
                    (-2 + 6 * s) / element_length,
                ]
            )
            finite_displacement = float(shape @ values)
            exact_displacement = (
                load
                * x**2
                * (6 * length**2 - 4 * length * x + x**2)
                / (24 * elastic * inertia)
            )
            finite_moment = elastic * inertia * float(curvature_shape @ values)
            exact_moment = load * (length - x) ** 2 / 2
            measure = element_length * weight / 2
            displacement_squared += measure * (finite_displacement - exact_displacement) ** 2
            reference_displacement_squared += measure * exact_displacement**2
            moment_squared += measure * (finite_moment - exact_moment) ** 2
            reference_moment_squared += measure * exact_moment**2
            finite_energy += measure * finite_moment**2 / (2 * elastic * inertia)
    exact_energy = load**2 * length**5 / (40 * elastic * inertia)
    return {
        "n": n,
        "dofs": 6 * (n + 1),
        "displacement_error": float(np.sqrt(displacement_squared / reference_displacement_squared)),
        "stress_error": float(np.sqrt(moment_squared / reference_moment_squared)),
        "energy_error": abs(finite_energy / exact_energy - 1.0),
        "tip_displacement": result["node_displacements"][n + 1]["dy"],
    }


def _shell_model(
    formulation: str,
    n: int,
    *,
    thickness: float,
    distortion: float = 0.0,
    aspect: float = 1.0,
) -> tuple[FemModel, dict]:
    length, width, elastic, load = 2.0, 1.0, 1000.0, 1.0e-4
    # hx=2/n and hy=1/ny, so this makes the physical hy/hx ratio ``aspect``.
    ny = max(1, round(n / (2 * aspect)))
    model = FemModel()
    model.add_material(1, "plate benchmark", elastic, 0.0)

    def node(ix: int, iy: int) -> int:
        return ix * (ny + 1) + iy + 1

    for ix in range(n + 1):
        for iy in range(ny + 1):
            x, y = length * ix / n, width * iy / ny
            if 0 < ix < n and distortion:
                x += distortion * length / n * np.sin(np.pi * ix / n) * (2 * y / width - 1)
            model.add_node(node(ix, iy), x, y, 0.0)
    element_id = 1
    for ix in range(n):
        for iy in range(ny):
            corners = [
                node(ix, iy), node(ix + 1, iy),
                node(ix + 1, iy + 1), node(ix, iy + 1),
            ]
            connectivities = (
                [corners]
                if formulation == "mindlin"
                else [[corners[0], corners[1], corners[2]], [corners[0], corners[2], corners[3]]]
            )
            for connectivity in connectivities:
                model.add_element(
                    element_id, "shell", connectivity, 1,
                    thickness=thickness, formulation=formulation,
                )
                model.boundary.add_pressure(element_id, "F1", load)
                element_id += 1
    for iy in range(ny + 1):
        model.add_restraint(node(0, iy), True, True, True, True, True, True)
    result = model.run()
    return model, result


def _triangle_quadrature(order: int = 6):
    points, weights = np.polynomial.legendre.leggauss(order)
    unit, unit_weights = (points + 1) / 2, weights / 2
    for i, r in enumerate(unit):
        for j, v in enumerate(unit):
            yield np.array([r, (1 - r) * v]), unit_weights[i] * unit_weights[j] * (1 - r)


def _quad_quadrature(order: int = 6):
    points, weights = np.polynomial.legendre.leggauss(order)
    for i, r in enumerate(points):
        for j, s in enumerate(points):
            yield np.array([r, s]), weights[i] * weights[j]


def shell_benchmark(
    formulation: str,
    n: int,
    *,
    thickness: float = 0.02,
    distortion: float = 0.0,
    aspect: float = 1.0,
) -> dict:
    """Cantilevered plate strip under uniform pressure, with nu=0.

    DKT is compared with Kirchhoff plate theory.  MITC4 is compared with the
    matching Reissner--Mindlin strip, including the element's 5/6 shear term.
    """
    model, result = _shell_model(
        formulation, n, thickness=thickness, distortion=distortion, aspect=aspect
    )
    length, elastic, load = 2.0, 1000.0, 1.0e-4
    bending_stiffness = elastic * thickness**3 / 12
    shear_stiffness = (5 / 6) * elastic / 2 * thickness
    exact_tip = load * length**4 / (8 * bending_stiffness)
    exact_energy = load**2 * length**5 / (40 * bending_stiffness)
    if formulation == "mindlin":
        exact_tip += load * length**2 / (2 * shear_stiffness)
        exact_energy += load**2 * length**3 / (6 * shear_stiffness)

    tip_nodes = [node for node, xyz in model.mesh.nodes.items() if np.isclose(xyz[0], length)]
    tip = -float(np.mean([result["node_displacements"][node]["dz"] for node in tip_nodes]))

    displacement_squared = reference_displacement_squared = 0.0
    moment_squared = reference_moment_squared = 0.0
    finite_energy = 0.0
    displacement = np.asarray(result["displacement"])
    for element_id, element in model.elements.items():
        indices = model.solver.layout.element_dofs(element_id, element)
        element_displacement = displacement[indices]
        local_coords, basis = element._local_frame()
        local_displacement = (element_displacement.reshape(-1, 3) @ basis.T).ravel()
        quadrature = _triangle_quadrature() if element.n_nodes == 3 else _quad_quadrature()
        for point, weight in quadrature:
            membrane, curvature, shear, determinant = element._strain_matrices(point, local_coords)
            if element.n_nodes == 4:
                shear = element._assumed_quad_shear(point, local_coords)
            xyz = element.get_shape_functions(point) @ element.get_element_coordinates()
            x = float(xyz[0])
            local_moment = thickness**3 / 12 * element.get_stress_strain_matrix() @ (
                curvature @ local_displacement
            )
            local_tensor = np.array(
                [[local_moment[0], local_moment[2], 0.0],
                 [local_moment[2], local_moment[1], 0.0],
                 [0.0, 0.0, 0.0]]
            )
            global_tensor = basis.T @ local_tensor @ basis
            finite_moment = np.array([global_tensor[0, 0], global_tensor[1, 1], global_tensor[0, 1]])
            exact_moment = np.array([load * (length - x) ** 2 / 2, 0.0, 0.0])
            measure = determinant * weight
            nodal_w = local_displacement.reshape(element.n_nodes, 6)[:, 2]
            finite_w = -float(element.get_shape_functions(point) @ nodal_w)
            exact_w = load * x**2 * (6 * length**2 - 4 * length * x + x**2) / (
                24 * bending_stiffness
            )
            if formulation == "mindlin":
                exact_w += load * (length * x - x**2 / 2) / shear_stiffness
            displacement_squared += measure * (finite_w - exact_w) ** 2
            reference_displacement_squared += measure * exact_w**2
            moment_squared += measure * float((finite_moment - exact_moment) @ (finite_moment - exact_moment))
            reference_moment_squared += measure * float(exact_moment @ exact_moment)
            finite_energy += 0.5 * measure * (
                float((curvature @ local_displacement) @ local_moment)
                + float((shear @ local_displacement) @ ((5 / 6) * elastic / 2 * thickness * (shear @ local_displacement)))
            )
    return {
        "n": n,
        "dofs": model.solver.layout.size,
        "ny": max(1, round(n / (2 * aspect))),
        "thickness": thickness,
        "distortion": distortion,
        "aspect": aspect,
        "displacement_error": float(np.sqrt(displacement_squared / reference_displacement_squared)),
        "stress_error": float(np.sqrt(moment_squared / reference_moment_squared)),
        "energy_error": abs(finite_energy / exact_energy - 1.0),
        "tip_displacement": tip,
    }


def _hexa_shape(point: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    r, s, t = point
    signs = np.array(
        [
            [-1, -1, -1], [1, -1, -1], [1, 1, -1], [-1, 1, -1],
            [-1, -1, 1], [1, -1, 1], [1, 1, 1], [-1, 1, 1],
        ],
        dtype=float,
    )
    factors = 1 + signs * np.array([r, s, t])
    shape = np.prod(factors, axis=1) / 8
    derivatives = np.empty((3, 8))
    for axis in range(3):
        other = [index for index in range(3) if index != axis]
        derivatives[axis] = signs[:, axis] * np.prod(factors[:, other], axis=1) / 8
    return shape, derivatives


def _elastic_matrix(elastic: float, poisson: float) -> np.ndarray:
    factor = elastic / ((1 + poisson) * (1 - 2 * poisson))
    matrix = np.zeros((6, 6))
    matrix[:3, :3] = poisson
    np.fill_diagonal(matrix[:3, :3], 1 - poisson)
    np.fill_diagonal(matrix[3:, 3:], (1 - 2 * poisson) / 2)
    return factor * matrix


def solid_benchmark(
    n: int,
    *,
    poisson: float = 0.25,
    distortion: float = 0.0,
    depth: float = 0.25,
) -> dict:
    """Q1 hexahedra for a divergence-free manufactured elasticity field.

    u=(a*x**2, -2*a*x*y, 0), body=(-2*mu*a, 0, 0).  Exact displacement is
    imposed only on the x/y perimeter; interior x/y DOFs are solved.  Although
    the continuum field is isochoric, its Q1 interpolant is not, so the Poisson
    sweep exposes volumetric locking as nu approaches 0.5.
    """
    elastic, coefficient = 1000.0, 0.01
    model = FemModel()
    model.add_material(1, "solid benchmark", elastic, poisson)

    def node(ix: int, iy: int, iz: int) -> int:
        return (ix * (n + 1) + iy) * 2 + iz + 1

    for ix in range(n + 1):
        for iy in range(n + 1):
            x, y = ix / n, iy / n
            if 0 < ix < n and 0 < iy < n and distortion:
                x += distortion / n * np.sin(np.pi * ix / n) * np.sin(np.pi * iy / n)
            for iz in range(2):
                model.add_node(node(ix, iy, iz), x, y, depth * iz)
    element_id = 1
    connectivities = []
    for ix in range(n):
        for iy in range(n):
            connectivity = [
                node(ix, iy, 0), node(ix + 1, iy, 0),
                node(ix + 1, iy + 1, 0), node(ix, iy + 1, 0),
                node(ix, iy, 1), node(ix + 1, iy, 1),
                node(ix + 1, iy + 1, 1), node(ix, iy + 1, 1),
            ]
            model.add_element(element_id, "hexa", connectivity, 1)
            connectivities.append(connectivity)
            element_id += 1

    shear = elastic / (2 * (1 + poisson))
    body = np.array([-2 * shear * coefficient, 0.0, 0.0])
    points, weights = np.polynomial.legendre.leggauss(4)
    for connectivity in connectivities:
        coords = np.array([model.mesh.nodes[node_id] for node_id in connectivity])
        nodal_loads = np.zeros((8, 3))
        for i, r in enumerate(points):
            for j, s in enumerate(points):
                for k, t in enumerate(points):
                    shape, derivatives = _hexa_shape(np.array([r, s, t]))
                    determinant = np.linalg.det(derivatives @ coords)
                    nodal_loads += weights[i] * weights[j] * weights[k] * determinant * shape[:, None] * body
        for node_id, force in zip(connectivity, nodal_loads):
            model.add_load(node_id, fx=force[0], fy=force[1], fz=force[2])

    for ix in range(n + 1):
        for iy in range(n + 1):
            for iz in range(2):
                node_id = node(ix, iy, iz)
                x, y, _ = model.mesh.nodes[node_id]
                exact = [coefficient * x**2, -2 * coefficient * x * y, 0.0]
                perimeter = ix in (0, n) or iy in (0, n)
                model.boundary.add_restraint(
                    node_id,
                    [perimeter, perimeter, True, False, False, False],
                    exact,
                )
    result = model.run()
    displacement = np.asarray(result["displacement"])
    elastic_matrix = _elastic_matrix(elastic, poisson)
    displacement_squared = reference_displacement_squared = 0.0
    stress_squared = reference_stress_squared = 0.0
    finite_energy = exact_energy = 0.0
    for element_id, element in model.elements.items():
        indices = model.solver.layout.element_dofs(element_id, element)
        element_displacement = displacement[indices].reshape(8, 3)
        coords = element.get_element_coordinates()
        for i, r in enumerate(points):
            for j, s in enumerate(points):
                for k, t in enumerate(points):
                    shape, derivatives = _hexa_shape(np.array([r, s, t]))
                    jacobian = derivatives @ coords
                    determinant = np.linalg.det(jacobian)
                    gradient = np.linalg.solve(jacobian, derivatives) @ element_displacement
                    strain = np.array(
                        [
                            gradient[0, 0], gradient[1, 1], gradient[2, 2],
                            gradient[0, 1] + gradient[1, 0],
                            gradient[1, 2] + gradient[2, 1],
                            gradient[0, 2] + gradient[2, 0],
                        ]
                    )
                    xyz = shape @ coords
                    exact_displacement = np.array(
                        [coefficient * xyz[0] ** 2, -2 * coefficient * xyz[0] * xyz[1], 0.0]
                    )
                    exact_strain = np.array(
                        [2 * coefficient * xyz[0], -2 * coefficient * xyz[0], 0.0,
                         -2 * coefficient * xyz[1], 0.0, 0.0]
                    )
                    finite_stress = elastic_matrix @ strain
                    exact_stress = elastic_matrix @ exact_strain
                    measure = weights[i] * weights[j] * weights[k] * determinant
                    finite_displacement = shape @ element_displacement
                    displacement_squared += measure * float(
                        (finite_displacement - exact_displacement) @ (finite_displacement - exact_displacement)
                    )
                    reference_displacement_squared += measure * float(exact_displacement @ exact_displacement)
                    stress_squared += measure * float((finite_stress - exact_stress) @ (finite_stress - exact_stress))
                    reference_stress_squared += measure * float(exact_stress @ exact_stress)
                    finite_energy += 0.5 * measure * float(strain @ finite_stress)
                    exact_energy += 0.5 * measure * float(exact_strain @ exact_stress)
    return {
        "n": n,
        "dofs": model.solver.layout.size,
        "poisson": poisson,
        "distortion": distortion,
        "depth": depth,
        "displacement_error": float(np.sqrt(displacement_squared / reference_displacement_squared)),
        "stress_error": float(np.sqrt(stress_squared / reference_stress_squared)),
        "energy_error": abs(finite_energy / exact_energy - 1.0),
    }


def run_benchmarks(*, quick: bool = False) -> dict:
    sizes = [2, 4] if quick else [2, 4, 8]
    result = {
        "reference": {
            "beam": "Euler--Bernoulli closed form: EI w'''' = q",
            "shell": "Kirchhoff or Reissner--Mindlin cantilever strip, nu=0",
            "solid": "manufactured u=(a*x^2,-2*a*x*y,0), b=(-2*mu*a,0,0)",
        },
        "beam": _with_orders([beam_benchmark(n) for n in sizes]),
        "dkt": _with_orders([shell_benchmark("dkt", n) for n in sizes]),
        "mitc4": _with_orders([shell_benchmark("mindlin", n) for n in sizes]),
        "hexa8": _with_orders([solid_benchmark(n) for n in sizes]),
    }
    if not quick:
        result["limits"] = {
            "dkt_distortion": [shell_benchmark("dkt", 8, distortion=value) for value in (0.0, 0.2, 0.4)],
            "mitc4_distortion": [shell_benchmark("mindlin", 8, distortion=value) for value in (0.0, 0.2, 0.4)],
            "mitc4_thickness": [shell_benchmark("mindlin", 8, thickness=value) for value in (0.2, 0.02, 0.002)],
            "shell_aspect": [shell_benchmark("mindlin", 8, aspect=value) for value in (1.0, 2.0, 4.0)],
            "hexa8_distortion": [solid_benchmark(8, distortion=value) for value in (0.0, 0.2, 0.4)],
            "hexa8_poisson": [solid_benchmark(8, poisson=value) for value in (0.25, 0.49, 0.499)],
            "hexa8_aspect": [solid_benchmark(4, depth=value) for value in (0.025, 0.25, 2.5)],
        }
    return result


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--quick", action="store_true", help="run only 2- and 4-division convergence cases")
    parser.add_argument("--output", help="write JSON to this path instead of stdout")
    arguments = parser.parse_args()
    text = json.dumps(run_benchmarks(quick=arguments.quick), indent=2, ensure_ascii=False, allow_nan=False) + "\n"
    if arguments.output:
        from pathlib import Path

        Path(arguments.output).write_text(text, encoding="utf-8")
    else:
        print(text, end="")


if __name__ == "__main__":
    main()
