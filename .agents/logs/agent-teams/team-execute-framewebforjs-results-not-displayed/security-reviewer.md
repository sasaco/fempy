# Work Log: security-reviewer

## Summary

Reviewed the legacy-cases-v1 backend/frontend compatibility change for trust
boundaries, availability abuse, negotiation, validation, atomicity, shell/solid
scope, and information disclosure. Verdict is BLOCK because the new public path
performs an unbounded caller-selected number of full FEM solves.

## Tasks Completed

- Traced exact and unknown-version `Accept` handling through success and error
  responses.
- Traced compressed request decoding through legacy input validation and
  per-case solving.
- Reviewed load-case cardinality, `select_case()` copying, result accumulation,
  model isolation, and late-failure atomicity.
- Reviewed shell/solid rejection and the beam-only `shell_fsec` contract.
- Reviewed frontend response validation before state commit and worker dispatch.
- Checked public diagnostics for implementation-detail or secret leakage.
- Wrote the detailed security report with severity and file:line evidence.

## Files Modified

- `.agents/docs/research/review-security-framewebforjs-results-not-displayed.md`
  — security findings and ship recommendation.
- `.agents/logs/agent-teams/team-execute-framewebforjs-results-not-displayed/security-reviewer.md`
  — this work log.

No product, test, or API documentation file was modified.

## Key Decisions

- Rated unbounded case fan-out High because it is newly introduced, performs a
  full solve per caller-controlled entry, deep-copies the whole request per
  case, retains atomic results, and is reachable through an application that
  documents no authentication or rate limiting.
- Rated unbounded gzip expansion Medium and explicitly inherited because it
  predates this representation, while still composing with the new route.
- Treated exact-version fail-closed behavior, fresh-model isolation,
  shell/solid rejection, sanitized unexpected errors, and response atomicity as
  verified controls.
- Did not treat shallow numeric-leaf validation as a security blocker; retained
  the planned browser/non-empty-result acceptance gate as a correctness note.

## Communication with Teammates

- Reported one High blocking availability finding for lead remediation and
  re-review.
- Preserved the supplied verification evidence (139 backend tests and both
  frontend TypeScript checks) without claiming it covers resource budgets.

## Issues Encountered

- One read-only `rg` command initially failed because PowerShell does not use
  shell-style backslash escaping for embedded double quotes. The search was
  rerun with single-quoted regex arguments; no review scope or result was lost.
- No implementation or test changes were made, per review-only ownership.

## Re-review

- Re-reviewed only the prior High load-case fan-out blocker after remediation.
- Confirmed `MAX_LEGACY_CASES = 256` is enforced in the mandatory validator
  before the case loop, `select_case()`, `FemModel` construction, or solving.
- Confirmed boundary coverage for 255/256 acceptance and 257 HTTP 400 rejection;
  the overflow test proves zero `FemModel` construction.
- Independently reran the focused legacy-case suite: **11 passed in 0.35s**.
- Accepted lead evidence that the broader backend selection is **142 passed**
  and ruff passes on changed files.
- Updated verdict: prior High is **RESOLVED** and no open Critical or High
  security issue remains. Existing Medium/Low follow-ups stay non-blocking.
- No implementation, test, or endpoint-documentation file was modified during
  re-review.
