# Result Contract Refactor Implementation Plan

## Purpose

Replace ambiguous old/new result terminology with explicit `AnalysisResult`, `AnalysisResultSet`, and `FrameResultSet` contracts while preserving current single-case clients, FrameWebforJS behavior, and saved result files. The refactor separates representation negotiation, per-case execution, canonical serialization, and display projection without changing FEM algorithms or numeric solver behavior.

## Scope

- Include: contract freeze, deterministic `Accept` negotiation, ephemeral legacy-beam case execution, canonical and Frame result-set envelopes, deprecated alias preservation, strict frontend normalization, explicit case-order propagation, tests, and documentation.
- Exclude: new endpoints, solver changes, modern-input/shell/solid multi-case support, nonlinear-step flattening, saved-file migration, general worker lifecycle redesign, and alias removal.

## Implementation Steps

0. Freeze the wire contract and align `DESIGN.md` before parallel implementation starts.
1. Freeze existing behavior with characterization tests for the default single-case response and the `legacy-cases-v1` bare map.
2. Establish an Angular test baseline that executes at least one real spec.
3. Add backend contract definitions and a pure, deterministic `Accept` negotiator.
4. Extract legacy-beam case enumeration and fresh-model execution into an ephemeral per-case `CaseSolution` flow.
5. Extract Frame projection and make the existing legacy solver a thin compatibility adapter.
6. Add `AnalysisResultSet v1` and `FrameResultSet v1` envelopes and dispatch them from the existing `POST /` route.
7. Add strict TypeScript contracts and separate normalizers for HTTP envelopes and saved raw legacy maps.
8. Propagate explicit ordered `caseIds` through result services and workers, then move the HTTP client to `frame-result-set-v1`.
9. Complete documentation, full regression tests, build checks, and Ct case 1/case 11 browser verification.

## 2. File Changes by Step

### Step 0: Contract freeze and design alignment

- Modify `.agents/docs/DESIGN.md` through the design-tracker workflow before implementation is delegated.
- Freeze the exact v1 envelopes, sibling-representation rule, case/step axes, legacy-beam-only scope, and the following compatibility rules:
  - exact supported vendor types only; wildcards select the default representation and never trigger multi-case work;
  - `q=0` excludes a candidate, malformed quality values and positive-q unknown FrameWeb vendor versions return 406;
  - highest quality wins, explicit vendor wins a tie with default, and a tie between supported vendor types returns 406;
  - `AnalysisResultSet` values remain unscaled;
  - Frame/legacy results apply `rate` exactly once and preserve the current non-finite/invalid fallback to `1.0` in v1;
  - `AnalysisResultSet` may carry modal canonical results, while Frame projection rejects modal; the deprecated alias preserves its current modal error behavior;
  - new envelopes are atomic ordered arrays, while the alias remains the current bare map.
- Completion evidence: reviewed design diff and a frozen contract table shared by backend/frontend work packages.

### Step 1: Characterization

- Modify `FrameWeb/tests/io/test_legacy_cases_api.py`: freeze body shape, Content-Type, rate behavior, order limitations, atomic failure, and case-count boundaries.
- Modify `FrameWeb/tests/io/test_http.py`: freeze default `application/json` behavior and transport independence.

### Step 2: Angular test baseline

- Confirm the configured test entry points and run the existing suite before frontend changes.
- Require the runner to report at least one executed spec; a zero-spec success is a failure.
- If repository-owned test configuration is directly broken, make only the minimal blocking correction within the frontend work package and record it separately from contract changes.
- Completion evidence: a captured successful ChromeHeadless run with a nonzero spec count.

### Step 3: Backend contract boundary

- Add `FrameWeb/src/fem/result_contracts.py`: media-type constants, representation enum/type, v1 TypedDicts, result-variant validation, and pure Accept selection.
- Add focused backend tests in `FrameWeb/tests/io/test_result_set_api.py`: exact media types, parameters, wildcard behavior, unknown versions, invalid/zero quality values, and ambiguous supported vendor types.

### Step 4: Ephemeral case execution

- Add `FrameWeb/src/fem/result_sets.py`: legacy-beam case validation/enumeration, 1-256 limit, fresh `FemModel` per case, and ephemeral `CaseSolution` iteration.
- Keep one solved model alive at a time. Accumulate only requested wire results, never a collection of solved models.
- Preserve source case IDs as strings and input order.

