# Test Coverage and Acceptance Re-review: C# FrameWeb Desktop Step 7

## Verdict

**PASS WITH LOW FOLLOW-UPS** — Critical: 0, High: 0, Medium: 0, Low: 2.

All prior High and Medium test-review findings are remediated. The final suite
now exercises the Angular-derived moving-load semantics, the real Ct 11-case
boundaries, formula-safe and fully ordered exports, production-derived PICKUP
formats, typed scenes, complete invalid-state controls, shared presentation
budgets, and atomic persistence. No product or test files were changed by this
re-review.

**Coverage: not measured.** The current evidence remains `coverage=null`; no
percentage is estimated from test counts.

## Scope and Method

- Re-read the final Step 7 Core/UI acceptance tests and the production result
  presentation, PICKUP engineering envelope, CSV, Shell candidate, projection,
  budget, and atomic-writer paths they exercise.
- Compared the C# moving fixture with the Angular fixture and regressions.
- Parsed the Ct Angular asset and the new C# fixture independently: all 11
  `(case_id, symbol, name)` tuples match in order.
- Rechecked the normalized Step 6 integration fixtures and active-project
  boundary for weakened assertions, skips, or a legacy result adapter.

## Resolved Findings and Acceptance Evidence

### Angular moving-load parent/later-child semantics — resolved

- `AngularMovingFixture_GroupsLaterChildrenWithoutMovingTheOrdinarySecondPage`
  at `Step7ResultPresentationAcceptanceTests.cs:91` uses result order
  `1, 2, 1.1, 1.2`, asserts pages `[MOVING, 2]`, source order
  `[1, 1.1, 1.2]`, original case indices `[0, 2, 3]`, and both later children.
- `AngularMovingFixture_ReactionSignedEnvelopeUsesAllSourcesButAbsoluteUsesChildrenOnly`
  at line 281 asserts all six reaction components, child-only absolute values
  and provenance, stable first-child ties, immutable scene projection, and the
  parent-only fallback.
- `MovingParentSceneUsesChildOnlyAggregateWhileChildSelectionUsesThatChild` at
  `Step7ReviewRemediationTests.cs:97` proves the parent scene uses the child
  aggregate while selecting `1.2` displays that child's own force and moment.

### Ct first/last and all 11 ordered cases — resolved

- `ct-analysis-result-set-v1.fixture` is a strict `AnalysisResultSet v1` fixture
  with 11 results. Independent parsing confirms its 11 case IDs, symbols, and
  Japanese names exactly match `サンプル（Ct桁）.json` in asset order.
- `CtV1Fixture_PreservesAllElevenAssetCasesAndExactBoundaryPages` at
  `Step7ResultPresentationAcceptanceTests.cs:110` pins the complete ordered
  tuple list and first `1/D1/固定死荷重` through last `11/W/風荷重` pages.
- `CtFixtureNavigatesExactFirstAndLastCasesAcrossSelectorsTablesAndScene` at
  `Step7ReviewRemediationTests.cs:15` proves 11 selector items, first/last
  navigation, exact table values, result coordinates, and typed scenes.

### Formula-safe deterministic export — resolved

- `BaseStaticCsv_NeutralizesSpreadsheetTextButKeepsNegativeNumbersNumeric` at
  `Step7ResultExportAcceptanceTests.cs:78` covers `=`, `+`, textual `-`, `@`,
  TAB, CR, LF, and leading whitespace while retaining negative doubles as
  numeric cells.
- `PickupCsv_NeutralizesEveryUntrustedTextColumnBeforeRfc4180Escaping` at line
  307 covers PICKUP/member/source/station text fields. The exporter classifies
  text, constants, and numbers separately at `ResultCsvExporter.cs:740-747`
  and neutralizes only untrusted text at lines 791-870.
- `MixedBaseAndMovingCsv_AreExactOrderedGoldensWithNonLexicalIdsAndBoundaryLimits`
  at line 264 pins complete base rows across displacement, reaction, member I/J,
  shell, and solid data, plus moving displacement/reaction/member/global
  extrema provenance. Both golden shapes exercise exact and +1 row/byte/work
  limits without sorting non-lexical IDs.

### Real PICKUP 3D/2D semantics and Shell export — resolved

- `PickupEngineeringExports_AreBuiltFromServiceWithExactProvenanceVectorsAndStableTies`
  at `Step7ResultExportAcceptanceTests.cs:103` obtains PICKUP through
  `BuildDerivedResults`, then pins component-focused winners, correlated full
  force vectors, source provenance, station/end/distance, stable ties, exact
  21-column 3D CSV bytes, exact M/S/N fixed-width 2D bytes, and limits.
- `ProductionPickupSelectionExportsAndAtomicallySavesExactDimensionSpecificBytes`
  at `Step7ResultExportUiTests.cs:15` selects the actual derived PICKUP in
  `ProjectDocumentContent`, verifies command enablement, exports exact 3D CSV,
  switches the document to 2D, exports exact `.pik`, and saves both artifacts.

