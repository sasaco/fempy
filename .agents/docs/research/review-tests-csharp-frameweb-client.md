# Test Coverage and Acceptance Re-review: C# FrameWeb Desktop Step 6

## Verdict

**PASS WITH LOW FOLLOW-UPS** — Critical: 0, High: 0, Medium: 0, Low: 4.

The accepted High dependency-invalidation defect and all four original Medium
test gaps are remediated. Post-fix full runs pass Rendering 56/56 and UI
133/133, with no failures or skips. The focused Step 6 UI set passes 33/33.
Both .NET solutions build Release with 0 warnings and 0 errors when run
sequentially.

**Coverage: not measured.** The supplied collection evidence remains
`coverage=null`; no percentage is estimated from test counts.

## Scope and Method

This re-review compared the original Step 6 review against the current
Rendering scene model/compiler, Shell projector/content/MainForm paths, all
Step 6 Rendering/UI tests, RendererProbe verification, the security remediation,
and the new H1/H2/H3/M1/M3 regressions. Step 5 dirty tests remain outside the
Step 6 finding count.

The same acceptance matrix was rechecked: independent layers and all load
shapes; 2D/3D cameras and decorations; hit/hover/selection; case/state paging;
signed extrema; invalidation/coalescing; PNG boundaries; lifecycle; result and
compiler work budgets; preset/10,000-node performance; malformed input;
determinism; and skipped tests.

## Resolved Findings and Exact Evidence

### Resolved High — coordinate-dependent cache invalidation

- `ViewportSceneModel.NodeCoordinateDependencies` and
  `MemberTopologyDependencies` at
  `FramePrintPDF/PDF_Manager.Rendering/Scene/ViewportSceneModel.cs:427-451`
  define explicit closures. `GetChangedLayers` applies them at lines 561-595
  while distinguishing visibility-only changes and retaining layer-specific
  presentation masks.
- `Step6RenderingContractTests.SceneDiffAppliesExplicitCoordinateDependencyClosureWithoutExpandingVisibilityChanges`
  at `Step6RenderingContractTests.cs:72` pins node, member-topology, and
  visibility-only masks.
- The displacement compiler builds deformed lines from `scene.Members` at
  `ViewportSceneCompiler.cs:333-347`; `MemberTopologyDependencies` therefore
  includes `SceneLayerMask.Displacements`. The actual cache regression
  `Step6RendererLifecycleTests.MemberTopologyEditRecompilesCachedDisplacementsWithUpdatedCoordinates`
  at `Step6RendererLifecycleTests.cs:83` keeps displacement results unchanged,
  edits a member endpoint, and proves the cached displacement layer is replaced
  with different projected vertices.
- `Step6ViewportIntegrationTests.NodeEditInvalidatesAndRecompilesEveryCoordinateDependentLayer`
  at `Step6ViewportIntegrationTests.cs:195` compiles before/after an ordinary
  node edit, asserts the exact closure, preserves the camera, and proves changed
  vertices for connected members, supports, springs, panels, notice points,
  and loads.

### Resolved Medium — signed extrema are visible and deterministic

- `ViewportResultExtrema` defines one shared policy for scene and tables: a
  row's representative is its signed greatest-absolute component; equal
  components and equal row extrema retain contract order. `Values`, `Minimum`,
  `Maximum`, and `AbsoluteMaximum` select the same entities in the scene,
  signed legend, and result grid.
- `ProjectorAppliesSignedExtremaToSceneAndLegendWithStableTies` at
  `Step6ViewportBehaviorTests.cs:151` covers all four modes with signed
  multi-value data and an absolute-value tie.
- `ExtremaChangesSceneLegendAndEveryResultTableAndClearsExcludedSelection` at
  `Step6ViewportIntegrationTests.cs:255` covers displacement, reaction, and
  section-force rows, legend signs, coordinate stability, and selection
  reconciliation. `ProjectDocumentContent.ReconcileResultSelection` at
  `ProjectDocumentContent.cs:846` clears an excluded result key before the
  asynchronous scene replacement.

### Resolved Medium — complete load projection and case isolation

- `ProjectDocumentSceneProjector` filters nodal loads, member loads, and
  prescribed displacements by the active result `CaseId` at lines 92-133.
- `ProjectorNeverMixesLoadsFromDifferentActiveResultCases` at
  `Step6ViewportBehaviorTests.cs:196` proves two cases do not blend.
- `ProjectorMapsEveryPointMomentDirectionWithExactStationsAndVectors` at line
  241 covers local X/Y/Z on a rotated member and global X/Y/Z, including
  `SceneLoadVectorKind.Moment`, both normalized stations, and signed vectors.
- `ProjectorPreservesPointForceDistributedAndThermalMemberLoadSemantics` at
  line 274 pins point-force pairing, distributed positive-L2 extent, and
  thermal top/bottom values.

### Resolved Medium — one bounded result-table policy

- `ProjectDocumentContent.MaximumResultTableRows` and
  `TryReserveResultRows` at `ProjectDocumentContent.cs:18,743-757` bound
  displacement, reaction, and atomic I/J section-force rows with a localized
  truncation marker.
- `AllResultTablesUseOneDeterministicRowLimitAndExposeTruncation` at
  `Step6ViewportIntegrationTests.cs:400` exercises limit+1 input for all three
  table modes and pins the retained last key.

### Resolved Medium — aggregate and derived rendering work budgets

