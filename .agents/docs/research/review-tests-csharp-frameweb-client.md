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

## Step 3 Addendum (2026-09-20)

### Step 3 Verdict

**PASS** — Critical: 0, High: 0, Medium: 0, Low: 3.

No blocking Step 3 test defect was found. The 37 STA UI cases provide direct,
assertion-bearing coverage of the required docking identity/lifetime, layout,
activation, command, close, cancellation, exception, localization, and repeated
cleanup behaviors. The remaining findings concern three narrow regression-test
gaps; they do not contradict the clean Step 3 execution evidence.

Coverage is **NOT MEASURED**. `PDF_Manager.UiTests.csproj` contains no coverage
collector, no `.runsettings` or coverage report is present, and no percentage is
estimated from test counts.

### Scope and Independent Evidence

Reviewed the complete Step 3 patch, current shell/lifecycle implementation, all
files under `FramePrintPDF/PDF_Manager.UiTests`, the Step 3 plan acceptance
criteria, and the retained Step 1/2 findings above. The review emphasized happy,
error, and boundary assertions; deterministic STA isolation; cancellation and
coalescing races; validate-before-side-effect layout restore; event/form leaks;
and failure specificity.

- Lead clean evidence: both solution test graphs pass 131/131; the UI project
  passes 37/37; targeted format and Agent-only gates pass.
- Independent reviewer rerun:
  `dotnet test FramePrintPDF/PDF_Manager.UiTests/PDF_Manager.UiTests.csproj -c Release --no-build --no-restore`
  — PASS, 37/37, 0 failed/skipped, 12 seconds.
- The 37 cases comprise activation/reducer 6, close/cancellation/exception 9,
  registry 4, layout 3, localization 5, integrated MainForm 6, repeated shell
  smoke 1, and command-state 3.

### Acceptance and Regression Map

| Required behavior | Direct test evidence | Assessment |
|---|---|---|
| Same-key reuse and multi-document coexistence | `DockContentRegistryTests.cs:10-72` checks one tool factory call, same instance, two distinct document instances, selective disposal/removal, and remaining identity. | Covered |
| Tool hide/reuse and document dispose/remove | `DockContentRegistryTests.cs:34-43,65-71` checks the explicit registry close path, hidden non-disposed tool reuse, and disposed/removed document behavior. | Covered for the shell API; see S3-L01 for the dock-tab close path |
| Invalid factories | `DockContentRegistryTests.cs:76-104` checks null delegate, null result, unkeyed result, mismatched key, wrapped factory failure/inner exception, and zero published content. | Covered |
| Duplicate subscriptions and event leaks | `DockContentRegistryTests.cs:108-140` performs 50 document open/close cycles and requires exactly 50 factory/created/activated/removed events and an empty registry. | Covered |
| Layout state/order/bounds/active document | `DockLayoutAdapterTests.cs:10-51` captures five ordered panes including a float, round-trips deterministic JSON into a fresh host, and reasserts the active document. | Covered |
| Unknown version/key and whitelist-only restore | `DockLayoutAdapterTests.cs:54-71` supplies validly shaped unknown-version and unknown-key JSON and requires zero factories/panes before rejection. | Behavior covered; see S3-L02 for exception specificity |
| Pure activation and stale completion | `ActivationLifecycleTests.cs:13-56` asserts immutability, revision progression, obsolete completion/cancel/failure suppression, and explicit current cancel/fault state. | Covered |
| Coalescing, serialization, cancellation, sync-completion race | `ActivationLifecycleTests.cs:59-181` uses controllable tasks, observes prior-token cancellation, requires maximum concurrency 1/latest-wins, proves a pending request is coalesced, preserves canceled state, and completes 100 synchronous activations in exact order. | Covered |
| Command state | `ShellCommandStateTests.cs:9-63` checks no-document, dirty-with-results, and running-operation matrices including cancel/close behavior. | Covered |
| Dirty close confirmation | `CloseCancellationBoundaryTests.cs:10-61` checks clean bypass and all Save/Discard/Cancel decisions; `MainFormIntegrationTests.cs:77-126` proves cancel keeps the form alive and save happens before disposal. | Covered |
| Cancellation ownership and status preservation | `CloseCancellationBoundaryTests.cs:64-93` covers replacement ownership and token observation after owner disposal; `MainFormIntegrationTests.cs:160-185` verifies running-command state, token cancellation, state restoration, and the final canceled status text. | Covered |
| User-safe exception boundary | `CloseCancellationBoundaryTests.cs:96-144` checks typed message/diagnostics, unexpected-detail redaction, and silent cancellation; `MainFormIntegrationTests.cs:130-156` proves event-boundary containment and visible diagnostics. | Covered |
| Language switching | `LocalizationServiceTests.cs:8-51` covers ja/en/zh, deduplicated notifications, and unknown resources; `MainFormIntegrationTests.cs:52-73,201-242` checks all menu, pane, navigation, editor, status, viewport, title, and culture updates. | Covered |
| STA isolation and open-form cleanup | `TestAssembly.cs:3` disables in-process parallelization; `StaTestRunner.cs:9-55` creates named STA threads with pre/post `Application.OpenForms` assertions and cleanup; `MainFormSmokeTests.cs:9-35` repeats 10 complete shell lifecycles. | Covered, with timeout caveat S3-L03 |

### Step 3 Findings

#### [Low] S3-L01 — The actual DockPanel close-button path is not exercised

- **Evidence:** Tool lifetime is asserted through `host.Registry.Close(key)` at
  `DockContentRegistryTests.cs:34`, which calls the explicit shell policy at
  `DockContentRegistry.cs:113-131`. Production also sets `HideOnClose` at
  `DockContentRegistry.cs:253,310` and `ShellDockContent.cs:20`, but no test
  invokes `DockContent.DockHandler.Close()`, the DockPanelSuite path used by a
  dock-tab close button. Direct WinForms `Form.Close()` is a different API and
  is not the intended tool-close contract.
- **Impact:** A future DockPanel integration change could break user-initiated
  tool hide/reuse while the registry-command test remains green. Current
  production configuration and registry behavior are correct, so this is Low.
- **Recommended fix:** Add an STA test that opens a tool and a document, invokes
  each content's `DockHandler.Close()`, pumps messages, and asserts hidden/reused
  tool versus disposed/removed document with exact event counts.

#### [Low] S3-L02 — Invalid layout tests accept any exception type

- **Evidence:** `DockLayoutAdapterTests.cs:67` uses
  `Assert.ThrowsAny<Exception>` for both the version-2 layout and an
  unregistered key. Production distinguishes these through
  `UnknownContractVersionException` and `UnknownContentKeyException` before
  restore side effects (`DockLayoutAdapter.cs:63-68`). The zero-created/empty
  assertions at test lines 69-70 correctly prove validate-before-create, but a
  different parser or implementation exception would also satisfy line 67.
- **Impact:** Rejection and atomicity are protected, but the typed error boundary
  could regress unnoticed.
- **Recommended fix:** Split the theory into named facts and require the exact
  exception type plus its version/key payload while retaining the zero-side-
  effect assertions.

#### [Low] S3-L03 — An STA timeout can leave its background thread running

