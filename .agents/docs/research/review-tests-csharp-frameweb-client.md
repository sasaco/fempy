# Test Coverage and Validation Review: C# FrameWeb Desktop Step 1

## Verdict

**PASS** — Critical: 0, High: 0, Medium: 4, Low: 2.

The current Step 1 implementation is demonstrably buildable and its existing tests and real-context probe pass. No Critical or High test defect was found. The principal gaps are regression-detection gaps: the real OpenGL lifecycle check is not part of `dotnet test`, the UI smoke test does not assert that the form is actually the required DockPanelSuite shell, and the solution/legacy/publish boundaries are only partially executable contracts.

Coverage percentage is **not measured**. No current coverage report or .NET coverage collector/package was found, so no percentage is estimated.

## Scope and Evidence

Reviewed the supplied patch, the Step 1 plan and design decisions, both solution files, all added or changed C# tests, and the production code exercised by those tests. The review focused on Step 1 project boundaries, STA form lifecycle, real OpenGL context lifecycle, assertion strength, test independence, solution membership, restricted-asset exclusion, and the meaning of the reported 61 tests plus the 100-cycle probe.

Fresh verification on 2026-09-20:

- `dotnet build FrameWeb.sln -c Release --no-restore` — PASS, 0 warnings, 0 errors.
- `dotnet test FrameWeb.sln -c Release --no-build` — PASS, 61/61: Core 41, composition/Printing 9, Rendering 10, STA UI 1.
- `dotnet test FramePrintPDF/FramePrintPDF.sln -c Release --no-build` — PASS, the same 61/61 split.
- `dotnet run --project FramePrintPDF/PDF_Manager.RendererProbe/PDF_Manager.RendererProbe.csproj -c Release --no-build -- --verify --cycles 100` — exit 0, `Status=pass`, 100 contexts, 600 frames, 200 captures, and zero live contexts/subscriptions/windows; OpenGL 3.3.0; deterministic sampled colors `#0D1933` and `#F25926`.
- `dotnet sln ... list` — both solutions currently contain the desktop, Core, Rendering, typed Printing, LegacyPrinting, renderer probe, and all four automated test projects; the old manual PDF harness is not active.

## Findings

### [Medium] M-01 — The initialized real-GL lifecycle is not exercised by the automated test graph

- **Evidence:** `FramePrintPDF/PDF_Manager.Rendering.Tests/RendererLifecycleTests.cs:8` covers only disposal before initialization. Its assertions at lines 16-28 never create a real context, upload a model, render, capture, resize, or release GPU objects after initialization. The meaningful initialized-context coverage instead lives in the standalone WinExe at `FramePrintPDF/PDF_Manager.RendererProbe/RendererVerification.cs:10`; its per-cycle create/render/capture/resize/teardown checks are at lines 18-72. `FramePrintPDF/PDF_Manager.Rendering.Tests/PDF_Manager.Rendering.Tests.csproj:23` references only the Rendering library, so both 61-test solution runs can pass without running the probe.
- **Impact:** A regression in the teardown order, context recreation, real pixels, or idle-render behavior can leave all 61 automated tests green. This is material because the implementation history already demonstrated that the real-context path finds defects the pre-initialization unit test cannot. The separate probe and the fresh 100-cycle pass mitigate current release risk, so this is Medium rather than High.
- **Recommended fix:** Add an environment-qualified C# integration gate that launches the probe, requires exit 0, parses the JSON, and validates the cycle/context/capture/live-counter contract. Run a short real-context tier on normal Windows validation and the 100-cycle tier on release/nightly validation. Keep the present no-context unit test for fast deterministic coverage.

### [Medium] M-02 — The STA UI smoke would pass for a plain Form and does not enforce the DockPanelSuite shell contract

- **Evidence:** `FramePrintPDF/PDF_Manager.UiTests/MainFormSmokeTests.cs:18-26` asserts only that `MainForm` becomes visible, appears in `Application.OpenForms`, closes, and leaves no open forms. The required shell behavior is implemented at `FramePrintPDF/PDF_Manager/Shell/MainForm.cs:18-26`, where a `DockPanel` is created with `DockStyle.Fill`, `DocumentStyle.DockingWindow`, and a `VS2015LightTheme`, but none of those properties or owned-resource disposal are asserted. The test uses one create/show/close cycle and does not run a normal `Application.Run` message loop.
- **Impact:** Removing or misconfiguring DockPanelSuite, changing the document style, or introducing a repeated-open lifecycle leak could still leave the single smoke test green.
- **Recommended fix:** Inspect the public control tree and assert exactly one fill-docked `DockPanel`, `DockingWindow` document style, and the expected theme. Exercise closure through a real message loop (for example, schedule `Close` and call `Application.Run(form)`), then repeat creation/show/close/dispose on the same STA thread and assert no open forms or undisposed owned controls remain.

