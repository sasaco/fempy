# Code Quality Review: C# FrameWeb Desktop (Steps 1-4)

## Current Integrated Result (Step 4 final quality review, 2026-09-20)

**CHANGES REQUIRED for Step 4 completion** — Critical: 0; High: 3;
Medium: 5; Low: 1. The prior Step 1-3 findings remain resolved, but the first
vertical slice has two production-path defects (scene frames are not painted,
and asynchronous runtime startup can resume the WinForms entry point on MTA),
and the required live-Python end-to-end acceptance has not been demonstrated.
The Step 4 section at the end is authoritative for the current tree.

The historical review sections below are retained as the audit record. Their
old verdicts describe the code at the time of each review; the final status
matrix at the end is authoritative for the current tree.

## Historical Step 1 Result

**PASS** — no Critical or High findings. One Medium finding concerns the accuracy and reproducibility of the recorded build evidence; it does not invalidate the Step 1 project boundaries or runtime behavior.

## Review Scope

Reviewed the Step 1 implementation diff, the approved plan, `DESIGN.md`, repository rules, changed C# project files, shell, typed printing boundary, rendering lifecycle, renderer probe, automated tests, and both solution integrations. The review focused on dependency direction, WinForms/OpenGL thread affinity and disposal, exception propagation, legacy bridge containment, maintainability, and consistency with the Step 1 acceptance criteria.

## Findings

### [Medium] The recorded zero-warning solution-build claim is not reproducible

- **Evidence**: `.agents/docs/plans/csharp-frameweb-client.md:136` states that both solution builds pass with zero warnings/errors, and `HANDOFF.md:55` repeats that the solution build has zero warnings/errors. However, the canonical full-check log `.agents/logs/check-20260920T035935489Z-32252.log:1655-1726` records 28 compiler warnings from `PDF_Manager.LegacyPrinting` and ends with `28` warnings. The implementation work log independently acknowledges the same behavior at `.agents/logs/agent-teams/team-execute-csharp-frameweb-client/library-integrator.md:55,66` for clean or incrementally invalidated builds.
- **Current implementation**: `FramePrintPDF/PDF_Manager.LegacyPrinting/PDF_Manager.LegacyPrinting.csproj:14-23` links the unchanged legacy source tree into an active solution project. Those sources contain the warning sites reported by the canonical build. An incremental build may report zero when the project is already up to date, but that does not establish a clean-build zero-warning result.
- **Impact**: The build succeeds, but the acceptance record overstates warning cleanliness and can mislead a fresh-session or clean-checkout verifier. It also obscures the intentionally accepted Step 8 migration debt.
- **Recommended fix**: Amend the plan and handoff to state that solution builds succeed, new Step 1 projects are warning-free, and a clean `LegacyPrinting` build emits 28 inherited warnings. If zero warnings is intended as a hard gate, either fix those warning sites during the approved legacy migration or establish an explicit, narrowly documented legacy-warning baseline; do not rely on incremental up-to-date output.

## Verified Strengths

- `PDF_Manager` references only Core, Rendering, and typed Printing; direct `dotnet list ... reference` inspection confirmed that it does not reference `LegacyPrinting` or Azure. The built desktop output and `PDF_Manager.deps.json` contain no `LegacyPrinting`, PdfSharp, Newtonsoft, or restricted-font entries.
- `FramePrintAzure` references only `PDF_Manager.LegacyPrinting`, while `FrameWeb.Startup` remains portable through the Azure project. This preserves the existing host without introducing a `net8.0` to `net8.0-windows` dependency.
- `PDF_Manager.Printing` targets portable `net8.0`, has no package or project dependencies, validates finite page geometry, and does not promote the untyped legacy print contract.
- `OpenGlViewportLifecycle` consistently checks the creating managed thread before public lifecycle operations. Initialization is idempotent, model and resize updates avoid redundant work, rendering is invalidation-driven, and GPU objects are released while the GLControl is still available.
- `RendererProbeDocument.Dispose` now disposes the renderer before base WinForms disposal removes/disposes the child control (`RendererProbeWindow.cs:85-94`), matching the corrected WGL teardown order. Verification mode configures WinForms to throw UI-thread exceptions to the outer process boundary (`Program.cs:12-18`) instead of showing a modal Continue dialog.
- Renderer disposal unsubscribes events, attempts GPU cleanup, always disposes the control, balances diagnostics in `finally`, and rethrows failures with their original stack through `ExceptionDispatchInfo` (`OpenGlViewportLifecycle.cs:211-258`).
- Both solutions contain the Step 0/1 production and automated-test projects, and the obsolete manual `PDF_Test` harness is absent from the active print solution without deleting its sources.

## Validation Performed

- `dotnet test FrameWeb.sln -c Release --no-build`: PASS, 61/61 tests (Core 41, composition/Printing 9, Rendering 10, UI 1).
- `dotnet run --project FramePrintPDF/PDF_Manager.RendererProbe/PDF_Manager.RendererProbe.csproj -c Release --no-build -- --verify --cycles 3`: PASS; 3 contexts, 18 frames, 6 captures, zero live contexts/subscriptions/windows.
- `dotnet list` reference/package inspection: dependency direction and dependency-free typed Printing confirmed.
- `dotnet sln ... list`: expected Step 0/1 projects present in both solutions.
- `git diff --check`: PASS (line-ending notices only).

## Remaining Uncertainties

- The reviewer reran a 3-cycle real-context probe, not the full 100-cycle sequence. The recorded 100-cycle result is consistent with the same executable and lifecycle path, but was not independently repeated in this review.
- The requested nested read-only Codex consultation was unavailable: the first call failed while persisting PowerShell stdin because of an encoding error; the single UTF-8 retry produced no response for approximately ten minutes and was terminated. No conclusion in this report depends on that consultation.
- Repository-wide Python and Angular failures are known and outside this C# Step 1 quality scope; this PASS is not a repository-wide green verdict.

## Step 2 Addendum (2026-09-20)

### Step 2 Result

**PASS** — no Critical, High, or Medium Step 2 finding remains. The review found
two public-boundary gaps during verification: required JSON objects set to
`null` escaped the typed format boundary, and direct moving-load envelope calls
did not reject duplicate or reverse-ordered source cases. Both were fixed and
covered before this verdict.

### Verified Strengths

- `ProjectDocument` is an input-only aggregate. Serializer output excludes
  runtime result, selection, and dirty state, while preserving dependency order
  for chained derived definitions and canonical ordering for independent IDs.
- `AnalysisResultSet` collections are defensively copied and read-only. Parsing,
  semantic validation, indexing, and state commit remain separate; rejected
  candidates cannot replace the current result.
- The C# contract consumes the repository's six positive and seven negative
  Python fixtures directly instead of maintaining a forked fixture copy.
- Presentation logic leaves base results unchanged, carries source provenance,
  rejects non-static/incompatible/overflowing inputs, and deterministically
  handles DEFINE, COMBINE, PICKUP, paging, and signed envelopes.
- Core remains package-free and has no UI, rendering, PDF, HTTP, filesystem-host,
  or legacy-dictionary leakage across its typed service interfaces.

### Validation

- Core tests: 75/75 PASS.
- `FrameWeb.sln` and `FramePrintPDF.sln`: 95/95 PASS in each solution graph.
- Both Release solution builds: PASS; clean legacy-warning behavior remains the
  Step 1 baseline rather than a new Step 2 warning.