- **Evidence:** `StaTestRunner.cs:15-26` starts an `IsBackground` STA thread and
  returns a failed assertion if `Join` times out. It cannot stop that thread;
  cleanup at lines 48-54 runs only when the action eventually exits. In-process
  test parallelism is correctly disabled, but that does not prevent another
  `dotnet test` process from contending for the same UI resources or a timed-out
  thread from overlapping later cases in its process.
- **Impact:** A slow/hung UI case can produce cascading open-form failures that
  obscure the first cause. The clean 37/37 runs and bounded waits make this a
  harness-diagnostics issue rather than a product blocker.
- **Recommended fix:** Run only one UI-test process at a time. For stronger
  isolation, execute the STA fixture in a killable child process or terminate
  the test host after a timeout, preserving the first timeout as the primary
  diagnostic instead of continuing with shared WinForms state.

### Continuity with Earlier Reviews

- Step 3 substantially closes the original Step 1 M-02 gap: tests now assert
  the four DockPanelSuite pane roles/states, command composition, runtime
  localization, and repeated shell teardown with no open forms. The smoke still
  uses `Show`/message pumping rather than a full `Application.Run` loop, so the
  historical recommendation remains relevant for a later end-to-end tier.
- Step 1 M-01/M-03/M-04, L-01/L-02 and the Step 2 resource/fuzz debt are outside
  this shell-only change and remain unchanged.
- The legacy host security findings and redistribution NO-GO are not altered by
  these tests.

### Step 3 Priority Summary

| Severity | Count | Merge assessment |
|---|---:|---|
| Critical | 0 | None |
| High | 0 | None |
| Medium | 0 | None |
| Low | 3 | Follow-up hardening; no Step 3 blocker |

Overall: **PASS** for Step 3 test/validation review. Required regressions are
covered with deterministic, bounded STA tests and strong state/event
assertions; coverage percentage remains **NOT MEASURED**.

## Final Remediation Re-review (2026-09-20)

### Final Verdict

**PASS** for the remediated Step 3 test surface. Outstanding Step 3 findings:
Critical 0, High 0, Medium 0, Low 1. The one residual is the STA harness timeout
isolation limitation, not a product-lifecycle failure. Coverage remains **NOT
MEASURED** because `PDF_Manager.UiTests.csproj:13-19` configures the test SDK
and runner but no coverage collector, `.runsettings`, or current coverage
report exists.

This section supersedes the Step 3 counts at lines 123-151 without rewriting
their historical evidence. The UI suite has grown from 37 to 74 cases and the
remediation-specific tests now exercise the real DockPanel close API, exact
typed layout failures, serialized dirty-document transitions, bounded
asynchronous close, production layout storage, cancellation classification,
and late-completion rejection.

### Fresh Execution Evidence

- `dotnet build FramePrintPDF/PDF_Manager.UiTests/PDF_Manager.UiTests.csproj -c Release --no-restore --disable-build-servers`
  - PASS, 0 warnings, 0 errors.
- `dotnet test FramePrintPDF/PDF_Manager.UiTests/PDF_Manager.UiTests.csproj -c Release --no-build --no-restore --disable-build-servers`
  - PASS, 74/74, 0 failed, 0 skipped, 8 seconds.
- Focused results, all run serially from a freshly rebuilt Release output:
  - `Step3BoundaryRemediationTests`: 7/7 PASS.
  - `Step3MainFormRemediationTests`: 6/6 PASS.
  - `ShellLayoutStoreTests`: 7/7 PASS.
  - `MainFormCancellationClassificationTests`: 6/6 PASS.
  - `Step3DockingRemediationTests`: 5/5 PASS.
  - `Step3LifecycleRemediationTests`: 6/6 PASS.
  - `MainFormIntegrationTests`: 6/6 PASS.
  - `MainFormSmokeTests`: 1/1 PASS (ten form lifecycles inside the case).
- Python and Angular full suites were deliberately not run for this C# UI-only
  re-review.

### Status of Every Prior Finding

| Finding | Status | Current evidence and assessment |
|---|---|---|
| M-01: initialized real-GL lifecycle absent from the automated graph | **Residual (Medium)** | `RendererLifecycleTests.cs:8-29` still covers only dispose-before-initialization, and `PDF_Manager.Rendering.Tests.csproj:22-24` still references only the Rendering library. The standalone probe remains valuable but is not launched by `dotnet test`. |
| M-02: STA smoke did not enforce the DockPanelSuite shell | **Residual (reduced to Low)** | The principal gap is now covered: `MainFormIntegrationTests.cs:23-32` asserts all four pane keys and DockPanel states, and `MainFormSmokeTests.cs:15-37` repeats ten clean lifecycles. The remaining narrow gap is that tests still use `Show` plus message pumping rather than `Application.Run`, and no assertion locks `DockSurface.DocumentStyle` or theme. |
| M-03: solution membership and LegacyPrinting boundary lack an executable guard | **Residual (Medium)** | `ProjectBoundaryTests.cs:50-90` still validates only the desktop project references and literal exclusions. It does not parse either solution or assert `FramePrintAzure.csproj:18` and `PDF_Manager.LegacyPrinting.csproj:9,20-23` as a repository-level graph contract. |
| M-04: restricted-source/font assertions inspect declarations rather than evaluated/publish items | **Residual (Medium)** | `ProjectBoundaryTests.cs:75-90` still checks literal `Remove` attributes from `PDF_Manager.csproj:13-16`; it does not inspect evaluated `Compile`/resource items or `ResolvedFileToPublish`. |
| L-01: sparse `PrintPageLayout` boundary coverage | **Residual (Low)** | `PrintPageLayoutTests.cs:7-38` remains the same three cases and still omits vertical-boundary, non-finite, negative, zero-page, portrait/default, and just-valid inputs. |
| L-02: sparse `RenderSceneModel` public edge coverage | **Residual (Low)** | `RenderSceneModelTests.cs:14-55` still omits null/blank stable IDs, negative infinity, `HasSameContent(null)`, and a multi-triangle model. |
| Step 2 resource/fuzz debt | **Residual (deferred to Step 4)** | `ProjectDocumentJson.cs:51-56` and `AnalysisResultSetJson.cs:40-49` cap nesting but not bytes/entities/results; `AnalysisResultSetJson.cs:70-77` buffers an arbitrary stream before parsing. No property/fuzz corpus covers near-limit nesting or large collections. |
| S3-L01: actual DockPanel close-button path untested | **Resolved** | `Step3BoundaryRemediationTests.cs:107-140` calls `tool.DockHandler.Close()` and `document.DockHandler.Close()` directly, pumps messages, and asserts hidden/reused tool identity, disposed/removed document identity, and exact created/activated/removed event sequences. This exercises the production policy installed at `DockContentRegistry.cs:429-434,471-490`, not the registry command surrogate. |
| S3-L02: invalid layout tests accepted any exception | **Resolved** | `Step3BoundaryRemediationTests.cs:59-103` requires exact `UnknownContractVersionException` and `UnknownContentKeyException`, asserts version/contract/key payloads, and retains zero-factory/empty-registry atomicity assertions. The older theory also now uses `Assert.IsType` at `DockLayoutAdapterTests.cs:73-77`. |
| S3-L03: an STA timeout can leave its background thread running | **Residual (Low)** | `StaTestRunner.cs:15-26` still starts an `IsBackground` thread and only asserts `Join(timeout)`; cleanup at lines 48-55 can run only if that thread eventually exits. Disabling test parallelism prevents normal in-process overlap but cannot terminate a hung STA or protect against a concurrent external test process. |

