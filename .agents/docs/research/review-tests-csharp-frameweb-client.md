# SECOND-FINAL Test Coverage Review: C# FrameWeb Desktop Step 8

## Verdict

**PASS WITH LOW FOLLOW-UPS** — Critical: 0, High: 0, Medium: 0, Low: 4.

The last two Medium findings are closed by executable evidence through the
production writer/exporter and the real WinForms preview dialog. Whole-document
preview now validates once, spends one aggregate work budget, and preflights exact
and +1 boundaries before rendering. Text layout is owned by the immutable plan and
is consumed by both PDF and preview renderers; the real dialog exposes the complete
unabridged page text independently of fitted/ellipsized display text.

**Coverage percentage was not measured.** The 567/567 totals are test counts, not
a coverage percentage. CJK glyph appearance in a real GUI/PDF viewer remains
unavailable and is explicitly not reported as passing evidence.

## Scope and Method

- Re-read the regenerated final patch from base `452528b` and the final production
  and test sources.
- Re-reviewed all earlier findings plus the two last Mediums: whole-document shared
  budgeting and horizontal text fidelity.
- Inspected single validation, exact/+1 preflight, 22-page auto-fit, 13-column
  25%/400% layout, authoritative `TextRuns`, full selectable preview text, bounded
  golden rasterizer clipping, and golden approval rationale.
- Checked for deleted/weakened assertions, skipped tests, helper-only confidence,
  platform-sensitive evidence, and untested fail-closed branches.
- Independently ran the focused new Printing/golden and UI acceptance filters. Full
  solution totals below are lead-provided evidence.

## Last Two Medium Findings

### Closed: whole-document preview uses one validation and one aggregate budget

Production `RenderPreviewAsync` builds one ordered page set and enters
`RenderPreviewPagesAsync` once (`PdfSharpPrintWriter.cs:123-159`). That method owns
one `PrintWorkBudget`, validates the complete job/plan once, reserves every page's
text/render work, preflights all raster images and pixels, and only then renders
the pages (`PdfSharpPrintWriter.cs:161-196`). The desktop exporter uses this
whole-document API with explicit document-budget auto-fit
(`DesktopPdfExporter.cs:212-223`) rather than looping the single-page API.

`Step8WholePreviewBudgetAndTextTests.cs:70-140` proves:

- session start and plan validation each occur exactly once;
- the baseline snapshot aggregates all pages, text, images, and layout work;
- an exact limit profile succeeds with the identical snapshot;
- image, text-character, and layout-work +1 cases fail before any page is rendered;
- the original typed `PrintLimitExceededException` resource and failed snapshot are
  retained.

`Step8WholePreviewBudgetAndTextTests.cs:11-67` then exercises the production writer
with 22 page-specific result diagrams. Auto-fit selects dimensions below the
maximum while staying within the shared limits, returns all 22 ordered identities,
and produces 22 distinct capture hashes. The test uses deliberately distinct RGB
payloads, so the page-identity assertion is not dependent on synthetic text strokes.

This closes the prior integrated-budget Low residual as well as the final
per-page-budget Medium.

### Closed: fitted/clipped table text is authoritative and the dialog retains full text

The planner creates each page's render content once and stores it in the
`PrintPagePlan` (`PrintPlanning.cs:489-507`). `PrintPageContentExtractor` generates
the title, headings, table cells, result context, diagrams, and footer from the
same planned geometry (`PrintPageContent.cs:44-139,142-333`).
`PrintTextLayoutPolicy` normalizes single-line display text, deterministically fits
font size, falls back to grapheme-safe ellipsis, and records full `Text`, fitted
`DisplayText`, bounds, measured width, alignment, bold, and truncation state
(`PrintPageContent.cs:364-458`; `PrintModels.cs:744-802`).

Both renderers consume the plan-owned objects: the bounded RGB preview uses
`page.TextRuns` and a per-run pixel clip (`PrintPreviewRenderer.cs:144-220,326-343`),
while PDF export uses the same `pagePlan.TextRuns` and an `IntersectClip` around
each table run (`PdfSharpPrintWriter.cs:319-380`).

