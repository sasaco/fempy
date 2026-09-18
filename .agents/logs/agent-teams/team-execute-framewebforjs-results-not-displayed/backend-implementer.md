# Work Log: backend-implementer

## Summary

Implemented the explicit `legacy-cases-v1` backend representation for FrameWebforJS while preserving the default flat API and the pre-existing compressed-transport edits. The compatibility path solves every original legacy load case independently, projects the old result shape, and returns no partial case map on failure.

## Tasks Completed

- Added exact `Accept: application/vnd.frameweb.legacy-cases-v1+json` negotiation and 406 rejection for unknown legacy vendor versions.
- Added legacy beam input validation, per-case isolated solving with a fresh `FemModel`, case-aware diagnostics, and atomic response construction.
- Added projection of `disg`, `reac`, `fsec`, `shell_fsec`, and `size`, including rate application, 2D reaction behavior, generated-point filtering, legacy end-force signs, and all-case mesh subdivisions.
- Documented the opt-in representation, supported input scope, response fields, units, failure behavior, and independence from compressed transport.
- Ran focused compatibility, existing HTTP, compressed transport, projection, and static checks.

## Files Modified

- `FrameWeb/main.py` — compatibility negotiation/transport integration; retained unrelated compressed-transport edits already present.
- `FrameWeb/src/fem/legacy_results.py` — new legacy case orchestration and projection module.
- `FrameWeb/docs/wiki/endpoints.md` — compatibility representation documentation; retained existing transport documentation edits.
- `.agents/logs/agent-teams/team-execute-framewebforjs-results-not-displayed/backend-implementer.md` — this work log.

## Key Decisions

- Kept the no-selector execution and serialization path unchanged so the flat API remains the default.
- Required the exact v1 media type and rejected unknown `application/vnd.frameweb.legacy-cases-*` versions instead of silently falling back.
- Limited v1 to legacy beam input; modern `nodes` and shell/solid input fail explicitly.
- Preserved original `load` insertion order, used `select_case` for each key, and created a fresh model for every solve.
- Used `_all_member_loads` behavior in `select_case` so every case has the same all-case split mesh while exposing labels only for original nodes and active generated points.
- Applied case `rate` only to displacement, reaction, and force components after solving; did not scale `L` or `size`.
- Kept the projection in production code and did not import test-only helpers.

## Communication with Teammates

- Shared the public helper name `solve_legacy_cases` and expected 400/406 error behavior with the backend tester.
- Notified the root and tester when production implementation landed and after the final formatting-only cleanup.
- The backend tester independently confirmed 8/8 focused compatibility tests and 29/29 combined compatibility plus existing HTTP tests on the final production tree.

## Issues Encountered

- The required command `uv run --project FrameWeb --locked --extra dev ruff check FrameWeb/main.py FrameWeb/src/fem/legacy_results.py` exits 1 because the project dev environment has no `ruff` executable (`Failed to spawn: ruff`). `uv tool run ruff check FrameWeb/src/fem/legacy_results.py` and its format check both exit 0; the new module is clean. The existing `main.py` has nine known baseline findings unrelated to this change.
- `bash .agents/skills/_shared/verify.sh` exits 1 before the script runs because Windows resolves `bash` through WSL and `/bin/bash` is unavailable. No verification JSON or overall result was emitted, so this is reported as unavailable rather than passed.
- `git diff --check` exits 0; Git only reports expected LF-to-CRLF conversion warnings for tracked Windows working-copy files.

## Security Review Fix

- Resolved SEC-1 by exposing `MAX_LEGACY_CASES = 256` and rejecting larger `load` maps during legacy input validation, before `select_case`, request deep-copying, `FemModel` construction, or solving.
- Documented the 256-case request limit and the pre-solve HTTP 400 `invalid_input` response for 257 or more cases.
- Added boundary coverage for 255 and 256 cases without executing solves, plus an HTTP test proving that 257 cases return 400 and never instantiate `FemModel`.
- Focused legacy API gate: 11 passed. Combined legacy API and existing HTTP gate: 32 passed. Auxiliary Ruff on the changed Python module and test: all checks passed.
