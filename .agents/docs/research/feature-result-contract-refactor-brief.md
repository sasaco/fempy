## Feature Brief: Result Contract Refactor

### Current State
- Architecture: `POST /` returns one canonical `AnalysisResult` by default. An explicit `application/vnd.frameweb.legacy-cases-v1+json` request instead enumerates legacy beam load cases, creates a fresh `FemModel` for each case, solves it, and immediately projects the final state to a bare `{caseId: {disg, reac, fsec, shell_fsec, size}}` map for FrameWebforJS.
- Relevant files: `FrameWeb/main.py`, `FrameWeb/src/fem/legacy_results.py`, `FrameWeb/src/fem/solver_results.py`, `FrameWebforJS/src/app/app.component.ts`, `FrameWebforJS/src/app/providers/result-data.service.ts`, `FrameWebforJS/src/app/components/menu/menu.component.ts`, and their backend/frontend tests.
- Patterns: HTTP representation selection uses the `Accept` header; compressed transport is independent of result representation; each load case is solved with an isolated model; nonlinear step history is nested inside one case-level `AnalysisResult`; FrameWebforJS workers consume the existing display-oriented case map.

### Feature Goal
Replace ambiguous old/new format terminology with explicit, first-class `AnalysisResult`, `AnalysisResultSet`, and `FrameResultSet` contracts. Preserve current clients while separating load-case execution, canonical result serialization, and FrameWebforJS projection into clear responsibilities.

### Scope
- Include: Introduce an ordered `AnalysisResultSet` wire envelope with a `cases` array; introduce an ordered `FrameResultSet` wire envelope; keep default single-case `AnalysisResult` unchanged; retain `legacy-cases-v1` as a deprecated bare-map alias; extract reusable per-case execution and Frame projection; migrate FrameWebforJS HTTP handling to `frame-result-set-v1`; retain a normalizer for saved raw legacy maps; add strict boundary validation and regression tests; document media types, nonlinear step semantics, rate behavior, and beam-only limitations.
- Exclude: FEM algorithms and numeric behavior; redefining compatibility `rate` as a solver load factor; modern-input, shell, or solid multi-case support; expanding nonlinear steps into cases; forced migration of saved result files; worker lifecycle redesign; compression or C# print contracts; immediate removal of `legacy-cases-v1`; a new HTTP endpoint.

### Complexity Classification (from Codex)
- Classification: COMPLEX
- Estimated files: 15-20 product, test, and documentation files
- Estimated LOC: 450-850 lines changed or added
- Implementation route: `team-execute` implementation and review

### Integration Points
- HTTP entry point: `FrameWeb/main.py` deterministically negotiates default, canonical result-set, frame result-set, and deprecated alias media types on the existing `POST /` route.
- Case execution: a fresh `FemModel` is created per requested case; an ephemeral internal `CaseSolution` carries the case ID, canonical `AnalysisResult`, solved model, source metadata, and compatibility rate only long enough to serialize/project that case.
- Canonical result contract: `AnalysisResultSet` preserves request order and case IDs and embeds the complete per-case `AnalysisResult`, including nonlinear `step_results` and `convergence_history` when present.
- Frame projection: `FrameResultSet` and the deprecated alias are sibling representations produced from the internal case solution, not transformations of the `AnalysisResultSet` wire payload.
- Frontend boundary: HTTP responses use the new frame envelope and are normalized to the existing worker input map; saved legacy bare maps continue through a separate compatibility normalizer.

### Risks
- Memory growth from retaining every solved model: stream each ephemeral `CaseSolution` into its requested representation and release the model before solving the next case.
- Case reordering in JavaScript: use an ordered `cases` array for new wire envelopes and never rely on integer-like object-key enumeration order.
- Nonlinear history loss or axis confusion: keep cases at the result-set level and steps inside each case-level `AnalysisResult`; project only the final accepted state to Frame results.
- Accidental numeric changes: keep canonical results unscaled; apply compatibility `rate` exactly once only in Frame projection and preserve the current non-finite-rate fallback behavior in v1.
- Media-type ambiguity: define deterministic handling for unknown versions, `q=0`, and competing FrameWeb vendor types and cover it with HTTP tests.
- Variant validation: validate canonical results by `analysis_type`; explicitly reject unsupported modal Frame projection.
- Compatibility regression: assert byte-equivalent JSON structure for `legacy-cases-v1`, unchanged default single-case behavior, and successful loading of saved raw maps.
- Overstated support: document and test that v1 load-case-set execution remains limited to the existing legacy beam input path.

### Success Criteria
- `AnalysisResultSet` is demonstrably not the old display format: its v1 response is `{kind, schema_version, cases: [{case_id, result}]}` and each `result` is a canonical `AnalysisResult`.
- `FrameResultSet` v1 is `{kind, schema_version, cases: [{case_id, result}]}` where each result contains `disg`, `reac`, `fsec`, `shell_fsec`, and `size`.
- Default `application/json` continues to solve and return one case exactly as before.
- `application/vnd.frameweb.legacy-cases-v1+json` keeps the existing bare case-map body and behavior.
- Case IDs and input order are preserved by both new envelopes, including integer-like IDs.
- A nonlinear case keeps all `step_results` and `convergence_history` inside its canonical `AnalysisResult`; Frame output uses the top-level final accepted state only.
- FrameWebforJS rejects malformed/empty HTTP envelopes instead of treating them as a successful empty result and still loads existing saved raw maps.
- Backend and frontend contract tests, regression tests, type checks, and targeted builds pass.
