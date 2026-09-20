# Step 6 Final Quality Closeout: Rendering and Interaction Parity

## Verdict

**PASS WITH LOW FOLLOW-UPS** — Critical: 0, High: 0, Medium: 0, Low: 3.

All three original High findings and all four original Medium findings are resolved in the current tree. During final closeout, one remaining branch of H1 was found: member-topology changes did not invalidate the displacement buffer even though displaced member lines consume `scene.Members`. That branch was remediated and is now covered by an actual lifecycle cache regression. No new Critical, High, or Medium finding remains.

## Scope and method

This review first evaluated the original Step 6 patch in `.agents/logs/review-diff-csharp-frameweb-client.patch`, treating the pre-existing Step 5 Core/editor delta as out of scope. The final closeout then re-read the current rendering contracts/model/compiler/lifecycle/diagnostics, `ProjectDocumentContent`, `MainForm`, `Shell/Viewport/**`, localized resources, RendererProbe, and every Step 6 Rendering/UI test after the security, quality, and test remediations.

The review traced all twelve compiled layers and five decoration families through projection, dependency invalidation, coalescing, compilation/composition, GPU upload/draw, Paint/capture/PNG, hit testing, hover/selection, result paging/extrema, camera replacement, failure reporting, handle creation, and creating-thread disposal. It also searched the Step 6 scope for placeholders, hardcoded substitute models, diagnostic-only behavior, swallowed product errors, unfinished marker implementations, and Step 7/8 boundary leakage.

Independent final validation passed:

- Rendering: 56/56.
- UI full: 133/133; focused Step 6 UI: 33/33.
- Focused dependency/cache/performance set: 3/3.
- Actual masked-update baseline: 10,000 nodes / 9,999 members, two Loads invalidations coalesced to one batch and one layer compilation, unchanged buffers and selection retained, 30,000 vertices / 20,000 batches / 20,000 hit targets, 5.873 ms in the final focused run against a 5,000 ms budget (an earlier independent run measured 11.824 ms).
- Retained real-GL acceptance: 100 contexts, 1,700 frames, 600 captures, 100 orthographic captures, 100 perspective captures, 200 PNG captures, and zero live contexts/subscriptions/windows.
- AgentOnly retained pass: `.agents/logs/check-20260920T171340965Z-48448.log`.

Coverage percentage was not measured.

## Resolved original High findings

### H1. Coordinate and topology edits could leave dependent cached layers stale — resolved

`ViewportSceneModel.cs:427-449,561-622` now expands node-coordinate and member-topology changes through explicit dependency closures while distinguishing content changes from visibility-only changes. The compiler trace confirms that node coordinates cover every coordinate-dependent geometry/load/result layer plus grid and labels. Member topology directly affects members, rigid zones, notice points, member loads, displaced member lines, section forces, and labels; all are now included. The last closeout omission, `SceneLayerMask.Displacements`, was added at `ViewportSceneModel.cs:447`.

`Step6RenderingContractTests.cs:72-116` pins both exact closures and visibility-only behavior. More importantly, `Step6RendererLifecycleTests.cs:83-112` installs a scene, caches the displacement command buffer, changes only member endpoints while retaining equal displacement-layer content, consumes the deferred invalidation, and proves that the displacement buffer and vertices are rebuilt. `Step6ViewportIntegrationTests.cs:195-252` separately proves node-edit dependency recompilation through the shell while preserving the camera.

### H2. Grid, axes, labels, scale, and color legend were compiled but not drawn — resolved

`OpenGlViewportLifecycle.cs:34-67,855-1218,1301-1345` rasterizes the existing five decoration command families into a bounded transparent BGRA texture and composites it through the same `DrawKnownFrame` path used by normal Paint and capture/PNG. Empty or disabled decoration collections clear `_hasDecorationOverlay`; resize/camera changes regenerate the overlay; unrelated layer invalidations reuse it; and its program/VAO/VBO/texture are released through the creating-thread lifecycle.

`ProjectDocumentSceneProjector.cs:423-427` selects an XZ grid for 2D and XY for 3D. `RendererVerification.cs:135-302,411-459` exercises orthographic and perspective Paint/capture, asserts pixels from all five decoration families, disables `SceneLayerMask.Decorations` and proves those signatures disappear, and retains the no-decoration legacy path. The final 100-cycle real-context probe remained leak-free.

### H3. Extrema selection was inert and active result pages blended load cases — resolved

`ViewportResultExtrema.cs:21-139` supplies one deterministic signed policy shared by scene projection and all result tables: each row uses its greatest-absolute signed component, stable ties retain contract order, and Values/Minimum/Maximum/AbsoluteMaximum select consistently. `ProjectDocumentSceneProjector.cs:59-90,408-460` applies the filtered values to result glyphs and signed legend extrema. `ProjectDocumentContent.cs:255-287,646-720,846-869` uses the same policy for tables and reconciles a selection excluded by the new mode.

`ProjectDocumentSceneProjector.cs:92-136` filters nodal loads, member loads, and prescribed displacements by the selected result case. The signed extrema, stable-tie, table/selection, and two-case isolation regressions are at `Step6ViewportBehaviorTests.cs:146-239` and `Step6ViewportIntegrationTests.cs:255-296`.

## Resolved original Medium findings

### M1. Document replacement could retain or derive a camera from the previous scene — resolved