### Typed nonlinear/modal/static scenes — resolved

`ValidNonlinearModalAndStaticVariantsProjectTypedSceneGeometryAndValues` at
`Step7ReviewRemediationTests.cs:50` walks both accepted nonlinear steps and both
modal modes and asserts scene case/state/index plus displacement vectors. It
also asserts static reaction context/value and member-force I/J positions and
vectors. This valid-fixture coverage replaces the cross-variant scene intent
lost when the invalid Step 6 combined fixture was normalized.

### Invalid/partial result atomicity and controls — resolved

`InvalidPartialResultCannotEnableEmptyPresentationOrReplacePriorValidDisplay`
at `Step7ResultPresentationTests.cs:102` checks every empty selector, table,
extrema, export ability/button, navigation method, and grid. It then establishes
a rich moving-child/PICKUP/table/scene/export state, rejects a partial set, and
proves the result, coordinate, derived selection, parent/child objects, moving
envelope, scene, rows, selector states, and export bytes remain unchanged.

### Shared presentation budgets — resolved

- `SharedBudget_AcceptsExactCompositePagesDerivedPickupAndMovingCosts` at
  `Step7ResultPresentationBudgetTests.cs:9` pins the exact aggregate use of one
  budget across pages, DEFINE/PICKUP, operands, output entities, scalar work,
  and a moving envelope.
- `EveryPresentationLimit_RejectsExactPlusOneWithTypedStableDetails` at line 44
  covers all six runtime limit kinds with typed code/resource key/limit/actual
  details.
- `ProjectDocumentContent.BuildResultPresentationCandidate` at
  `ProjectDocumentContent.cs:696-777` shares one budget across all candidate
  work and completes it before `SetResult` publishes state at lines 348-368.

### Atomic result persistence — resolved

`ProductionPickupSelectionExportsAndAtomicallySavesExactDimensionSpecificBytes`
overwrites a stale target, verifies exact bytes for 3D and 2D, proves no `.tmp`
residue, forces a locked-target failure, checks typed failure provenance, and
proves the previous destination remains intact. `AtomicResultExportWriter.Write`
at `AtomicResultExportWriter.cs:7-55` uses same-directory `CreateNew`, durable
flush, overwrite-on-move, and failure cleanup.

### Step 6 normalization and legacy boundary — resolved

The normalized signed, overflow, failure, paging, and oversized fixtures retain
their behavioral assertions while satisfying strict topology/result identity
validation. The removed invalid cross-kind scene use is now replaced by the
valid Step 7 typed-scene test. No `Skip`/`Ignore` additions or weakened
assertions were found. Active new code consumes typed `AnalysisResultSet` /
presentation models directly; no `disg`/`reac`/`fsec` adapter was introduced.

## Remaining Low Follow-ups

### [Low] Presentation-limit constructor hard caps lack direct +1 tests

`ResultPresentationLimits` defines six hard caps and validates them in its
constructor at `ResultPresentationLimits.cs:15-85`. Runtime exact/+1 behavior
for all six kinds is covered, but the constructor itself has no matrix proving
each hard cap is accepted and hard-cap+1 is rejected.

**Remediation:** add a data-driven constructor test for the six hard caps,
mirroring `ExportLimitConfiguration_AcceptsHardCapsAndRejectsEveryHardCapPlusOne`.

### [Low] Secondary atomic-cleanup failure attachment is not directly injectable

The primary write/replace failure, target preservation, and normal cleanup are
covered. The branch at `AtomicResultExportWriter.cs:51-53` that attaches a
secondary temp-file deletion failure to `Exception.Data` has no deterministic
test seam, so its non-masking guarantee remains code-reviewed rather than
regression-tested.

**Remediation:** isolate filesystem operations behind an internal injectable
boundary and force both a primary replace failure and cleanup failure; assert
the primary exception remains authoritative and the cleanup exception is
attached under `CleanupFailureDataKey`.

## Test Execution Results

- Independent focused Core Step 7: PASS, 29/29, failed 0, skipped 0.
- Independent focused UI Step 7: PASS, 9/9, failed 0, skipped 0.
- Final full-suite evidence supplied by the lead: PASS, 501/501 — Core 272,
  Printing/composition 17, Rendering 56, LocalRuntime 14, UI 142; failed 0,
  skipped 0.
- Independent AgentOnly: PASS, `overall=pass`, warnings 0; product gates were
  intentionally skipped. Log:
  `.agents/logs/check-20260921T002520985Z-39092.log`.
- Coverage: **not measured** (`coverage=null`).

## Priority Summary

| Severity | Count | Disposition |
|---|---:|---|
| Critical | 0 | None. |
| High | 0 | All prior High findings resolved. |
| Medium | 0 | All prior Medium findings resolved. |
| Low | 2 | Constructor hard-cap matrix and secondary cleanup-failure injection. |
