# Work Log: backend-tester
## Summary
Added focused backend contract coverage for the explicit `legacy-cases-v1` representation while pinning the existing flat response as the default. Verified the contract red before implementation and green after the backend implementation landed.
## Tasks Completed
- [x] Reviewed diagnosis and historical contracts: confirmed the flat/case-map mismatch and first-case reduction.
- [x] Added an ordered two-case real beam fixture with distinct case references, a non-unit output rate, and a notice-point split.
- [x] Covered default flat behavior, exact media-type selection, ordered complete projections, unsupported selectors, repeatability/input immutability, and atomic failure.
- [x] Independently validated displacement, reaction, and section-force expectations before the production implementation landed.
- [x] Ran focused, combined HTTP, lint, and shared verification commands and recorded exact outcomes.
## Files Modified
- `FrameWeb/tests/io/test_legacy_cases_api.py`: Added eight focused integration cases for response negotiation and legacy projection semantics.
## Key Decisions
- Sent raw JSON in HTTP tests because Flask's `json=` helper sorts object keys and would invalidate insertion-order and later-case atomicity coverage.
- Used a two-metre axial beam split at 0.5 m so node labels, metres/radians, reaction zero-fill/signs, `P1..Pn`, interval lengths, `size`, and `rate=2.5` remain hand-checkable.
- Did not test the raw Ct preset because its frontend-only `m1`/`m2` normalization would make a backend test misleading; no suitable normalized Ct fixture exists in the backend suite.
## Communication with Teammates
- → backend-implementer: Shared the exact test contract, fixture design, red evidence, and raw-JSON ordering caveat.
- ← backend-implementer: Confirmed the public `solve_legacy_cases` helper and expected 406/400 status contracts, then reported the production implementation had landed.
## Issues Encountered
- Red gate before implementation: exit 1, 6 failed and 2 passed, as expected for the missing compatibility route.
- Exact project-env ruff command: exit 1 because the locked `dev` extra has no `ruff` executable; `uvx ruff check FrameWeb/tests/io/test_legacy_cases_api.py` passed with exit 0.
- Shared `verify.sh`: exit 1 because WSL could not execute `/bin/bash` on this Windows host.
- Green gates: focused 8 passed (exit 0); combined `test_http.py` plus new contract 29 passed (exit 0).