- Shared Python result-contract tests: 14/14 PASS.
- Formatting and whitespace checks: PASS before the repository-wide gate.

The hand-written wire mapper and semantic validator are intentionally explicit
but large; future contract changes should continue to start from shared fixtures
and focused semantic tests to prevent drift. Parallel reviewer runtimes were
unavailable at their usage limit, so this addendum records the lead fallback
review rather than an independent-agent verdict.

## Step 3 Addendum (2026-09-20)

### Step 3 Result

**CHANGES REQUESTED** — one High, five Medium, and three Low findings. The
stable-key registry, hide/dispose split, reducer transitions, cancellation-token
lease fix, subscription symmetry, localized resource coverage, and focused UI
tests are sound, but document replacement/close is not serialized and layout
capture/restore is not yet a complete production lifecycle.

### Findings

#### [High] Dirty-save replacement and close are reentrant and can close an unchecked newer document

- **Evidence**: `FramePrintPDF/PDF_Manager/Shell/MainForm.cs:160-181` performs
  the dirty-document replacement check before entering `RunOperationAsync`;
  `MainForm.cs:533-545` likewise saves for replacement/close outside the
  operation owner. After the await, `MainForm.cs:517-530` unconditionally
  publishes the saved document through `SetCurrentDocument`. The close path at
  `MainForm.cs:732-763` sets `_allowClose` after that asynchronous check, while
  `MainForm.cs:473-486` derives command enablement only from
  `_isOperationRunning`, not `_closeCheckRunning`.
- **Failure scenario**: close dirty document A, choose Save, and let the store
  yield. The form remains interactive. A New/Open command can publish document
  B while A is saving. A's continuation then republishes A and the original
  close continuation closes the form without checking B's dirty state. The
  same stale continuation can overwrite a newer document during ordinary
  New/Open replacement.
- **Remediation**: serialize every document transition (New, Open, dirty Save,
  and close) behind one shell-owned gate/revision. Disable mutating commands
  while that transition is pending and, after each await, verify that the
  captured document/revision is still current before publishing or allowing
  close. Add a controllable delayed-store regression that switches documents
  during dirty Save and proves that the newer document cannot be replaced or
  closed without its own confirmation.

#### [Medium] Form close cancels the current operation but does not wait for its terminal cleanup

- **Evidence**: `FramePrintPDF/PDF_Manager/Shell/MainForm.cs:732-758` calls
  `CancelCurrent()` and immediately proceeds toward `_allowClose`; the task
  created by `RunOperationAsync` at `MainForm.cs:489-515` is not retained.
  `MainForm.cs:284-298` again cancels and then immediately disposes the
  cancellation owner and UI tree.
- **Impact**: a slow or cancellation-ignoring analysis/store/exporter can keep
  filesystem, stream, HTTP, or other resources active after the shell has torn
  down its owner and WinForms context. The post-await token checks protect most
  UI commits, but they do not establish terminal cleanup or ensure that a
  continuation posted to the closing UI context runs.
- **Remediation**: retain the current operation task, request cancellation, and
  asynchronously await its terminal cleanup before allowing final close. Keep
  the form in a closing/busy state during that wait and define an explicit
  bounded-shutdown policy for a non-cooperative external implementation before
  Step 4 connects HTTP/runtime work.

#### [Medium] Cancellation callback exceptions can strand ownership and interrupt disposal

- **Evidence**: `FramePrintPDF/PDF_Manager/Shell/Lifecycle/OperationCancellationOwner.cs:21-38`
  installs the new source as `current` before cancelling the previous source.
  Its `TryCancel` at `OperationCancellationOwner.cs:94-109`, and the equivalent
  helper at `ActivationCoordinator.cs:247-255`, catch only
  `ObjectDisposedException`. `CancellationTokenSource.Cancel()` may propagate
  exceptions from registered callbacks. `MainForm.cs:270-275` and
  `MainForm.cs:284-298` invoke these cancellation paths outside the user
  exception boundary and without a cleanup `finally` that continues teardown.
- **Impact**: a throwing callback can make `Begin` fail after publishing a new
  source but before returning its lease, or abort MainForm disposal before dock
  content and the theme are released.
- **Remediation**: separate cancellation notification from ownership cleanup:
  finalize/return/dispose owned state in `finally`, aggregate callback failures
  for diagnostics, and route command-facing failures through the exception
  boundary without preventing the remaining teardown sequence. Cover throwing
  callbacks for replacement, explicit cancel, and dispose.

#### [Medium] Versioned layout persistence is not connected to the production shell lifecycle

- **Evidence**: `FramePrintPDF/PDF_Manager/Program.cs:11` constructs a default
  `MainForm`. Its constructor always opens the four hard-coded panes at
  `FramePrintPDF/PDF_Manager/Shell/MainForm.cs:95-108`. The layout adapter is
  only constructed, exposed, and disposed (`MainForm.cs:82,119,294`); no
  production path calls `CaptureJson` or `RestoreJson`, and `MainFormServices`
  has no layout store. The only callers are
  `FramePrintPDF/PDF_Manager.UiTests/DockLayoutAdapterTests.cs:10-50`.
- **Impact**: the adapter round-trip passes, but the desktop always starts from
  the fixed default layout and never saves a user's layout. The Step 3
  save/restore behavior is therefore not user-reachable or durable.
- **Remediation**: add an explicit bounded layout-store boundary, restore after
  whitelisted factories are registered with a safe default fallback, and save
  the final validated layout during the serialized close lifecycle. Add a
  MainForm restart test that proves production composition actually consumes
  and rewrites the persisted versioned JSON.

#### [Medium] Captured layout order and docked dimensions do not represent the live DockPanel arrangement

- **Evidence**: `FramePrintPDF/PDF_Manager/Shell/Docking/DockLayoutAdapter.cs:15,22-24,108-116`
  changes `order` only when content is created or removed. User tab reorder,
  redocking, and pane rearrangement do not update it. `DockLayoutAdapter.cs:41-45`
  captures bounds only for floating content; docked pane width/height or dock
  proportion is not recorded. `DockStateMapper.cs:20-29` also normalizes
  DockPanel auto-hide states to ordinary side states.
- **Impact**: a capture after real user rearrangement can serialize the original
  creation order and default docked sizes rather than the current layout, so
  the successful open-in-order test is not a full layout round-trip.
- **Remediation**: either capture the actual DockPanel pane/tab graph (including
  supported dimensions and state) into the stable-key DTO, or explicitly
  constrain the UI and contract to the smaller representable subset. Add tests
  that reorder tabs, resize/redock panes, capture, restore into a new host, and
  compare the supported live arrangement.

#### [Medium] Restore validation is fail-fast but restore application is not transactional

- **Evidence**: `FramePrintPDF/PDF_Manager/Shell/Docking/DockLayoutAdapter.cs:65-86`
  validates the candidate, closes all omitted live content, and then opens each
  candidate sequentially. Whitelist validation cannot prove that a registered
  factory or DockPanel `Show` will succeed. `DockContentRegistry.cs:186-199`
  cleans up only the newly created content whose own show failed; it cannot
  restore panes already closed or placements already changed.
- **Impact**: a registered factory exception or UI placement failure can leave
  a partially applied layout after document windows were disposed. The current
  invalid-JSON/unknown-key test proves only pre-mutation validation.
- **Remediation**: stage all missing instances and placements before destructive
  changes, then commit as one UI transaction, or snapshot the current layout
  and provide a tested rollback path. Add a restore test in which a later
  registered factory throws and assert that every prior live key, placement,
  order, and active document remains unchanged.

