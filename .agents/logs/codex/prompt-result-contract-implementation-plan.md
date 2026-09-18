# Result Contract Refactor: Implementation Plan

Work read-only in the FrameWeb3 repository. Create a dependency-ordered implementation plan; do not edit repository files.

Read:

- `.agents/docs/research/feature-result-contract-refactor-brief.md`
- `.agents/docs/research/feature-result-contract-refactor-codebase.md`
- `.agents/logs/codex/20260918T091748Z-result-contract-architecture.md`
- `.agents/logs/codex/20260918T092723Z-result-contract-complexity-correction.md`
- `.agents/docs/DESIGN.md`

Inspect source and tests as needed. Complexity is `COMPLEX` and the implementation route is `team-execute`.

## Constraints

- Order steps by dependency and make each step independently testable.
- Use test-first steps where practical.
- Preserve the default single-case response and deprecated alias body.
- Extract ephemeral per-case execution without retaining all models.
- Add both new media types and ordered array envelopes.
- Make `Accept` negotiation a tested pure boundary.
- Preserve case order through Angular and workers, including integer-like IDs.
- Keep saved legacy raw maps readable.
- Keep load cases and nonlinear steps as independent axes.
- Do not expand scope into solver changes, new endpoints, shell/solid multi-case support, or saved-file migration.
- Account for the repository's current tests/build tools rather than inventing commands.
- Propose work packages with non-overlapping file ownership suitable for parallel `team-execute` implementation.

## Required response

Use these exact headings:

1. Implementation Steps
2. File Changes by Step
3. Test Plan by Step
4. Dependencies Between Steps
5. Parallel Work Packages
6. Integration Sequence
7. Verification Commands
8. Estimated Effort per Step
9. Rollback and Compatibility Checks
10. Verdict (`READY` or `NEEDS_REVISION`)

For every step, specify concrete files, change type, expected behavior, and completion evidence. Flag any hidden coupling that would make the proposed parallel ownership unsafe.