There are no outstanding **New** findings after the final remediation. Two gaps
found during this re-review were fixed before this verdict and are recorded
below for traceability rather than counted as open findings.

### Remediation Adequacy

#### R3-N01 - Resolved: unexpected cancellation was being classified as normal cancellation

- **Former risk:** the shell transition and operation boundaries caught any
  `OperationCanceledException`, even when the shell-owned token was not
  cancelled, which could hide transport timeouts and unrelated failures.
- **Current implementation:** `MainForm.cs:573-576` now routes every transition
  exception through the user-safe boundary; the operation catch at lines
  629-636 suppresses cancellation only when its owned token is cancelled.
  Dirty-save cancellation is likewise filtered by the passed token at lines
  691-745.
- **Test evidence:** `MainFormCancellationClassificationTests.cs:15-70`
  verifies both uncancelled `OperationCanceledException` and
  `TaskCanceledException` from analysis/project storage produce one original
  diagnostic and a safe UI error. Lines 73-101 separately prove an owned
  cancellation remains silent and preserves the terminal cancelled status.

#### R3-N02 - Resolved: close-time dirty save and layout persistence lacked a bounded policy

- **Former risk:** the initial remediation bounded the already-running
  operation, but a dirty-document save or layout-store save started during
  close could remain non-cooperative indefinitely.
- **Current implementation:** `MainForm.cs:1038-1064` gives the dirty-save and
  layout-save phases distinct cancellation sources and routes both through the
  timeout helper at lines 1155-1190. Dirty-save timeout aborts close; layout
  timeout is diagnosed and allows close; both late tasks are observed.
- **Test evidence:** `MainFormCancellationClassificationTests.cs:105-153`
  requires dirty-save timeout to cancel the owned token, keep the window and
  original dirty document alive, skip layout persistence, and reject a later
  successful save publish. Lines 156-183 require layout timeout to cancel the
  store token, diagnose `TimeoutException`, and dispose within a bounded time.

The earlier race and lifecycle fixes also have direct, assertion-bearing
coverage:

- `Step3MainFormRemediationTests.cs:17-70` holds a dirty save open, verifies all
  mutating commands are disabled, changes the document revision, queues a
  second transition, and proves the stale continuation neither replaces nor
  closes the newer document.
- `Step3MainFormRemediationTests.cs:73-138` distinguishes cooperative cleanup
  (no disposal or layout save before terminal cleanup) from the bounded
  non-cooperative path (timeout diagnostic, one final layout save, disposal,
  and safe late completion).
- `Step3MainFormRemediationTests.cs:141-216` verifies injected layout-store
  restart round-trip, absent/invalid/oversized fallback with diagnostics, and
  deterministic null active-document state.
- `ShellLayoutStoreTests.cs:9-110` covers missing-file behavior, exact byte
  limit and one-byte-over rejection, invalid UTF-8 typed failure, invalid-JSON
  layering, same-directory replacement with no temporary sibling, and
  pre-cancelled no-mutation behavior against the production store.

### Remaining Action

Keep UI test processes serialized. To close S3-L03, move STA cases into a
killable child process (or terminate the test host after the first timeout) so
a hung UI thread cannot contaminate later cases. The historical Step 1/2
residuals remain follow-up work in their original component/step; they do not
block the remediated Step 3 shell verdict.

## Step 4 Final Test and Acceptance Review (2026-09-20)

### Verdict

**CHANGES REQUIRED before Step 4 may be marked complete.** The implementation
has strong bounded unit and in-process integration coverage, but it does not
satisfy two explicit Step 4 verification clauses: no test or durable manual
evidence starts the actual Python service and calls it through the real
`FrameWebAnalysisClient` with the representative preset, and no approved
rendered PDF page golden exists. An injected `HttpMessageHandler`, a fake
desktop runtime, a PowerShell child process, deterministic PDF bytes, and PDF
string/xref assertions are useful evidence, but they are not equivalent to
those two acceptance checks.

Step 4 finding counts: Critical 0, High 2, Medium 3, Low 3. Coverage percentage
is **NOT MEASURED**. No collector or fresh coverage report describes this
change, so no percentage or 80% estimate is reported.

### Fresh Execution Evidence

Reviewer-run focused checks:

- Core Step 4 filters: 25/25 PASS (`FrameWebAnalysisClientTests`,
  `FrameWebAnalysisRequestJsonTests`, `ProjectDocumentEditSessionTests`, and
  `ProjectDocumentPresetsTests`).
- Rendering Step 4 filters: 7/7 PASS (`ViewportSceneTests` and
  `RendererLifecycleTests`).
- Typed PDF filters: 4/4 PASS (`TypedPdfExporterTests`).
- UI vertical filters: 4/4 PASS (`Step4VerticalIntegrationTests`).
- `FrameWeb.LocalRuntime.Tests`: 9/9 PASS.
- Python shared result-contract tests: 14/14 PASS.

That is 63/63 focused tests (49 .NET plus 14 Python). The lead's broader
evidence is also consistent: both solution runs pass 217/217 (Core 102,
Printing/composition 13, Rendering 16, UI 78, LocalRuntime 9), both Release
solution builds pass, and the standalone real-GL probe passes 20 cycles / 120
frames / 40 captures with zero live contexts, subscriptions, or windows.

### Step 4 Acceptance Matrix