#### [Low] Null active-document state is not published or restored consistently

- **Evidence**: `FramePrintPDF/PDF_Manager/Shell/MainForm.cs:709-715` returns
  when DockPanel has no active document, leaving `ActiveDocumentKey` stale.
  During restore, `DockLayoutAdapter.cs:76-86` opens every non-hidden document
  (each open activates it at `DockContentRegistry.cs:206-210`) and performs no
  clearing action when `LayoutState.ActiveDocument` is null.
- **Impact**: closing the last document or restoring a contract-valid layout
  with `activeDocument: null` can leave the shell property pointing at a
  removed key or make the last restored document active, respectively.
- **Remediation**: define null semantics explicitly. Clear/cancel activation
  when DockPanel loses its active document, and either restore null deliberately
  or reject null whenever visible documents make that state unsupported.

#### [Low] Dock operations verify STA apartment, not the owning UI thread

- **Evidence**: `FramePrintPDF/PDF_Manager/Shell/Docking/DockContentRegistry.cs:350-355`
  accepts any STA thread. `Contents`, `TryGet`, and `TryGetKey` at
  `DockContentRegistry.cs:37-43,62-80` do not perform even that check or lock the
  dictionaries modified by UI events.
- **Impact**: a second STA thread can pass the guard and touch WinForms content
  or race registry dictionaries, despite the lifecycle being UI-thread-only.
- **Remediation**: capture the creating thread ID or SynchronizationContext and
  enforce it on every public registry/layout operation and property. Prefer a
  single documented UI-thread-only contract over partial locking of WinForms
  objects.

#### [Low] Activation observer delivery is neither revision-ordered nor failure-isolated

- **Evidence**: `FramePrintPDF/PDF_Manager/Shell/Lifecycle/ActivationCoordinator.cs:51-69`
  publishes state after releasing the lock. Two concurrent callers can observe
  revision 2 and then revision 1 if the first caller is preempted before
  `Observe`. In the pump, `ActivationCoordinator.cs:213-216` invokes the
  external observer without protection; if it throws, the pump exits before
  the normal empty-queue path clears `pumpTask` at lines 138-141, so later
  requests can remain pending behind a faulted non-null task.
- **Impact**: the current MainForm supplies no observer, but the reusable Step 3
  coordinator exposes a callback contract that can regress latest-wins state or
  strand future work when a consumer is added.
- **Remediation**: serialize observer delivery and drop obsolete revisions,
  isolate/report callback failures, and clear/restart pump ownership in an outer
  `finally`. Add concurrent notification-order and throwing-observer tests.

### Explicit No-Finding Areas

- Exact `DocumentKey` reuse and multi-document identity are correct in the
  registry: same keys reuse one live instance, different document keys coexist,
  tools hide, documents dispose, and attach/detach subscriptions are symmetric.
- Candidate JSON/version/key/order validation completes before any restore
  mutation. The finding above concerns runtime factory/show failure after that
  successful validation, not the whitelist or parser.
- The activation reducer ignores obsolete completion/cancel/failure revisions;
  the pump yields before ownership assignment, cancels outside `syncRoot`,
  serializes effects, coalesces pending work, and caches lease tokens so they
  remain observable after owner disposal.
- The previously reported integration repairs are present: the shell event
  args type is explicitly qualified, cancellation status is not overwritten by
  `SetBusy(false)`, and xUnit UI tests disable in-process parallelization and
  clean `Application.OpenForms`. Separate test processes have independent
  WinForms statics.
- MainForm event subscriptions and layout-adapter registry subscriptions are
  removed before owned controls are disposed. Localized menu/pane/status
  refresh is symmetric, and neutral/en/ja/zh resources each contain the same 53
  unique keys.
- The empty viewport label and unavailable analysis/print commands are
  intentional Step 3 boundaries for Step 4 and Step 8, not concealed completed
  implementations. No additional hardcoded user-visible caption or naming
  defect was found in the reviewed Step 3 paths.

### Validation and Codex Consultation

- `dotnet test FramePrintPDF/PDF_Manager.UiTests/PDF_Manager.UiTests.csproj -c Release --no-build --verbosity minimal`:
  PASS, 37/37 tests in 6 seconds.
- Resource parity with explicit UTF-8 XML reads: PASS, 53 unique keys in each
  neutral/en/ja/zh resource; no missing or extra keys.
- The required bounded read-only Codex consultation succeeded in 537.39 seconds
  using `gpt-5.6-sol`. Response:
  `.agents/logs/codex/20260920T091345Z-quality-review-step3-csharp-frameweb-client.md`.
  It independently identified the dirty-save reentrancy, non-awaited close,
  cancellation callback, layout-fidelity, non-transactional restore,
  stale-active-key, and thread-affinity issues. Its findings were verified
  against source before inclusion; production layout wiring and observer
  delivery were added by the direct review.
- Previously supplied evidence remains: both Release builds pass, both solution
  test graphs pass 131/131, targeted formatting and ownership checks pass, and
  AgentOnly is green. This Step 3 verdict does not change the known
  repository-wide Python/Angular baseline or the legacy print-host security
  NO-GO recorded by the security review.

## Final Step 3 Remediation Re-review (2026-09-20)

### Finding Status Matrix

#### [Resolved, Step 1 Medium] Clean-build warning evidence is now accurate

- **Evidence**: `.agents/docs/plans/csharp-frameweb-client.md:138` and
  `HANDOFF.md:26,64` now distinguish warning-free new projects from the 28
  inherited warnings emitted by a clean or invalidated `LegacyPrinting` build,
  and explicitly reject an up-to-date zero-warning build as clean-build proof.
- **Disposition**: Resolved. The implementation still carries the legacy
  warnings, but the inaccurate reproducibility claim identified by this review
  has been corrected.

#### [Resolved, Step 3 High] Dirty-save replacement and close are serialized

- **Evidence**: `FramePrintPDF/PDF_Manager/Shell/MainForm.cs:182-265` routes
  New/Open/Save through one transition path; `MainForm.cs:530-603` disables
  mutating commands while transitions/close are pending and checks document
  revision, reference, and path before publishing; `MainForm.cs:659-745`
  carries the captured snapshot through save and replacement authorization.
- **Regression**:
  `FramePrintPDF/PDF_Manager.UiTests/Step3MainFormRemediationTests.cs:17-70`
  holds Save A open, injects document B, verifies disabled commands and a queued
  transition, and proves neither stale replacement nor unchecked close occurs.
- **Disposition**: Resolved.

#### [Resolved, Step 3 Medium] Close awaits current cleanup and applies a bounded policy to every close phase

- **Evidence**: `FramePrintPDF/PDF_Manager/Shell/MainForm.cs:993-1072` cancels
  the current operation, awaits it, serializes transition ownership, then
  separately bounds dirty-document authorization/save and layout persistence.
  `MainForm.cs:1130-1209` implements the timeout, owned-token cancellation,
  safe late observation, and diagnostic paths. A dirty-save timeout aborts the
  close to preserve unsaved data; a layout-save timeout is diagnosed and close
  proceeds.
