# SECOND-FINAL Step 8 Quality and Correctness Review: Typed Printing

## Verdict

**PASS** — Critical: 0, High: 0, Medium: 0, Low: 0.

The two Medium findings from the preceding FINAL review are closed. PDF and preview now consume one immutable plan-owned text layout; fitted text is width-measured, grapheme-safe ellipsized, and clipped to the planned cell in both renderers, while the synchronized dialog exposes the complete unabridged page text as selectable content. Whole-document preview now validates the plan once, uses one aggregate work budget, preflights all pages before rendering, preserves strict exact/+1 behavior by default, and lets the production desktop opt explicitly into bounded raster auto-fit.

No new Critical, High, Medium, or Low quality/correctness finding was identified in the overall Step 8 scan. No product or test file was modified by this review.

## Scope and method

This independent read-only SECOND-FINAL review re-read the regenerated 532,434-byte patch at `.agents/logs/review-diff-csharp-frameweb-client.patch` (base `452528b`, SHA-256 `07EB9EBE3FD782004EB2AA73939856E5361A3F03916F455B22A34551F77FA522`) and inspected the final source/tests directly. The review re-evaluated every earlier finding and scanned the typed contracts, page geometry, pagination, preview/export plan identity, rendering lifecycle, deterministic semantics, CJK installed-font/TTC path, bounded work, atomic save/error causes, active result context, UI-thread captures, preview preservation, localization, Step 4 compatibility, deletion candidates, and legacy isolation.

Lead-supplied full-gate evidence is recorded without presenting it as independently rerun: both Release solution builds are 0 warnings/0 errors; both solution test runs pass 567/567 (Core 272, Printing 73, Rendering 56, LocalRuntime 14, UI 152); ownership is clean; AgentOnly passes; typed vulnerable packages are zero. Coverage percentage was not measured.

## Disposition of the preceding Medium findings

### M-F1 — preview/PDF table text fidelity: **Resolved**

- `PrintPagePlan` owns one immutable `PrintPageRenderContent`; the content defensively materializes text runs, rectangles, and images, and exposes the same read-only `TextRuns` to every consumer (`FramePrintPDF/PDF_Manager.Printing/PrintPageContent.cs:18-41`; `PrintModels.cs:610-641`). Each run retains both complete `Text` and deterministic fitted `DisplayText`, its exact bounds, measured display width, alignment, font size, weight, and truncation status (`PrintModels.cs:744-802`).
- The fitting policy normalizes only the drawn single line, measures Unicode runes, scales down within a bounded minimum, and ellipsizes through `StringInfo.GetTextElementEnumerator`, so it does not split surrogate pairs or grapheme elements (`PrintPageContent.cs:364-493`). The complete input remains in `Text`; only `DisplayText` is abbreviated.
- PDF export and preview iterate the same plan-owned runs. PDF table text is enclosed in `Save`/`IntersectClip`/`Restore`, so even a wider real installed-font glyph cannot enter an adjacent cell (`PdfSharpPrintWriter.cs:319-380`). The raster preview applies the identical planned bounds and an active per-run pixel clip (`PrintPreviewRenderer.cs:144-150,170-221,326-343`).
- The concrete 13-column A4 table is covered at both 25% and 400%: run bounds do not overlap, measured display width stays inside each cell, 400% uses explicit ellipsis, repeated planning is identical, preview/export retain the same run instances, and the PDF program contains a clip for every table run (`Step8WholePreviewBudgetAndTextTests.cs:143-211`).
- User-visible textual fidelity is no longer inferred from the synthetic preview glyph strokes. Desktop preview builds bounded full page text from each run's unabridged `Text` (`DesktopPdfExporter.cs:233-254`), Core makes pages/captures/text defensively immutable and enforces aggregate decoded-byte/text limits (`OperationContracts.cs:222-280,305-398`), and the real dialog presents it in a read-only, multiline, scrollable, selectable `TextBox` synchronized on every navigation (`PrintUiModels.cs:302-326,404-437`). UI acceptance proves the full 13-column content and long section name at 25%/400%, select-all behavior, and simultaneous text/image changes on navigation (`Step8FinalPreviewTextAcceptanceTests.cs:16-137`).

The page bitmap therefore provides exact plan/page geometry, fitted-text occupancy, clipping, diagrams, and navigation identity; the adjacent synchronized text pane provides exact user-readable content. It is not claimed to be a pixel-identical installed-font PDF raster. That distinction no longer loses user-visible text and is not a remaining correctness finding.