`ProjectDocumentContent.cs:225-233,526-568` carries explicit replacement intent, installs the projected scene first, then applies the 2D/3D camera policy and homes when requested. `MainForm.cs:502-515,979,1011-1022` passes `resetCamera: true` for document replacement/reopen and `false` for ordinary edits. `Step6ViewportIntegrationTests.cs:300-335` covers same-dimension replacement, cross-dimension replacement, and edit-time camera preservation.

### M2. Support, joint-release, and thermal semantics collapsed to indistinguishable glyphs — resolved

`ViewportSceneCompiler.cs:277-294` maps all six support DOFs to their translation or rotation axes. Lines 549-601 do the same for all six joint-release flags. Lines 692-739 retain thermal top/bottom offsets, sign colors, and a visible gradient. `Step6SceneSnapshotTests.cs:352-488` distinguishes every support/joint DOF and all thermal sign combinations.

### M3. Viewport failures lost their original exception and operation context — resolved

`ViewportOperationFailure.cs:3-39` defines a typed failure carrying operation, localized safe message, expected/unexpected classification, and original inner exception. `ProjectDocumentContent.cs:572-580,799-805,872-885` publishes that typed failure while retaining the legacy safe-message event. `MainForm.cs:910-923,1052-1075` routes unexpected failures through the existing exception boundary and reports expected failures diagnostically. `Step6ViewportIntegrationTests.cs:355-396` proves operation, classification, safe message, original exception, compatibility event, and `LastViewportFailure`.

### M4. Masked work lacked hard bounds and had no actual large masked-update baseline — resolved to Low residual

Hard availability bounds are now enforced before further copies/appends: aggregate scene materialization at `ViewportSceneModel.cs:479-495,782-853`, and vertices, batches, hit targets, hit-target points, decoration commands, and legend entries at `ViewportSceneCompiler.cs:151-156,412-487,965-1260`. Exact/+1 and fail-fast expansion tests are at `Step6SceneSnapshotTests.cs:228-349`.

`Step6MaskedUpdatePerformanceTests.cs:20-126` now drives an actual deferred lifecycle update on 10,000 nodes / 9,999 members. It proves two invalidations coalesce, only Loads is recompiled, other cached layer instances and selection are retained, command counts are stable, and the update completes well inside the recorded five-second ceiling. The remaining global node-map and bounds reconstruction in `ViewportSceneCompiler.Compile` (`ViewportSceneCompiler.cs:242,269,373-395,908`) is bounded and measured but still O(N); it is retained below as Low rather than Medium.

## Remaining Low findings

### L1. Rendering and shell orchestration retain oversized, mixed responsibilities

`ViewportSceneCompiler.cs` still combines command DTOs, twelve layer emitters, decoration command generation, hit testing, camera math, bounds, and budget accounting. `OpenGlViewportLifecycle.cs` combines GL ownership, cache invalidation, compilation, overlay rasterization, capture, and lifecycle. `ProjectDocumentContent.cs` combines layout, scheduling, projection, selection, result grids, capture, localization, and error policy. The contracts are now testable, but extracting per-layer compilation/overlay services and result/toolbar coordination would reduce omission risk.

### L2. `ViewportUpdateScheduler` validates a timing option it does not use

`ViewportUpdateScheduler.cs:24-33` accepts and validates `intervalMilliseconds` but stores no interval or timer; dispatch timing is determined by the posted callback/caller flush. Remove the parameter or implement and document its actual coalescing-window semantics.

### L3. A masked layer compile still reconstructs global scene indexes and bounds

`ViewportSceneCompiler.CompileLayer` delegates to the monolithic compiler, which rebuilds the full node dictionary and global bounds even when one layer is affected. The new actual 10k masked-update test demonstrates 5.873-11.824 ms locally and all derived work is hard-bounded, so this is not a current responsiveness or availability defect. A future optimization can cache immutable node/bounds indexes per scene or split true per-layer compilers, while retaining the current correctness and budget tests.

## Explicit checks and non-findings

- Critical findings: 0.
- High findings: 0.
- Medium findings: 0.
- Low findings: 3.
- New hardcoded substitute or fallback model: 0.
- Placeholder or unimplemented production path in Step 6 scope: 0.
- Diagnostic-only user-visible behavior: 0.
- Swallowed viewport/render/capture product errors: 0.
- Public `IViewportScene`, `ICameraController`, or `IHitTestService` compatibility breaks: 0.
- New lifecycle leaks, duplicate selection/hover events, or idle render loops: 0.
- Unbounded scene/command/result-table work: 0.
- Step 7 calculation/result-envelope or Step 8 printing/package scope leakage: 0.

Hit-test ordering remains deterministic by distance and kind priority. Empty workspaces keep the GL control hidden, capture and Paint use the same frame path, and creating-thread disposal releases subscriptions and GPU resources in the required order.

## Codex consultation status

The required read-only nested Codex consultation was attempted twice through the documented `.agents/skills/_shared/codex_consult.py` wrapper after reading the skill instructions. Both invocations exited successfully but returned only context-loader readiness and requested a later Objective, so they provided no review analysis and are recorded as **unavailable**. Artifacts: `.agents/logs/codex/20260920T172332Z-quality-review-step6.md` and `.agents/logs/codex/20260920T172522Z-quality-review-step6-retry.md`. No consultation claim was used as proof.