- **Regression**:
  `FramePrintPDF/PDF_Manager.UiTests/Step3MainFormRemediationTests.cs:74-138`
  covers cooperative cleanup and a
  non-cooperative pre-existing operation.
  `FramePrintPDF/PDF_Manager.UiTests/MainFormCancellationClassificationTests.cs:105-183`
  covers non-cooperative
  close-time dirty save and layout save, including cancellation-token
  observation, bounded return, late-publish rejection, and the distinct
  abort-close/permit-close policies.
- **Disposition**: Resolved. The final re-review initially found that the
  close-time save calls were outside the timeout; lines 1038-1064 and the new
  tests close that last gap.

#### [Resolved, Step 3 Medium] Cancellation callback failures cannot strand ownership or teardown

- **Evidence**:
  `FramePrintPDF/PDF_Manager/Shell/Lifecycle/OperationCancellationOwner.cs:46-95,111-155`
  swaps ownership under lock, cancels outside it with all callbacks enabled,
  finalizes/disposes in `finally`, and preserves an observable diagnostic.
  `FramePrintPDF/PDF_Manager/Shell/Lifecycle/ActivationCoordinator.cs:168-195,354-399`
  similarly restarts pump ownership
  in `finally` and isolates cancellation/reporting failures.
- **Regression**:
  `FramePrintPDF/PDF_Manager.UiTests/Step3LifecycleRemediationTests.cs:13-93`
  exercises throwing callbacks during
  replacement, explicit cancel, and dispose while verifying every callback and
  the final ownership state.
- **Disposition**: Resolved.

#### [Resolved, Step 3 Medium] Production layout persistence is wired into shell startup and close

- **Evidence**:
  `FramePrintPDF/PDF_Manager/Shell/Contents/MainFormServices.cs:100-150`
  provides the injectable production layout store and bounded close policy;
  `MainForm.cs:826-879,982-991,1013-1072` restores after factories/default
  panes exist and persists the final validated layout during serialized close.
- **Regression**:
  `FramePrintPDF/PDF_Manager.UiTests/Step3MainFormRemediationTests.cs:142-198`
  proves injected-store restart,
  final-layout save, absent/invalid/oversized fallback, safe diagnostics, and
  default panes. `FramePrintPDF/PDF_Manager.UiTests/ShellLayoutStoreTests.cs:9-110`
  covers missing storage,
  exact/over byte limits, malformed UTF-8, raw invalid JSON handoff, replacement
  cleanup, and cancellation for the production store.
- **Disposition**: Resolved.

#### [Resolved, Step 3 Medium] Persisted arrangement matches the explicitly supported live subset

- **Evidence**:
  `FramePrintPDF/PDF_Manager/Shell/Docking/DockLayoutAdapter.cs:10-21,76-118,221-271`
  documents the representable subset and captures live pane/tab order, exact
  supported dock state, floating bounds, and logical active document.
  `FramePrintPDF/PDF_Manager/Shell/Docking/DockStateMapper.cs:20-34` rejects
  auto-hide instead of silently normalizing
  it; `DockLayoutAdapter.PersistsDockedDimensions` explicitly reports that
  docked proportions are outside v1.
- **Regression**:
  `FramePrintPDF/PDF_Manager.UiTests/Step3DockingRemediationTests.cs:203-267`
  redocks, reorders, floats/resizes,
  captures, restores into a fresh host, and compares the supported live state.
- **Disposition**: Resolved by an explicit, tested v1 constraint rather than by
  claiming unsupported DockPanel state is persisted.

#### [Resolved, Step 3 Medium] Restore application is transactional with observable rollback failure

- **Evidence**:
  `FramePrintPDF/PDF_Manager/Shell/Docking/DockLayoutAdapter.cs:155-219`
  snapshots before apply, restores the snapshot after apply failure, rethrows
  the original failure when rollback succeeds, and emits
  `DockLayoutTransactionException` with both failures when rollback fails.
  `FramePrintPDF/PDF_Manager/Shell/Docking/DockContentRegistry.cs:437-469`
  safely detaches staged tools from the live
  layout while retaining reusable instances.
- **Regression**:
  `FramePrintPDF/PDF_Manager.UiTests/Step3DockingRemediationTests.cs:80-133,161-199`
  verifies exact prior keys,
  object identity, placement/order/bounds/active state, removal of staged keys,
  and exact apply-plus-rollback aggregation.
- **Disposition**: Resolved.

#### [Resolved, Step 3 Low] Null active-document state is explicit

- **Evidence**:
  `FramePrintPDF/PDF_Manager/Shell/Docking/DockLayoutAdapter.cs:42-73,203-214,344-382`
  preserves logical
  null state; `MainForm.cs:838-854,953-961` clears both public shell state and
  coordinator state when no document is active.
- **Regression**:
  `FramePrintPDF/PDF_Manager.UiTests/Step3DockingRemediationTests.cs:137-157`
  and `FramePrintPDF/PDF_Manager.UiTests/Step3MainFormRemediationTests.cs:202-216`
  cover restore and closing the last
  document.
- **Disposition**: Resolved.

#### [Resolved, Step 3 Low] Registry and layout operations enforce the creating UI thread

- **Evidence**: `DockContentRegistry.cs:28-35,37-98,100-150,235-262` captures
  the owner thread and guards public state/event/disposal access;
  `DockLayoutAdapter.cs:23-39,47-79,111-153,385-396` applies the same invariant.
- **Regression**:
  `FramePrintPDF/PDF_Manager.UiTests/Step3DockingRemediationTests.cs:11-76`
  invokes every public
  stateful registry/layout surface from a second STA thread and requires the
  exact rejection without mutation.
- **Disposition**: Resolved.

#### [Resolved, Step 3 Low] Activation notifications are ordered and failure-isolated

- **Evidence**:
  `FramePrintPDF/PDF_Manager/Shell/Lifecycle/ActivationCoordinator.cs:168-195,317-399`
  clears/restarts pump ownership in `finally`, serializes observer delivery,
  drops obsolete revisions, and isolates observable observer/cancellation
  failures.
- **Regression**:
  `FramePrintPDF/PDF_Manager.UiTests/Step3LifecycleRemediationTests.cs:96-190`
  covers concurrent
  revision order/latest-wins behavior, a throwing observer followed by later
  successful work, and null-active clearing.
- **Disposition**: Resolved.

#### [Resolved, Step 3 Low found during final re-review] Uncancelled OCE is reported as unexpected

- **Evidence**:
  `FramePrintPDF/PDF_Manager/Shell/MainForm.cs:555-576,611-637,691-745,864-879`
  no longer treats
  an arbitrary `OperationCanceledException` as expected control flow; operation
  suppression is filtered by the owned token and other shell phases diagnose
  unexpected failures.
  `FramePrintPDF/PDF_Manager/Shell/Lifecycle/UserExceptionBoundary.cs:38-77`
  applies the same rule
  at the reusable boundary.
- **Regression**:
  `FramePrintPDF/PDF_Manager.UiTests/MainFormCancellationClassificationTests.cs:15-101`
  covers uncancelled OCE,
  uncancelled `TaskCanceledException`, project-store timeout classification,
  and genuine owned cancellation;
  `FramePrintPDF/PDF_Manager.UiTests/Step3BoundaryRemediationTests.cs:144-184`
  covers the boundary directly.
- **Disposition**: Resolved before final verdict.

### Final Validation

- `dotnet test FramePrintPDF/PDF_Manager.UiTests/PDF_Manager.UiTests.csproj -c Release --no-restore --verbosity minimal`:
  PASS, 74 passed, 0 failed, 0 skipped (independent final run).
