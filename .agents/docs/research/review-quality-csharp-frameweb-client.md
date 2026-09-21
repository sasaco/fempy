# Step 7 Final Quality Closeout: Result Presentation and Export

## Verdict

**PASS WITH LOW FOLLOW-UPS** — Critical: 0, High: 0, Medium: 0, Low: 2.

The remediated implementation closes both prior High parity gaps and both prior Medium robustness gaps. Moving reactions now retain all-source signed max/min while using the Angular child-only, stable-first absolute result in the envelope table and parent-page scene. PICKUP now has a distinct engineering envelope with correlated full max/min vectors and source provenance, exported as dimension-specific 3D CSV or 2D `.pik`. Presentation work is aggregated under one checked candidate budget, and export failures retain the primary exception without cleanup masking it.

## Scope and method

This read-only closeout re-reviewed the current Step 7 product/test tree against base `2b3b494`, with focused attention to the six findings from the prior quality review. It directly compared the moving reaction implementation with `FrameWebforJS/src/app/providers/analysis-result-presentation.ts:129-160` and the PICKUP implementation with `ResultDataService.GetPicUpText/GetPicUpText2D` plus `result-pickup-fsec1.worker.ts`.

Independent closeout validation:

- `PDF_Manager.Core.Tests`: 272/272 passed, 0 skipped.
- `PDF_Manager.UiTests`: 142/142 passed, 0 skipped.
- Both test commands rebuilt their product dependencies successfully and emitted no warning or error.
- Coverage percentage was not measured.
- No product or test file was modified by this review.

## Resolved prior High findings

### H1. Moving reaction absolute child semantics and projection — resolved

`ResultPresentationService` keeps all sources for signed maximum/minimum but selects absolute reaction components from `sources.Skip(1)` whenever the moving definition has children, falling back to the sole parent only for a parent-only definition (`FramePrintPDF/PDF_Manager.Core/Results/ResultPresentationService.cs:393-410,588-641`). Strict `>` replacement preserves the first child on equal absolute magnitude.

The typed `AbsoluteSupportReactions` and scene-ready `AbsoluteReactionProjection` retain each component's source case (`FramePrintPDF/PDF_Manager.Core/Results/ResultPresentationModels.cs:476-552`). The parent page supplies that projection to the scene (`FramePrintPDF/PDF_Manager/Shell/Contents/ProjectDocumentContent.cs:689-694,928-934`) and the moving envelope table uses the same child-only values/provenance (`ProjectDocumentContent.cs:1291-1327`). Selecting an individual child returns to that child's own reaction result.

The Core Angular oracle covers a non-contiguous case order (`1`, ordinary `2`, then children `1.1`/`1.2`), parent value 100, equal-absolute children, stable first-child provenance, and parent-only fallback (`FramePrintPDF/PDF_Manager.Core.Tests/Results/Step7ResultPresentationAcceptanceTests.cs:91-112,281-316`). The UI acceptance proves the parent scene contains the component-wise child aggregate and a later-child selection contains that child's values (`FramePrintPDF/PDF_Manager.UiTests/Step7ReviewRemediationTests.cs:97-133`).

### H2. PICKUP engineering export parity — resolved

Generic displayed PICKUP values intentionally remain signed greatest-absolute scalars, preserving the DESIGN decision. A separate `PickupEngineeringEnvelope` now records, for every member end and focus component, independent signed maximum/minimum winners, source IDs, station/distance, and the complete correlated six-component vector (`FramePrintPDF/PDF_Manager.Core/Results/ResultPresentationModels.cs:284-375`). `PickupEngineeringEnvelopeBuilder` applies term factors, uses strict signed comparisons for stable ties, and retains the entire winning vector (`FramePrintPDF/PDF_Manager.Core/Results/PickupEngineeringEnvelopeBuilder.cs:10-137`).

The exporter writes the six 3D focus-component groups with both winner IDs and both complete vectors, and the M/S/N 2D projection as bounded fixed-width `.pik` records (`FramePrintPDF/PDF_Manager.Core/Results/ResultCsvExporter.cs:159-295`). The shell selects the artifact by document dimension and saves the exact bytes (`FramePrintPDF/PDF_Manager/Shell/Contents/ProjectDocumentContent.cs:404-423`). Core acceptance pins exact bytes, provenance, vectors, ordering, stable ties, and row/work/byte boundaries (`FramePrintPDF/PDF_Manager.Core.Tests/Results/Step7ResultExportAcceptanceTests.cs:103-214`); UI acceptance drives the real derived-selection path for both dimensions (`FramePrintPDF/PDF_Manager.UiTests/Step7ResultExportUiTests.cs:15-96`).

## Resolved prior Medium findings

### M1. Unbounded eager presentation amplification — resolved to bounded Low residual