### [Medium] M-03 — Solution membership and the LegacyPrinting compatibility boundary have no executable regression guard

- **Evidence:** `FramePrintPDF/PDF_Manager.Tests/ProjectBoundaryTests.cs:50-60` correctly locks the desktop's three direct project references, but no test or repository check parses either solution, `FramePrintPDF/FramePrintAzure/FramePrintAzure.csproj`, or `FramePrintPDF/PDF_Manager.LegacyPrinting/PDF_Manager.LegacyPrinting.csproj`. The current entries are correct (`FrameWeb.sln:28-44` and `FramePrintPDF/FramePrintPDF.sln:10-26`), Azure currently references the bridge at `FramePrintPDF/FramePrintAzure/FramePrintAzure.csproj:18`, and the bridge is non-packable at `FramePrintPDF/PDF_Manager.LegacyPrinting/PDF_Manager.LegacyPrinting.csproj:9` with the known source exclusion at lines 20-23. None of those facts causes an existing test to fail if it regresses. A removed test project also removes its tests from the observed total instead of producing a failing test.
- **Impact:** Future solution edits can silently weaken the 61-test gate or re-couple Azure to the Windows shell. Changes under the bridge's wildcard source include can also enter compatibility builds without a reviewed allowlist check.
- **Recommended fix:** Add a canonical repository contract check for the exact required project set in both solutions, absence of the manual harness, and expected per-assembly test totals. Add project-graph assertions for `FramePrintAzure -> LegacyPrinting`, `LegacyPrinting` non-packability/explicit exclusions, and the absence of any desktop-to-LegacyPrinting path. Put solution membership in a gate outside the test projects themselves so removing a test project cannot remove the guard.

### [Medium] M-04 — Restricted-source and font assertions inspect XML declarations, not evaluated or publish items

- **Evidence:** `FramePrintPDF/PDF_Manager.Tests/ProjectBoundaryTests.cs:75-90` reads literal `Remove` attributes and checks that expected glob strings are present. The declarations are currently present at `FramePrintPDF/PDF_Manager/PDF_Manager.csproj:13-16`, but the test does not inspect evaluated `Compile`, `Content`, `EmbeddedResource`, `None`, or `ResolvedFileToPublish` items and would not detect a later explicit include or another target copying a restricted file. The compatibility bridge intentionally embeds the legacy fonts at `FramePrintPDF/PDF_Manager.LegacyPrinting/PDF_Manager.LegacyPrinting.csproj:26-39`, which makes a product-scoped evaluated-output check especially important.
- **Impact:** The structural test can remain green while the effective desktop build or publish graph contains a forbidden source/resource. Redistribution remains explicitly NO-GO, so this is not a current shipping approval defect, but the test name and Step 1 boundary claim are stronger than the assertion.
- **Recommended fix:** Add an MSBuild evaluated-item check for the desktop project and a publish-manifest scan that fails on the restricted fonts, legacy `Printing/**`, `PrintInput.cs`, `PrintData.cs`, printable base types, and other forbidden assets. Keep the XML declaration test as a fast intent check, but do not use it as proof of publish contents.

### [Low] L-01 — PrintPageLayout guard and boundary branches are sparsely covered

- **Evidence:** `FramePrintPDF/PDF_Manager.Tests/PrintPageLayoutTests.cs:7-38` covers one valid landscape case, one horizontal equal-to-page margin failure, and one unknown enum. Production validation at `FramePrintPDF/PDF_Manager.Printing/PrintPageLayout.cs:22-41` separately guards positive finite page dimensions, non-negative finite margins, horizontal and vertical printable extents; portrait/default behavior is selected at lines 74-96.
- **Impact:** Regressions in vertical margins, NaN/infinity handling, negative values, zero-sized pages, portrait/default A4, and just-inside/at-boundary values are not localized by tests.
- **Recommended fix:** Add theories covering each numeric parameter with zero, negative, NaN, and both infinities where applicable; vertical and horizontal exact-boundary failures; just-valid margins; and default/explicit portrait dimensions.

