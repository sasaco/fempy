The cleanup is decomposed into 10 dependency-ordered stages. No repository files were changed. The intended artifact is `.agents/docs/plans/agents-framework-cleanup.md`, but this read-only session could not create or validate it on disk.

## Implementation Plan: Agents Framework Cleanup

### Purpose

Replace copied, repository-specific agent state with a safe framework aligned to FrameWeb3’s real Windows monorepo: Python/uv under `FrameWeb/`, Angular/npm under `FrameWebforJS/`, and .NET projects in the root solution.

Evidence: [README.md](C:/Users/sasai/Documents/FrameWeb3/README.md:1), [FrameWeb/pyproject.toml](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/pyproject.toml:1), [FrameWebforJS/package.json](C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/package.json:1), [FrameWeb.sln](C:/Users/sasai/Documents/FrameWeb3/FrameWeb.sln:5).

### Scope

- In scope: `.agents/**`, `AGENTS.md`, `CLAUDE.md`, `.claude/**`, `.codex/config.toml`, `PROGRESS.md`.
- New artifacts: focused `.agents/tests/`, `PROGRESS.md`, and a hash-guarded state-migration mechanism.
- Conditional: Windows-native checker/runtime configuration and removal of unused optional workflows.
- Out of scope: product behavior or dependencies under `FrameWeb/`, `FrameWebforJS/`, `FramePrintPDF/`, `FrameGConverter/`, and `tools/`.
- No dependency upgrades or global CLI updates.

### Implementation Steps

#### Step 1: Freeze the baseline and resolve bootstrap decisions

- [ ] Record HEAD, tracked changes, every untracked path, and hashes for artifacts that must survive cleanup.
- [ ] Explicitly preserve the current troubleshooting research, reproduction script, and Codex logs until individually classified.
- [ ] Decide the primary runtime, retained multi-runtime support, Windows discovery/link strategy, and `.codex` permission policy.
- [ ] Never revert commit `2ee50f4` wholesale; it includes real product changes.

**Verification:** Re-running `git status --short --untracked-files=all`, `git diff`, and the preservation-manifest hash check produces the same baseline before editing.

#### Step 2: Repair the root bootstrap contract

Files: `AGENTS.md`, `CLAUDE.md`, `.claude/agents`, `.claude/skills`, optional `.claude/settings.json`, `.codex/config.toml`.

- [ ] Populate the currently empty `AGENTS.md` with the required language protocol, repository boundaries, component working directories, and supported commands.
- [ ] Implement the selected Claude discovery strategy: real links or an explicitly supported regular-file/config alternative.
- [ ] Remove the missing `TEMPLATE_DESIGN_LOG.md` reference.
- [ ] Replace implicit `danger-full-access` policy with the approved explicit policy.
- [ ] Defer `.agents/INDEX.md` until optional-framework curation is complete.

**Verification:** Required headings exist; every referenced path resolves; Git modes match the chosen discovery method; no unapproved global-update or permission policy remains.

#### Step 3: Make repository detection monorepo-aware

Files: `.agents/skills/init/detect_stack.py` plus new focused tests under `.agents/tests/`.

- [ ] Detect nested `FrameWeb/pyproject.toml` and `FrameWebforJS/package.json`.
- [ ] Detect `.sln` and `.csproj` projects.
- [ ] Report uv, npm, and dotnet independently.
- [ ] Treat GitHub Actions as present only when workflow files exist, not merely an empty directory.
- [ ] Validate the chosen runtime-discovery representation.

**Verification:** Fixture tests cover positive, absent, and malformed layouts. Running the detector against FrameWeb3 reports Python/uv, Angular/npm, and .NET without the current false CI result.

#### Step 4: Align portable rules and tooling in four separate gates

1. **Rules and documented commands**

   Adapt `.agents/rules/coding-principles.md`, `dev-environment.md`, `testing.md`, `security.md`, and `language.md`. Remove unconditional root-level Ruff, ty, marimo, poe, and Unix activation assumptions.

2. **Collectors**

   Adapt `.agents/skills/catchup/collect_repo_state.py` and `.agents/skills/update-lib-docs/lib_inventory.py` to inventory nested manifests and .NET projects.

3. **Checkers and test runners**

   Adapt `.agents/check.sh`, `.agents/skills/_shared/verify.sh`, and `run_tests.py`, or introduce a Windows-native/cross-platform entry point. Keep logs under the root `.agents/logs`, even when a component working directory is used.

4. **Hook behavior**

   Adapt `.agents/hooks/lint-on-save.py` so it never runs undeclared Ruff/ty commands. Keep hooks inactive until their exact command passes independently.

**Verification:** Give each unit its own standard-library test file. Test success, command failure, missing-tool, and “no gates found” behavior separately; then run an actual-repository dry-run that lists the three component gate families.

#### Step 5: Remove foreign live state through a guarded migration

