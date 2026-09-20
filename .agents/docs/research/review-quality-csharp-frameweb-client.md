# Code Quality Review: C# FrameWeb Desktop Step 1

## Overall Result

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
