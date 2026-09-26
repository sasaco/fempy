# Handoff: C# result COMBINE and PICKUP views

## Goal

Reproduce the Angular `result-combine-fsec`, `result-combine-reac`, `result-pickup-disg`, `result-pickup-fsec`, and `result-pickup-reac` screens in `FrameWebforCS/components/result`, following the background aggregation and display pattern already implemented for `ResultCombineDisgComponent`.

## Current Progress

- Implemented COMBINE section force and reaction aggregators, immutable input snapshots, load coordinators, and asynchronous WinForms Spread views. The views offer 2D/3D focus modes and reject stale calculation results after edits or reloads.
- Implemented all three PICKUP views with a shared asynchronous table control and separate displacement, section force, and reaction selectors. PICKUP chooses a COMBINE result per focus mode and row while preserving its correlated vector and case label.
- `ResultFsecService` and `ResultReacService` now emit change events and atomically parse finite, nullable values. `InputDataService.JsonDataOpen` starts, completes, or invalidates all three COMBINE coordinators together. Input-only files cannot display results from an earlier file.
- Updated the old displacement UI test that expected 20 PICKUP placeholder sheets. Added aggregator, UI, integration, malformed-input, and stale-result tests under `FrameWebforCS.Tests`.
- Final validation on 2026-09-27 JST: `dotnet test FrameWeb.sln -c Release --no-restore -clp:ErrorsOnly` passed **48/48**; `dotnet build FrameWeb.sln -c Release --no-restore -clp:ErrorsOnly` passed with 0 errors (1,381 warnings); `git diff --check` passed. Changes remain uncommitted.
- `& .agents/check.ps1 -AgentOnly` still fails on the pre-existing `.agents/repository.toml` references to missing `tools/FrameWeb.Startup/FrameWeb.Startup.csproj` and `scripts/smoke-local.py`; its scope-isolation and diff checks pass.

## What Worked

- The existing `ResultCombineDisgAggregator` and coordinator supplied the DEFINE → COMBINE pattern and generation-based cancellation model.
- PICKUP references COMBINE IDs through the **values** of `InputCombineService.PickupRows[row].Coefficients`. In JSON, `"C1":7` selects COMBINE ID 7; `"C7":1` selects ID 1.
- Independent PICKUP UserControls avoided changing COMBINE component base classes. They retain public `setActiveSheet(int)` for `AppRoutingModule` reflection; the sidebar's `option=2` is a route category, not a sheet index.
- FSEC station mismatches are reported explicitly. The old Angular worker has a broken cleanup loop for inconsistent stations, so the C# path fails with a clear error instead of publishing partial results.

## What Didn't Work

- Parallel `dotnet test` runs locked `FrameWebforCS.Tests.dll` through competing `testhost` processes. Run .NET gates sequentially.
- The first PICKUP test fixture used `"C7":1`, which referenced the wrong COMBINE result and produced an empty view. It was corrected to `"C1":7`.
- An FSEC snapshot builder initially used `MoveToImmutable()` before its capacity matched its count; `ToImmutable()` fixed the runtime failure.
- A test helper's unqualified `Action` conflicted with `FarPoint.Win.Spread.Action`; `System.Action` resolved the compile error.

## Next Steps

1. Review the uncommitted changes with `git status --short` and `git diff`; inspect new untracked aggregator/coordinator/test files as well as tracked diffs.
2. Run `dotnet test FrameWeb.sln -c Release --no-restore` and `dotnet build FrameWeb.sln -c Release --no-restore` sequentially after any follow-up edit. Recheck `git diff --check`.
3. If visual sign-off is required, open actual 2D and 3D projects in the Windows client and compare the five screens with the corresponding Angular screens. Automated STA tests cover values and lifecycle, but do not provide side-by-side screenshots.
4. If infrastructure configuration is addressed separately, rerun `& .agents/check.ps1 -AgentOnly`; keep its repository-path failure separate from the passing C# product tests.
