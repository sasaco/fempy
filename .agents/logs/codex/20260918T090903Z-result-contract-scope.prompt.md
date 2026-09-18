# Result Contract Refactor: Scope and Impact Review

You are reviewing a proposed refactor in the FrameWeb3 repository. Work read-only. Inspect the referenced files and current source as needed.

## User intent

The user wants to stop calling the contracts "old format" and "new format", clarify whether `AnalysisResultSet` is the old format, and cleanly refactor the implementation into formally named capabilities without breaking existing consumers.

## Established terminology

- `AnalysisResult`: canonical result of one analysis case. For nonlinear analysis, one `AnalysisResult` may contain multiple `step_results` plus `convergence_history`; its top-level fields represent the final accepted state.
- `AnalysisResultSet`: ordered collection of per-case canonical `AnalysisResult` values. This is not the old UI format.
- `FrameResultSet`: FrameWebforJS-oriented projection with `disg`, `reac`, `fsec`, `shell_fsec`, and `size`. This is the currently deployed case-map body previously called the legacy/old format.
- Load cases and nonlinear steps are independent axes: a result set contains cases, and each nonlinear case result may contain steps.

## Compatibility constraints

- Keep the default `application/json` single-case `AnalysisResult` behavior unchanged.
- Add/retain explicit media types for the two first-class multi-case representations:
  - `application/vnd.frameweb.analysis-result-set-v1+json`
  - `application/vnd.frameweb.frame-result-set-v1+json`
- Keep `application/vnd.frameweb.legacy-cases-v1+json` temporarily as a deprecated alias with its response body unchanged.
- Preserve case input order and case IDs.
- Each load case must be solved with an isolated `FemModel`.
- `AnalysisResultSet` must preserve canonical solver values; compatibility `rate` scaling belongs only to the frame projection.
- The FrameWebforJS projection is final-state-only for nonlinear results; step histories remain in each canonical `AnalysisResult`.
- Existing saved FrameWebforJS result files may still contain the raw legacy case map and must remain readable.

## Existing investigation

Read:

- `.agents/docs/research/feature-result-contract-refactor-codebase.md`
- `.agents/docs/DESIGN.md` (especially Result Contracts and decisions)
- `FrameWeb/main.py`
- `FrameWeb/src/fem/legacy_results.py`
- `FrameWeb/src/fem/solver_results.py`
- `FrameWebforJS/src/app/app.component.ts`
- `FrameWebforJS/src/app/providers/result-data.service.ts`
- relevant tests found by search

The codebase scan found that the current multi-case path solves each case and immediately projects it to the UI map. It recommends an internal, non-wire `CaseSolutionSet` retaining both canonical `AnalysisResult` and solved model/projection context, then separate serializers/projectors for `AnalysisResultSet` and `FrameResultSet`. It also recommends an ordered `cases` array for the canonical wire set rather than numeric-like top-level object keys, because JavaScript enumeration can reorder integer-like keys.

## Required response

Return a concise but concrete engineering review with these exact headings:

1. Scope
2. Complexity Classification (`SIMPLE`, `MODERATE`, or `COMPLEX`, with rationale)
3. Integration Points
4. Affected Files
5. Risks and Edge Cases
6. Recommended Architecture
7. Explicit Non-Goals
8. Verdict (`PROCEED`, `NEEDS_REVISION`, or `STOP`)

Resolve these questions explicitly:

- Is the terminology and containment model correct?
- Should the existing POST `/` plus `Accept` negotiation remain, or is a separate endpoint necessary for correctness?
- Is an internal `CaseSolutionSet` justified given that Frame projection needs the solved model and source metadata?
- What exact wire envelope should `AnalysisResultSet` use?
- How should `FrameResultSet` be named and exposed while preserving the legacy body?
- What is the smallest clean staged implementation?
