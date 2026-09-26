# Codebase Scan: C# `joint` JSON Persistence

## Scope and conclusion

The requested implementation belongs primarily in
`FrameWebforCS/components/input/InputJointService.cs`. The existing file is an
empty 11-line class, while the neighboring services use a singleton-owned
dictionary, `clear()`, a `set*Json(JsonElement)` loader, and a `get*Json()`
serializer.

`DataHelperModule` already supports every scalar and container type needed for
`joint`: `string`, nullable `int`, nullable `float`, object dictionaries, and
arrays of reflected classes. No helper change is required for the simple
implementation.

There is one important integration distinction:

- One product file is sufficient to satisfy the literal request to add the
  load/save functions to `InputJointService.cs`.
- Two product files are required for file-open/file-save behavior to actually
  use those functions, because `FrameWebforCS/providers/InputDataService.cs`
  currently neither calls `setJointJson` nor emits a `"joint"` entry from
  `GetSaveJson()`.

## Affected files

| File | Current state | Expected change |
|---|---|---|
| `FrameWebforCS/components/input/InputJointService.cs` | Empty class; only unused `System`/collections/text imports | Required: add `clsJoint`, singleton state, `clear`, load, and save methods |
| `FrameWebforCS/providers/InputDataService.cs` | Loads/saves node, member, element, fix, load, notice-points, and combine data, but not joint | Optional but required for end-to-end file persistence: add one load call and one `"joint"` save entry |
| `FrameWebforCS/providers/DataHelperModule.cs` | Already has the required generic converters | No change expected |
| `FrameWebforCS/components/input/InputJointComponent.cs` | Builds six spreadsheet tabs but does not currently bind `InputJointService` | No change for JSON persistence; UI binding is a separate task |

The working tree already contains unrelated modifications in
`InputNodesComponent.cs` and `InputNodesService.cs`. They must remain untouched.

## Existing C# patterns and naming

`InputMembersService` is the closest flat-object example:

- internal DTO class with public fields;
- `Lazy<T>` singleton exposed as `Instance`;
- private dictionary state initialized by `clear()`;
- `setMemberJson(JsonElement)` calls
  `DataHelperModule.JsonToDict<clsMember>(jsonData, "member")`;
- `getMemberJson()` maps each DTO through
  `DataHelperModule.ClassToDictionary`.

`InputElementsService` is the closest nested-container example. It uses the
converter overload of `JsonToDict` for a top-level case/set dictionary, then
uses a second helper conversion for each nested value. `joint` has the analogous
shape `Dictionary<string, List<clsJoint>>`, so its natural reader is:

```csharp
DataHelperModule.JsonToDict(
    jsonData,
    "joint",
    static caseJson => DataHelperModule.JsonToList<clsJoint>(caseJson));
```

Its writer should iterate the case dictionary, map each row with
`ClassToDictionary`, and store the resulting list under the original case key.

The established public method spelling is lower camel case despite C#
conventions: `clear`, `setMemberJson`, `getMemberJson`, `setElementJson`, and
`getElementJson`. The joint implementation should therefore use `clear`,
`setJointJson`, and `getJointJson` for consistency.

`InputCombineService` is not currently a `DataHelperModule` example: it uses
private, stricter manual parsers and serializers. It is useful for singleton and
atomic-replacement naming only. The requested simple helper-based behavior is
better modeled on `InputMembersService` and `InputElementsService`.

## Legacy TypeScript `joint` contract

The authoritative legacy shape in
`FrameWebforJS/src/app/components/input/input-joint/input-joint.service.ts` is:

```text
joint: {
  <case id string>: [
    {
      row: number,
      m: string,
      xi: number | null,
      yi: number | null,
      zi: number | null,
      xj: number | null,
      yj: number | null,
      zj: number | null
    }
  ]
}
```

Field meaning and behavior:

| Field | Legacy load behavior | Legacy save behavior |
|---|---|---|
| case ID | Preserved as an object key/string | Preserved; optional `targetCase` filters to one case |
| `row` | Uses the JSON number when present; otherwise defaults to one-based array index | Emitted unchanged |
| `m` | `null`/missing becomes `""`; otherwise `toString()` | Parsed only to decide whether the row is empty, but the original string value is emitted |
| `xi`/`yi`/`zi`/`xj`/`yj`/`zj` | `null`/missing becomes `""`; otherwise the numeric value is rounded/formatted with `toFixed(0)` for the UI | UI string is converted to a number; blank/invalid becomes the caller-provided `empty` value, defaulting to `null` |

Additional contract details:

- If the top-level `joint` property is absent, `setJointJson` returns without
  changing the service. If it is present, the old state is cleared before all
  cases are loaded.
- A row is omitted during save only when `m` and all six release values are
  blank/non-numeric.
- A case is omitted during save when no rows survive that filter.
- The optional legacy signature is `getJointJson(empty = null,
  targetCase = '')`.
- The Angular file-save path calls the default `empty = null`. Calculation
  calls use `empty = 0`, which is translated to release default `1`; printing
  uses `empty = 1`. Thus blank releases mean connected (`1`) outside the
  persisted file representation.
- Angular's top-level loader clears all services before calling
  `setJointJson`. The current C# `JsonDataOpen` does not perform a global clear,
  so retaining old state when `joint` is absent follows neighboring C# service
  behavior but can retain stale data across sequential opens.

A fitting C# DTO is therefore a public-field internal class containing nullable
`int row`, nullable/string `m`, and nullable `float` release fields. Nullable
`row` allows the loader to distinguish an omitted row from an explicit zero and
then apply the legacy one-based fallback after `JsonToList` returns.

## `DataHelperModule` suitability and limitations

Suitable calls:

- `JsonToDict(jsonData, "joint", converter)` reads the case-ID object.
- `JsonToList<clsJoint>(caseJson)` reads each case array.
- `JsonToClass<clsJoint>` (used by `JsonToList`) populates public fields whose
  names match the JSON keys.
- `ClassToDictionary(row)` produces the row dictionary for serialization.

No `DataHelperModule` extension is needed. Its current conversion rules already
map JSON numbers to nullable `int`/`float`, and map either a JSON string or a
JSON number to `string` through `JsonElement.ToString()`.

The helper is intentionally permissive, which creates these edge cases:

- Unknown JSON fields are ignored.
- A failed scalar conversion is treated like a missing/null value rather than
  throwing.
- A non-array case value makes `JsonToList` return null, and `JsonToDict` skips
  that case.
- An all-default row is dropped by `JsonToClass`; a row containing only `row`
  is retained because `row` is non-default.
- `ClassToDictionary` emits every field, including nulls. This matches the
  legacy file-save default for release fields, but save-side filtering is still
  needed to omit wholly blank rows and cases exactly as the TypeScript service
  does.
- The helper has no array-index context, so the legacy missing-`row` fallback
  must be a small post-processing loop in `setJointJson`.

For consistency with `InputElementsService`, parsed data should be built in a
local variable and assigned to `_joint` only after conversion completes. The
current helper normally returns null instead of throwing on shape/conversion
problems, so this is consistency rather than strict validation.

## Dependencies and downstream consumers

- `InputDataService.JsonDataOpen` is the file-open orchestrator and is the
  natural caller of `InputJointService.Instance.setJointJson(rootElement)`.
- `InputDataService.GetSaveJson` feeds `MenuComponent` file saving and is the
  natural source of `["joint"] = InputJointService.Instance.getJointJson()`.
- `InputLoadService` already has a nullable `joint` selector in each load case;
  preserving case IDs is therefore required for selector references.
- The C# `InputJointComponent` currently has no service dependency, so the new
  state will not yet populate or collect spreadsheet cells. This task can still
  implement JSON persistence in the same incomplete-service style as the other
  current C# inputs, but it does not complete joint editing UI behavior.
- In the legacy web client, `InputDataService` loads and saves this service, and
  the Three.js joint renderer requests one case with `getJointJson(1,
  targetCase)`. That targeted rendering overload has no current C# consumer.