- Focused final MainForm cancellation/close remediation:
  `MainFormCancellationClassificationTests`: PASS, 6/6 (implementer run,
  followed by the independent 74-test run above).
- Lead final evidence is green: both solution graphs 168/168,
  prior Step 3 stress 24/24 repeated three times, targeted formatting,
  `git diff --check`, and AgentOnly `overall=pass` at
  `.agents/logs/check-20260920T101620531Z-29020.log`.
- Python/Angular full suites were not rerun, as required. Coverage was not
  measured; no percentage is inferred.

### Remaining Non-finding Limitations

- LayoutState v1 intentionally excludes auto-hide and docked pane proportions;
  unsupported states fail explicitly rather than round-tripping inaccurately.
- STA UI tests remain a single-process gate. Child-process isolation was
  optional and was not added.
- The legacy Azure/local-print security findings and publication NO-GO are
  outside the Step 3 quality verdict and remain tracked by the security report.

## Step 4 Final Quality Review (2026-09-20)

### Verdict

**CHANGES REQUIRED** — Critical: 0; High: 3; Medium: 5; Low: 1.

The typed boundaries, strict request/response handling, document revision
publication rules, runtime ownership design, and low-level PDF structure are
substantially sound. Step 4 cannot yet be called a completed vertical MVP,
however: the scene-based viewport does not paint through its normal invalidation
path, the real asynchronous startup path can leave the WinForms main thread's
STA apartment, and no automated acceptance executes the desktop client against
the live Python HTTP service. The remaining Medium findings show that several
visible Step 4 semantics are only partially wired.

### Findings

#### [High] Scene-based documents never paint through the normal invalidation path

- **Evidence/current behavior**:
  `FramePrintPDF/PDF_Manager.Rendering/OpenGlViewportLifecycle.cs:179-195`
  makes `SetScene` assign `_scene` and clear `_model`. `Render` then returns
  whenever `_model is null` at `OpenGlViewportLifecycle.cs:322-329`, even
  though `EnsureReady` correctly accepts either a model or a scene at
  `OpenGlViewportLifecycle.cs:573-583`. The production document calls
  `SetScene` at
  `FramePrintPDF/PDF_Manager/Shell/Contents/ProjectDocumentContent.cs:243-249`,
  and Paint delegates to `Render` at `OpenGlViewportLifecycle.cs:411-439`.
  Capture works only because it bypasses `Render` and calls `DrawKnownFrame`
  directly at lines 336-355.
- **Impact**: a project can compile/upload scene commands and even produce a
  capture while the on-screen GL surface remains blank. The reported real-GL
  probe does not cover this path: it uses `SetModel` at
  `FramePrintPDF/PDF_Manager.RendererProbe/RendererVerification.cs:88-99`;
  the scene lifecycle test only exercises pre-initialization state at
  `FramePrintPDF/PDF_Manager.Rendering.Tests/RendererLifecycleTests.cs:33-74`.
- **Concrete fix**: change the render guard to reject only when both `_model`
  and `_scene` are null (or use the compiled vertex/command readiness
  invariant). Add a real-context regression that calls `SetScene`, requests a
  paint, observes an incremented rendered-frame count, and verifies known
  scene pixels without invoking Capture first.

#### [High] Awaiting real runtime startup can resume the WinForms entry point on an MTA pool thread

- **Evidence/current behavior**:
  `FramePrintPDF/PDF_Manager/Program.cs:9-21` declares an async
  `[STAThread] Main` and awaits `DesktopApplicationSession.RunAsync`.
  `DesktopApplicationSession.cs:61-75` awaits runtime startup before creating
  `MainForm` and invoking `Application.Run`. At that point no WinForms message
  loop or control-backed synchronization context has been installed, so
  `ConfigureAwait(true)` at line 72 has no STA context to capture. A genuinely
  asynchronous `FrameWebLocalRuntime.StartAsync` can therefore continue on a
  ThreadPool MTA thread. The test double masks this: its `StartAsync` returns
  `Task.CompletedTask` at
  `FramePrintPDF/PDF_Manager.UiTests/Step4VerticalIntegrationTests.cs:224-252`.
- **Impact**: the real one-command startup path can create and run the entire
  WinForms tree outside STA. OLE-backed dialogs, clipboard/drag-drop, COM, and
  other WinForms operations may fail or behave inconsistently even though the
  synchronously completing fake test passes.
- **Concrete fix**: keep form construction and `Application.Run` on the
  original STA thread—for example, use a synchronous STA entry point that
  blocks on the runtime's fully `ConfigureAwait(false)` startup/stop tasks, or
  introduce an explicit STA dispatcher before the await. Add a fake whose
  startup completes asynchronously on another thread and assert that
  `createForm`/`runForm` execute on the original STA thread.

#### [High] The required live-Python vertical acceptance and rendered PDF golden are absent

- **Evidence/current behavior**: the test named as the Step 4 vertical
  integration reads an ARS fixture and injects `ResultAnalysisClient` at
  `FramePrintPDF/PDF_Manager.UiTests/Step4VerticalIntegrationTests.cs:16-38,160-196`;
  it never constructs `FrameWebAnalysisClient` or starts Python. Core HTTP
  tests use message-handler stubs, and runtime tests pair a PowerShell loop with
  an independent `LocalReadyServer`, for example
  `tools/FrameWeb.LocalRuntime.Tests/FrameWebLocalRuntimeTests.cs:8-33`.
  The PDF test checks deterministic bytes/text/xref, but there is no Poppler (or
  equivalent) rendered-page comparison; the Step 4 test only checks length and
  `%PDF-1.4` at `Step4VerticalIntegrationTests.cs:67-70`.
- **Impact**: the plan's acceptance at
  `.agents/docs/plans/csharp-frameweb-client.md:151` is unproven for the exact
  seams most likely to drift: serialized request -> real Flask route -> strict
  ARS response -> UI publication, real Python process cleanup, and visual PDF
  composition. The green 217-test count cannot establish clean-checkout
  calculate/no-orphan acceptance and did not expose either High defect above.
- **Concrete fix**: add a bounded, isolated live integration harness that
  starts the actual FrameWeb service through `FrameWebLocalRuntime` on a free
  loopback port, waits for readiness, submits the representative project with
  `FrameWebAnalysisClient`, validates/publishes the ARS response, exports the
  report, stops/disposes the runtime, and asserts the owned PID tree is gone.
  Render the exported PDF with the approved renderer and compare the page to an
  reviewed golden (with explicit tolerance/version policy). Make this one
  documented clean-checkout command.

#### [Medium] PDF export substitutes a second schematic renderer for the inspected viewport

- **Evidence/current behavior**:
  `ProjectDocumentContent.CaptureViewport` exists at
  `FramePrintPDF/PDF_Manager/Shell/Contents/ProjectDocumentContent.cs:175`, but
  `MainForm` creates a print request containing only the document/result at
  `FramePrintPDF/PDF_Manager/Shell/MainForm.cs:295-323`.
  `DesktopPdfExporter` then rebuilds nodes/members and calls
  `ModelViewportCapture.Create` at
  `FramePrintPDF/PDF_Manager/Shell/Printing/DesktopPdfExporter.cs:24-44`.
  That schematic has its own fixed projection, omits loads and the displacement
  layer, and is not the camera/layer the user inspected. It also reads
  `Document.Selection.NodeIds`, while the selection bridge only updates the
  renderer/editor at `MainForm.cs:1001-1014`; the runtime-only document
  selection is never updated.
