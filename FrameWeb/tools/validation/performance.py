"""Reproducible PQ-12 performance measurements and same-host regression checks.

The benchmark deliberately lives outside the normal pytest timing path.  Its
JSON records enough environment and workload information to decide whether two
runs are comparable.  Pytest only checks the model and report contracts.
"""

from __future__ import annotations

import argparse
import gc
import hashlib
import json
import os
import platform
import statistics
import subprocess
import sys
import tempfile
import threading
import time
import tracemalloc
from contextlib import contextmanager
from datetime import datetime, timezone
from pathlib import Path
from typing import Callable

import numpy as np
import scipy
import fem.equilibrium as equilibrium
import fem.linear_precision as linear_precision
import fem.solver as solver_module

from fem.file_io import write_vtk
from fem.material import BarParameter, MaterialProperty
from fem.model import FemModel


SCHEMA_VERSION = 1
PHASES = (
    "element_setup",
    "stiffness_assembly",
    "load_assembly",
    "boundary_conditions",
    "solve",
    "postprocess",
    "model_save",
    "result_save",
    "vtk_save",
)
PROFILE_SIZES = {
    "smoke": {
        "beam": (("small", 4, 1),),
        "precision": (("small", 4, 1),),
        "shell": (("small", 2, 1),),
        "solid": (("small", 1, 1),),
        "history": (("small", 2, 3),),
    },
    "baseline": {
        "beam": (("small", 16, 1), ("medium", 64, 3), ("large", 256, 5)),
        "precision": (("small", 16, 1),),
        "shell": (("small", 2, 1), ("medium", 6, 2), ("large", 12, 3)),
        "solid": (("small", 1, 1), ("medium", 2, 2), ("large", 3, 3)),
        "history": (("small", 2, 5), ("medium", 4, 20), ("large", 8, 50)),
    },
}


def _cpu_name() -> str:
    if platform.system() == "Windows":
        return os.environ.get("PROCESSOR_IDENTIFIER", platform.processor())
    if Path("/proc/cpuinfo").exists():
        for line in Path("/proc/cpuinfo").read_text(errors="replace").splitlines():
            if line.lower().startswith("model name"):
                return line.split(":", 1)[1].strip()
    return platform.processor() or "unknown"


def _rss_bytes() -> int | None:
    if sys.platform == "win32":
        import ctypes
        from ctypes import wintypes

        class ProcessMemoryCounters(ctypes.Structure):
            _fields_ = [
                ("cb", wintypes.DWORD),
                ("PageFaultCount", wintypes.DWORD),
                ("PeakWorkingSetSize", ctypes.c_size_t),
                ("WorkingSetSize", ctypes.c_size_t),
                ("QuotaPeakPagedPoolUsage", ctypes.c_size_t),
                ("QuotaPagedPoolUsage", ctypes.c_size_t),
                ("QuotaPeakNonPagedPoolUsage", ctypes.c_size_t),
                ("QuotaNonPagedPoolUsage", ctypes.c_size_t),
                ("PagefileUsage", ctypes.c_size_t),
                ("PeakPagefileUsage", ctypes.c_size_t),
            ]

        kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        psapi = ctypes.WinDLL("psapi", use_last_error=True)
        kernel32.GetCurrentProcess.restype = wintypes.HANDLE
        psapi.GetProcessMemoryInfo.argtypes = [
            wintypes.HANDLE,
            ctypes.POINTER(ProcessMemoryCounters),
            wintypes.DWORD,
        ]
        psapi.GetProcessMemoryInfo.restype = wintypes.BOOL
        counters = ProcessMemoryCounters()
        counters.cb = ctypes.sizeof(counters)
        ok = psapi.GetProcessMemoryInfo(
            kernel32.GetCurrentProcess(),
            ctypes.byref(counters),
            counters.cb,
        )
        return int(counters.WorkingSetSize) if ok else None
    status = Path("/proc/self/status")
    if status.exists():
        for line in status.read_text(errors="replace").splitlines():
            if line.startswith("VmRSS:"):
                return int(line.split()[1]) * 1024
    return None


