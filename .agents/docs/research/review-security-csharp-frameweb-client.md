# SECOND-FINAL Security Review: C# FrameWeb Desktop Step 8

## Verdict

**PASS WITH LOW-RISK HARDENING** — Critical: 0, High: 0, Medium: 0,
Low: 1.

The two final Medium remediations introduce no new Critical, High, or Medium
security finding. Authoritative full text is retained separately from bounded
display text, table text is clipped in both PDF and preview rendering, and the
desktop now renders a document through one validation and one aggregate work
budget before allocating page rasters. The one Step 8 Low remains the known
atomic-save parent-directory/reparse race.

The legacy `FramePrintAzure` / local print host is outside the typed Step 8
scope. Its accepted baseline remains High: 2 and Medium: 2, and redistribution
of the completed application remains **NO-GO** until that host is retired or
rebuilt. Those legacy findings are not included in the Step 8 totals.

## Scope and Method

- Re-read the regenerated
  `.agents/logs/review-diff-csharp-frameweb-client.patch` against base
  `452528b`, SHA-256
  `07EB9EBE3FD782004EB2AA73939856E5361A3F03916F455B22A34551F77FA522`.
  The patch contains 43 `FramePrintPDF` product/test/resource paths plus the
  three specialized review reports.
- Inspected final source and tests for authoritative `TextRuns`, full `Text`
  versus `DisplayText`, Unicode ellipsis, clipping, selectable preview text,
  whole-document validation/budgeting, auto-fit, exact/+1 limits, observer
  instrumentation, aggregate allocations, serialization/cancellation, and the
  bounded test rasterizer.
- Rechecked PDF/content injection, formula/shell-like exposure, file/path
  safety, atomic replacement, untrusted image/font/TTC parsing, installed-font
  lookup, integer overflow, PDFsharp global resolver state, exception causes,
  `/ToUnicode`, temporary data, dependency health, and legacy-boundary
  isolation.
- Rechecked the eight hunk-level deletion candidates previously identified by
  the repository verifier. None removes a security control or reintroduces a
  legacy dependency.

## Open Finding

### [Low] Atomic PDF replacement does not anchor parent-directory identity

- **Evidence:** `FramePrintPDF/PDF_Manager/Shell/MainForm.cs:998-1003`
  resolves the destination and sibling temporary paths as strings;
  `MainForm.cs:1011-1012` creates the random temporary file with `CreateNew`,
  asynchronous I/O, and `WriteThrough`; `MainForm.cs:1036-1048` flushes and
  commits with `File.Replace` or `File.Move`; `MainForm.cs:1067-1086` performs
  exception-preserving cleanup.
- **Impact:** ordinary overwrite, cancellation, and failure behavior is atomic
  and preserves an existing destination. A same-user local actor able to swap
  an ancestor directory or reparse point between those separately resolved
  operations could redirect commit/cleanup or cause an availability failure.
  No privilege escalation is demonstrated.
- **Remediation:** require an existing regular parent; define and enforce a
  reparse-point policy for the parent chain and destination; bind the save to a
  verified parent-directory identity and revalidate it immediately before
  commit. Keep same-directory random `CreateNew`, durable flush, and cleanup
  that cannot mask the primary exception. Add junction/symlink, parent-swap,
  destination-type-change, and secondary-cleanup-failure tests.

## Second-Final Remediation Verification

### Authoritative text, Unicode-safe fit, and clipping

- `PrintPagePlan` owns immutable authoritative render content and exposes the
  same read-only `TextRuns` used by preview and PDF
  (`PrintModels.cs:610-641,744-811`; `PrintPageContent.cs:14-40`). Each run
  retains unabridged `Text`, while normalized/fitted `DisplayText`, exact
  bounds, font size, measured display width, alignment, and truncation state
  are separate fields (`PrintModels.cs:744-811`). Untrusted strings continue to
  reach PDFsharp drawing APIs, not raw PDF syntax or a command shell.
- Fitting preserves full text, normalizes line breaks only in display text,
  uses `Rune` enumeration for width estimation, and ellipsizes by .NET text
  element rather than UTF-16 code unit, so it does not split a surrogate pair
  or combining text element (`PrintPageContent.cs:364-458`). Checked model
  bounds, the 4,000,000-character text budget, and the 5,000,000-unit layout
  budget bound the associated work.
- Preview rendering consumes `DisplayText` and enforces an active pixel clip for
  every text run (`PrintPreviewRenderer.cs:170-221,326-343`). PDF table headers
  and cells save graphics state, intersect the exact run rectangle, draw, and
  restore it (`PdfSharpPrintWriter.cs:319-377`). The 13-column 25%/400% test
  checks deterministic full/display runs, bounds, truncation, shared plan/
  preview/export objects, and at least one PDF clip operator per table run
  (`Step8WholePreviewBudgetAndTextTests.cs:143-210`).