- **Impact**: the exported image can disagree with the visible camera,
  projection, result layer, and current selection while still passing byte
  determinism tests. This is a hardcoded substitute rather than a deterministic
  capture of the live Step 4 viewport.
- **Concrete fix**: capture a bounded bitmap/RGB payload from the document
  renderer on the UI thread before asynchronous file output and pass it through
  a typed print request/job. Preserve the active projection, layer, and
  selection; test the exported image against the actual inspected frame.

#### [Medium] Valid rotational supports and moment-only nodal loads disappear from the scene

- **Evidence/current behavior**: request readiness accepts any restrained
  translational or rotational DOF at
  `FramePrintPDF/PDF_Manager.Core/Analysis/FrameWebAnalysisRequestJson.cs:143-147`,
  and the editor exposes all six support and load components. Scene creation
  filters out supports unless `FixX/Y/Z` is set and maps only `Fx/Fy/Fz` at
  `FramePrintPDF/PDF_Manager/Shell/Contents/ProjectDocumentContent.cs:276-294`.
  `SceneSupport` has only three translation flags and rejects translation-free
  supports at
  `FramePrintPDF/PDF_Manager.Rendering/Scene/ViewportSceneModel.cs:54-60,178-184`;
  a zero force vector is silently omitted by
  `ViewportSceneCompiler.cs:335-345` even when `Mx/My/Mz` is nonzero.
- **Impact**: a document accepted by the editor and analyzer can visually hide
  its only restraint or load, giving the user a materially misleading model.
- **Concrete fix**: carry all six DOFs and force/moment components into the
  typed scene. Draw distinct rotational-restraint and moment glyphs, or reject
  those components as unsupported before edit/analysis until they are visible.
  Cover rotation-only and moment-only models in scene command snapshots and
  selection tests.

#### [Medium] Member-force and PDF result tables silently omit valid results

- **Evidence/current behavior**: the member-force UI emits only
  `segment.StationI` and `segment.IEnd` at
  `FramePrintPDF/PDF_Manager/Shell/Contents/ProjectDocumentContent.cs:350-369`;
  every J end, including the terminal station and possible discontinuities, is
  absent. PDF mapping takes at most 8 displacement, 6 reaction, and 5 member
  records, and only the first segment I end, at
  `FramePrintPDF/PDF_Manager/Shell/Printing/DesktopPdfExporter.cs:64-80`.
  The writer then silently takes 18 rows at
  `FramePrintPDF/PDF_Manager.Printing/TypedPdfExporter.cs:398-424`, so even the
  mapper's maximum 19 rows guarantees a dropped row with no notice.
- **Impact**: “inspect” and “one result table” can present a plausible but
  incomplete engineering result without identifying it as a sample. Users may
  miss a terminal/end force or assume the PDF is exhaustive.
- **Concrete fix**: present both I and J station/end values using the contract's
  canonical order. Paginate PDF rows or clearly label and count a bounded
  excerpt; never silently truncate. Add fixtures where I/J differ and where
  row count exceeds one page.

#### [Medium] PDF input limits are enforced only after unbounded enumeration or allocation

- **Evidence/current behavior**: `TypedPdfJob` calls `ToArray()` on all public
  enumerables before checking maximum counts at
  `FramePrintPDF/PDF_Manager.Printing/TypedPdfExporter.cs:72-87`.
  `ModelViewportCapture.Create` does the same and allocates
  `new byte[checked(width * height * 3)]` before `ViewportCapture` validates
  dimensions at `TypedPdfExporter.cs:162-183`; the actual dimension limits are
  only in the later constructor at lines 13-38.
- **Impact**: an oversized, lazy, or non-terminating enumerable can consume
  unbounded time/memory before the advertised bound rejects it, and attacker-
  supplied dimensions can trigger huge allocation/overflow before the public
  range check. The boundary is not resource-bounded despite its constants.
- **Concrete fix**: materialize with `Take(max + 1)` (and cancellation where
  appropriate), reject immediately on the extra item, validate dimensions and
  checked byte length before allocation, and add over-limit/lazy-enumerable
  tests that prove bounded consumption.

#### [Medium] New analysis/PDF presentation strings bypass localization and PDF text is silently lossy

- **Evidence/current behavior**: Core embeds Japanese user-facing analysis
  messages at
  `FramePrintPDF/PDF_Manager.Core/Analysis/FrameWebAnalysisClient.cs:144-152,193-209,213-250,287-316`;
  `UserExceptionBoundary` publishes `CoreOperationException.UserMessage`
  directly. The PDF path embeds English captions/errors at
  `FramePrintPDF/PDF_Manager/Shell/Printing/DesktopPdfExporter.cs:51-80` and
  `FramePrintPDF/PDF_Manager.Printing/TypedPdfExporter.cs:398-424`.
  `EscapePdfText` replaces every non-ASCII character with `?` at
  `TypedPdfExporter.cs:430-457`, so a valid Japanese/Chinese project name is
  silently corrupted.
- **Impact**: switching the shell to English or Chinese can still show Japanese
  analysis errors; exported reports ignore the selected language and lose
  valid project metadata. Core also assumes a presentation language, weakening
  its otherwise clean responsibility boundary.
- **Concrete fix**: propagate typed failure kind/code plus diagnostic cause and
  localize in the shell resources. Pass a localized typed report-label set to
  the dependency-free writer. Use an approved embedded Unicode font/encoding,
  or explicitly reject unsupported text until the Step 8 font decision—do not
  silently substitute `?`. Add en/ja/zh error and non-ASCII PDF text tests.

#### [Low] Production HTTP/concurrency resources have no explicit owner-disposal path

- **Evidence/current behavior**:
  `DesktopApplicationSession.CreateServices` allocates an `HttpClient` at
  `FramePrintPDF/PDF_Manager/Shell/Composition/DesktopApplicationSession.cs:49-58`.
  `FrameWebAnalysisClient` explicitly does not own that client and owns a
  `SemaphoreSlim` without implementing disposal at
  `FramePrintPDF/PDF_Manager.Core/Analysis/FrameWebAnalysisClient.cs:93-112`.
  `MainForm.Dispose` disposes shell resources but not the injected analysis
  service at `FramePrintPDF/PDF_Manager/Shell/MainForm.cs:340-356`.
- **Impact**: the current one-form process normally lets OS process teardown
  reclaim these objects, but restartable hosting/tests or future multiple
  sessions leak sockets/handlers and wait-handle resources; ownership is
  ambiguous.
- **Concrete fix**: make composition own and dispose a service/session scope
  containing `HttpClient` and disposable clients, while preserving the rule
  that an injected `HttpClient` is not disposed by `FrameWebAnalysisClient`.

### Acceptance Assessment

| Step 4 acceptance item | Assessment |
|---|---|
| New/open/edit/undo/redo/save/reopen representative project | **Test-backed with in-process fakes** by `Step4VerticalIntegrationTests`; document v1 round-trip and optional legacy-v1 sections are separately covered. |
| Calculate through the current Python JSON path | **Unit-backed but end-to-end unproven**. Serializer/client and Python contract fixtures are tested independently; no live HTTP test joins them. |
| Inspect viewport and basic results | **Not satisfied as shipped** because scene Paint returns early. Table publication is test-backed, but member J-end forces and rotation/moment glyphs are missing. |
| Export typed PDF | **Partially test-backed**. Byte determinism, xref/length, escaping safety, cancellation, and basic text are covered; live viewport fidelity, complete table semantics, Unicode, and rendered golden are not. |
| Cancellation/backend failure preserves prior results | **Code- and test-backed** through captured document/revision/token publication checks and the Step 4 prior-result tests. |
| One clean-checkout command starts desktop + Python | **Code-backed only**, and the async STA defect makes the real path unsafe. No clean-checkout launch smoke is recorded. |
| Exit leaves no Python child process | **Job/process design and simulated-process tests are strong**, including child-tree cleanup, but an actual Flask process launched by the desktop path is not part of the acceptance suite. |

