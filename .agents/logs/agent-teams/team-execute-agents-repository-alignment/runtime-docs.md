# Work Log: runtime-docs

## Summary

Aligned the owned runtime and documentation surfaces with FrameWeb3's
Codex-first, Windows/PowerShell monorepo policy. Removed inactive
runtime-specific discovery assets and retained the reusable skills with
repository-root uv commands and model-neutral collaboration language.

## Tasks Completed

- [x] Added a real root `AGENTS.md` with language, component, command,
  ownership, concurrency, and infrastructure-entrypoint contracts.
- [x] Removed the Claude pseudo-links, runtime-specific agent definitions, and
  inactive Antigravity workflows.
- [x] Set Codex to read-only by default with explicit implementation overrides.
- [x] Rewrote the registry, runtime-change runbook, rules, and handoff playbook.
- [x] Swept every owned `SKILL.md` and Codex reference for copied Unix commands,
  global CLI updates, fixed Claude model roles, and plugin-only assumptions.
- [x] Rebuilt all 15 modified `SKILL.md` files from clean `HEAD` UTF-8 content
  after detecting mojibake, then reapplied only the Codex-first/PowerShell
  semantic changes with `apply_patch`.
- [x] Replaced the final `general-purpose-opus` example in
  `checkpointing/references/formats.md` with a capability-based role.
- [x] Corrected the canonical Python product-test command in `AGENTS.md`,
  `rules/testing.md`, and `rules/dev-environment.md` so pytest runs with
  `FrameWeb/` as its working directory.

## Files Modified

- Added/rewritten: `AGENTS.md`, `.codex/config.toml`, `.agents/INDEX.md`,
  `.agents/change_main.md`, `.agents/rules/*.md`,
  `.agents/docs/CODEX_HANDOFF_PLAYBOOK.md`, `.agents/skills/*/SKILL.md`, and
  `.agents/skills/codex-system/references/*.md`.
- Also updated `.agents/skills/checkpointing/references/formats.md` during the
  UTF-8/stale-role repair.
- Deleted: `CLAUDE.md`, `.claude/agents`, `.claude/skills`,
  `.agents/agents/*`, and `.agents/workflows/antigravity/*`.

## Key Decisions

- PowerShell from the repository root is canonical.
- Python helpers use
  `uv run --project FrameWeb --locked --extra dev python ...`.
- Python product tests use
  `uv --directory FrameWeb run --locked --extra dev python -m pytest tests -q`;
  targeted tests retain the same prefix and use a `tests/...` path.
- `.agents/check.ps1` replaces the removed Bash verification entrypoints.
- `.codex/config.toml` keeps `approval_policy = "never"` while defaulting to
  `sandbox_mode = "read-only"`; implementation opts into `workspace-write`.
- Skills route by available capability rather than copied model/agent names.

## Communication with Teammates

- Informed the lead that canonical rules were immediately recreated after the
  replacement step and confirmed that all ten rule paths are present.
- Incorporated the lead's notice that `.agents/check.sh` and
  `_shared/verify.sh` were being removed by the tooling workstream.
- Reported implementation status and remaining verification work to the lead.
- Reported the UTF-8 repair and routed the one remaining out-of-scope
  `_shared/README.md` Claude CLI reference to the tooling owner.
- Applied the tooling workstream's finding that repo-root pytest execution
  caused five working-directory-sensitive false failures.

## Verification

- `& .agents/check.ps1`: agent pytest, repository detection, document
  contracts, live references, foreign-reference scan, and `git diff --check`
  passed. The cache-absence gate remained red pending the separate cache-cleanup
  workstream. The script then started the full FrameWeb product suite; that
  out-of-scope long-running test process was stopped without a product verdict.
- `load_context.py`: exit 0; ten rules loaded, Main Agent `Codex`,
  `missing=[]`, `unreadable=[]`, `warnings=[]`, design non-placeholder.
- Required path-absence, root heading, sandbox/approval, and stale-reference
  checks passed in the runtime-docs scope.
- `rg -n '[鬩郢謇繧闔譎蜷隱髫莠豁陦]' .agents/skills -g '*.md'`: no matches.
- Owned `SKILL.md`/reference stale scan for removed agent names, global CLI
  updates, Bash entrypoints, `python3`, `mktemp`, and slash-plugin assumptions:
  no matches.
- `uv run --project FrameWeb --locked --extra dev python -m pytest .agents/tests -q`:
  26 passed.
- `validate_doc.py --contract work-log --dir ... --expect-files 3`: exit 0;
  three files checked, zero failed, zero warnings.
- `validate_doc.py` for `design-doc`, `state-doc`, and the approved `plan-doc`:
  all exited 0.
- `load_context.py`: repeated after the UTF-8 repair with exit 0, ten rules,
  empty missing/unreadable/warnings arrays, and Main Agent `Codex`.
- `git diff --check -- .agents/skills .../runtime-docs.md`: exit 0.
- Repository-wide `git diff --check`: exit 0 (line-ending notices only).
- Canonical product-test scan confirms the approved `uv --directory FrameWeb`
  command in all three owned documents and no remaining repo-root
  `pytest FrameWeb/tests` form in the owned active Markdown surface.

## Remaining Work

- Re-run the full `.agents/check.ps1` after the cache-cleanup teammate removes
  the owned `__pycache__` and `.pyc` artifacts.
- Lead must integrate other workstreams and run the final combined scope gate.

## Issues Encountered

- A first large patch and one normalization script failed before editing; both
  were retried as smaller exact operations.
- The earlier PowerShell text reconstruction decoded UTF-8 Markdown as CP932
  and introduced mojibake. The repair used clean `git show HEAD:<path>` content
  as the source and `apply_patch` for every write; no shell text recoding was
  used.
- The shared gate's cache failure is outside runtime-docs ownership and was not
  bypassed or hidden.