class _RssSampler:
    def __init__(self, interval: float = 0.005):
        self.interval = interval
        self.before = _rss_bytes()
        self.peak = self.before
        self._stop = threading.Event()
        self._thread = threading.Thread(target=self._sample, daemon=True)

    def _sample(self) -> None:
        while not self._stop.wait(self.interval):
            value = _rss_bytes()
            if value is not None and (self.peak is None or value > self.peak):
                self.peak = value

    def __enter__(self):
        self._thread.start()
        return self

    def __exit__(self, *_):
        self._stop.set()
        self._thread.join()
        value = _rss_bytes()
        if value is not None and (self.peak is None or value > self.peak):
            self.peak = value


def _beam_model(segments: int, *, high_precision: bool = False) -> tuple[FemModel, int]:
    model = FemModel()
    model.add_material(101, "PQ-12 beam", 210e9, 0.3, density=7850.0)
    model.material.add_bar_parameter(
        101, BarParameter(area=0.01, Iy=8.0e-6, Iz=8.0e-6, J=1.6e-5)
    )
    for index in range(segments + 1):
        model.add_node(index + 1, 4.0 * index / segments, 0.0, 0.0)
    for index in range(segments):
        model.add_element(
            index + 1,
            "beam",
            [index + 1, index + 2],
            101,
            section_id=101,
            shear_correction=not high_precision,
        )
    model.add_restraint(1, True, True, True, True, True, True)
    model.add_load(segments + 1, fy=-1000.0)
    return model, segments + 1