`Step8WholePreviewBudgetAndTextTests.cs:146-210` covers a 13-column table at 25%
and 400%. It proves 26 header/cell runs, positive/non-overlapping bounds, fitted
width within every cell, no embedded line breaks, no truncation at 25%, explicit
ellipsis/truncation at 400%, deterministic repeated planning, the same authoritative
`TextRuns` in preview/export, and a PDF clip operator for every table run.

The production UI acceptance at
`Step8FinalPreviewTextAcceptanceTests.cs:19-137` goes further: it uses the real
13-column input projection, production exporter, and real `PrintPreviewDialog` at
both scales. It asserts every complete header/cell value is retained in exact CRLF
page text, a deliberately long section name remains unabridged, the read-only
multiline text box is selectable, and both text and bitmap change in sync during
real page navigation. Thus fitted raster/PDF text cannot cross cell boundaries,
while users can still inspect and copy the complete source text.

This closes the last text-layout Medium without claiming that the synthetic preview
strokes are real font glyphs.

## Rasterizer and Golden Assessment

- The bounded rasterizer limits page dimensions and operator count, rejects
  unsupported or unbalanced state, parses PDFsharp's rectangular `W`/`W*` clip
  paths, carries clip state across `q`/`Q`, and clips text coverage
  (`PdfSharpSubsetPageRasterizer.cs:17-44,49-191,255-307,439-457`).
- The A4 golden executes the real writer's clip operators through that independent
  parser and exact checked-in raster (`Step8RenderedPdfGoldenTests.cs:9-29`).
  Missing/mismatched fixtures emit candidates and fail rather than auto-approving
  (`Step8RenderedPdfGoldenTests.cs:74-102`). The A3 test separately proves corrupt
  image streams fail closed (`Step8RenderedPdfGoldenTests.cs:32-65`).
- `Goldens/README.md:54-61` records the second-final owner approval: only the A4
  fixture changed; A3 stayed byte-identical. The A4 delta is 88/500,990 pixels
  (0.017565%), absolute-difference sum 19,624, maximum delta 223, bounded to
  x=30..389/y=99..787. Original-resolution inspection retained margins, the full
  grid, repeated header, every row, title, and centered footer.

The valid clip path is executable evidence. Malformed clip-path branches are
fail-closed in source but lack a dedicated negative mutation test; that narrow
test-harness gap remains Low below.

## Remaining Findings

### [Low] Independent CJK glyph visual evidence is still unavailable

**Evidence:** installed-font, `/FontFile2`, `/ToUnicode`, Unicode mapping, lazy
language lookup, and malformed TTC tests are green. The repository rasterizers
intentionally paint deterministic coverage strokes instead of installed glyph
outlines. CUA/browser and Chrome-headless attempts were blank or unavailable.

**Impact:** a viewer-specific Japanese or Simplified Chinese missing-glyph/shape
problem could remain despite correct structural embedding.

**Remediation:** add a repeatable independent PDF renderer and text extractor on
the approved Windows font matrix, with Japanese/Chinese image evidence and exact
extracted text. Do not replace the current fast structural gates.

### [Low] Multi-page `PrintTextSection` still lacks a direct no-loss boundary test

**Evidence:** planning and authoritative `TextRuns` implement ordered
`TextLineStart`/`TextLineCount` chunks, and the new selectable dialog proves exact
unabridged text for the production 13-column table. No test supplies a text section
long enough to span multiple pages and reconstructs every line across all page
ranges.

**Impact:** a future line-wrap/page-transition regression could duplicate or omit
body text while the table-focused final tests remain green.

**Remediation:** add exact one-page, +1-line, and multi-page text cases, including a
long unbroken token and CJK text; assert contiguous ranges and reconstructed full
text in plan, PDF extraction, and selectable page content.

### [Low] Malformed clip-path fail-closed branches are source-backed but not mutation-tested

**Evidence:** `PdfSharpSubsetPageRasterizer.cs:588-648` rejects too many/few
vertices, incomplete/non-rectangular/empty paths, and non-axis-aligned edges. The
A4 golden proves valid generated clips, but no test corrupts `m/l/h/W/n` ordering
or geometry and asserts the corresponding `InvalidDataException`.

