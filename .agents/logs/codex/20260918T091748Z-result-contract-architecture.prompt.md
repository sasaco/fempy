# Result Contract Refactor: Architecture Design

Work read-only in the FrameWeb3 repository. Produce a concrete architecture design for the approved feature direction, but do not edit repository files.

Read these artifacts first:

- `.agents/docs/research/feature-result-contract-refactor-brief.md`
- `.agents/docs/research/feature-result-contract-refactor-codebase.md`
- `.agents/logs/codex/20260918T090903Z-result-contract-scope.md`
- `.agents/docs/DESIGN.md`

Inspect the relevant backend/frontend source and tests as needed.

## Required constraints

- Default `application/json` remains the current single-case `AnalysisResult`.
- `AnalysisResultSet` is the ordered canonical multi-case contract, not the old UI format.
- `FrameResultSet` is a sibling representation projected from the same internal per-case solution, not a direct conversion of the `AnalysisResultSet` wire payload.
- Load cases and nonlinear steps are independent axes; each nonlinear case result retains its own `step_results` and `convergence_history`.
- Use the existing `POST /` with deterministic `Accept` negotiation.
- New envelopes use an ordered `cases` array.
- The legacy media type keeps its existing bare map body unchanged as a deprecated alias.
- Do not retain all solved `FemModel` instances. Use an ephemeral per-case internal representation.
- Canonical results are unscaled. Compatibility `rate` is applied exactly once only to frame projections; v1 preserves the existing non-finite fallback to 1.0.
- Frame projection v1 remains legacy-beam-only and rejects modal output explicitly.
- FrameWebforJS HTTP uses the new envelope but workers continue receiving the existing internal case map after normalization.
- Saved raw legacy result maps remain readable through a separate compatibility normalizer.

## Questions to resolve

1. Exact Python module boundaries, public functions/types, and dependency direction.
2. Exact TypeScript contract types and normalization/validation boundaries.
3. Exact JSON schemas for `AnalysisResultSet v1` and `FrameResultSet v1`, including version field type/value and case ID type.
4. Deterministic `Accept` selection rules for exact types, parameters, `q=0`, wildcards, unknown versions, and conflicting supported vendor types.
5. Atomicity behavior when one case fails.
6. How to keep the legacy alias body equivalent without duplicating solve/project logic.
7. How to prevent materializing all models while still building one JSON response.
8. Test architecture and migration order.

## Required response

Use these exact headings:

1. Architecture Summary
2. Backend Components and APIs
3. Frontend Components and APIs
4. Wire Contracts
5. Accept Negotiation Rules
6. Data Flow
7. Failure and Compatibility Semantics
8. Test Strategy
9. File-Level Change Map
10. Open Decisions
11. Verdict (`APPROVED` or `NEEDS_REVISION`)

Prefer the smallest maintainable design. Flag any brief requirement that is internally inconsistent or unsafe.