def _shell_model(divisions: int) -> tuple[FemModel, tuple[int, ...]]:
    model = FemModel()
    model.material.add_material(101, MaterialProperty("PQ-12 shell", 30e9, 0.2, density=2400.0))
    ny = max(1, divisions // 2)
    node = lambda i, j: i * (ny + 1) + j + 1
    for i in range(divisions + 1):
        for j in range(ny + 1):
            model.add_node(node(i, j), 4.0 * i / divisions, j / ny, 0.0)
    element_id = 1
    for i in range(divisions):
        for j in range(ny):
            model.add_element(
                element_id,
                "shell",
                [node(i, j), node(i + 1, j), node(i + 1, j + 1), node(i, j + 1)],
                101,
                thickness=0.1,
            )
            element_id += 1
    for j in range(ny + 1):
        model.add_restraint(node(0, j), True, True, True, True, True, True)
    tips = tuple(node(divisions, j) for j in range(ny + 1))
    for tip in tips:
        model.add_load(tip, mz=100.0 / len(tips))
    return model, tips


def _solid_model(divisions: int) -> tuple[FemModel, tuple[int, ...]]:
    model = FemModel()
    model.add_material(101, "PQ-12 solid", 30e9, 0.25, density=2400.0)
    node = lambda i, j, k: i * (divisions + 1) ** 2 + j * (divisions + 1) + k + 1
    for i in range(divisions + 1):
        for j in range(divisions + 1):
            for k in range(divisions + 1):
                model.add_node(node(i, j, k), i / divisions, j / divisions, k / divisions)
    element_id = 1
    for i in range(divisions):
        for j in range(divisions):
            for k in range(divisions):
                model.add_element(
                    element_id,
                    "hexa",
                    [
                        node(i, j, k), node(i + 1, j, k), node(i + 1, j + 1, k), node(i, j + 1, k),
                        node(i, j, k + 1), node(i + 1, j, k + 1),
                        node(i + 1, j + 1, k + 1), node(i, j + 1, k + 1),
                    ],
                    101,
                )
                element_id += 1
    for j in range(divisions + 1):
        for k in range(divisions + 1):
            model.add_restraint(node(0, j, k), True, True, True)
    tips = tuple(node(divisions, j, k) for j in range(divisions + 1) for k in range(divisions + 1))
    for tip in tips:
        model.add_load(tip, fx=1000.0 / len(tips))
    return model, tips


def _history_model(elements: int, steps: int) -> tuple[FemModel, int]:
    model = FemModel()
    model.add_nonlinear_material(
        101,
        "PQ-12 history",
        E=10000.0,
        nu=0.25,
        delta_1=0.001,
        delta_2=0.004,
        delta_3=0.010,
        P_1=10.0,
        P_2=16.0,
        P_3=22.0,
        beta=0.0,
        K_min=100.0,
        shear_modulus=4000.0,
    )
    model.material.add_bar_parameter(101, BarParameter(1.0, 1.0, 1.0, 1.0))
    for index in range(elements + 1):
        model.add_node(index + 1, 2.0 * index / elements, 0.0, 0.0)
    for index in range(elements):
        model.add_nonlinear_bar_element(
            index + 1, [index + 1, index + 2], 101, 101, ["axial"]
        )
    model.add_restraint(1, True, True, True, True, True, True)
    model.add_load(elements + 1, fx=8.0)
    model.analysis_params.update(
        n_load_steps=steps,
        max_iterations=25,
        tolerance=1e-8,
    )
    return model, elements + 1


def _environment() -> dict:
    thread_variables = {
        name: os.environ.get(name)
        for name in ("OMP_NUM_THREADS", "OPENBLAS_NUM_THREADS", "MKL_NUM_THREADS", "NUMEXPR_NUM_THREADS")
    }
    return {
        "python": platform.python_version(),
        "implementation": platform.python_implementation(),
        "platform": platform.platform(),
        "machine": platform.machine(),
        "processor": _cpu_name(),
        "logical_cpus": os.cpu_count(),
        "numpy": np.__version__,
        "scipy": scipy.__version__,
        "thread_environment": thread_variables,
        "timer": "time.perf_counter",
        "memory": "tracemalloc Python peak plus sampled process RSS",
    }


def environment_fingerprint(environment: dict) -> str:
    comparable = {
        key: environment[key]
        for key in (
            "python", "implementation", "platform", "machine", "processor",
            "logical_cpus", "numpy", "scipy", "thread_environment",
        )
    }
    payload = json.dumps(comparable, sort_keys=True, separators=(",", ":")).encode()
    return hashlib.sha256(payload).hexdigest()


def _provenance() -> dict:
    root = Path(__file__).resolve().parents[2]
    try:
        commit = subprocess.check_output(
            ["git", "rev-parse", "HEAD"], cwd=root, text=True, stderr=subprocess.DEVNULL
        ).strip()
        dirty = bool(
            subprocess.check_output(
                ["git", "status", "--porcelain"], cwd=root, text=True, stderr=subprocess.DEVNULL
            ).strip()
        )
    except (OSError, subprocess.CalledProcessError):
        commit, dirty = None, None
    tool = Path(__file__)
    return {
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "git_commit": commit,
        "git_dirty": dirty,
        "tool_sha256": hashlib.sha256(tool.read_bytes()).hexdigest(),
    }


@contextmanager
def _timed_method(target, name: str, phase: str, phases: dict[str, float]):
    original = getattr(target, name)

    def measured(*args, **kwargs):
        start = time.perf_counter()
        try:
            return original(*args, **kwargs)
        finally:
            phases[phase] += time.perf_counter() - start

    setattr(target, name, measured)
    try:
        yield
    finally:
        setattr(target, name, original)


def _measure_once(family: str, size: str, scale: int, load_cases: int, directory: Path) -> dict:
    if family == "beam":
        model, loaded = _beam_model(scale)
        steps = 0
    elif family == "precision":
        model, loaded = _beam_model(scale, high_precision=True)
        steps = 0
    elif family == "shell":
        model, loaded = _shell_model(scale)
        steps = 0
    elif family == "solid":
        model, loaded = _solid_model(scale)
        steps = 0
    else:
        model, loaded = _history_model(scale, load_cases)
        steps = load_cases
        load_cases = 1

    phases = {phase: 0.0 for phase in PHASES}
    contexts = [
        _timed_method(model, "_create_elements", "element_setup", phases),
        _timed_method(model.solver, "create_stiffness_matrix", "stiffness_assembly", phases),
        _timed_method(model.solver, "_assemble_tangent_stiffness", "stiffness_assembly", phases),
        _timed_method(model.solver, "_assemble_internal_forces", "stiffness_assembly", phases),
        _timed_method(model.solver, "assemble_load_vector", "load_assembly", phases),
        _timed_method(model.solver, "apply_boundary_conditions", "boundary_conditions", phases),
        _timed_method(model.solver, "solve_linear_system", "solve", phases),
        _timed_method(model.solver, "_solve_newton_system", "solve", phases),
        _timed_method(equilibrium, "solve_direct_system", "solve", phases),
        _timed_method(equilibrium, "_refine_beam_equilibrium", "solve", phases),
        _timed_method(linear_precision, "solve_precise_frame", "solve", phases),
        _timed_method(model, "_post_process_results", "postprocess", phases),
        _timed_method(solver_module, "snapshot", "postprocess", phases),
        _timed_method(solver_module, "final_result", "postprocess", phases),
    ]
    gc.collect()
    tracemalloc.start()
    start = time.perf_counter()
    with _RssSampler() as rss:
        for context in contexts:
            context.__enter__()
        try:
            result = None
            high_precision_runs = 0
            base_loads = {node: load.forces.copy() for node, load in model.boundary.loads.items()}
            for case_index in range(load_cases):
                factor = (case_index + 1) / load_cases
                for node, values in base_loads.items():
                    model.boundary.loads[node].forces = factor * values
                result = model.run("material_nonlinear" if family == "history" else "static")
                high_precision_runs += int(result["metadata"]["solver"]["high_precision"])
        finally:
            for context in reversed(contexts):
                context.__exit__(None, None, None)
        assert result is not None
        path = directory / f"{family}-{size}"
        phase_start = time.perf_counter()
        model.save_model(path.with_suffix(".model.json"))
        phases["model_save"] = time.perf_counter() - phase_start
        phase_start = time.perf_counter()
        model.save_results(path.with_suffix(".result.json"))
        phases["result_save"] = time.perf_counter() - phase_start
        phase_start = time.perf_counter()
        write_vtk({"mesh": model.mesh}, result, path.with_suffix(".vtk"))
        phases["vtk_save"] = time.perf_counter() - phase_start
    wall = time.perf_counter() - start
    _, python_peak = tracemalloc.get_traced_memory()
    tracemalloc.stop()
    stiffness = model.solver.assembled_stiffness
    if stiffness is None:
        stiffness = model.solver._assemble_tangent_stiffness(
            model.mesh,
            model.material,
            model.elements,
            np.asarray(result["displacement"]),
            model.solver.layout.stride,
        )
    response = result["node_displacements"][loaded if isinstance(loaded, int) else loaded[0]]
    return {
        "family": family,
        "size": size,
        "scale": scale,
        "nodes": len(model.mesh.nodes),
        "elements": len(model.mesh.elements),
        "dofs": model.solver.layout.size,
        "stiffness_nnz": int(stiffness.nnz),
        "load_cases": load_cases,
        "history_steps": steps,
        "high_precision_runs": high_precision_runs,
        "wall_seconds": wall,
        "phase_seconds": phases,
        "python_peak_bytes": python_peak,
        "rss_before_bytes": rss.before,
        "rss_peak_bytes": rss.peak,
        "rss_peak_delta_bytes": (
            max(0, rss.peak - rss.before)
            if rss.peak is not None and rss.before is not None
            else None
        ),
        "response_checksum": float(sum(response.values())),
    }


def _summary(samples: list[dict]) -> dict:
    walls = [sample["wall_seconds"] for sample in samples]
    python_peaks = [sample["python_peak_bytes"] for sample in samples]
    rss_peaks = [sample["rss_peak_bytes"] for sample in samples if sample["rss_peak_bytes"] is not None]
    rss_deltas = [
        sample["rss_peak_delta_bytes"]
        for sample in samples
        if sample["rss_peak_delta_bytes"] is not None
    ]
    phase_medians = {
        phase: statistics.median(sample["phase_seconds"][phase] for sample in samples)
        for phase in PHASES
    }
    median = statistics.median(walls)
    deviations = [abs(value - median) for value in walls]
    mad = statistics.median(deviations)
    return {
        "wall_median_seconds": median,
        "wall_min_seconds": min(walls),
        "wall_max_seconds": max(walls),
        "wall_mad_seconds": mad,
        "phase_median_seconds": phase_medians,
        "python_peak_max_bytes": max(python_peaks),
        "rss_peak_max_bytes": max(rss_peaks) if rss_peaks else None,
        "rss_peak_delta_max_bytes": max(rss_deltas) if rss_deltas else None,
        "limits": {
            "wall_seconds": max(max(walls) * 1.10, median * 1.25, median + 6 * mad),
            "python_peak_bytes": max(python_peaks) + max(1_048_576, int(max(python_peaks) * 0.10)),
            "rss_peak_delta_bytes": (
                max(rss_deltas) + max(4_194_304, int(max(rss_deltas) * 0.20)) if rss_deltas else None
            ),
        },
    }


def run_benchmarks(*, profile: str = "baseline", repeats: int = 5, warmups: int = 1) -> dict:
    if profile not in PROFILE_SIZES:
        raise ValueError(f"Unknown profile: {profile}")
    if repeats <= 0 or warmups < 0:
        raise ValueError("repeats must be positive and warmups nonnegative")
    environment = _environment()
    cases = []
    with tempfile.TemporaryDirectory(prefix="fempython-pq12-") as temporary:
        directory = Path(temporary)
        for family, configurations in PROFILE_SIZES[profile].items():
            for size, scale, load_cases in configurations:
                for _ in range(warmups):
                    _measure_once(family, size, scale, load_cases, directory)
                samples = [
                    _measure_once(family, size, scale, load_cases, directory)
                    for _ in range(repeats)
                ]
                cases.append({
                    "id": f"{family}-{size}",
                    "workload": {
                        key: samples[0][key]
                        for key in (
                            "family", "size", "scale", "nodes", "elements", "dofs",
                            "stiffness_nnz", "load_cases", "history_steps",
                            "high_precision_runs",
                        )
                    },
                    "samples": samples,
                    "summary": _summary(samples),
                })
    return {
        "schema_version": SCHEMA_VERSION,
        "provenance": _provenance(),
        "profile": profile,
        "repeats": repeats,
        "warmups": warmups,
        "environment": environment,
        "environment_fingerprint": environment_fingerprint(environment),
        "cases": cases,
    }


def compare_results(current: dict, baseline: dict) -> list[str]:
    failures = []
    if current.get("schema_version") != baseline.get("schema_version"):
        failures.append("schema version differs")
    if current.get("provenance", {}).get("tool_sha256") != baseline.get("provenance", {}).get("tool_sha256"):
        failures.append("benchmark tool hash differs; regenerate the baseline deliberately")
        return failures
    if current.get("environment_fingerprint") != baseline.get("environment_fingerprint"):
        failures.append("environment fingerprint differs; measurements are not comparable")
        return failures
    expected = {case["id"]: case for case in baseline["cases"]}
    actual = {case["id"]: case for case in current["cases"]}
    if set(actual) != set(expected):
        failures.append(f"case IDs differ: current={sorted(actual)}, baseline={sorted(expected)}")
        return failures
    for case_id, case in actual.items():
        reference = expected[case_id]
        if case["workload"] != reference["workload"]:
            failures.append(f"{case_id}: workload differs")
            continue
        limits = reference["summary"]["limits"]
        observed = case["summary"]
        for observed_key, limit_key in (
            ("wall_median_seconds", "wall_seconds"),
            ("python_peak_max_bytes", "python_peak_bytes"),
            ("rss_peak_delta_max_bytes", "rss_peak_delta_bytes"),
        ):
            limit = limits[limit_key]
            value = observed[observed_key]
            if limit is not None and value is not None and value > limit:
                failures.append(f"{case_id}: {observed_key} {value} exceeds {limit}")
    return failures


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", choices=sorted(PROFILE_SIZES), default="baseline")
    parser.add_argument("--repeats", type=int, default=5)
    parser.add_argument("--warmups", type=int, default=1)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--compare", type=Path)
    arguments = parser.parse_args(argv)
    result = run_benchmarks(
        profile=arguments.profile,
        repeats=arguments.repeats,
        warmups=arguments.warmups,
    )
    arguments.output.parent.mkdir(parents=True, exist_ok=True)
    arguments.output.write_text(
        json.dumps(result, indent=2, ensure_ascii=False, allow_nan=False) + "\n",
        encoding="utf-8",
    )
    if arguments.compare:
        baseline = json.loads(arguments.compare.read_text(encoding="utf-8"))
        failures = compare_results(result, baseline)
        if failures:
            for failure in failures:
                print(f"FAIL: {failure}")
            return 1
    print(f"wrote {arguments.output} ({len(result['cases'])} cases)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
