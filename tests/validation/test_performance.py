"""PQ-12 benchmark model, schema and regression-gate contracts."""

import copy
import json
from pathlib import Path

from tools.validation.performance import (
    PHASES,
    compare_results,
    environment_fingerprint,
    run_benchmarks,
)


def test_smoke_profile_covers_phases_and_workload_dimensions():
    result = run_benchmarks(profile="smoke", repeats=1, warmups=0)

    assert result["schema_version"] == 1
    assert len(result["provenance"]["tool_sha256"]) == 64
    assert result["provenance"]["git_commit"]
    assert result["environment_fingerprint"] == environment_fingerprint(result["environment"])
    assert {case["id"] for case in result["cases"]} == {
        "beam-small", "precision-small", "shell-small", "solid-small", "history-small"
    }
    for case in result["cases"]:
        workload = case["workload"]
        sample = case["samples"][0]
        assert workload["nodes"] > 1
        assert workload["elements"] > 0
        assert workload["dofs"] > 0
        assert workload["stiffness_nnz"] > 0
        assert set(sample["phase_seconds"]) == set(PHASES)
        assert all(value >= 0 for value in sample["phase_seconds"].values())
        assert sample["wall_seconds"] > 0
        assert sample["phase_seconds"]["solve"] > 0
        assert sample["python_peak_bytes"] > 0
        assert sample["rss_peak_bytes"] is not None
        assert sample["rss_peak_delta_bytes"] is not None
        assert sample["response_checksum"] == sample["response_checksum"]
    history = next(case for case in result["cases"] if case["id"] == "history-small")
    assert history["workload"]["history_steps"] == 3
    assert history["workload"]["load_cases"] == 1
    beam = next(case for case in result["cases"] if case["id"] == "beam-small")
    precision = next(case for case in result["cases"] if case["id"] == "precision-small")
    assert beam["workload"]["high_precision_runs"] == 0
    assert precision["workload"]["high_precision_runs"] == 1


def test_regression_gate_rejects_environment_workload_and_limit_changes():
    baseline = run_benchmarks(profile="smoke", repeats=1, warmups=0)
    assert compare_results(copy.deepcopy(baseline), baseline) == []

    changed = copy.deepcopy(baseline)
    changed["provenance"]["tool_sha256"] = "0" * 64
    assert "tool hash differs" in compare_results(changed, baseline)[0]

    changed = copy.deepcopy(baseline)
    changed["environment"]["python"] = "0.0"
    changed["environment_fingerprint"] = environment_fingerprint(changed["environment"])
    assert "environment fingerprint differs" in compare_results(changed, baseline)[0]

    changed = copy.deepcopy(baseline)
    changed["cases"][0]["workload"]["dofs"] += 1
    assert "workload differs" in compare_results(changed, baseline)[0]

    changed = copy.deepcopy(baseline)
    changed["cases"][0]["summary"]["wall_median_seconds"] = (
        baseline["cases"][0]["summary"]["limits"]["wall_seconds"] * 2
    )
    assert "exceeds" in compare_results(changed, baseline)[0]


def test_committed_baseline_covers_all_pq12_dimensions():
    path = Path(__file__).parents[2] / "docs" / "report" / "product-quality-pq12-baseline.json"
    baseline = json.loads(path.read_text(encoding="utf-8"))

    assert baseline["profile"] == "baseline"
    assert baseline["provenance"]["git_commit"] == "1057788988e7bea6db513e192d75c64a33f80f66"
    assert baseline["provenance"]["git_dirty"] is True
    assert baseline["repeats"] >= 5
    assert baseline["warmups"] >= 1
    assert len(baseline["cases"]) == 13
    assert {case["workload"]["size"] for case in baseline["cases"]} == {
        "small", "medium", "large"
    }
    assert {case["workload"]["family"] for case in baseline["cases"]} == {
        "beam", "precision", "shell", "solid", "history"
    }
    assert max(case["workload"]["load_cases"] for case in baseline["cases"]) == 5
    assert max(case["workload"]["history_steps"] for case in baseline["cases"]) == 50
    assert sum(case["workload"]["high_precision_runs"] for case in baseline["cases"]) == 1
    assert all(value == "1" for value in baseline["environment"]["thread_environment"].values())