- Desktop selectable text joins the authoritative full `run.Text` values only
  after an 8,000,000-character aggregate preflight
  (`DesktopPdfExporter.cs:233-274`). The result contract independently enforces
  the same aggregate text ceiling (`OperationContracts.cs:225-265,371-385`).
  UI acceptance verifies the unabridged 13-column value, every header/cell,
  page-specific text/raster state, read-only multiline selection, and
  accessibility metadata (`Step8FinalPreviewTextAcceptanceTests.cs:14-130`).
  Truncation therefore affects painted display only, not selectable content.

### One validation, one shared preview budget, and allocation order

- `RenderPreviewAsync` routes all planned page numbers through one private
  session under the process-wide semaphore. It constructs one `PrintWorkBudget`,
  calls `ValidateExistingPlan` once, reserves content for every requested page,
  selects one size, and reserves every raster's decoded bytes and pixels before
  the render loop (`PdfSharpPrintWriter.cs:123-197`).
- Auto-fit divides remaining aggregate decoded-byte and layout-work budgets by
  the page count, validates the one-pixel minimum, then binary-searches the
  largest common dimension. The selected raster budget is charged for all
  pages before any `new byte[]`; the renderer adopts its owned allocation rather
  than copying it (`PrintPreviewRenderer.cs:14-108,111-150`;
  `PrintModels.cs:690-740`). All size and cumulative arithmetic is checked.
- The production-shaped 22-page test verifies auto-fit, distinct captures,
  aggregate counters, one session, one plan validation, and ordered rendering.
  Exact aggregate limits pass, while image/text/layout exact+1 cases fail before
  any page observer event (`Step8WholePreviewBudgetAndTextTests.cs:10-140,
  213-233`). The internal observer is instrumentation only; its failure callback
  is prevented from replacing the original engine exception
  (`PdfSharpPrintWriter.cs:199-213`).
- The desktop bridge converts the already bounded renderer result into
  immutable Core captures and the Core result rechecks a 64 MiB aggregate
  (`DesktopPdfExporter.cs:212-254`; `OperationContracts.cs:305-389`). The
  defensive copies at `DesktopPdfExporter.cs:247` and
  `OperationContracts.cs:330` temporarily retain the engine aggregate, the
  converted aggregate, and at most one additional page buffer. This increases
  peak memory but remains deterministically bounded by the 64 MiB aggregate and
  per-page dimension/byte limits; it is not an unbounded pre-allocation bypass.
  A future owned-buffer handoff could reduce the peak.

### Fonts, TTC parsing, Unicode PDF structure, and global state

- Installed-font resolution accepts only constant face names, canonicalizes
  beneath the Windows Fonts directory, and lazily loads only requested faces
  through execution-and-publication `Lazy` instances
  (`InstalledWindowsFontResolver.cs:34-68,80-134`).
- The earlier pre-allocation issue remains fixed: the resolver checks a
  readable/seekable stream's length against 64 MiB and `int.MaxValue` before
  allocation, reads exactly that length, and rejects growth. The same bound
  covers standalone TTF and TTC input
  (`InstalledWindowsFontResolver.cs:136-176`). This meets the plan's
  pre-allocation resource-limit requirement.
- TTC extraction bounds face count to 64, table count to 256, every offset and
  range, checked aligned output to 64 MiB, and the required `head` table before
  output allocation (`TrueTypeCollectionExtractor.cs:5-87,109-139`). Its tests
  exercise valid one/two-face collections and malformed signature/count/index/
  offset/length/directory/`head` cases (`TrueTypeCollectionExtractorTests.cs`).
- PDFsharp's process-global resolver is installed through one static
  execution-and-publication `Lazy`; a static cancellable semaphore serializes
  PDF export and preview across writer instances
  (`PdfSharpPrintWriter.cs:8-15,66-97,148-228,231-240`). Embedded font and
  `/ToUnicode` tests cover English/Japanese/Simplified Chinese structure and
  mapped non-ASCII code points. **CUA/browser visual inspection of CJK glyph
  shapes was unavailable and is not claimed as PASS.**

### Cancellation, exceptions, file data, and boundaries

- Cancellation is honored before and during planning, while waiting for the
  shared semaphore, per page/run/image/row, and before/after synchronous
  PDFsharp work. The semaphore is released in `finally`; the failure observer
  cannot replace the original exception (`PdfSharpPrintWriter.cs:36-97,
  141-228`). Font, destination, and expected print failures retain their source
  exceptions through typed wrappers.
- Encoded PDF bytes are staged in a bounded 128 MiB stream before the atomic
  file path. Temporary files use unpredictable GUID names, exclusive `CreateNew`
  in the destination directory, durable flush, and best-effort cleanup without
  document/font/credential logging. The Low directory-identity race above is
  the remaining path issue.