- The current scene/compiler enforce aggregate scene and derived command
  budgets, including `ViewportSceneCompiler.MaximumVertexCount` at
  `ViewportSceneCompiler.cs:151` and pre-append checks around lines 1207-1240.
- `Step6SceneSnapshotTests.AggregateEntityBudgetRejectsTheFirstItemFromTheNextLayerBeforeLayerCopies`
  (line 228), `DerivedGeometryBudgetRejectsGlyphExpansionBeforeTheVertexLimitIsExceeded`
  (line 249), decoration exact-limit/limit+1 tests (lines 271 and 297), and
  color-legend entry bounds (line 329) provide boundary evidence.

## Additional Acceptance Corrections

- 2D grid/camera parity: `ProjectDocumentSceneProjector.CreatePresentation`
  selects XZ for 2D and XY for 3D; `ProjectorUsesCameraAlignedGridPlanesForTwoAndThreeDimensionalDocuments`
  at `Step6ViewportBehaviorTests.cs:131` pins the policy.
- Camera replacement: `ProjectDocumentContent.SetDocument(document,
  resetCamera)` defers policy/home until after installing the new scene;
  `MainForm.SetCurrentDocument` and content reopen pass `true`, while
  `OnDocumentEdited` passes `false`. The same- and cross-dimension regression is
  `DocumentReplacementHomesNewSceneWhileOrdinaryEditPreservesCamera` at
  `Step6ViewportIntegrationTests.cs:300`.
- Failure provenance: `ViewportOperationException` retains operation, safe
  localized message, expected/unexpected classification, and original inner
  exception. MainForm routes unexpected failures through `UserExceptionBoundary`.
  `TypedViewportFailurePreservesOperationOriginalExceptionAndLocalizedSafeMessage`
  at `Step6ViewportIntegrationTests.cs:355` covers the typed and compatibility
  events.

## Remaining Low Gaps

### [Low] Real PNG limit and file-save behavior are not exercised

The real probe verifies successful in-memory PNG signatures and normal size,
while the UI regression checks invalid arguments before creating a GL context.
No test generates an actual PNG that crosses a caller byte/dimension limit or
executes `SaveViewportPng` to a temporary file. Add process-isolated over-budget,
decode/pixel, successful save, and I/O-failure tests.

### [Low] Scene snapshots remain mostly deterministic self-comparisons

The snapshot suite proves repeatability and now has strong targeted projector
oracles, but still lacks reviewed per-layer command hashes or complete pinned
vertices/batches/colors for both camera policies. Add compact canonical hashes
plus readable assertions for high-risk glyph layers.

### [Low] Empty-workspace no-context behavior is still indirect

Repeated empty MainForm smoke proves no crash, but does not assert per-cycle
renderer handle/context/subscription counters at the empty-workspace boundary.
Add a process-isolated diagnostic-counter regression proving no GL context is
created before a document exists.

### [Low] Malformed/exact-boundary matrices remain incomplete

Aggregate budgets and selected invalid constructors are covered, but the new
rigid-zone, spring, joint, panel, notice-point, prescribed-displacement,
reaction, section-force, label, and legend contracts do not each have a compact
NaN/infinity/empty-ID/invalid-enum/reference/exact-limit matrix. Add data-driven
constructor tests without duplicating Core validation tests.

## Test Execution Results

- Rendering full: PASS, 56/56, failed 0, skipped 0; 457 ms reported.
- Focused Rendering contract/lifecycle: PASS, 15/15, failed 0, skipped 0;
  127 ms reported.
- UI full: PASS, 133/133, failed 0, skipped 0; 22 s reported.
- Focused Step 6 UI: PASS, 33/33, failed 0, skipped 0; 2 s reported.
- Focused `Step6ViewportBehaviorTests`: PASS, 23/23, failed 0.
- Focused remediation selection/projector set: PASS, 17/17, failed 0.
- `FrameWeb.sln` Release build: PASS, 0 warnings / 0 errors.
- `FramePrintPDF/FramePrintPDF.sln` Release build: PASS, 0 warnings / 0 errors.
  An initial concurrent build attempt hit a shared Azure WorkerExtensions file
  lock; both canonical builds passed when rerun sequentially, so this was a
  test-runner collision rather than a product failure.
- Targeted whitespace format verification over every owned product/test file:
  PASS. `git diff --check`: PASS (line-ending notices only).
- Step 6 source scan: no `Skip`/`Ignore` additions.
- Retained real-context evidence: PASS, 100 contexts, 1,400 frames, 500
  captures, 100 2D and 100 3D captures, 200 PNG encodes, and all final live
  counters zero.
- Retained performance evidence: four presets × 40 frames under 10 seconds;
  10,000 nodes/9,999 members compiled in 111.967 ms and diffed in 10.036 ms.
- AgentOnly `.agents/logs/check-20260920T171340965Z-48448.log`: `overall=pass`,
  passed 10 / failed 0 / skipped 1.
- Coverage: not measured (`coverage=null`).

## Priority Summary

| Severity | Count | Disposition |
|---|---:|---|
| Critical | 0 | None. |
| High | 0 | Dependency-aware invalidation is implemented and regression-tested. |
| Medium | 0 | Extrema, load projection, result rows, and renderer budgets are covered. |
| Low | 4 | PNG save/real limits, semantic snapshots, empty-workspace counters, and broader malformed matrices remain follow-ups. |