Files: `.agents/STATE.md`, `.agents/docs/DESIGN.md`, `PROGRESS.md`.

- [ ] Do not use checkpoint compaction: it would preserve some copied TickReplay blocks.
- [ ] Produce replacement candidates in dry-run mode with the original file hashes.
- [ ] Apply only when the expected hashes still match, preventing concurrent work from being overwritten.
- [ ] Replace, rather than append to, the copied TickReplay/DuckDB state.
- [ ] Populate FrameWeb3 identity and design through the typed writers after reset.
- [ ] Create one genuine checkpoint so `PROGRESS.md` is meaningful.

**Verification:** Document contracts pass, and `load_context.py` reports `missing=[]`, `unreadable=[]`, `design.placeholder=false`, and `progress.entries>=1`. A scoped scan of only `AGENTS.md`, `STATE.md`, `DESIGN.md`, and `PROGRESS.md` finds no TickReplay, DuckDB, stock, or nonexistent `src/tickreplay` references.

#### Step 6: Curate optional and reusable framework content

- [ ] Build a reference graph for skills, agents, templates, hooks, and workflows.
- [ ] Retain generic validators, typed writers, delegation checks, templates, and active FrameWeb research.
- [ ] Decide whether `.agents/workflows/antigravity/` and unused runtime-specific agents remain.
- [ ] Remove the unconditional “upgrade global CLIs every session” instruction.
- [ ] Update `.agents/INDEX.md` only after the retained set is final.

**Verification:** Every retained entry is reachable from the index or an approved workflow; every removed entry has zero remaining references.

#### Step 7: Activate only the selected runtime integration

- [ ] Add only the settings/hooks required by the chosen runtime.
- [ ] Test discovery in a disposable repository copy.
- [ ] Activate save hooks only after their underlying command succeeds.
- [ ] Confirm runtime configuration cannot modify product files during a read-only task.

**Verification:** A disposable session discovers the intended rules, skills, and hooks exactly once, without broken links or unexpected writes.

#### Step 8: Run integration gates in a disposable candidate copy

- [ ] Create a disposable worktree/copy containing the complete cleanup diff and explicitly allowed untracked files.
- [ ] Run framework structural and semantic checks there.
- [ ] Run supported product gates:

  - `FrameWeb`: full pytest, result/baseline audit, and Wiki validation.
  - `FrameWebforJS`: build and non-watch ChromeHeadless unit tests.
  - Root: `dotnet build FrameWeb.sln`.
  - Windows integration: `FrameWeb\.venv\Scripts\python.exe scripts/smoke-local.py`.

- [ ] Keep generated `dist`, `bin`, `obj`, and smoke artifacts outside the working tree being reviewed.

**Verification:** Each component reports its own status; source hashes still match the Step 1 out-of-scope manifest.

#### Step 9: Verify writer and checkpoint idempotence correctly

- [ ] Run design/state writers twice and require the second run to be a no-op.
- [ ] Compare checkpoint previews using identical fixed inputs.
- [ ] For distinct applied checkpoints, require additive history, exactly one progress tracker, and no reintroduction of foreign content.
- [ ] Do not require two timestamped checkpoint applications to produce no diff; timestamps are intentionally additive.

**Verification:** Fixed-input previews are identical, repeated identity/design updates are stable, and state structure remains valid.

#### Step 10: Delete classified debris last

- [ ] Remove only target-listed `__pycache__`/`.pyc` files, zero-byte responses, duplicate prompt copies, and obsolete empty directories.
- [ ] Preserve active research/reproduction evidence unless explicitly retired.
- [ ] Add a clear log/cache retention policy to `.gitignore`.
- [ ] Run remaining Python checks with `PYTHONDONTWRITEBYTECODE=1` if cache absence is part of acceptance.

**Verification:** Preservation hashes still match, classified debris is absent, `git diff --check` passes, and no product directory has changed.

### Risks & Considerations

- Existing document validators can pass structurally valid but semantically foreign content.
- `/init` must not run before stack detection is repaired.
- Checkpoint compaction cannot serve as the foreign-state deletion mechanism.
- Windows Git symlink behavior depends on configuration and privileges.
- Product builds require Chrome, .NET, and local services; failures must be separated into cleanup defects versus missing environment prerequisites.
- Integration testing must include the uncommitted candidate; a plain worktree from HEAD is insufficient.

### Open Questions

Blocking before Step 2:

1. Primary runtime: Codex only, Claude + Codex, or retain all copied runtimes?
2. Windows discovery: true Git symlinks or a supported regular-file/config alternative?
3. `.codex` default: read-only recommended, with write access granted explicitly per task?

Deferrable until Step 6:

- Retain or remove the experimental Antigravity workflow?
- Keep troubleshooting research/reproduction artifacts permanently, or archive them after that investigation closes?

The revised dependency order received an independent read-only `PASS`. Formal on-disk plan gates remain pending because the session cannot create the plan artifact.