One `ResultPresentationBudget` is shared across page construction, all derived results, and all moving envelopes for a candidate (`FramePrintPDF/PDF_Manager/Shell/Contents/ProjectDocumentContent.cs:696-777`). It validates the result set once and cumulatively limits pages, derived results, moving definitions, operands, output entities, and scalar work with checked arithmetic and typed stable failures (`FramePrintPDF/PDF_Manager.Core/Results/ResultPresentationLimits.cs:5-304`). Service accounting occurs before each materialization (`FramePrintPDF/PDF_Manager.Core/Results/ResultPresentationService.cs:64-87,265-275,367-380`). Exact composite totals and every +1 boundary are pinned at `Step7ResultPresentationBudgetTests.cs:9-133`.

The work is no longer unbounded. Its remaining synchronous/eager responsiveness characteristic is retained as L1 below.

### M2. Export exception provenance and cleanup masking — resolved

`ResultExportOperationException` preserves the original filesystem exception as `InnerException` and exposes a localized resource key (`FramePrintPDF/PDF_Manager/Shell/Viewport/ResultPresentationUiError.cs:11-21`). The shell publishes that typed failure without copying the raw filesystem message into user-facing text (`FramePrintPDF/PDF_Manager/Shell/Contents/ProjectDocumentContent.cs:836-875,1525-1569`).

The atomic writer records the primary write/flush/move exception and, if temporary-file cleanup also fails, attaches the cleanup exception to the primary exception instead of replacing it (`FramePrintPDF/PDF_Manager/Shell/Viewport/AtomicResultExportWriter.cs:20-54`). UI filesystem acceptance verifies overwrite, exact bytes, locked-target failure, preserved original exception, unchanged target, and no temporary-file residue (`FramePrintPDF/PDF_Manager.UiTests/Step7ResultExportUiTests.cs:47-96`).

## Resolved prior Low finding

### Constructor source compatibility — resolved

The original seven-argument `PresentedStaticResult` constructor is restored and forwards empty shell/solid/envelope values; the intermediate shell/solid constructor also remains (`FramePrintPDF/PDF_Manager.Core/Results/ResultPresentationModels.cs:8-78`). The original five-argument `MovingLoadEnvelope` constructor is likewise retained and forwards empty member extrema (`ResultPresentationModels.cs:476-511`). Full Core compilation and tests exercise both compatibility paths.

## Remaining Low findings

### L1. Presentation is bounded but still eagerly computed on the UI thread

`SetDocument` and `SetResult` synchronously build the complete immutable candidate before commit, including every derived graph and moving envelope (`FramePrintPDF/PDF_Manager/Shell/Contents/ProjectDocumentContent.cs:323-371,696-777`). Defaults permit up to 2,000,000 output entities and 20,000,000 scalar operations (`FramePrintPDF/PDF_Manager.Core/Results/ResultPresentationLimits.cs:15-27`). This is now finite and fail-fast, so it is no longer an availability-level Medium finding, but a near-limit valid candidate can still cause a perceptible UI pause and large transient allocation.

**Remediation:** add a large-valid responsiveness/allocation baseline. If measured latency is unacceptable, build the candidate on a cancellable background Core path or materialize derived/envelope views lazily while preserving the current single-budget and atomic commit contract.

### L2. `ProjectDocumentContent` remains an oversized result subsystem boundary

The class is now 1,830 lines and still owns candidate construction, navigation, selector reconciliation, table projection, scene routing, localization, CSV/PIK choice, dialogs, atomic save invocation, and result error policy (`FramePrintPDF/PDF_Manager/Shell/Contents/ProjectDocumentContent.cs:323-423,686-894,1040-1413,1520-1569`). The new Core models and `ResultExportArtifact` improve separation, but the shell controller remains the main omission/regression hotspot.

**Remediation:** extract a result-presentation coordinator/view model and an export command/service, leaving the DockContent responsible for control binding and committing validated immutable state.

## Explicit checks and non-findings

- Static, nonlinear-step, and modal-mode coordinates remain ordered and distinct; the Ct fixture pins all 11 real cases and boundary pages.
- Moving later children need not be adjacent to the parent; ordinary intervening cases remain ordinary pages.
- Reaction absolute values use child-only stable-first provenance in Core, table, CSV, and parent scene; signed max/min remain all-source.
- PICKUP max/min winner selection retains full correlated vectors and stable source provenance; generic signed greatest-absolute display semantics remain independent.
- Presentation limits aggregate across the whole candidate and fail before the over-limit materialization is committed; prior valid UI state remains intact on invalid or over-budget candidates.
- CSV/PIK generation remains deterministic, invariant, bounded, and formula-safe where fields are CSV text.
- Atomic save writes a same-directory temporary file, flushes to disk, replaces only after completion, preserves the primary failure, and does not silently swallow cleanup errors.
- No hardcoded substitute, weakened test, legacy result adapter, swallowed product exception, or Step 8 printing/package scope leak was found.

## Consultation status

No nested Codex consultation was invoked. This was an explicitly delegated independent closeout, and direct inspection of the remediated C#, Angular oracle, and focused acceptance tests supplied the required evidence. No consultation result is claimed.
