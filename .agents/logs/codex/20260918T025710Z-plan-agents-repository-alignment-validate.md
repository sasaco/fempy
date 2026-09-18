## TL;DR

**Verdict: NEEDS_REVISION**

The plan has the correct document shape and broadly accurate repository diagnosis, but it is not yet safe or dependency-complete enough to execute.

## Analysis

1. **Three implementation-blocking decisions remain unresolved.** Main Agent ownership, multi-runtime retention, and Windows Claude discovery are explicitly “required before implementation” but remain in Open Questions. They must be resolved—or represented as an explicit stop/approval gate—before Step 2. [plan](/C:/Users/sasai/Documents/FrameWeb3/.agents/docs/plans/agents-repository-alignment.md:122)

2. **Step 2 depends on Step 3.** Step 2 verifies runtime discovery through `detect_stack.py`, but the current detector only accepts Unix-style symlinks. Supporting another Windows discovery method requires modifying that detector in Step 3 first. [plan](/C:/Users/sasai/Documents/FrameWeb3/.agents/docs/plans/agents-repository-alignment.md:37), [detect_stack.py](/C:/Users/sasai/Documents/FrameWeb3/.agents/skills/init/detect_stack.py:306)

3. **The destructive document migration is underspecified.** Step 5 promises hash-guarded removal of copied STATE/DESIGN content using “modified detector/writer” code, but no writer modification is scoped. Existing writers can replace Repository Identity or append typed design entries; they cannot safely remove all specified foreign blocks. [plan](/C:/Users/sasai/Documents/FrameWeb3/.agents/docs/plans/agents-repository-alignment.md:61), [writer contract](/C:/Users/sasai/Documents/FrameWeb3/.agents/skills/_shared/README.md:170)

4. **Nested discovery needs explicit exclusions.** Broad `**/*.csproj` and nested manifest discovery would encounter generated `obj` projects, vendored `rxfire`/`paramquery` packages, and `tools/local-tools/package.json`. The plan needs inclusion boundaries or exclusions for `obj`, `bin`, `.venv`, `node_modules`, and vendored packages.

5. **Final verification can produce false confidence.**

   - `git diff --name-only -- …` neither fails when output is non-empty nor covers staged and untracked files.
   - Step 8 does not run the proposed `.agents/tests`.
   - “Gates for which an environment exists” lacks a required/optional gate matrix.
   - The shared runtime documentation references a missing `test_shared_script_contract.py`, which the plan does not explicitly restore or remove. [plan](/C:/Users/sasai/Documents/FrameWeb3/.agents/docs/plans/agents-repository-alignment.md:96), [shared contract](/C:/Users/sasai/Documents/FrameWeb3/.agents/skills/_shared/README.md:149)

## Plan

Required revision:

1. Resolve the three blocking runtime decisions before execution.
2. Move discovery-contract changes ahead of bootstrap verification.
3. Add a dedicated dry-run, atomic, hash-guarded STATE/DESIGN migration mechanism and tests.
4. Define exact manifest/project inclusion and exclusion rules.
5. Add executable agent-test, link/reference, staged/untracked scope, and mandatory-environment gates.

## Patch Strategy

No files were modified.

## Validation

The plan’s structural contract passes. Its main factual baseline was confirmed: empty `AGENTS.md`, pseudo-link Claude files, missing `PROGRESS.md` and settings, foreign STATE/DESIGN content, empty-workflow false detection, root-only manifest detection, and twelve cached `.pyc` files.

## Risks

Executing the current plan risks deleting concurrent state, validating Windows discovery before it is supported, treating vendored/generated projects as components, and reporting success while agent tooling or scope-isolation checks have not actually passed.