| Clause | Executable evidence | Assessment |
|---|---|---|
| Representative preset | `ProjectDocumentPresetsTests.cs:8-31` validates all persisted physical input and round-trip; `FrameWebAnalysisRequestJsonTests.cs:9-17` locks the exact request bytes. | Covered locally |
| New / open / save / save-as | `Step4VerticalIntegrationTests.cs:16-70` uses real `JsonProjectStore` for new, save-as, reopen, and atomic PDF export; `JsonProjectStoreTests.cs:8-88` covers replace/cancel/error behavior. | Partial: ordinary save-to-current-path UI behavior is not exercised (M-02) |
| Minimum editors, validation, undo/redo | `ProjectDocumentEditSessionTests.cs:8-66` covers all five edit types, invalid rollback, bounded history, undo and redo; the vertical UI test edits only one node through the pane API. | Partial: real grid edit/validation/button wiring is not covered (M-02) |
| Viewport Z-up/model/load/result layers | `ViewportSceneTests.cs:54-103` asserts deterministic command buffers, supports/loads, Z-up displacement overlay, and hit testing in both projections. | Partial: the typed scene is never exercised through a real GL context (M-01) |
| Camera / resize / selection sync | Compiler tests cover orthographic/perspective hit testing; `Step4VerticalIntegrationTests.cs:83-104` covers stable table-to-viewport and programmatic viewport-to-table selection; the standalone probe covers resize on the older `SetModel` path. | Partial: no real `SetScene` camera/resize/mouse-click test (M-01) |
| Strict analysis request and result | `FrameWebAnalysisRequestJsonTests.cs:9-50` is an exact request golden; `FrameWebAnalysisClientTests.cs:13-229` covers sole-schema parsing, content type, status mapping, bytes, UTF-8, malformed/partial/alternate roots, depth, case, result, and entity limits. | Covered at an injected HTTP seam; live compatibility is unproved (H-01) |
| Cancellation and prior-result preservation | Client tests distinguish user cancellation from timeout; `Step4VerticalIntegrationTests.cs:108-136` preserves the prior validated result on fake backend failure and cancellation. | Covered in process; live service path remains part of H-01 |
| LocalRuntime readiness/output/timeout/job cleanup | `FrameWebLocalRuntimeTests.cs:8-162` uses real PowerShell processes and sockets for readiness, stdout/stderr, bounded output, early exit, timeout, cancellation, descendant-job cleanup, and disposal; command-factory tests lock loopback and omit Angular. | Strong component evidence; actual Python/app exit is unproved (H-01), and post-ready/race edges remain L-03 |
| No Angular on desktop path | `FrameWebRuntimeCommandFactoryTests.cs:6-25` locks the `uv --locked` Python-only command and rejects Angular arguments; `DesktopApplicationSession.cs:49-58` composes only runtime, analysis, and printing services. | Covered structurally |
| Static result tables and result scene | `Step4VerticalIntegrationTests.cs:59-65` switches all three tables and checks row counts; scene compiler tests cover a displacement overlay. | Partial: values/order and the UI displacement layer are not asserted (M-03) |
| Typed PDF structure/text/page/capture | `TypedPdfExporterTests.cs:9-41` checks deterministic bytes, catalog/page/image/text, one page, EOF and every xref offset; lines 45-60 cover bounded capture and selection-dependent pixels. | Covered for the pure writer; the actual desktop adapter receives only a header check, and no rendered golden exists (H-02) |
| Resource parity | A reviewer read-only parity check found exactly 99 keys in each of `Strings.resx`, `Strings.ja.resx`, `Strings.en.resx`, and `Strings.zh.resx`, with no missing or extra keys. | Current files match; no executable parity guard (L-01) |
| Test independence | UI, Rendering, and LocalRuntime assemblies disable parallel execution; temporary directories are GUID-scoped and runtime ports are dynamically assigned. | Generally sound; timed-out STA threads remain in process (L-02) |

### Findings

#### [High] H-01 - The required real Python / real client cross-process vertical path is absent

- **Evidence:** `FrameWebAnalysisClientTests.cs:15-34` injects `StubHandler`;
  `Step4VerticalIntegrationTests.cs:33-38,191-214` injects a fake analysis
  client; lines 140-157 and 224-252 inject a fake desktop runtime.
  `FrameWebLocalRuntimeTests.cs:8-162` launches real PowerShell children against
  `LocalReadyServer`, not `FrameWeb/main.py`. The implementation work logs also
  explicitly state that no live Python HTTP end-to-end run was performed.
- **Missing test:** no process starts `FrameWebLocalRuntime` with its production
  command, constructs a real `FrameWebAnalysisClient` from `runtime.Endpoint`,
  submits `ProjectDocumentPresets.CreateRepresentativeFrame()`, validates the
  returned `AnalysisResultSet`, then exits and proves the Python PID and its
  descendants are gone. There is likewise no clean-checkout one-command smoke
  that opens/edits/saves/reopens/calculates/inspects/exports through the actual
  composition root.
- **Why it matters:** the exact request golden and Python result-contract tests
  prove the two ends separately, but cannot detect endpoint, process startup,
  content-type, serialization, solver-input, response, or shutdown drift
  between them. This is an explicit Step 4 verification clause, so HTTP fakes
  and fake runtimes cannot close it.
- **Concrete test to add:** add a serial, process-isolated
  `RealPythonVerticalMvpTests` fixture that reserves a port, starts the
  production runtime, calls the real client with the representative preset,
  asserts expected case/result coordinates and representative values, stops or
  kills the host process, and asserts no Python descendant remains. Run it with
  `dotnet test <integration-test-project> -c Release --filter FullyQualifiedName~RealPythonVerticalMvpTests`.

#### [High] H-02 - There is no approved rendered-page PDF golden

- **Evidence:** `TypedPdfExporterTests.cs:24-41` proves deterministic bytes and
  searches Latin-1 object text/xref entries; `Step4VerticalIntegrationTests.cs:67-70`
  checks only file length and `%PDF-1.4`. Repository search finds no approved
  PDF-page PNG, reference PDF, image-diff manifest, or rendered-golden runner.
- **Missing test:** the actual `DesktopPdfExporter` output is not parsed by an
  independent PDF parser, rendered to pixels, and compared with an approved
  representative page image/tolerance.
- **Why it matters:** deterministic object bytes can still describe a blank,
  clipped, inverted, unreadable, or otherwise incorrectly rendered page. The
  Step 4 verification text explicitly requires an approved rendered-page
  golden; structural/text/page assertions alone are not that golden.
- **Concrete test to add:** check in an owner-approved representative page
  golden plus provenance/tolerance, export through `DesktopPdfExporter`, render
  page 1 with the selected deterministic renderer, assert page count/text with
  an independent parser, and image-diff the rendered page. Run it with
  `dotnet test FramePrintPDF/PDF_Manager.Tests/PDF_Manager.Tests.csproj -c Release --filter FullyQualifiedName~ApprovedRenderedPageGolden`.

#### [Medium] M-01 - The typed `ViewportScene` path has no real-GL process-isolated coverage

- **Evidence:** `ViewportSceneTests.cs:54-103` tests only pure compilation and
  hit testing. `RendererLifecycleTests.cs:34-74` calls `SetScene`, projection,
  fit and home only before initialization. The standalone probe is valuable and
  process-isolated, but `RendererVerification.cs:88-99` drives
  `SetModel(ProbeSceneModel.KnownFrame)`, not the new typed `SetScene` command
  buffers. The Step 4 UI tests never show the form or create a GL context.
- **Missing test:** no initialized context uploads/draws the representative
  typed scene, toggles both cameras, resizes, dispatches a real mouse selection,
  captures the model and displacement layer, and verifies teardown counters.
- **Why it matters:** compiler-only assertions cannot catch GL primitive/batch,
  upload, context, resize, capture, or WinForms event integration defects in the
  new vertical path.
- **Concrete test to add:** extend the child-process renderer probe with a
  `--vertical-scene` mode using `SetScene`, both projections, selection and the
  displacement layer; require deterministic nonblank captures before/after
  resize and zero live resources. Exercise it with
  `dotnet run --project FramePrintPDF/PDF_Manager.RendererProbe/PDF_Manager.RendererProbe.csproj -c Release --no-build -- --verify --vertical-scene --cycles 20`.

#### [Medium] M-02 - The real grid editor and ordinary-save UI paths are bypassed

- **Evidence:** `ProjectDocumentEditSessionTests.cs:8-66` directly calls domain
  methods for every edit type. `Step4VerticalIntegrationTests.cs:44-50`
  directly calls `EditorPane.UpsertNode`, `Undo`, and `Redo`; it does not commit
  a `DataGridView` edit, observe `ValidationFailed`, or click the toolbar.
  Lines 52-57 cover save-as and reopen, but no UI test subsequently invokes
  ordinary save with the remembered path.
- **Missing test:** no STA theory edits node/member/support/load-case/load cells
  through the actual grid, verifies parse/reference validation and rollback,
  clicks undo/redo, and proves ordinary save reuses the current path without a
  second save dialog.