### Explicit No-Finding Areas

- Project dependency direction remains clean: package-free Core owns document,
  ARS, and operation contracts; Rendering and typed Printing do not reference
  WinForms composition or the legacy print bridge; the desktop composition root
  alone references Core, Rendering, Printing, and LocalRuntime. No
  `disg`/`reac`/`fsec`, legacy dictionary, Azure handler, or UI-specific result
  schema leaked into the active calculation boundary.
- `ProjectDocument` v1 still reads legacy files with absent `sections`, writes
  independent entities deterministically, preserves explicit section values,
  validates finite values/references, and keeps results/selection/dirty state
  out of persisted JSON.
- `FrameWebAnalysisRequestJson` explicitly requires `kN-m`, canonical positive
  numeric node/member/section IDs, persisted section properties, deterministic
  order, request byte/entity/case limits, and cancellation. No synthesized
  physical section default was found.
- `FrameWebAnalysisClient` validates endpoint/options, bounds concurrency and
  response bytes/depth/entities/cases/results, distinguishes caller
  cancellation from transport timeout, maps HTTP failures without exposing
  response bodies, and validates the complete ARS before return. The finding
  above concerns localization/ownership, not result-contract strictness.
- MainForm analysis publication remains revision/reference/path/token guarded;
  edit/undo/redo increments the document revision, marks dirty, clears stale
  results, and backend/cancellation failures preserve the prior result.
  Selection forwarding is reentrancy-safe for the live editor/viewport; the
  stale selection finding is limited to the separate PDF request path.
- Apart from the Render guard, GL operations enforce the creating thread,
  invalidation is demand-driven, command compilation/hit-testing is
  deterministic, and `ProjectDocumentContent` tears down the renderer before
  the child control.
- `FrameWebLocalRuntime` serializes start/stop, cleans failed starts for retry,
  bounds output capture, isolates output-subscriber failures, uses a kill-on-job-
  close Job Object, handles parent exit, and makes dispose idempotent. Startup's
  Angular behavior remains separate from the desktop Python-only route.
- The PDF writer's object numbering, byte offsets, xref, `/Length`, stream
  layout, literal delimiter escaping, and deterministic single-page byte output
  are internally consistent and directly tested. The findings concern semantic
  fidelity, Unicode, truncation, and pre-validation resource use.

### Evidence and Consultation

- Source review covered the supplied full patch against `090cc5a`, the current
  product and test sources, AGENTS/rules, DESIGN, Step 4 plan, and HANDOFF.
- Supplied automated evidence was considered but not rerun by this reviewer:
  both Release builds at zero warnings; both solution graphs 217/217 (Core 102,
  composition/Printing 13, Rendering 16, UI 78, Runtime 9); Python contracts
  14/14; real-GL probe 20 cycles; AgentOnly pass. Those results do not exercise
  the missing scene-Paint, asynchronous-STA, or live-Python acceptance paths.
- A bounded read-only Codex consultation completed mechanically, but its
  response contained only a request for the objective after an apparent
  encoding/context failure. No finding or verdict relies on it. Artifacts:
  `.agents/logs/codex/20260920T110645Z-quality-review-step4-csharp-frameweb-client.prompt.md`
  and `.agents/logs/codex/20260920T110645Z-quality-review-step4-csharp-frameweb-client.md`.

## Step 4 Post-remediation Final Quality Re-review (2026-09-20)

### Current Verdict

**PASS for Step 4 quality with one tracked Low limitation** — Critical: 0;
High: 0; Medium: 0; Low: 1.

This section supersedes the pre-remediation Step 4 verdict above. All three
original High findings, four of the five original Medium findings, the original
Low ownership finding, and the additional capture/scene-bound/rasterizer/WGL
issues found during re-review are resolved. The remaining language/PDF finding
is reclassified from Medium to Low for this checkpoint because the Step 4
representative vertical is intentionally an ASCII, single-page PDF subset and
the approved plan assigns multilingual PDF/font parity to Step 8. It remains a
real product-fidelity limitation and is not waived for completed-app
distribution.

The quality count above intentionally does not duplicate the specialized
security review's Step 4 Low findings (pre-Job process-start window,
pre-truncation whole-line output allocation, and server work surviving client
timeout) or the test review's residual isolation/race-coverage Lows. Those
remain open under their owning reports.

### Remaining Quality Finding

#### [Low] Analysis errors and the typed PDF subset do not yet preserve the selected language or CJK metadata

- **File/line and current behavior**:
  `FramePrintPDF/PDF_Manager.Core/Analysis/FrameWebAnalysisClient.cs:212,258-322,399-415`
  constructs Japanese user-facing messages in UI-independent Core, and
  `FramePrintPDF/PDF_Manager/Shell/Lifecycle/UserExceptionBoundary.cs:85`
  publishes that message directly. The report writer fixes its headings and
  captions in English at
  `FramePrintPDF/PDF_Manager.Printing/TypedPdfExporter.cs:445-460`; its
  `EscapePdfText` maps every non-ASCII character to `?` at lines 490-504.
  `FramePrintPDF/PDF_Manager.Tests/TypedPdfExporterTests.cs:205-218` now
  documents that fallback explicitly rather than claiming Unicode support.
- **Impact**: English/Chinese shells can still show Japanese analysis errors,
  and a valid Japanese/Chinese project name is lossy in the Step 4 PDF. This
  does not invalidate the ASCII representative vertical, but it prevents
  language-parity claims and remains part of the completed-app
  publication/redistribution NO-GO.
- **Concrete fix**: return typed failure code/kind plus diagnostic cause from
  Core and localize in the shell; pass a localized typed label set into
  Printing; in Step 8 select and license an approved Unicode font/encoding and
  prove Japanese/Chinese output with rendered goldens. Until then, surface the
  documented limitation or reject unsupported report text instead of silently
  losing it.

### Remediation Closure Evidence

