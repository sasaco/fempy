# Security Review: C# FrameWeb Desktop Step 6

## Verdict

**PASS WITH LOW HARDENING ITEMS** — Critical: 0, High: 0, Medium: 0, Low: 3.

Step 6 preserves the authentication, dependency, creating-thread, finite-value,
and native-resource boundaries established in prior steps. The two local
availability findings from the initial review were remediated and re-tested:
all result tables now share a deterministic row budget, and scene materialization
and compilation now enforce aggregate and derived-work budgets before oversized
copies or GPU upload. No remote execution, credential disclosure, authorization
bypass, or command injection was found.

The inherited legacy-host findings are unchanged and are not included in the
Step 6 totals: legacy High: 2 and Medium: 2 remain open, and redistribution of
the complete application remains **NO-GO**.

## Scope and Method

- Read the Step 6 plan, DESIGN, HANDOFF, security rules, implementation work
  logs, and the complete patch at
  `.agents/logs/review-diff-csharp-frameweb-client.patch`. The patch also
  contains the pre-existing Step 5 Core/editor work; findings below count only
  the Step 6 rendering, viewport shell, resources, probe, and test delta.
- Inspected the current implementations of `OpenGlViewportLifecycle`, all
  Rendering scene contracts/model/compiler/interaction types,
  `ProjectDocumentContent`, the Shell viewport projector/state/navigator/
  scheduler, renderer probe, resources, and all Step 6 Rendering/UI tests.
- Traced untrusted project and validated analysis-result values through scene
  projection, labels, stable keys, hit targets, command buffers, GL upload,
  result grids, diagnostics, PNG encoding, and filesystem save.
- Checked creating-thread enforcement, posted-update disposal, native context
  teardown, exception containment, numeric finiteness/ranges, integer
  arithmetic, collection materialization, resource-exhaustion ceilings, and
  file overwrite behavior.
- Scanned the full patch for secrets, authentication changes, dependency
  changes, diagnostic placeholders, swallowed exceptions, and weakened tests.
  No project/dependency manifest changed. `git diff --check` over the Step 6
  scope passed.
- Ran
  `dotnet list FramePrintPDF/PDF_Manager/PDF_Manager.csproj package --vulnerable --include-transitive --no-restore`;
  it exited 0 and reported no vulnerable package in the standalone desktop
  graph.
- Re-ran the remediated Step 6 scope: Rendering 54/54, UI 125/125, and the
  Step 6 filters 27/27 and 25/25 passed; `FramePrintPDF.sln` Release build
  completed with 0 warnings / 0 errors; targeted `dotnet format
  --verify-no-changes` and `git diff --check` passed. The existing 10,000-node /
  9,999-member performance fixture remained green.
- Also relied on the supplied acceptance evidence: AgentOnly `overall=pass` at
  `.agents/logs/check-20260920T171340965Z-48448.log`; real GL 100 contexts,
  1,400 frames, 500 captures, and final live counters zero. Coverage was not
  measured.

## Remediation Re-review

- **Resolved Medium — result-table materialization**:
  `ProjectDocumentContent.cs:18,210,656-720,742-756` applies the public
  `MaximumResultTableRows` budget to displacement, reaction, and member-force
  tables through one reservation path. It reserves room for the existing
  localized truncation marker, keeps member I/J rows atomic, and exposes the
  result through `ResultTableTruncated`. The cap+1 regression covers all three
  table modes at `Step6ViewportIntegrationTests.cs:206-292`.
- **Resolved Medium — scene and derived-work expansion**:
  `ViewportSceneModel.cs:426,479-494,796-840` reserves already-materialized
  result layers and consumes one aggregate 250,000-entity budget while each
  input layer is enumerated, so the first item from an over-budget later layer
  is rejected before layer/key copies. `ViewportSceneCompiler.cs:151-156,
  412-487,965-1259` checks vertices, batches, hit targets, hit-target points,
  decoration commands, and color-legend entries before projection/list
  materialization, uses checked offset arithmetic, and re-applies all budgets
  when cached layer/decorations are composed. Regressions at
  `Step6SceneSnapshotTests.cs:228-359` verify pre-copy rejection, fail-fast
  glyph expansion, and exact/+1 decoration limits through both typed scene
  compilation and composition; the existing performance fixture at lines
  170-225 remains green.

## Findings

### [Low] The PNG byte limit is checked only after full encoding and allocation

- **Location**:
  `FramePrintPDF/PDF_Manager.Rendering/OpenGlViewportLifecycle.cs:532-582`;
  `FramePrintPDF/PDF_Manager.Rendering/Scene/ViewportInteraction.cs:105-121`.
- **Description**: dimensions and pixel count are correctly checked before the
  readback, but `Bitmap.Save` writes the complete PNG to an uncapped
  `MemoryStream`; only then is `output.Length` compared with `MaximumBytes`, and
  `ToArray` adds another complete copy on success. A small caller-supplied byte
  limit therefore does not constrain compression work or peak allocation. The
  16,777,216-pixel ceiling keeps this finite, so the residual impact is local
  memory/latency rather than an unbounded remote image bomb.
- **Recommended fix**: encode through a stream that throws as soon as the byte
  budget would be exceeded, with overflow-safe accounting. Avoid a second full
  copy where the API can consume an owned buffer or stream, and test a
  high-entropy image whose encoded output crosses the limit during encoding.