### M-F2 — per-page fresh preview budgets: **Resolved**

- `RenderPreviewAsync` requests all page numbers in one session. `RenderPreviewPagesAsync` enters the process-wide serialization boundary once, creates one `PrintWorkBudget`, validates/hashes the supplied job/plan once, reserves content work for every page, reserves the aggregate raster count/decoded bytes/pixels, and only then renders pages (`PdfSharpPrintWriter.cs:123-197`).
- Strict mode remains the default. Production auto-fit is an explicit option; it binary-searches the largest common raster size that fits remaining decoded-byte and layout-work budgets, while image-count exhaustion still fails through the checked counter path (`PrintModels.cs:145-169`; `PrintPreviewRenderer.cs:43-109`). Desktop is the explicit production caller with `fitToDocumentBudget: true` and uses the whole-document API instead of looping the single-page API (`DesktopPdfExporter.cs:212-233`).
- Tests prove one validation event, one shared budget, exact-limit success, images/text/layout +1 failure before the first rendered page, and a 22-page production-shaped auto-fit whose pages remain distinct and share the authoritative plan identity (`Step8WholePreviewBudgetAndTextTests.cs:10-140`).

`RenderPreviewPageAsync` still creates an isolated budget through the shared internal session for its one requested page (`PdfSharpPrintWriter.cs:100-120`). That is the intentional single-page API contract, not the desktop whole-document path, and it no longer permits aggregate counters to reset between pages of one preview operation.

## Original-finding disposition

| Prior finding | SECOND-FINAL disposition | Evidence |
|---|---|---|
| H1 raw viewport used as every preview page | **Resolved, including the prior M-F1 textual residual.** Every page has its own planned raster and identity; navigation changes both bitmap and complete selectable text. | `DesktopPdfExporter.cs:212-254`; `PrintUiModels.cs:302-437`; `Step8FinalPreviewTextAcceptanceTests.cs:83-136` |
| H2 one bitmap mislabeled as model/load/result | **Resolved.** Captures are keyed by semantic kind; UI-thread presentation changes are flushed and transactionally restored, with honest result fallback and original/restore causes retained. | `ViewportCaptureProvider.cs:10-51,83-178`; `DesktopPrintJobFactory.cs:89-123,302-323` |
| H3 only seven input tables | **Resolved.** All 21 input surfaces plus Moving Loads are projected in stable order and included in checked preflight accounting. | `DesktopInputTableProjection.cs:40-202,288-390`; `Step8DesktopProjectionRemediationTests.cs:121-213` |
| M1 25%-400% planner/writer drift | **Resolved, including horizontal table fit.** Shared metrics drive pagination and run geometry; deterministic fitting and clips prevent the 13-column/400% overlap case. | `PrintRenderingShared.cs:7-65`; `PrintPageContent.cs:198-298,364-493`; `Step8LayoutRemediationTests.cs:86-160` |
| M2 all languages' fonts eagerly required | **Resolved.** Only the requested language is loaded, with bounded TTF/TTC reads and preserved causes. | `InstalledWindowsFontResolver.cs:71-176`; `Step8PreviewAndFontAcceptanceTests.cs:61-103` |
| L1 `RepeatHeader=false` ignored | **Resolved.** Planning and both renderers follow the flag. | `PrintPlanning.cs:333-370`; `PrintPageContent.cs:246-271`; `Step8LayoutRemediationTests.cs:63-84` |
| L2 untranslated labels / Chinese bold | **Resolved in source and structural tests.** Presentation labels are language-specific and Chinese bold uses explicit simulation. | `PrintModels.cs:90-142`; `InstalledWindowsFontResolver.cs:97-113`; localized `Strings.*.resx` |

## Overall Step 8 scan