### [Low] L-02 — RenderSceneModel omits small public-contract edge cases

- **Evidence:** `FramePrintPDF/PDF_Manager.Rendering.Tests/RenderSceneModelTests.cs:14-55` covers position length/finite checks, input copying, and same/different content. It does not cover the stable-ID guard at `FramePrintPDF/PDF_Manager.Rendering/RenderSceneModel.cs:11`, the null argument guard in `HasSameContent` at lines 24-28, multiple complete triangles, or negative infinity.
- **Impact:** These are straightforward public API branches; failures would be easy to diagnose but the current 10 Rendering test cases overstate lifecycle breadth if read only as a count.
- **Recommended fix:** Add focused theories for null/empty/whitespace stable IDs and both infinity signs, plus facts for `HasSameContent(null)` and a valid two-triangle model.

## Test-Suite Assessment

The 61 tests are meaningful, but the number needs its composition to be understood:

- The 41 Core tests provide substantive deterministic coverage of identity, layout persistence/validation, factory/lifetime policies, and the portable dependency boundary.
- The 9 composition/Printing tests are primarily six architecture/build-intent checks plus three pure page-layout cases. They are valuable boundary locks, not broad printing behavior coverage.
- The 10 Rendering cases include two project-boundary tests, two model behavior facts, five invalid-position theory rows, and one pre-initialization lifecycle test. They deliberately do not create a real GL context.
- The one UI test is a useful STA open/close smoke, but not a DockPanel composition or repeated-lifecycle test.

The separate 100-cycle probe is also meaningful. It uses the production renderer with a real `GLControl`, checks deterministic pixels before and after resize, verifies unchanged model/size idempotence, checks that idle message pumping does not render, validates per-cycle live resources, and recreates the context 100 times. The fresh pass therefore provides strong evidence for the current machine/driver path. Its internal counters are a managed ownership signal rather than an external GPU-leak measurement, and because it is a standalone manual command, it does not currently strengthen the 61-test `dotnet test` result unless the command is explicitly run.

## Priority Summary

| Severity | Count | Merge assessment |
|---|---:|---|
| Critical | 0 | None |
| High | 0 | None |
| Medium | 4 | Follow up to harden regression gates; no current blocker |
| Low | 2 | Add with the next focused test pass |

Overall: **PASS** for Step 1 test/validation review, with the Medium recommendations tracked before relying on the solution test count alone as the long-term lifecycle and redistribution boundary gate.

## Step 2 Addendum (2026-09-20)

### Step 2 Verdict

**PASS** — no Critical or High Step 2 test gap was found. Coverage percentage
remains **not measured** and is not inferred from test counts.

### Evidence

- Core tests pass 75/75 after adding typed-failure tests for `null` required JSON
  objects and duplicate/reverse-ordered moving-load envelope sources.
- Both solution test graphs pass 95/95: Core 75, composition/Printing 9,
  Rendering 10, and STA UI 1.
- The C# suite reads all six positive and seven negative
  `FrameWeb/tests/data/contracts` fixtures in place and asserts that no fixture
  copies exist under the C# test source tree.
- `uv --directory FrameWeb run --locked --extra dev python -m pytest
  tests/io/test_result_contracts.py -q` passes 14/14, retaining the producer-side
  contract gate.
- Focused tests cover deterministic project serialization, strict parsing,
  validation/reference failures, atomic save/cancellation/temp cleanup,
  immutable indexes and failed-candidate preservation, derived-result chaining,
  PICKUP sign behavior, moving paging, and envelope provenance.

### Remaining Test Debt

- No fuzz/property corpus currently explores arbitrary JSON `null` placement,
  extreme nesting near the accepted limit, or large collection/resource
  budgets. Add these with the Step 4 transport limits rather than claiming a
  measured robustness percentage now.
- Existing Step 1 OpenGL/UI/publish-boundary Medium findings remain unchanged;
  Step 2 did not modify those components.

Parallel reviewer runtimes stopped at their usage limit before reviewing code;
this addendum is the lead fallback test review of the gathered Step 2 patch.