- Formula-leading text has no execution semantics in PDF or read-only preview;
  there is no shell invocation. Product diagrams are validated raw RGB24 and
  encoded by owned code, so no caller-provided compressed image decoder is
  introduced. Installed font binaries are allow-listed system files and remain
  size-bounded.
- `PDF_Manager.Printing` has no project references. The desktop references only
  Core, Rendering, typed Printing, and LocalRuntime. Both typed dependency
  graphs report zero known vulnerable packages and no active
  `PDF_Manager.LegacyPrinting` or `FramePrintAzure` reference.

### Bounded test rasterizer

- The new rasterizer is test-only. It limits page and image dimensions to 2048,
  limits content lines/operators to 100,000 before its page-pixel allocation,
  uses checked pixel/image lengths, accepts only an explicit PDF operator subset,
  requires balanced state, and restricts clip paths to four-point axis-aligned
  rectangles (`PdfSharpSubsetPageRasterizer.cs:10-59,61-315,318-360,523-648`).
- PDFsharp and the existing `PdfRawInspection` decode content before all of
  those test-side checks. Therefore the helper is suitable for PDFs generated
  by the bounded writer, not as a hostile arbitrary-PDF security boundary. No
  product path calls it.

## Deletion-Candidate Disposition

The eight verifier candidates remain intentional and do not regress security:

1. `CoreOperationBoundaryTests.cs` replaces a single reflection assertion with
   typed preview/export assertions while retaining dependency prohibitions.
2. `OperationContracts.cs` extends the bounded typed preview/export contract.
3. `TypedPdfExporter.cs` shares the dimension limit and retains the Step 4
   compatibility overload.
4. `ProjectBoundaryTests.cs` rejects `PdfSharpCore`, pins official PDFsharp, and
   retains legacy/font isolation checks.
5. `Step4VerticalIntegrationTests.cs` replaces raw PDF 1.4/plaintext assumptions
   with parser/content/image/metadata/`ToUnicode` assertions.
6. `MainFormServices.cs` composes typed print services without weakening
   lifecycle/cancellation boundaries.
7. `MainForm.cs` enables input-only printing and adds page setup/preview while
   retaining revision, plan-identity, and atomic-export guards.
8. `DesktopPdfExporter.cs` replaces the truncated Step 4 projection with the
   bounded typed factory; live semantic capture remains the desktop default.

## Residual Risks and Coverage Gaps

- The Low parent-directory/reparse race remains unremediated.
- Preview conversion uses bounded defensive RGB copies and can temporarily
  exceed the logical 64 MiB retained-result cap, although the peak remains
  fixed by the same aggregate and per-page limits. An owned-buffer boundary
  would reduce memory pressure.
- Font loading and PDFsharp's synchronous `Save` are bounded but cannot be
  cooperatively cancelled inside their library calls; cancellation latency is
  finite by size/work limits rather than immediate.
- There is no direct Unicode grapheme/emoji ellipsis regression case even
  though the implementation uses `StringInfo` text elements. Add surrogate,
  combining-mark, and emoji-ZWJ cases.
- The bounded rasterizer is not a hostile-PDF parser and must remain test-only.
- CUA/browser visual inspection of Japanese/Chinese glyph shape remains
  unavailable; structural embedding and `/ToUnicode` checks do not replace it.
- Reparse/junction race tests and a coverage percentage remain absent.

## Validation

- Independently rerun focused Step 8/font/boundary tests: PASS, 76/76 selected
  tests — Core 7, Printing 57, Rendering 2, UI 10; LocalRuntime had no matching
  test.
- Independently rerun typed Printing and desktop vulnerable-package scans:
  exit 0, zero vulnerable packages reported.
- Independently ran `git diff --check -- FramePrintPDF`: exit 0; only existing
  line-ending conversion warnings were emitted by Git.
- Lead-provided final evidence: both Release solution builds have 0 warnings /
  0 errors; both full solution runs pass 567/567; ownership is clean; AgentOnly
  passes. These supplied full-gate results are not represented as independent
  reviewer reruns.

## Explicit Counts

- Critical findings: 0
- High findings: 0
- Medium findings: 0
- Low findings: 1
- New Critical/High/Medium findings introduced by final remediation: 0
- Vulnerable packages in typed Printing/desktop graphs: 0
- Raw PDF/content-injection paths: 0
- Caller-controlled compressed image/font decode paths: 0
- Active desktop references to LegacyPrinting/FramePrintAzure: 0
- Hardcoded secrets or credentials: 0
- New shell/command/network/authentication surfaces: 0
- Inherited legacy-host findings outside Step 8 totals: High 2 / Medium 2