### Step 5: Frame projection and compatibility adapter

- Add `FrameWeb/src/fem/frame_results.py`: final-state `FrameCaseResult` projection, exactly-once compatibility rate scaling, Frame envelope construction, and legacy bare-map construction.
- Modify `FrameWeb/src/fem/legacy_results.py`: retain `solve_legacy_cases()` as a deprecated thin adapter over the shared execution/projector path.
- Extend legacy tests to assert structural and numeric equivalence.

### Step 6: HTTP representations

- Modify `FrameWeb/main.py`: dispatch default `AnalysisResult`, `AnalysisResultSet v1`, `FrameResultSet v1`, and the deprecated alias after deterministic negotiation.
- Add `FrameWeb/tests/io/test_result_set_api.py`: ordered array envelopes, exact Content-Type, canonical unscaled values, Frame scaling, nonlinear history nesting, modal projection rejection, compressed/uncompressed parity, and all-or-nothing failure.
- Add or update nonlinear/model contract tests only where needed to prove that step history remains inside each case result.

### Step 7: Frontend contract boundary

- Add `FrameWebforJS/src/app/providers/result-contracts.ts`: v1 interfaces, strict HTTP envelope normalization, saved-map compatibility normalization, and `{caseIds, byId}` output.
- Add `FrameWebforJS/src/app/providers/result-contracts.spec.ts`: kind/version/field/numeric validation, empty results, duplicate/missing/extra IDs, and integer-like order tests.
- Modify `FrameWebforJS/src/app/providers/result-data.service.ts` and its spec: accept normalized data and prevent worker startup after validation failure.
- Keep the normalized/saveable `byId` data immutable from downstream processing. Give reaction processing its own mutable deep-enough copy so its in-place adjustments cannot alter the map later written to saved results.

### Step 8: Ordered frontend flow and client migration

- Modify `FrameWebforJS/src/app/app.component.ts`: request `application/vnd.frameweb.frame-result-set-v1+json`, strictly normalize it, and dispatch only valid results.
- Modify the displacement, reaction, and section-force result services and their three workers: carry `caseIds` explicitly instead of deriving case order with `Object.keys()`.
- Modify the saved-result load path only as required to call the separate raw-map normalizer; continue saving the compatible raw `byId` map.
- Add service/worker tests proving that `"2", "1", "01", "a"` retains that order.

### Step 9: Documentation and integration

- Modify `FrameWeb/docs/wiki/endpoints.md` and `FrameWeb/docs/wiki/results.md`: formal names, media types, envelopes, case/step axes, rate semantics, atomicity, and legacy-beam-only scope.
- Run backend, frontend, and relevant .NET/build gates; verify Ct case 1 and case 11 in the browser.

## 3. Test Plan by Step

- Step 0: Review the exact contract table against backend and frontend type definitions before code work starts.
- Step 1: Existing fixtures lock down all externally visible behavior before extraction, including modal alias behavior and non-finite `rate` fallback.
- Step 2: The Angular runner must execute a nonzero number of existing specs before new frontend tests are accepted.
- Step 3: Table-driven unit tests cover every Accept decision without invoking body decode or model construction.
- Step 4: Spy/fake model tests prove pre-validation, fresh instances, input immutability, order preservation, and no retained model collection.
- Step 5: Golden/structural comparisons prove the deprecated alias is unchanged; numeric fixtures distinguish unscaled canonical output from exactly-once Frame scaling.
- Step 6: HTTP contract tests cover both new envelopes, nonlinear histories per case, final-state-only Frame projection, unsupported modal projection, and partial-failure rejection.
- Step 7: TypeScript unit tests reject malformed or empty HTTP success payloads, prove reaction mutation cannot affect saveable data, and accept existing saved raw maps through only the compatibility path.
- Step 8: Service/worker tests prove explicit case order through worker output and UI case lists.
- Step 9: Full regression suites, production build, and Ct browser checks cover integration.

## 4. Dependencies Between Steps