- **Why it matters:** event wiring, invariant-culture parsing, checkbox values,
  validation diagnostics, and path reuse can fail while the domain API remains
  green.
- **Concrete test to add:** add `EditorGridWorkflowTests` and
  `MainFormSaveWorkflowTests`, then run
  `dotnet test FramePrintPDF/PDF_Manager.UiTests/PDF_Manager.UiTests.csproj -c Release --filter "FullyQualifiedName~EditorGridWorkflowTests|FullyQualifiedName~MainFormSaveWorkflowTests"`.

#### [Medium] M-03 - Result tables and the UI result layer are asserted only by row count

- **Evidence:** `Step4VerticalIntegrationTests.cs:59-65` checks 2/1/1 rows after
  changing table indices, but not headers, IDs, coordinates, component values,
  ordering, or the active result coordinate. It never selects
  `ResultLayerSelector` index 1. `ViewportSceneTests.cs:54-83` supplies a
  hand-built displacement layer, bypassing `ProjectDocumentContent.CreateScene`.
- **Missing test:** the shared static fixture is not verified end-to-end through
  each visible table and the document-to-displacement-layer adapter.
- **Why it matters:** swapped components, wrong ordering, wrong case/state, or a
  disconnected displacement selector would still pass the current vertical
  test.
- **Concrete test to add:** assert every representative table cell against the
  fixture, select the displacement layer, and inspect or capture the compiled
  displaced geometry in an STA/real-GL vertical test. Run it with
  `dotnet test FramePrintPDF/PDF_Manager.UiTests/PDF_Manager.UiTests.csproj -c Release --filter FullyQualifiedName~ResultPresentationVerticalTests`.

#### [Low] L-01 - Resource-key parity is verified manually, not by a regression test

- **Evidence:** all four resource files currently contain the same 99 keys, but
  `LocalizationServiceTests.cs:8-51` samples captions and fallback behavior; it
  does not compare complete key sets. A missing localized key can therefore
  fall back and keep the test green.
- **Concrete test to add:** parse all `Strings*.resx` files and require exact
  key-set equality plus nonblank values in `LocalizationResourceParityTests`.

#### [Low] L-02 - STA timeout isolation remains in-process

- **Evidence:** `StaTestRunner.cs:15-26` and
  `RendererLifecycleTests.cs:77-101` start background STA threads and only join
  with a timeout. Neither can terminate a hung thread before later tests run.
- **Concrete test/infrastructure change:** execute each STA fixture in a
  killable child test process, or terminate the host immediately after the
  first timeout. Keep UI and Rendering test processes serialized meanwhile.

#### [Low] L-03 - LocalRuntime post-readiness and start/stop race edges are not exercised

- **Evidence:** the runtime tests cover successful readiness, exit before
  readiness, unavailable-port timeout, cancellation, explicit stop, and
  disposal. `LocalReadyServer` supports non-success statuses, but no test uses
  one; no test makes a ready child exit unexpectedly or calls `StopAsync` while
  `StartAsync` is polling.
- **Concrete test to add:** add cases for repeated HTTP 503 until timeout,
  post-ready child exit to `Faulted`, and concurrent stop-during-start with
  bounded completion and no surviving process tree.

### Required Fixes Before Step 4 Completion

H-01 and H-02 are completion blockers because they are literal Step 4
verification requirements. M-01 through M-03 should also be closed before the
vertical MVP is used as the base for broader editor/render/result work: each
currently leaves a user-visible integration seam protected only on either side
of a fake or pure helper. L-01 through L-03 may remain tracked follow-up if the
two High and three Medium gaps are closed with stable evidence. The inherited
legacy-host publication/redistribution NO-GO remains unchanged and is outside
this Step 4 test count.

## Step 4 Final Remediation Re-review (2026-09-20)

### Final Verdict

**CHANGES REQUIRED** — Critical: 0, High: 0, Medium: 1, Low: 3.

The acceptance gaps in the initial Step 4 review were materially remediated.
There is now a real `uv`/Flask process test using the production
`FrameWebAnalysisClient`, a process-isolated typed-scene GL probe, a shown-form
live viewport capture child test, real `DataGridView` validation and ordinary
Save coverage, I/J result and displacement-layer assertions, an executable
resource parity check, and an owner-approved full-page rendered golden backed
by an independently implemented fail-closed PDF-subset interpreter.

Step 4 should nevertheless not be marked complete yet. The real typed-scene GL
acceptance probe is retry-free but not stable: with no other renderer or live
capture probe running, three serialized 20-cycle invocations produced PASS /
FAIL / PASS. The failure is in the product context transition used by projection
toggle, not in test parsing. This is one reproducible Medium completion blocker.

Coverage percentage is **NOT MEASURED**. No coverage collector output or
current `coverage.json`/`coverage.xml` exists for this patch. Test counts are not
converted into a percentage and no 80% estimate is made.

### Final Independent Execution Evidence

- Current project-level Release runs: Core 112/112, Printing/composition 17/17,
  Rendering 27/27, UI 83/83, and LocalRuntime 14/14 — 253/253 .NET tests.
- Focused Python contracts only, as authorized:
  `tests/io/test_local_runtime_security.py` plus
  `tests/io/test_result_contracts.py` — 44/44 PASS. The full Python and Angular
  suites were not rerun.
- The actual-process test
  `FrameWebRealProcessIntegrationTests.RealUvFlask_AnalyzesRepresentativeFrameAndDisposalKillsPythonTree`
  was rerun alone — 1/1 PASS. It starts production `uv --directory FrameWeb
  run --locked python -m flask --app main:app run`, observes the owned root and
  Python PIDs, sends a wrong-token request, calls the real client with
  `CreateRepresentativeFrame()`, and proves every observed PID exits on
  disposal.
- Final PDF independence filter:
  `dotnet test FramePrintPDF/PDF_Manager.Tests/PDF_Manager.Tests.csproj -c
  Release --no-restore --filter "FullyQualifiedName~TypedPdfExporterTests"`
  — 8/8 PASS. The rasterizer follows xref/trailer/catalog/pages/page,
  resources, content, image streams, `q`/`cm`/`Do`, and text operations. Its
  mutation test rejects missing `Do`/`cm`, broken resources and corrupt image
  data, and proves matrix/pixel changes alter the page raster.
- The UI total includes the bounded `LiveCaptureProbe` child test, which shows
  the actual document/renderer and requires a nonempty SHA-256-tagged capture.
- The real-GL flake was then checked without concurrent-probe interference.
  Process inventory was restricted to `PDF_Manager.RendererProbe.exe`,
  `LiveCaptureProbe.exe`, and matching `dotnet.exe` DLL hosts, excluding the
  inventory PowerShell itself. Initial, before-run, and after-run counts were
  all zero.

Exact GL command:

```text
dotnet run --project FramePrintPDF/PDF_Manager.RendererProbe/PDF_Manager.RendererProbe.csproj -c Release --no-restore -- --verify --cycles 20
```

Controlled serialized results (UTC; local time is UTC+09:00):

