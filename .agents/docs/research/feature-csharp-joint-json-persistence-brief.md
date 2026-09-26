## Feature Brief: C# Joint JSON Persistence

### Current State
- Architecture: `FrameWebforCS` input services are internal `Lazy<T>` singletons that keep class-backed state and expose lower-camel-case `set*Json` / `get*Json` methods. JSON conversion is centralized in `DataHelperModule`.
- Relevant files: `FrameWebforCS/components/input/InputJointService.cs`, `FrameWebforCS/providers/DataHelperModule.cs`, and the legacy reference `FrameWebforJS/src/app/components/input/input-joint/input-joint.service.ts`.
- Patterns: For `Dictionary<string, List<T>>`, use `DataHelperModule.JsonToDict` with a `JsonToList<T>` converter, then serialize each row with `DataHelperModule.ClassToDictionary`.

### Feature Goal
Add simple `joint` JSON load and save functions to `InputJointService.cs`, preserving the legacy case-to-row-array shape and using the existing C# `DataHelperModule` helpers.

### Scope
- Include: an internal `clsJoint` DTO; singleton service state; `clear()`; `setJointJson(JsonElement)`; `getJointJson()`; missing-`row` one-based fallback; legacy empty-row and empty-case omission; preservation of case IDs and row order.
- Exclude: changes to `InputDataService.cs`; changes to `DataHelperModule.cs`; joint spreadsheet binding; analysis-request construction; rendering; new dependencies; edits to the dirty `InputNodesComponent.cs` and `InputNodesService.cs` files.

### Complexity Classification (from Codex)
- Classification: SIMPLE
- Estimated files: 1 product file
- Estimated LOC: 70-95 physical lines, dominated by the established service/DTO boilerplate
- Implementation route: Codex direct

### Integration Points
- `DataHelperModule`: reads the top-level case dictionary and row arrays, and converts DTO fields back to dictionaries without modification.
- `InputDataService`: is the later end-to-end file-open/save integration point, but is intentionally outside this user-named service-only change.
- Legacy Angular service: defines the serialized fields `row`, `m`, `xi`, `yi`, `zi`, `xj`, `yj`, and `zj`, plus row fallback/filtering behavior.

### Risks
- Service methods remain unused by the file orchestrator in this scoped change: keep the boundary explicit instead of silently modifying a second file.
- A bare helper-only mapping does not restore missing row numbers or omit blank rows/cases: add small local post-processing loops while retaining helper-based conversion.
- Missing top-level `joint` must not replace existing state: assign only when `JsonToDict` returns a value.
- Existing unrelated dirty node files could be overwritten: modify only `InputJointService.cs` and inspect the final diff.

### Success Criteria
- `setJointJson` loads multiple case IDs into ordered `List<clsJoint>` values through `DataHelperModule`, preserves numeric/string member IDs as strings, and supplies one-based row numbers when `row` is absent.
- `getJointJson` emits the legacy case-to-row-array shape with all eight fields, omits wholly blank rows and empty cases, and preserves `0`, `1`, and `null` release values.
- A missing top-level `joint` leaves current state unchanged.
- `DataHelperModule.cs`, `InputDataService.cs`, and existing dirty node files are unchanged.
- A focused round-trip smoke test and `dotnet build FrameWebforCS/FrameWebforCS.csproj --no-restore` pass, or any pre-existing build blocker is reported separately.
