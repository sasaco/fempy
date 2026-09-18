Objective: Decompose the cleanup of a copied `.agents` framework into an ordered, independently testable implementation plan aligned with the actual FrameWeb3 repository.

Context:
- Purpose: remove unnecessary copied artifacts and correct wrong repository-specific content, commands, discovery, validation, and runtime integration without deleting reusable generic orchestration capabilities.
- Scope in: `.agents/**`; root `AGENTS.md`, `CLAUDE.md`, `.claude/**`, `.codex/config.toml`, and `PROGRESS.md` only where they are direct integration points required by `.agents`.
- Scope out: product behavior changes in `FrameWeb/**`, `FrameWebforJS/**`, `FramePrintPDF/**`, and `tools/**`; dependency upgrades; global CLI updates; destructive cleanup outside the named agent infrastructure.
- Constraints: preserve user/product code; use Windows/PowerShell-compatible commands; support the real monorepo layout; do not assume the main runtime or Claude discovery strategy where the user has not chosen it; distinguish files to delete from files to rewrite; preserve reusable generic skills unless evidence shows they cannot be made correct.

Current state:
- `README.md:1,12-21,40-44,54-71`, `FrameWeb/pyproject.toml:1-55`, `FrameWebforJS/package.json:4-22,128-130`, and `tools/FrameWeb.Startup/FrameWeb.Startup.csproj:1-9` show a Windows monorepo with Python FEM/Flask under `FrameWeb/`, Angular under `FrameWebforJS/`, and .NET 8 startup/printing projects.
- `.agents/STATE.md:23-294` and `.agents/docs/DESIGN.md:36-126` contain foreign TickReplay/DuckDB/SMA/stock-trading state whose referenced paths do not exist here.
- `.agents/STATE.md:9-11` is uninitialized and `.agents/docs/DESIGN.md:10-28` has empty core sections; `PROGRESS.md` is missing.
- Root `AGENTS.md` is zero bytes. `CLAUDE.md`, `.claude/agents`, and `.claude/skills` are ordinary files containing link-target text, not symlinks. `.claude/settings.json` is missing.
- `.agents/rules/language.md:3-4` requires a root `AGENTS.md` Language Protocol that does not exist.
- `.agents/rules/dev-environment.md`, `.agents/rules/testing.md`, `.agents/rules/security.md`, `.agents/hooks/lint-on-save.py`, `.agents/skills/init/detect_stack.py`, `.agents/skills/_shared/run_tests.py`, and `.agents/skills/_shared/verify.sh` assume a root-level single Python project, root ruff/ty/marimo/poe, Bash/python3, or generic dependency pinning that conflicts with the actual manifests and Windows host.
- Running `uv run --project FrameWeb --locked --extra dev python .agents/skills/init/detect_stack.py --project-root .` returns `languages=[]`, `package_managers=[]`, falsely reports `.github/workflows` as CI, and exits 1 for invalid Claude link markers.
- Twelve ignored `.pyc` files exist under `.agents/skills/**/__pycache__`; they are copied/generated caches and should be removed and prevented from returning.
- `.agents/skills/codex-system/SKILL.md:18` unconditionally instructs global CLI updates every session. `.codex/config.toml:17-18` references missing `TEMPLATE_DESIGN_LOG.md`.
- Generic directory structure, skills, agents, helper scripts, output `.gitkeep` files, and the handoff playbook appear reusable after alignment.

Unresolved user decisions:
- Whether the main agent is Codex or Claude Code.
- Whether to retain a multi-runtime layout (`.agents/agents`, hooks, Claude discovery, handoff playbook) or reduce to Codex-focused infrastructure.
- Whether Windows discovery should use true symlinks or a documented/configured non-symlink alternative.
- Whether explicitly experimental `.agents/workflows/antigravity/` is retained.

Constraints for the proposed steps:
- Order by dependency: establish decisions and repository identity before rewriting generated state or validators.
- Each step must be independently testable and name concrete files and verification commands.
- Put deletion safety, false-positive/false-negative verification risks, and runtime-discovery uncertainty early.
- Include a reference scan proving all foreign TickReplay/DuckDB/SMA content and nonexistent paths are gone.
- Include product gates for Python, Angular, and .NET, but do not invent commands unsupported by manifests.
- Include idempotence checks for initialization/checkpointing so regenerated files do not reintroduce wrong content.
- The output is a plan only; do not edit files.

Output format:
## Implementation Steps (ordered by dependency)
## Verification per Step
## Risks and Mitigations
## Open Questions