### [Low] PNG save truncates an existing file instead of replacing it atomically

- **Location**:
  `FramePrintPDF/PDF_Manager/Shell/Contents/ProjectDocumentContent.cs:362-370,743-753`.
- **Description**: the interactive dialog provides overwrite consent, and the
  PNG is fully captured before filesystem access, but `File.WriteAllBytes`
  opens/truncates the destination directly. Disk-full, device removal, process
  termination, or a late I/O failure can destroy a pre-existing destination and
  leave a partial image. The caught exception is reported, but the old file is
  not recoverable through this path.
- **Recommended fix**: write a uniquely named temporary file in the destination
  directory, flush and close it, then atomically replace/move it using the same
  guarded pattern as project persistence. Clean up the temporary on failure and
  preserve the original until replacement succeeds.

### [Low] Free-form identifiers are reused as delimiter-encoded stable keys and raw UI captions

- **Location**:
  `FramePrintPDF/PDF_Manager/Shell/Viewport/ProjectDocumentSceneProjector.cs:185-186,208,224-239,377-386,450-475,518`;
  `FramePrintPDF/PDF_Manager/Shell/Contents/ProjectDocumentContent.cs:377-385,615-626,698-708`;
  `FramePrintPDF/PDF_Manager.Rendering/Scene/ViewportSceneModel.cs:639-652`;
  permissive input boundary at
  `FramePrintPDF/PDF_Manager.Core/Documents/ProjectDocument.cs:900-901`.
- **Description**: domain IDs/names remain nonblank-only. Step 6 forms support,
  spring, joint, and section-force identities by joining unescaped IDs with
  `/`, while document names, case IDs, and node/member IDs are placed directly
  in captions, selectors, labels, table cells, and validation diagnostics. IDs
  such as `a/b` plus `c` and `a` plus `b/c` can collide and make an otherwise
  valid viewport fail closed or select the wrong table row. Controls and Unicode
  bidirectional formatting characters can also spoof the local UI/diagnostic
  presentation. Safe .NET controls prevent code execution, so this remains a
  local integrity/availability issue.
- **Recommended fix**: use typed composite key records instead of serialized
  delimiters, or length-prefix/escape every component. At the Core boundary set
  field-specific length limits and reject path/key separators where required,
  C0/C1 controls, CR/LF/TAB for single-line values, and bidi override/isolate
  characters; mirror the policy in JSON schema and projection tests.

## Positive Controls Verified

- `ScenePoint3`, result scales, relative positions, grid spacing, color values,
  camera extents, and the projector's double-to-float conversion reject
  non-finite or out-of-range values before GL upload. Projection also rejects
  non-finite clip coordinates.
- Stable keys carry an explicit `SceneEntityKind`; scene validation rejects
  duplicate keys and unknown references. Hit targets preserve one source-row key
  across multi-segment glyphs, including the two samples of a point-pair member
  load, and layer-masked hit testing does not execute identifier text.
- GL lifecycle operations enforce the creating thread. Posted shell flushes
  check `_disposed`, all Step 6 event subscriptions are removed before renderer
  disposal, teardown releases the current context in `finally`, and disposal
  rethrows rather than swallowing native failures.
- Empty workspaces keep the GL control hidden. Runtime scene/OpenGL argument,
  state, and overflow failures are converted to a contained unavailable-view
  state and diagnostic event; the Step 6 path adds no raw modal exception
  dialog. Unexpected failures are not silently converted to success.
- PNG capture checks width, height, 16,777,216 pixels, checked readback size, and
  a bounded accepted encoded length. The findings above concern peak work and
  atomic persistence, not absence of all bounds.
- Result-table display work is capped at 10,000 rows through one path for all
  modes, with deterministic truncation state and a localized marker.
- Scene construction rejects an aggregate over 250,000 entities while inputs
  are enumerated. Compilation and layer composition reject over-budget
  vertices, batches, hit targets, hit-target points, decoration commands, and
  color-legend entries before further materialization or GL upload.
- Result coordinates come from an already validated immutable result set;
  selection is rejected unless it belongs to the current scene, and stale
  selection is cleared when a replacement scene no longer contains the key.
- No placeholder implementation, ignored test, weakened assertion, or swallowed
  exception was found in the Step 6 delta.

## Explicit Counts

- Critical findings: 0
- High findings: 0
- Medium findings: 0
- Low findings: 3
- Hardcoded secrets or credentials: 0
- Authentication or authorization regressions: 0
- New command, shell, process, network, SQL, or HTML injection paths: 0
- Sensitive token/request/document logging additions: 0
- New raw modal exception-detail exposures: 0
- New or upgraded dependencies: 0
- Vulnerable packages reported in the standalone `PDF_Manager` graph: 0
- Diagnostic placeholders or swallowed exceptions: 0
- Weakened or skipped Step 6 tests: 0

## Inherited Release Constraints

Step 6 does not modify the legacy anonymous analysis/print hosts, their
dependency graph, restricted fonts, or endpoint policy. The accepted baseline
remains legacy High: 2, Medium: 2, and complete-application redistribution
**NO-GO**. Those inherited findings are not counted in the Step 6 totals.