- Typed jobs, rows, tables, captures, page content, plans, preview pages, and results defensively materialize mutable input. Plan identity covers settings, localized labels, all section values, semantic diagram bytes, pagination, and item ranges; preview and export reject a plan/job mismatch (`PrintRenderingShared.cs:96-250`).
- A3/A4 portrait/landscape dimensions, finite margins, 25%-400% scale, pagination, page numbering, repeated/non-repeated headers, text continuation, and aspect-preserving diagrams remain consistent with the plan.
- Static, nonlinear, modal, DEFINE, COMBINE, PICKUP, moving-parent, and moving-child selections still project the current typed table/provenance and semantic result capture; no first-result or hard-coded substitute was introduced.
- PDFsharp creation/save remains inside one process-wide static semaphore across writer instances. Document, page graphics, and images are disposed; cancellation is honored at the semaphore, page/run loops, raster loops, and staged copy boundaries.
- Installed-font discovery remains Windows-font-directory allow-listed, lazy per language, size-bounded before allocation, exact-read, TTC-offset/checksum aware, `/FontFile2` embedded, and `/ToUnicode` mapped. Missing/corrupt font causes survive localization wrapping.
- Desktop whole-preview/export uses the same authoritative plan identity. Export recaptures current semantics and compares the receipt with the retained preview before atomically replacing the target. Invalid, partial, failed, canceled, or revision-stale preview work leaves the last valid preview state intact.
- Atomic save still uses an exclusive sibling temporary file, durable flush, replace/move, and cleanup that cannot replace the primary cause.
- The Step 4 `TypedPdfJob` overload remains present and its compatibility tests pass. Typed desktop/Printing projects remain isolated from `PDF_Manager.LegacyPrinting`, `PdfSharpCore`, restricted embedded fonts, and `FramePrintAzure`.

All eight earlier deletion candidates remain intentional contract extensions/replacements rather than accidental deletions: Core boundary assertions, preview/result contracts, partial writer composition, official PDFsharp project boundary, Step 4 format assertions, shell composition, MainForm preview/export guards, and desktop projection. No dead product path, weakened contract, hard-coded behavioral substitute, or accidental source deletion was found.

## Residual risks and coverage gaps

- Coverage percentage remains unmeasured.
- CUA/browser visual inspection of Japanese/Chinese installed glyph shapes was unavailable. `/ToUnicode`, embedded font, code-point mapping, TTC, and Chinese-bold structural evidence exists, but **CJK GUI/PDF glyph visual is not claimed PASS**.
- The raster page preview intentionally uses deterministic coverage strokes rather than installed glyph outlines. Geometry/clip/display-run fidelity and synchronized full selectable text are covered; pixel-level preview-versus-PDF font-shape equivalence is not.
- Font-specific advance can differ from the deterministic plan estimator. The PDF clip guarantees isolation, but a CJK visual run remains useful to detect overly conservative ellipsis or last-glyph clipping.
- No fault-injection test forces semantic viewport capture and transactional restoration to fail simultaneously, although source retains both causes.
- PDFsharp's synchronous `document.Save` and installed-font reads cannot be interrupted internally. Work limits bound the call, but cancellation is not instantaneous.
- Full raw PDF bytes are not the determinism contract for the PDFsharp path; plan identity, parsed semantics, text mapping, image bytes, and rendered layout remain the deterministic boundaries.

## Validation

Reviewer-run read-only commands:

- `dotnet test FramePrintPDF/PDF_Manager.Tests/PDF_Manager.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~Step8WholePreviewBudgetAndTextTests"` — PASS 4/4.
- `dotnet test FramePrintPDF/PDF_Manager.UiTests/PDF_Manager.UiTests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~Step8FinalPreviewTextAcceptanceTests"` — PASS 2/2.
- `dotnet test FramePrintPDF/PDF_Manager.Tests/PDF_Manager.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~Step8|FullyQualifiedName~TrueTypeCollectionExtractorTests"` — PASS 50/50.
- `dotnet test FramePrintPDF/PDF_Manager.UiTests/PDF_Manager.UiTests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~Step8"` — PASS 10/10.
- `dotnet test FramePrintPDF/PDF_Manager.Tests/PDF_Manager.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~TypedPdfExporterTests|FullyQualifiedName~ProjectBoundaryTests"` — PASS 15/15.
- `dotnet test FramePrintPDF/PDF_Manager.UiTests/PDF_Manager.UiTests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~Step4VerticalIntegrationTests"` — PASS 9/9.
- `dotnet test FramePrintPDF/PDF_Manager.Tests/PDF_Manager.Tests.csproj -c Release --no-build --no-restore` — PASS 73/73.
- `dotnet test FramePrintPDF/PDF_Manager.UiTests/PDF_Manager.UiTests.csproj -c Release --no-build --no-restore` — PASS 152/152.
- Direct source/regenerated-patch inspection with `rg`, `Get-Content`, `git status --short`, `git diff --name-status 452528b -- FramePrintPDF`, `git diff --stat 452528b -- FramePrintPDF`, and `Get-FileHash`.

## Explicit counts

- Critical: 0
- High: 0
- Medium: 0
- Low: 0

## Consultation status

No nested Codex consultation was invoked. This was the explicitly delegated team-execute SECOND-FINAL quality/correctness reviewer pass.