| Run | Start UTC | Elapsed | Result | Evidence |
|---:|---|---:|---|---|
| 1 | `2026-09-20T12:30:24.9437423Z` | 7,356 ms | PASS | 20 contexts, 200 frames, 60 captures, all live counters 0; OpenGL `3.3.0 - Build 31.0.101.4502` |
| 2 | `2026-09-20T12:30:32.8375039Z` | 4,314 ms | **FAIL, exit 2** | `GLFWException: WGL: Failed to make context current`; localized OS suffix reported that the requested transform operation is unsupported |
| 3 | `2026-09-20T12:30:58.3391317Z` | 7,550 ms | PASS | 20 contexts, 200 frames, 60 captures, all live counters 0; same GL version and sampled colors |

The failing stack was
`OpenGlViewportLifecycle.RebuildSceneCommands(Boolean upload):537` <-
`SetProjection(ViewportProjection):249` <- `ToggleProjection():253` <-
`RendererVerification.VerifyTypedScene(...):137` <- `Run(...):60`.
An earlier independent invocation also exited 2 at
`RebuildSceneCommands:537`, then through `SetScene:194`. Thus the failure is not
classified as a one-off concurrent-run artifact.

### Final Acceptance Matrix

| Step 4 clause | Final executable evidence | Assessment |
|---|---|---|
| Representative preset and strict request golden | `ProjectDocumentPresetsTests` validates the physical model and round-trip; `FrameWebAnalysisRequestJsonTests` locks exact UTF-8 request bytes and bounds. | Covered |
| New/open/save/save-as, minimum editors, validation, undo/redo | `Step4VerticalIntegrationTests.RepresentativePreset_EditUndoRedoSaveReopenAnalyzeInspectAndExportPdf` edits an actual node grid cell, verifies invalid-edit rollback/diagnostics, performs undo/redo, Save As, reopen, and the ordinary Save menu to the remembered path. Domain tests cover all five editor entity types and bounded history. | Covered |
| Viewport, camera, resize, and selection sync | `RendererVerification.VerifyTypedScene` sends the typed scene to an initialized real GL context, toggles projection, resizes, home/fits, hit-tests, cross-selects, captures model/displacement/selection, and checks teardown. The UI tests exercise stable table/viewport keys, and `LiveViewportCaptureUsesShownRendererInBoundedStaChildProcess` captures the shown renderer. | Functionally covered, but the real-GL gate is unstable (S4-M01) |
| Rotation-only support, moment-only load, and layers | `ViewportSceneTests.Compile_RetainsAndRendersRotationOnlySupportAndMomentOnlyNodalLoad`, the UI scene-mapping test, and the typed GL probe assert rotation glyphs, moment glyphs, model/highlight/displacement layers, and finite command buffers. | Covered |
| Strict analysis client, UTF-8, cancellation, and prior-result preservation | C# client tests reject UTF-16, Latin-1, unknown charset, malformed UTF-8, alternate roots, depth/case/result/entity/byte excess, and distinguish user cancellation from timeout. Python raw-body tests reject malformed/UTF-16 JSON before analysis. UI failure/cancellation tests keep the prior validated result. | Covered |
| Actual Python service plus real client | `FrameWebRealProcessIntegrationTests` starts the production `uv`/Flask service, verifies authenticated ownership, calls the real client with the representative preset, receives the typed contract, disposes the job, and verifies all observed processes exit. | Covered; semantic assertions are shallow (S4-L01) |
| LocalRuntime readiness/output/timeout/job cleanup/no Angular/no orphan | Runtime tests cover readiness, both output streams and truncation, early exit, timeout/cancel, idempotent stop/dispose, Windows job descendant cleanup, wrong token, body size, no redirect, port squatter and ownership-loss non-disclosure. Command tests lock Python-only `uv --locked`; the actual-process test verifies real cleanup. | Covered; three post-ready/start-stop races remain S4-L03 |
| Static result tables, I/J results, and UI result layer | The vertical test asserts force row order `I`,`J`, values `10`,`20`, PDF I/J labels and embedded capture. `DisplacementLayerSelectorMapsAnalysisResultIntoCurrentScene` selects layer index 1 and verifies IDs, vectors, scale, selection, and zero viewport failures. | Covered |
| Typed PDF structure/text/page/capture/rendered golden | Writer tests assert deterministic bytes, one-page structure, xref offsets, text and image. The final subset rasterizer independently follows the emitted PDF graph/operators and compares all 595 x 842 Gray8 pixels with the checked-in owner-approved golden; mutation tests prove it is not hard-coded to the writer's expected placement. | Covered for the owned deterministic PDF subset |
| Bounded enumeration and allocation | Core/client/scene/PDF tests stop finite-over-limit and infinite sequences at maximum + 1, enforce request/response/depth/result/entity limits, and reject invalid image dimensions before enumeration/allocation. | Covered |
| Resource parity | `LocalizedResourcesHaveExactKeyParity` compares sorted exact key sets for neutral, English, Japanese, and Chinese resources; reviewer parsing also found 101 keys and no blank values in each. | Covered |
| Test independence and STA isolation | Test assemblies serialize UI/GL work, temp paths and ports are scoped, child probes are bounded and kill their own tree on timeout. | Partial: legacy in-process STA helpers cannot terminate a timed-out thread (S4-L02) |

### Disposition of Initial Step 4 Findings

| Initial finding | Final disposition |
|---|---|
| H-01 real Python/real client path absent | **Closed.** Actual production `uv`/Flask plus real-client and process-tree-cleanup test added. |
| H-02 no rendered-page golden | **Closed.** Owner-approved full-page raster golden plus independent graph/operator parser and mutation checks added. |
| M-01 no typed-scene real GL | **Acceptance coverage added**, but it exposed the new reproducible stability finding S4-M01 below. |
| M-02 grid/ordinary Save bypassed | **Closed.** Real `DataGridView` edit/validation and Save-menu path are exercised. |
| M-03 results/layer weak | **Closed.** I/J values/order and actual document-to-displacement-layer mapping are asserted. |
| L-01 resource parity manual only | **Closed.** Exact key-set equality is executable. |
| L-02 STA timeout continuation | **Remains** as S4-L02. |
| L-03 runtime race edges | **Remains** as S4-L03. |

### Remaining Findings

#### [Medium] S4-M01 — Typed real-GL projection/context transitions are reproducibly flaky

- **File/function:**
  `FramePrintPDF/PDF_Manager.Rendering/OpenGlViewportLifecycle.cs`,
  `RebuildSceneCommands` line 537 and `SetProjection` line 249;
  `FramePrintPDF/PDF_Manager.RendererProbe/RendererVerification.cs`,
  `VerifyTypedScene` line 137.
- **Executable evidence:** the exact no-restore command and controlled process
  inventory above produced PASS / FAIL / PASS. The failing run had no probe
  before or after it and failed during `ToggleProjection`. A separate earlier
  invocation failed at the same make-current point during `SetScene`.
- **Missing acceptance evidence:** there is no retry-free serialized run policy
  that can currently complete even three consecutive 20-cycle invocations.
- **Why it matters:** this is the initialized production GL path for a visible
  projection/layer transition. A WGL make-current failure can abort the renderer
  or desktop operation even though pure compiler and pre-initialization tests
  remain green. Retrying the acceptance command would hide the defect.
