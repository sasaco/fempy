# Security Review: C# FrameWeb Desktop Step 7

## Verdict

**PASS WITH LOW-RISK HARDENING** — Critical: 0, High: 0, Medium: 0, Low: 1.

The remediated Step 7 closes both prior Medium findings. CSV cells now carry an
explicit trusted-constant / numeric / untrusted-text classification, so
user/backend-controlled identifiers are neutralized without converting negative
result values into text. A shared checked `ResultPresentationBudget` now
aggregates pages, derived results, moving loads, operands, retained output
entities, and scalar work while the complete immutable presentation candidate
is built before result/document UI state is changed.

One Low path-integrity hardening item remains: the atomic export uses repeated
path-string resolution and does not anchor the parent-directory identity
against reparse-point or directory-swap races. The inherited legacy-host
findings are unchanged and excluded from these Step 7 totals: legacy High: 2
and Medium: 2 remain open, and complete-application redistribution remains
**NO-GO**.

## Scope and Method

- Re-reviewed the original 174,927-byte, 17-path patch at
  `.agents/logs/review-diff-csharp-frameweb-client.patch` against base
  `2b3b494`, then directly inspected the current remediation delta and current
  working-tree versions of all Step 7 product and test files.
- Reviewed `ResultCsvExporter`, `ResultPresentationLimits`,
  `ResultPresentationModels`, `ResultPresentationService`,
  `PickupEngineeringEnvelopeBuilder`, `ProjectDocumentContent`,
  `AtomicResultExportWriter`, `ResultExportArtifact`, result viewport
  projection/navigation/extrema code, localized resources, all Step 7 tests,
  and the normalized Step 6 fixture changes.
- Traced project/backend-controlled identifiers from the nonblank-only
  validators through derived and moving-load provenance into CSV, filename
  suggestion, UI errors, save selection, temporary creation, durable flush,
  replacement, and cleanup.
- Checked formula neutralization versus true numeric negatives, RFC 4180
  quoting, CR/LF and UTF-8 behavior, aggregate work/allocation bounds, checked
  arithmetic, validation-before-mutation, synchronous cancellation exposure,
  reparse/symlink races, overwrite behavior, cleanup provenance, secrets,
  authentication, network/process additions, dependencies, and legacy result
  adapters.
- The supplied guardrails (`placeholders=0`, `weakened_tests=0`,
  `out_of_scope=0`) were supporting evidence only, not a security verdict.

## Resolved Findings

### Resolved [Medium] Spreadsheet formula injection in identifier/provenance cells

- **Location**:
  `FramePrintPDF/PDF_Manager.Core/Results/ResultCsvExporter.cs:222-242,
  468-473,541-546,578-583,740-870`; reachability remains at
  `FramePrintPDF/PDF_Manager.Core/Analysis/AnalysisResultSetValidator.cs:508-510`
  and `FramePrintPDF/PDF_Manager.Core/Documents/ProjectDocument.cs:900-901`.
- **Closure evidence**: the identifiers remain reachable and untrusted: both
  validators still require only nonblank strings, so leading `=`, `+`, `-`,
  `@`, TAB, CR/LF, or whitespace followed by a formula marker are accepted.
  The exporter now represents cells as `CsvField.Text`, `Constant`, or
  `Number`. Every externally influenced identifier/provenance column uses
  `Text`; `WriteField` prefixes a leading apostrophe when a dangerous marker
  is present, including after leading whitespace, and then applies RFC 4180
  escaping. Schema literals and enum-derived labels use `Constant`.
- **Numeric distinction**: finite result values use `CsvField.Number` and
  invariant `"R"` formatting. A value such as `-2.5` therefore remains the
  numeric CSV cell `-2.5`, while an identifier such as `-case` becomes
  `'-case`. The mitigation does not depend on making hostile IDs unreachable.
- **Regression evidence**:
  `Step7ResultExportAcceptanceTests.cs:11-21,76-100,306-332` covers all
  formula prefixes, leading whitespace, quoted CR/LF fields, every PICKUP
  provenance text column, exact UTF-8 bytes, and negative numeric preservation.

### Resolved [Medium] Unbounded aggregate presentation amplification before publication

- **Location**:
  `FramePrintPDF/PDF_Manager.Core/Results/ResultPresentationLimits.cs:15-304`;
  `FramePrintPDF/PDF_Manager.Core/Results/ResultPresentationService.cs:8-90,
  151-296,299-435,796-844`; and
  `FramePrintPDF/PDF_Manager/Shell/Contents/ProjectDocumentContent.cs:324-368,
  696-799`.
- **Closure evidence**: one `ResultPresentationBudget` is reused across page
  grouping, every derived result (including PICKUP expansion), and every
  moving-load envelope. It validates the result set once and uses checked
  cumulative counters for pages, derived results, moving-load definitions,
  operands, output entities (including member segments and shell/solid
  locations), and scalar work. Default and immutable hard maxima prevent callers
  from disabling the bounds. Each service consumes its projected cost before
  materializing that output.
- **Mutation ordering**: `SetDocument` and `SetResult` build the complete
  candidate with the shared budget before assigning `_document`, `_resultSet`,
  navigator state, derived results, pages, or envelope maps. Limit failures are
  typed/localized and preserve the prior presentation state. Work remains
  synchronous, but it is now explicitly bounded; lack of cooperative
  cancellation no longer supports the prior unbounded-amplification Medium.
