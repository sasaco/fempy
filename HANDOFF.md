# Handoff: Verify Angular and C# result parity with the Ct preset

## Goal

In the next session, use `FrameWebforJS/src/assets/preset/サンプル（Ct桁）.json` to determine whether the result values produced by `FrameWebforJS/src/app/components/result/**/*.ts` match the corresponding implementation in `FrameWebforCS/components/result/*`. Compare actual outputs from the same sample input, including base results and the recently implemented COMBINE/PICKUP views; report every unsupported result type or discrepancy with a reproducible case, mode, and row. This parity check has **not yet been run**.

## Current Progress

- Implemented COMBINE section force and reaction aggregators, immutable input snapshots, load coordinators, and asynchronous WinForms Spread views. The views offer 2D/3D focus modes and reject stale calculation results after edits or reloads.
- Implemented all three PICKUP views with a shared asynchronous table control and separate displacement, section force, and reaction selectors. PICKUP chooses a COMBINE result per focus mode and row while preserving its correlated vector and case label.
- `ResultFsecService` and `ResultReacService` now emit change events and atomically parse finite, nullable values. `InputDataService.JsonDataOpen` starts, completes, or invalidates all three COMBINE coordinators together. Input-only files cannot display results from an earlier file.
- Updated the old displacement UI test that expected 20 PICKUP placeholder sheets. Added aggregator, UI, integration, malformed-input, and stale-result tests under `FrameWebforCS.Tests`.
- Prior implementation validation on 2026-09-27 JST: `dotnet test FrameWeb.sln -c Release --no-restore -clp:ErrorsOnly` passed **48/48**; `dotnet build FrameWeb.sln -c Release --no-restore -clp:ErrorsOnly` passed with 0 errors (1,381 warnings); `git diff --check` passed. These gates have not been rerun for this handoff-only update.
- `& .agents/check.ps1 -AgentOnly` still fails on the pre-existing `.agents/repository.toml` references to missing `tools/FrameWeb.Startup/FrameWeb.Startup.csproj` and `scripts/smoke-local.py`; its scope-isolation and diff checks pass.
- At the start of this handoff update, `git status --short` was clean. The Ct preset exists and is 9,574,957 bytes. Its root has `dimension: 3`, 11 load cases, 11 DEFINE rows, 94 COMBINE rows, 14 PICKUP rows, and 11 result cases. The first result case contains `disg`, `reac`, `fsec`, `shell_fsec`, and `size`. These are inventory facts, **not** parity results.
- During this handoff-only update, `FrameWebforCS/three/SceneService.cs` became modified by another active worker or the user. It was not edited here; inspect its owner before touching it in the next session.

## What Worked

- The existing `ResultCombineDisgAggregator` and coordinator supplied the DEFINE → COMBINE pattern and generation-based cancellation model.
- PICKUP references COMBINE IDs through the **values** of `InputCombineService.PickupRows[row].Coefficients`. In JSON, `"C1":7` selects COMBINE ID 7; `"C7":1` selects ID 1.
- Independent PICKUP UserControls avoided changing COMBINE component base classes. They retain public `setActiveSheet(int)` for `AppRoutingModule` reflection; the sidebar's `option=2` is a route category, not a sheet index.
- FSEC station mismatches are reported explicitly. The old Angular worker has a broken cleanup loop for inconsistent stations, so the C# path fails with a clear error instead of publishing partial results.
- The Ct sample can be parsed with Windows PowerShell `Get-Content -Raw -Encoding utf8 | ConvertFrom-Json`, then inspected through `.PSObject.Properties`. The installed PowerShell does not support `ConvertFrom-Json -AsHashtable`.

## What Didn't Work

- Parallel `dotnet test` runs locked `FrameWebforCS.Tests.dll` through competing `testhost` processes. Run .NET gates sequentially.
- The first PICKUP test fixture used `"C7":1`, which referenced the wrong COMBINE result and produced an empty view. It was corrected to `"C1":7`.
- An FSEC snapshot builder initially used `MoveToImmutable()` before its capacity matched its count; `ToImmutable()` fixed the runtime failure.
- A test helper's unqualified `Action` conflicted with `FarPoint.Win.Spread.Action`; `System.Action` resolved the compile error.
- The numerical comparison requested for the Ct preset has not been attempted. Do not interpret the 48 passing C# tests as evidence that Angular and C# agree on this 9.6 MB sample.

## Next Steps

1. Inventory the executable Angular result path under `FrameWebforJS/src/app/components/result/` and `FrameWebforJS/src/app/providers/result-data.service.ts`, alongside the C# services, aggregators, and views under `FrameWebforCS/components/result/`. Build a coverage map for base displacement/reaction/section force and all six COMBINE/PICKUP results. Identify what `shell_fsec` and `size` mean for this request and mark types without a C# counterpart explicitly.
2. Establish equivalent inputs from the **same Ct preset**. Check the Angular path's expected result contract before invoking it: `result-data.service.ts` currently refers to `AnalysisResultSetIndex`, while this preset has a legacy `result` map. If an adapter or worker harness is needed for comparison, document its field, order, unit, and sign mapping and keep it outside production code. Avoid treating a failed load or empty worker output as a passing comparison.
3. Execute Angular and C# calculations independently. Prefer the actual Angular services/workers or a small reproducible harness that runs their algorithms; do not copy C# formulas into the expected-value generator. For C#, load the preset through `InputDataService.JsonDataOpen` and collect base and COMBINE/PICKUP outputs after asynchronous completion. Use an isolated process or reset singleton services between cases.
4. Compare every available result case and derived ID, each mode, ordered node/member/station row, six-component vector, source-case label, and displayed rounding (displacement 4 decimal places; reaction and section force 2 decimals; section-force station 3 decimals). Distinguish raw numeric differences from formatting or order differences. Record mismatch counts plus the first reproducible mismatch with JSON path, mode, row key, Angular value, and C# value. Confirm that no output silently disappeared.
5. Add focused golden or differential tests only after the expected output is obtained independently, then run the relevant Angular tests and `dotnet test FrameWeb.sln -c Release --no-restore` sequentially. Report verified matches, mismatches, unsupported types, and any input-contract blockers separately. Do not claim full parity from the existing unit tests or from visual appearance alone.
6. If any product code is changed in that follow-up, rerun `dotnet build FrameWeb.sln -c Release --no-restore` and `git diff --check`. The unrelated `.agents/check.ps1 -AgentOnly` repository-path failure should be reported separately.