- **Concrete fix/test:** serialize ownership of every GL context transition,
  diagnose or remove the make-current race/invalid-handle window, then require
  the exact command above to pass at least three consecutive invocations with
  exit 0, 20 contexts/200 frames/60 captures each, and all live counters zero.
  Keep a single failure as a hard gate; do not add an automatic retry.

#### [Low] S4-L01 — The real Python E2E proves compatibility but weakly verifies solver semantics

- **File/function:**
  `tools/FrameWeb.LocalRuntime.Tests/FrameWebRealProcessIntegrationTests.cs`,
  `RealUvFlask_AnalyzesRepresentativeFrameAndDisposalKillsPythonTree`, lines
  52-56.
- **Missing test:** after the real call it asserts only the contract kind and
  that `Results` is nonempty; it does not assert expected case/state/result
  coordinates or representative displacement/reaction/member-force values for
  the preset.
- **Why it matters:** a live service returning the wrong but structurally valid
  result can satisfy the present cross-process smoke. Exact-request and Python
  contract tests mitigate this, so the residual is Low rather than a missing
  E2E blocker.
- **Concrete test to add:** extend the same actual-process test with stable
  assertions for the expected load case, static state, result type, node/member
  IDs, I/J coordinates, and selected numeric values. Run it using its existing
  fully qualified test-name filter.

#### [Low] S4-L02 — A timed-out legacy STA test thread can continue in-process

- **File/function:** `FramePrintPDF/PDF_Manager.UiTests/StaTestRunner.cs:25`
  and `FramePrintPDF/PDF_Manager.Rendering.Tests/RendererLifecycleTests.cs:122`.
- **Missing isolation:** both helpers join a background STA thread with a bound,
  but cannot terminate it on timeout. The new live-capture and renderer probes
  are child-isolated; the remaining UI and pre-initialization renderer fixtures
  are not.
- **Why it matters:** a hung thread can retain forms, GL state, or events and
  contaminate subsequent tests after the timeout assertion fires.
- **Concrete infrastructure test/change:** run each timed STA fixture in a
  killable child host, or fail/terminate the entire test host immediately on
  the first timeout. Add a deliberately hanging child fixture to prove bounded
  termination and continuation in a fresh host.

#### [Low] S4-L03 — Three LocalRuntime lifecycle race edges remain unexercised

- **File/function:** `tools/FrameWeb.LocalRuntime/FrameWebLocalRuntime.cs`
  readiness/monitor/stop transitions and
  `tools/FrameWeb.LocalRuntime.Tests/FrameWebLocalRuntimeTests.cs`.
- **Missing test:** no case returns repeated owned HTTP 503 responses until the
  readiness deadline, exits the child unexpectedly after `Ready`, or calls
  `StopAsync` concurrently while `StartAsync` is polling.
- **Why it matters:** these state transitions can regress fault classification,
  bounded completion, or job/process cleanup without affecting the successful
  actual-process path.
- **Concrete tests to add:** add three serial tests for 503-to-timeout,
  post-ready exit-to-`Faulted`, and stop-during-start. Each must finish within a
  fixed bound and assert no surviving job/process tree.

### Required Fix Before Step 4 Completion

Resolve S4-M01 and obtain a clean retry-free serialized real-GL run. The three
Low findings may remain explicitly tracked test debt: they narrow assertion and
failure-isolation/race coverage but do not negate the now-executable Step 4
vertical path. The legacy-host High/Medium/Low findings and redistribution
NO-GO remain unchanged and are outside this Step 4 finding count.

## Step 4 Post-fix Final Acceptance Re-review (2026-09-20)

### Superseding Verdict

**PASS** — Critical: 0, High: 0, Medium: 0, Low: 2.

This section supersedes the preceding Step 4 `CHANGES REQUIRED` verdict. The
sole Medium blocker, the reproducible WGL make-current failure, is fixed at its
production lifecycle root without a retry or serialization workaround. The
previous Low real-Python semantic-assertion gap is also closed. No required
Step 4 acceptance fix remains.

Coverage percentage remains **NOT MEASURED**. No current coverage collector
output or report exists, and no percentage is inferred from passing tests.

### Post-fix Change Inspection

- `OpenGlViewportLifecycle.EnsureContextCurrent` at
  `FramePrintPDF/PDF_Manager.Rendering/OpenGlViewportLifecycle.cs:579-590`
  validates the real context and calls `MakeCurrent` only when
  `Context.IsCurrent` is false. Scene upload, projection, render, capture, and
  viewport paths all use this single policy.
- `OpenGlViewportLifecycle.Dispose` at lines 370-414 releases GPU resources
  while current and calls `ReleaseCurrentContext` before disposing the
  `GLControl`; `ReleaseCurrentContext` at lines 593-600 uses
  `MakeNoneCurrent` only when needed.
- `RendererLifecycleTests.cs:30-45` locks the current/non-current/missing
  context decision. It is intentionally a pure policy regression; repeated
  initialized-context proof remains in the killable RendererProbe process.
- `FrameWebRealProcessIntegrationTests.cs:52-96` now asserts schema version,
  case/state/topology/member IDs, negative finite loaded-node displacement,
  the expected +10 support reaction within `1e-7`, and finite/nonzero member
  end forces, while retaining authentication and complete observed-PID exit.

The implementation is appropriately scoped: it corrects redundant activation
and teardown ordering instead of concealing the former failure with retries,
exception swallowing, or a serialized global test lock.

### Independent Post-fix Execution

- `dotnet test FramePrintPDF/PDF_Manager.Rendering.Tests/PDF_Manager.Rendering.Tests.csproj -c Release --no-restore`
  — 27/27 PASS.
- `dotnet build FramePrintPDF/PDF_Manager.RendererProbe/PDF_Manager.RendererProbe.csproj -c Release --no-restore`
  — PASS, 0 warnings, 0 errors.
- Five serialized executions of
  `dotnet run --project FramePrintPDF/PDF_Manager.RendererProbe/PDF_Manager.RendererProbe.csproj -c Release --no-build --no-restore -- --verify --cycles 20`
  — 5/5 PASS with no retries. Initial/before/after inventories found zero other
  RendererProbe or LiveCaptureProbe processes. The runs started from
  `2026-09-20T12:43:51.2590304Z` through
  `2026-09-20T12:44:21.2125666Z` and took 6,423 / 7,057 / 7,834 / 7,021 /
  7,035 ms. Every run reported 20 contexts, 200 frames, 60 captures,
  deterministic `#0D1933` / `#F25926` samples, OpenGL
  `3.3.0 - Build 31.0.101.4502`, and zero live contexts/subscriptions/windows.
- One corrected bounded concurrent round launched the same 20-cycle
  RendererProbe with the shown-form `LiveCaptureProbe` from
  `2026-09-20T12:45:58.3520829Z` to
  `2026-09-20T12:46:04.4055356Z`. Both children exited 0 without timeout;
  RendererProbe reported the same 20/200/60 and zero-live contract;
  LiveCapture reported `786 x 359` and SHA-256
  `6CC7CC47C5F41DB0051FAC3A6D20CB93CA6D01184FDF2FBE6084FE1E4B4DF3E0`;
  post-run inventory was zero.