| Prior or re-review issue | Final disposition and evidence |
|---|---|
| Scene documents did not paint | **Closed.** `OpenGlViewportLifecycle.cs:322-337` accepts either the legacy model or typed scene. The real probe now sets a typed scene, paints, changes projection/size/selection/displacement, captures, and tears down. |
| Async startup could leave STA | **Closed.** `Program.cs:10-21` uses a synchronous `[STAThread]` entry point and `DesktopApplicationSession.cs:75-105` keeps startup, form construction, message loop, stop, and disposal on the original STA owner. The async-completion fake regression is at `Step4VerticalIntegrationTests.cs:372-407`. |
| No live Python/client acceptance | **Closed.** `FrameWebRealProcessIntegrationTests.cs:12-106` starts actual locked `uv`/Flask, uses the production authenticated `HttpClient` and `FrameWebAnalysisClient`, asserts case/topology/displacement/reaction/member-force semantics, disposes the runtime, and proves all observed PIDs exit. |
| No independent rendered PDF golden | **Closed.** `PdfSubsetPageRasterizer.cs:30-35,73-228,877-1057` follows the actual xref/trailer/catalog/pages/page/resource/content graph and fail-closed interprets the owned graphics/text subset. `TypedPdfExporterTests.cs:50-101` proves missing `Do`/`cm`, broken resources, and corrupt image data fail, while matrix/pixel mutations change the raster. |
| PDF used a substitute schematic | **Closed.** Production uses `ViewportCaptureProvider.cs:11-26` to capture the shown renderer on the UI thread before asynchronous file output. `DesktopPdfExporter.cs:19-39` rejects the headless substitute by default; only explicit test construction may opt in. The exact captured RGB payload is asserted in the vertical test. |
| Rotation-only supports and moment-only loads disappeared | **Closed.** `ProjectDocumentContent.cs:287-300` maps all six restraint and force/moment components; `ViewportSceneCompiler.cs:114-157` emits selectable rotation/moment glyphs; scene and shell mapping regressions cover both. |
| Member J ends and truncation were silent | **Closed.** `DesktopPdfExporter.cs:130-179` emits both `M-I` and `M-J` and labels bounded excerpts explicitly. The vertical test uses differing I/J values and asserts both UI/PDF paths. |
| Scene/PDF materialization could be unbounded | **Closed for the Step 4 typed boundaries.** `ViewportSceneModel.cs:78-135,280-302` and `TypedPdfExporter.cs:37-55,91-105,164-226` use maximum-plus-one materialization and dimension-first checked byte sizing, including infinite-sequence regressions. |
| Viewport capture failure escaped the menu boundary | **Closed.** `MainForm.cs:314-329` captures inside the owned operation boundary; `Step4VerticalIntegrationTests.cs:313-365` proves a throwing provider is diagnosed, user-mapped, leaves no file, and clears operation state. |
| HTTP/client/concurrency resources lacked an owner | **Closed.** `DesktopApplicationSession.cs:53-72,108-124` owns the analysis client and runtime-created `HttpClient`; disposal rejects new analysis work while admitted calls release their slot. |
| WGL `MakeCurrent` was intermittently unstable | **Closed by current-context ownership, without retry.** `OpenGlViewportLifecycle.cs:579-610` calls `MakeCurrent` only when the control context is not already current and releases it before GLControl disposal. The pre-fix controlled baseline reproduced failures in 2 of 5 fresh runs; the independent post-fix evidence below is clean. |
| Result-to-displacement scene seam was only hand-built | **Closed.** `Step4VerticalIntegrationTests.cs:236-280` sends a real `AnalysisResultSet` through the shell result selector and verifies every displacement ID/vector, scale, selection, and zero viewport failures in `CurrentScene`. |

### Final Acceptance Assessment

| Step 4 acceptance item | Final assessment |
|---|---|
| Open/edit/undo/redo/save/reopen representative document | **Covered.** The STA vertical test edits a real `DataGridView`, exercises invalid rollback, undo/redo, Save As, ordinary Save to the remembered path, and reopen. Core separately proves deterministic v1 writing and backward reading with absent optional sections. |
| Calculate through the current Python path | **Covered across production seams.** Exact request/client tests are joined by the real locked `uv`/Flask test and semantic result assertions. No UI-only result schema or legacy `disg`/`reac`/`fsec` contract enters the desktop. |
| Inspect model, result tables, selection, and displacement | **Covered.** Typed scene compilation, shown real-GL capture, rotation/moment visibility, stable-key selection, I/J rows, and actual result-to-displacement mapping all have executable evidence. |
| Export typed PDF from the inspected viewport | **Covered for the owned Step 4 subset.** Production capture is live/UI-thread-only, the writer is deterministic and bounded, xref/object/stream lengths are checked, and the independent full-page golden reacts to graph/operator/image mutations. |
| Cancellation/backend failure preserves prior results | **Covered.** MainForm still applies token/revision/document guards and retains the last validated result on expected cancellation or mapped backend failure. Server-side work cancellation remains a security-owned Low limitation, not a false claim here. |
| One command starts desktop + Python; exit leaves no orphan | **Code- and process-backed.** Production composition directly wires the synchronous STA session to `FrameWebLocalRuntime`; the real process test proves authenticated analysis and Job cleanup. The automated proof is compositional rather than one unattended test driving a visible shell against real Python. |

### Final Independent Validation

- Current Release project runs: Core **112/112**, typed
  Printing/composition **17/17**, Rendering **27/27**, UI **83/83**, and
  LocalRuntime **14/14** — **253/253 PASS**. The LocalRuntime total includes the
  actual `uv`/Flask/real-client/no-orphan test with the strengthened semantic
  assertions; UI includes the bounded shown-renderer live-capture child.
- Focused Python desktop-runtime security contracts: **30/30 PASS**. Full
  Python and Angular suites were not rerun, as directed.
- WGL stability, no retry: five fresh serialized 20-cycle RendererProbe
  processes all passed. Each reported 20 contexts, 200 frames, 60 captures,
  deterministic `#0D1933` / `#F25926` samples, and zero live contexts,
  subscriptions, and windows. Three additional concurrent rounds pairing that
  same 20-cycle probe with `LiveCaptureProbe` also all passed; the live capture
  hash was identical in every round.
- Printing **17/17** independently revalidated the reference-following subset
  rasterizer and its mutations. The approved 595 x 842 golden was unchanged.
- The clean-checkout probe-wiring defect found after the first final draft is
  closed. After a targeted Release clean, both Debug and Release probe DLLs
  were absent; `dotnet build PDF_Manager.UiTests.csproj -c Release
  --no-restore` regenerated only
  `LiveCaptureProbe/bin/Release/net8.0-windows/LiveCaptureProbe.dll`, left Debug
  absent, and completed with 0 warnings / 0 errors. The exact shown-renderer
  live-capture filter then passed 1/1 with `--no-build`. The project now keeps a
  non-building reference for restore discovery and explicitly forwards
  `Configuration=$(Configuration)` in its bounded probe build target.
- Coverage percentage is **not measured** and no percentage is inferred from
  passing counts. The supplied full-solution Release builds, formatting, and
  agent gate remain lead-owned final evidence.

### Residual Limitations and Completion Boundary

- There is no single automated test that clicks a shown production shell while
  simultaneously hosting real Python and exporting the live-GL PDF. The UI
  vertical, shown-renderer child, real runtime/client process test, and direct
  production composition cover those seams compositionally. Treat a clean
  checkout interactive launch as a release smoke, not as evidence that is
  already contained in one test.
- The PDF rasterizer is intentionally an independent interpreter for the owned
  deterministic subset, not a general PDF conformance engine. Its actual
  reference/operator traversal and mutation failures are sufficient for the
  Step 4 writer but do not replace Step 8 third-party/production PDF
  characterization.
- Specialized residual Lows remain in the security and test reports: the
  process-start-to-Job window, pre-truncation line allocation, solver work that
  may outlive client timeout, in-process STA timeout isolation, and selected
  runtime transition coverage. They are not hidden by this quality verdict.
- The legacy print host High/Medium findings and complete-application
  redistribution NO-GO remain unchanged and outside the Step 4 quality count.

No Critical, High, or Medium quality fix remains before Step 4 can be marked
complete. The Low language/CJK item may remain explicitly owned by Steps 8/9,
but it and the legacy/security distribution gates must be resolved before the
application is represented as multilingual or redistributable.
