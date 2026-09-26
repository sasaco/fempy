## Implementation Plan: WinForms Dimension Change Notification

### Purpose
Selecting 2D or 3D in the legacy WinForms menu updates `InputDataService.dimension` and immediately reconfigures the displayed route's dimension-dependent items. Switching in either direction preserves entered model values, including fields hidden in 2D, and a cached route displays the current dimension when opened later.

### Scope
- New files: `FrameWebforCS/components/IDimensionAwareComponent.cs` (or an equivalent small shared view contract); focused WinForms tests under `FrameWebforCS.Tests/` for menu/service routing and dimension-sensitive sheets.
- Modified files: `FrameWebforCS/components/menu/MenuComponent.cs` and, only if needed for event hookup, `MenuComponent.Designer.cs`; `FrameWebforCS/providers/InputDataService.cs`; `FrameWebforCS/AppRoutingModule.cs`; `FrameWebforCS/components/input/InputFixNodeComponent.cs`, `InputFixMemberComponent.cs`, `InputJointComponent.cs`, `InputElementsComponent.cs`, `InputNodesComponent.cs`, `InputMembersComponent.cs`; dimension-dependent result views `FrameWebforCS/components/result/ResultDisgComponent.cs`, `ResultReacComponent.cs`, `ResultFsecComponent.cs`, `ResultCombineDisgComponent.cs`, `ResultCombineFsecComponent.cs`, `ResultCombineReacComponent.cs`, and `ResultPickupDisgComponent.cs`; `FrameWebforCS/three/SceneService.cs` only if the existing camera-mode change must be invoked from the menu path.
- Dependencies: Existing WinForms, FarPoint Spread, singleton service, and xUnit/STA test patterns. No new package is planned. The JSON file format and result-coordinator edits already in `InputDataService.cs` stay intact.
- Excluded: A new global notification for direct property assignment or JSON dimension loading, model coordinate conversion or deletion of out-of-plane values, changes to the Python/Angular clients, and unrelated result aggregation work.

### Implementation Steps

Implement the transition and route dispatch first, then update each view's existing sheet schema in place.

#### Step 1: Lock the switching contract and establish regression tests
- [ ] Inventory every `dimension` branch in `FrameWebforCS` and record the exact visible columns, `DataField` bindings, headers, spans, locks, widths, and camera behavior for 2D and 3D; confirm the listed views are complete before edits.
- [ ] Add isolated STA tests for 3D→2D→3D on a visible input sheet (including all six fix-node sheets), hidden 3D field preservation, current sheet preservation, and later activation of a cached view. Capture initial menu state and same-dimension selection as separate assertions.
- [ ] Establish the baseline build/test result and record the staged and unstaged `InputDataService.cs` diff and concurrently edited result paths so implementation does not overwrite other work.
**Verification**: Tests demonstrate the current missing live-switch behavior while existing JSON dimension tests and the baseline build result are documented.

#### Step 2: Add one menu-origin dimension transition
- [ ] Add a validated 2-or-3 menu transition in `InputDataService` that changes `dimension` and emits one notification only when the menu selects a different dimension; leave JSON assignment silent. Keep the existing serialization value and JSON validation unchanged.
- [ ] Connect both menu items to that transition, initialize and update their mutually exclusive checked state and parent 2D/3D text from the service, and prevent the parent item from independently toggling. Invoke the existing scene camera-mode switch when the menu changes dimension if a live scene is registered.
- [ ] After a file open, synchronize the menu's displayed choice from the loaded service value without emitting the menu-change notification.
**Verification**: Tests cover 2D/3D/same-value menu choices, notification count and ordering, saved `dimension`, exclusive checks/text, unchanged JSON notification behavior, and camera-mode invocation where a scene exists.

#### Step 3: Route the notification to the current view
- [ ] Introduce a small typed contract for dimension-aware views and make `AppRoutingModule` the single subscriber/dispatcher for the menu-origin notification. Dispatch on the WinForms UI thread, guard closed/disposed forms and controls, and avoid duplicate subscriptions.
- [ ] Apply the current dimension before showing a cached view and before the existing same-target early return, so inactive and repeated routes do not display a stale schema. Preserve `CurrentComponent`, route title, and `setActiveSheet(option)` behavior.
**Verification**: Router tests show exactly one refresh of the visible view per actual menu change, correct cached-view refresh on later navigation, no refresh for unchanged dimension, and no exception or lingering subscription after disposal.

#### Step 4: Reconfigure input sheets without losing model values
- [ ] Refactor each dimension-dependent input view in Scope so the 2D/3D schema can be reapplied to existing Spread sheets: column count, `DataField`, headers/spans, widths, protected/locked state, and host width. Keep the existing service-backed row collections and active sheet instead of rebuilding domain rows.
- [ ] Commit any in-progress cell edit before reconfiguring its sheet, or reject/defer a switch safely when an invalid edit cannot be committed. Clear stale schema metadata when moving in both directions; preserve hidden 3D fields in the underlying data and restore them on return to 3D.
**Verification**: STA tests exercise each affected input view in both directions, including a non-default sheet and a pending edit, and assert the full schema, unchanged service values, active selection, and no duplicate sheets/bindings.

#### Step 5: Reconfigure dimension-dependent result views
- [ ] Apply the same view contract to the dimension-dependent basic, combined, and pickup result components listed in Scope, coordinating with the owners of currently edited result files before touching them. Update displayed columns/labels/bindings without changing aggregation or loaded result data.
- [ ] Check any async result publication against the current dimension when it reaches the UI thread so an earlier calculation cannot restore the old column layout after a switch.
**Verification**: With populated 2D and 3D result fixtures, switch each visible result view in both directions and navigate back to cached result views; assert schema and result values while existing combination/pickup tests continue to pass.

#### Step 6: Integrate and validate
- [ ] Run the focused `FrameWebforCS.Tests` suite and build `FrameWebforCS/FrameWebforCS.csproj`; perform one manual WinForms route/menu check for a displayed fix-node view, an inactive cached input view, and a result view.
- [ ] Inspect `git status` and diffs against the Step 1 baseline, including the existing result-coordinator changes in `InputDataService.cs`, and confirm no unrelated product edits or lost user changes.
**Verification**: Relevant tests and build pass, 2D→3D restores hidden values and menu state, and the final diff is limited to the agreed component.

### Risks & Considerations
- Spread can retain obsolete `DataField`, header spans, locked columns, or edited-cell state when column counts shrink and expand; tests must inspect actual bindings and values, not only column counts.
- A cached view constructed at another dimension remains stale unless route activation reapplies its schema. The router's same-target early return is another required refresh path (`AppRoutingModule.cs:58-62,88-115`).
- JSON loading validates and assigns dimension after sequential service loads (`InputDataService.cs:153-199`); the menu-origin event must not fire during that partial load. A file-open action must still synchronize the menu display.
- Both menu children currently use independent `CheckOnClick` (`MenuComponent.Designer.cs:123-143`), allowing contradictory choices until the menu handler enforces one selection.
- Result components and the unstaged `InputDataService.cs` load-coordinator block are under concurrent modification; implement against a fresh diff and coordinate ownership before editing those paths.
- The repository's durable desktop architecture targets .NET 8, while this request names the legacy `FrameWebforCS` WinForms project; this plan confines product edits to the explicitly requested project.

### Open Questions
- None for the requested menu-origin switch: user specified that input values remain and only menu operations issue the dimension-change notification. JSON loading keeps its current non-notifying service behavior; menu display synchronization after open is in scope.