- `dotnet test tools/FrameWeb.LocalRuntime.Tests/FrameWeb.LocalRuntime.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName=FrameWeb.LocalRuntime.Tests.FrameWebRealProcessIntegrationTests.RealUvFlask_AnalyzesRepresentativeFrameAndDisposalKillsPythonTree"`
  — 1/1 PASS in 939 ms, exercising actual `uv`/Flask, the production client,
  structural semantics, and process-tree cleanup.
- Final AgentOnly evidence:
  `.agents/logs/check-20260920T115617091Z-37096.log` —
  `overall=pass`, 10 passed / 0 failed / 1 product-gate skip, including plan
  contract and `git diff --check`.

Full Python and Angular suites were not rerun, as instructed.

### Final Acceptance Matrix

| Step 4 clause | Final executable evidence | Assessment |
|---|---|---|
| Representative preset and strict request | Exact request golden, preset round-trip/physical data tests, and actual uv/Flask E2E with the production client. | Covered |
| New/open/save/save-as, editors, validation, undo/redo | Real grid valid/invalid edits, diagnostics rollback, undo/redo, Save As, reopen, and ordinary remembered-path Save in the vertical UI test; all five entity editors have domain coverage. | Covered |
| Viewport/camera/resize/selection/layers | Typed initialized GL probe exercises scene upload, both projections, resize, home/fit, table and hit-test selection, model/displacement/highlight capture, and teardown; shown-form live capture runs in a bounded child. | Covered and stable post-fix |
| Rotation-only support and moment-only load | Pure compiler/UI mapping tests plus typed real-GL glyph checks. | Covered |
| Strict analysis transport, UTF-8, bounds, cancellation, prior-result preservation | C# client and Python raw-body tests cover media type/charset/malformed UTF-8, bytes/depth/entities/results, failure mapping, cancellation, and prior validated result preservation. | Covered |
| Real Python/client semantics and cleanup | The actual-process test verifies authenticated ownership, typed topology/case/state, physical displacement/reaction/member-force properties, and exit of every observed uv/Python PID. | Covered |
| LocalRuntime readiness/output/timeout/security/no Angular/no orphan | Component/adversarial tests cover authenticated strict readiness, bounded/redacted output, timeout/cancel, Job cleanup, port squatter and ownership-loss token/body non-disclosure, redirects, and Python-only command construction. | Covered; rare lifecycle races remain S4-L02 |
| Static result tables, I/J values, and result scene | UI asserts I/J order/values and PDF labels; layer selector verifies result-to-displacement IDs/vectors/scale and selection. | Covered |
| Typed PDF and rendered-page golden | Deterministic structure/xref/text/image tests plus an owner-approved 595 x 842 full-page raster golden through the independent fail-closed subset parser and mutation tests. | Covered |
| Bounded enumeration/allocation | Request, response, scene, displacement, PDF model, and capture boundaries stop at maximum + 1 and reject invalid dimensions before allocation. | Covered |
| Resource parity | Exact executable neutral/en/ja/zh key-set equality; reviewer inspection found 101 nonblank values in each resource. | Covered |
| Test independence | Assemblies serialize STA/GL work; real GL and live capture use bounded killable children; paths and ports are scoped. | Covered for acceptance; in-process STA timeout continuation remains S4-L01 |

### Final Finding Disposition

- **Critical: 0 findings.**
- **High: 0 findings.**
- **Medium: 0 findings.** S4-M01 is closed by the lifecycle fix and independent
  5/5 serialized plus concurrent proof.
- **Low: 2 findings.** S4-L01 and S4-L02 below are retained test debt. The old
  semantic-E2E Low is closed by the new physics assertions.

#### [Low] S4-L01 — A timed-out legacy STA test thread can continue in-process

- **File/function:** `FramePrintPDF/PDF_Manager.UiTests/StaTestRunner.cs:25`
  and `FramePrintPDF/PDF_Manager.Rendering.Tests/RendererLifecycleTests.cs:122`.
- **Missing isolation:** a timed-out background STA thread cannot be terminated.
  The real GL and shown-form capture paths are now child-isolated, but remaining
  UI and pre-initialization fixtures can continue after their timeout failure.
- **Why it matters:** a hung thread can retain forms/events and contaminate
  later tests in that host.
- **Concrete follow-up:** move the remaining timed STA cases into killable
  child hosts, or terminate the test host after the first timeout; prove bounded
  termination with an intentionally hanging child fixture.

#### [Low] S4-L02 — Three LocalRuntime lifecycle race edges remain unexercised

- **File/function:** `tools/FrameWeb.LocalRuntime/FrameWebLocalRuntime.cs`
  readiness/monitor/stop transitions and
  `tools/FrameWeb.LocalRuntime.Tests/FrameWebLocalRuntimeTests.cs`.
- **Missing tests:** repeated owned HTTP 503 until readiness timeout,
  unexpected child exit after `Ready`, and concurrent `StopAsync` while
  `StartAsync` is polling.
- **Why it matters:** fault classification, bounded state transition, or final
  Job cleanup can regress on those uncommon interleavings without affecting the
  successful real-process test.
- **Concrete follow-up:** add three serialized bounded cases for
  503-to-timeout, post-ready exit-to-`Faulted`, and stop-during-start; each must
  assert no surviving Job/process tree.

### Completion Conclusion

Step 4 test/acceptance review is **PASS**. No Critical, High, or Medium issue is
open, and no test-acceptance fix is required before marking the vertical MVP
complete. Track the two Low isolation/race gaps without treating them as a
measured coverage percentage. The legacy host findings and completed-app
redistribution NO-GO remain unchanged and outside this Step 4 verdict.

## Final Release-configuration Acceptance Delta (2026-09-20)

The verdict remains **PASS** — Critical: 0, High: 0, Medium: 0, Low: 2.

A final clean-build defect was found after the post-fix PASS: the solution's
Release build could previously select a Debug `LiveCaptureProbe`, and a stale
Release probe could mask that configuration error. The UI test project now
keeps the probe `ProjectReference` only for restore discovery using
`BuildReference="false"`, `ReferenceOutputAssembly="false"`, and
`Private="false"`, then invokes an explicit `BeforeTargets="CoreCompile"`
MSBuild target with `Properties="Configuration=$(Configuration)"`. This avoids
the normal reference build/copy traversal while propagating the parent build
configuration to the probe.

Independent clean-artifact proof:

- `dotnet clean` was run for the exact LiveCaptureProbe project in Debug and
  Release. Both probe DLL paths were absent afterward.
- `dotnet build FramePrintPDF/PDF_Manager.UiTests/PDF_Manager.UiTests.csproj -c
  Release --no-restore` then passed with 0 warnings/errors, generated only
  `LiveCaptureProbe/bin/Release/net8.0-windows/LiveCaptureProbe.dll`, and left
  the Debug DLL absent.
- The process-isolated live-capture filter passed 1/1 from that Release-only
  output.
- The complete UI Release suite passed 83/83 in 11 seconds with `--no-build
  --no-restore`.
- The lead's final Release run confirmed the same 253/253 project aggregate in
  both solutions: Core 112, Printing/composition 17, Rendering 27,
  LocalRuntime 14, and UI 83.

This closes the clean-checkout/configuration-propagation risk rather than
accepting a passing test backed by stale binaries. The final matrix row for
shown-form live capture and test independence is therefore executable from a
clean Release build. No new finding is opened, and coverage remains **NOT
MEASURED**.