- The Python analysis boundary interprets release values as `0` for released
  and `1` for connected, making the blank-to-`1` calculation behavior
  semantically important if C# later builds analysis requests from this service.

## Validation and test/build pattern

The active `FrameWebforCS` project has no directly associated test project in
the current tree. `FrameWebforCS.v1/tests` belongs to the separate v1 snapshot
and should not be used as evidence for this project.

Recommended proportional checks:

1. Build the narrow project from the repository root:
   `dotnet build FrameWebforCS/FrameWebforCS.csproj --no-restore`.
2. If the integration calls are added, exercise a small JSON document through
   `InputDataService.JsonDataOpen` and `GetSaveJson`, or use a disposable
   reflection harness because the services are internal.
3. Cover at least: multiple case IDs, a missing `row` fallback, numeric and
   string `m`, zero/one releases, null releases, a wholly blank row, an empty
   case, absent top-level `joint`, and load-save-load round-trip.
4. Inspect `git diff --check` and confirm the pre-existing node-service and
   node-component changes are unchanged.

The solution-level fallback is `dotnet build FrameWeb.sln`, but the narrow
project build is the most relevant first gate. The project currently targets
`net10.0-windows` with nullable references and implicit usings enabled.

## Risks and implementation choices

1. **Integration omission:** implementing only `InputJointService.cs` leaves
   the methods unused by file open/save. This is the main functional risk.
2. **Legacy fidelity versus minimal helper mapping:** a bare helper round-trip
   will not add missing row numbers and will retain rows containing only a row
   number. Two small loops are needed for exact legacy behavior.
3. **Optional arguments:** neighboring C# save methods take no arguments, while
   the TypeScript method supports `empty` and `targetCase`. A zero-argument
   method is sufficient for C# file persistence. Preserve the optional behavior
   only if the caller also needs calculation defaults or one-case rendering;
   otherwise it is unused surface area.
4. **Stale state when `joint` is absent:** this matches current C# service
   patterns but differs from the effective Angular whole-file workflow, whose
   caller clears everything first.
5. **Float formatting:** TypeScript rounds loaded release values to integer UI
   strings. Storing C# release fields as `float?` preserves unexpected decimal
   inputs instead of rounding. Releases are semantically zero/one, but existing
   `DataHelperModule` does not enforce that domain constraint.
6. **Serialization shape:** do not model `joint` as a dictionary of row objects;
   each case value is an ordered array/list, and `row` is an explicit property.

## Estimated implementation size

- Literal service-only change: **1 product file**, approximately **70-95 net
  lines** in `InputJointService.cs` including DTO, singleton, state, clear,
  helper-based loader, legacy row fallback, filtering, and writer.
- End-to-end file persistence: **2 product files**, adding approximately **2-4
  more lines** to `InputDataService.cs`.
- `DataHelperModule.cs`: **0 lines expected**.
- Tests: no current in-project test target; a durable new test project would be
  disproportionate to this request, while a temporary reflection smoke test
  can validate the internal service without product changes.

## Files read

- `AGENTS.md`
- `.agents/INDEX.md`
- `.agents/STATE.md`
- `.agents/docs/DESIGN.md`
- `.agents/rules/*.md` selected by the context loader
- `FrameWebforCS/components/input/InputJointService.cs`
- `FrameWebforCS/components/input/InputCombineService.cs`
- `FrameWebforCS/components/input/InputElementsService.cs`
- `FrameWebforCS/components/input/InputMembersService.cs`
- `FrameWebforCS/components/input/InputJointComponent.cs`
- `FrameWebforCS/providers/DataHelperModule.cs`
- `FrameWebforCS/providers/InputDataService.cs`
- `FrameWebforCS/FrameWebforCS.csproj`
- `FrameWebforJS/src/app/components/input/input-joint/input-joint.service.ts`
- Relevant sections of `FrameWebforJS/src/app/providers/input-data.service.ts`
  and `FrameWebforJS/src/app/providers/data-helper.module.ts`

No implementation blocker was found. The only scope decision for the lead is
whether to modify `InputDataService.cs` now for end-to-end persistence or honor
the user's named-file boundary literally and add only the service methods.