```text
Step 0 -> Step 1 -> Step 3 -> Step 4 -> Step 5 -> Step 6
    \      \
     \      -> backend implementation
      -> Step 2 -> Step 7 -> Step 8 -> frontend implementation
Step 6 + Step 8 -> Step 9
```

- Contract names, wire envelopes, and compatibility behavior are frozen in Step 0 before backend/frontend implementation begins.
- Frontend normalizers can be built in parallel with backend execution/projection after the contract freeze.
- HTTP client migration waits for both the backend Frame envelope and frontend strict normalizer.
- Full integration waits for both product paths.

## 5. Parallel Work Packages

- Backend package owns only `FrameWeb/` product code, backend tests, and backend wiki files after Step 0.
- Frontend package owns only `FrameWebforJS/` product code and frontend tests.
- Lead/integration package owns `.agents/docs/DESIGN.md`, cross-layer contract comparison, final merges, full gates, and browser verification.
- Shared contract examples are copied from this plan; implementation agents do not edit each other's directories.
- If a frontend test-runner defect blocks relevant specs, fix only the directly blocking configuration in the frontend package and document it separately; do not broaden into general build cleanup.

## 6. Integration Sequence

1. Apply and review the design/contract freeze.
2. Establish backend characterization and the nonzero Angular spec baseline.
3. Integrate backend execution/projection extraction while keeping the legacy alias green.
4. Integrate new backend media types.
5. Integrate frontend contract normalizers, immutable save data, and order propagation.
6. Switch the HTTP client to `frame-result-set-v1`.
7. Run cross-layer contract fixtures and full suites.
8. Perform Ct browser verification and documentation review.

## 7. Verification Commands

Use repository-provided commands discovered at implementation time. Expected primary gates are:

```powershell
uv run --project FrameWeb --locked --extra dev python -m pytest FrameWeb/tests -q
npm --prefix FrameWebforJS run test -- --watch=false --browsers=ChromeHeadless
npm --prefix FrameWebforJS run build
dotnet build FrameWeb.sln
```

Run narrower test files after each step before these full gates. Run `.agents/check.ps1` after design/state artifacts are updated. Browser verification uses the existing local startup path documented by the repository.

## 8. Estimated Effort per Step

| Step | Relative effort |
|---|---:|
| 0. Contract/design freeze | Small |
| 1. Characterization | Small |
| 2. Angular test baseline | Small |
| 3. Contract/negotiation boundary | Medium |
| 4. Ephemeral case execution | Medium |
| 5. Frame projection/adapter | Medium |
| 6. HTTP representations | Medium |
| 7. Frontend normalizers | Medium |
| 8. Order propagation/client migration | Large |
| 9. Documentation/integration/E2E | Medium |

Overall complexity is `COMPLEX` because the change crosses more than five product/test/document files and several runtime boundaries, not because solver algorithms change.

## 9. Rollback and Compatibility Checks

- The default `application/json` path remains untouched and is the primary compatibility anchor.
- The deprecated alias retains its bare-map body and can remain the FrameWebforJS request target if frontend migration must be rolled back.
- New modules are additive; the legacy adapter stays available during migration.
- Saved result files remain raw maps and require no conversion.
- No database or destructive data migration exists.
- A rollback removes the two new vendor selections and restores the frontend Accept header without changing solver code or saved data.

## Risks & Considerations

- Integer-like case IDs can reorder when converted to object keys; `caseIds` remains the only ordering authority throughout the frontend.
- `AnalysisResultSet` responses may be large because nonlinear histories are canonical response data; only solved models are streamed/discarded, not the requested result payload.
- Reaction processing currently mutates data; it must never receive the same object graph used for saving.
- Media-type selection changes computation cardinality, so negotiation must finish before body decode/model creation and unknown vendor versions must fail explicitly.
- Frame projection still depends on solved-model/source context and remains legacy-beam-only; documentation must not imply general shell/solid support.
- The repository is already dirty in unrelated `.agents` areas; implementation must preserve all unrelated changes and use strict file ownership.

## Open Questions

None block v1 implementation. Alias sunset timing, a possible future endpoint, modern-input/shell/solid multi-case support, `rate` redesign, worker lifecycle redesign, and saved-file envelope migration are explicitly deferred.

## 10. Verdict

`READY`, subject to the mandatory independent validation gate and user approval before implementation.