- **Regression evidence**:
  `Step7ResultPresentationBudgetTests.cs:9-119` proves an exact composite
  pages/derived/PICKUP/moving budget and exact +1 rejection for all six limit
  dimensions with stable typed details.

## Open Finding

### [Low] Atomic export is not anchored against reparse-point or parent-directory identity races

- **Location**:
  `FramePrintPDF/PDF_Manager/Shell/Viewport/AtomicResultExportWriter.cs:12-55`;
  caller at
  `FramePrintPDF/PDF_Manager/Shell/Contents/ProjectDocumentContent.cs:1525-1569`.
- **Impact and evidence**: the normal overwrite and failure paths are materially
  safer: export bytes have trusted internal provenance and are bounded before
  filesystem mutation; the writer uses an unpredictable same-directory name,
  `CreateNew`, exclusive access, `WriteThrough`, `Flush(true)`, and a final
  overwrite move. If commit fails, the existing destination remains intact and
  the temp is removed. Cleanup failure no longer masks the primary exception;
  it is attached to the primary exception's `Data`.

  Residually, `GetFullPath` is lexical and create/write/move/cleanup resolve
  path strings independently. No parent handle or directory identity is held,
  and reparse points in the destination chain are not rejected. A concurrent
  junction/directory swap in an attacker-writable destination can therefore
  redirect the operation or make cleanup act in a different directory.
  Synchronous durable I/O can also stall the UI on a hostile or unavailable
  remote filesystem, although the bounded payload limits memory/byte work.
  Under the desktop user's authority this remains a local path-integrity and
  availability hardening issue, not a demonstrated privilege escalation.
- **Recommended fix**: define reparse behavior explicitly. Prefer requiring an
  existing regular parent, reject reparse points in the parent chain and
  destination, and bind create/replace/cleanup to a verified parent directory
  handle with identity revalidation before commit. Keep overwrite consent in
  the save dialog. For remote paths, perform durable I/O off the UI thread with
  cancellation where the platform permits it. Add junction/symlink,
  parent-swap, cleanup-secondary-failure, and remote-stall tests.

## Positive Controls Verified

- CSV generation is deterministic UTF-8 without BOM, invariant, CRLF
  terminated, and RFC 4180 escaped. Checked row/work preflight occurs before
  buffer construction; byte capacity is checked before growth; caller-supplied
  limits have hard ceilings.
- Suggested filenames sanitize path/control punctuation and cap the identifier
  portion at 64 characters. Save-dialog overwrite consent remains enabled.
- Existing-target preservation and same-directory temp cleanup are exercised by
  the production UI export test. The exported artifact has a private
  constructor and internal factories, preventing arbitrary external byte
  provenance at the writer boundary.
- Derived arithmetic rejects non-finite factors and non-finite output. Cost and
  row calculations use checked `long` arithmetic. User-visible errors use
  localized safe messages; raw exception text is not shown.
- Shell/solid results remain in the validated immutable canonical schema. No
  active `disg` / `reac` / `fsec` compatibility adapter was introduced.
- No Step 7 secret, credential, authentication/authorization, network,
  process-execution, SQL, HTML, shell-command, logging, permission, package, or
  dependency regression was found.

## Validation

- Independent focused Core:
  `dotnet test FramePrintPDF/PDF_Manager.Core.Tests/PDF_Manager.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~Step7" --verbosity minimal`:
  PASS, 29/29, 0 skipped.
- Independent focused UI:
  `dotnet test FramePrintPDF/PDF_Manager.UiTests/PDF_Manager.UiTests.csproj --no-restore --filter "FullyQualifiedName~Step7" --verbosity minimal`:
  PASS, 9/9, 0 skipped.
- Stable final suite evidence from the Step 7 test owner: Core 272/272, UI
  142/142, and `FramePrintPDF.sln` 501/501, all exit 0.
- `dotnet list FramePrintPDF/PDF_Manager/PDF_Manager.csproj package --vulnerable --include-transitive --no-restore`:
  exit 0; no vulnerable package reported for the standalone desktop graph.
- `git -c core.quotePath=false diff --check 2b3b494 -- FramePrintPDF`: PASS
  (only the existing line-ending notice for `ProjectDocumentContent.cs`).
- Coverage percentage was not measured. Full Python/Angular gates were not
  rerun for this read-only Step 7 closeout.

## Explicit Counts

- Critical findings: 0
- High findings: 0
- Medium findings: 0
- Low findings: 1
- Closed prior Medium findings: 2
- Hardcoded secrets or credentials: 0
- Authentication or authorization regressions: 0
- New network or process execution paths: 0
- New SQL, HTML, shell, or command-injection paths: 0
- Sensitive token/request/document logging additions: 0
- Raw internal error-detail disclosures in the user-visible Step 7 UI: 0
- New or upgraded dependencies: 0
- Vulnerable packages reported in the standalone `PDF_Manager` graph: 0
- Active legacy result adapters: 0
- Diagnostic placeholders: 0
- Weakened or skipped changed tests: 0
- Out-of-scope changed product files: 0

## Prior and Inherited Constraints

- Historical Step 6 Low hardening items are not counted as Step 7 findings.
- Step 7 does not modify the legacy anonymous analysis/print hosts, their
  dependency graph, restricted fonts, or endpoint policy. The accepted baseline
  remains legacy High: 2, Medium: 2, and complete-application redistribution
  **NO-GO**. These inherited items are not included in the Step 7 counts.