**Impact:** a regression in the test harness's strict rejection behavior could
weaken golden independence without affecting ordinary valid-golden runs.

**Remediation:** add bounded synthetic or generated-PDF mutations for missing `h`,
missing/duplicate `W`, missing `n`, diagonal edges, extra vertices, and empty
rectangles; assert fail-closed exceptions and no candidate approval.

### [Low] A test name still overstates byte determinism

**Evidence:** `Step8PdfExportAcceptanceTests.cs:92-110` remains named
`RepeatedAndParallelExports_AreByteDeterministicAndUseDeclaredSerializationPolicy`,
but compares `PdfSemanticSnapshot` values instead of raw output bytes. Separate
tests correctly prove cross-instance serialization and queued cancellation.

**Impact:** maintainers may cite it as byte-for-byte reproducibility evidence when
the supported invariant is semantic determinism.

**Remediation:** rename it to say semantic determinism, or add raw-byte equality
only if byte determinism becomes an explicit product contract.

## Prior Finding Disposition

| Finding | SECOND-FINAL disposition |
|---|---|
| Raw viewport shown for every preview page | Closed by real page-specific dialog navigation and plan identity tests. |
| Production result-selection/input matrix absent | Closed for static/nonlinear/modal/derived/moving plus all 21 input surfaces and Moving Loads. |
| Exact Shell RGB/member I/J assertions weakened | Closed by exact embedded RGB/hash and ToUnicode-aware result assertions. |
| Eager fonts and malformed TTC gaps | Closed by injectable lazy font and malformed collection matrices. |
| Per-page preview validation/budget reset | Closed by one whole-document session, exact/+1 preflight, and 22-page auto-fit. |
| Preview/PDF horizontal text fidelity | Closed by authoritative fitted/clipped `TextRuns` and exact selectable page text. |
| Integrated shared-budget residual | Closed by full preview exact/+1 execution before page rendering. |
| CJK independent visual proof | Open Low; unavailable, not represented as PASS. |
| Multi-page body-text no-loss proof | Open Low. |
| Semantic test labeled byte-deterministic | Open Low. |

## Assertion-Strength Assessment

- No new skipped/ignored tests were found.
- The two new engine suites use the actual production writer, plan, PDF stream, and
  observer. The observer makes validation/render counts observable but does not
  replace the production work.
- The UI suite uses the real factory, exporter, dialog, navigation buttons,
  bitmap, and selectable text box rather than a fake dialog/exporter.
- Earlier Step 4 broad assertions remain replaced by exact RGB/hash and mapped
  member/result checks. Removed legacy interface/package assertions correspond to
  intentional typed API and official PDFsharp changes, not weakened behavior.
- Golden comparison remains independent and fail-closed on absence or mismatch;
  refresh rationale is specific and reviewable.

## Prioritized Missing-Test List

1. Independent Japanese and Simplified Chinese glyph rendering and extraction.
2. Multi-page body-text exact no-loss/no-duplication reconstruction.
3. Negative malformed clip-path mutations for the bounded golden rasterizer.
4. Rename the semantic-determinism test or explicitly establish a byte contract.

## Test Execution Results

- Independently run whole-preview/text/golden filter: PASS, 6/6, failed 0,
  skipped 0.
- Independently run real final preview-text UI filter: PASS, 2/2, failed 0,
  skipped 0.
- Lead-provided both-solution evidence: PASS, 567/567 — Core 272, Printing 73,
  Rendering 56, LocalRuntime 14, UI 152.
- Lead-provided ownership: overlap 0 / unowned 0 / idle 0. AgentOnly: pass.
- Coverage percentage: **not measured**.

## Severity Summary

| Severity | Count | Disposition |
|---|---:|---|
| Critical | 0 | None. |
| High | 0 | None. |
| Medium | 0 | Both final Mediums and all earlier Mediums are closed. |
| Low | 4 | CJK visual, multi-page text, clip mutation, and evidence naming. |
