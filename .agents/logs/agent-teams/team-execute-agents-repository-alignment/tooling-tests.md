# Work Log: tooling-tests

## Summary

Aligned the copied `.agents` detection, test-running, dependency inventory, and
verification tooling with this Windows/PowerShell monorepo. Added focused
contract tests, normalized active Python helper examples, and removed the unused
Bash and Claude-hook runtime surfaces.

## Tasks Completed

- [x] Added `.agents/repository.toml` as the declared component/gate authority.
- [x] Made stack detection config-first, independently reporting Python/uv,
  Angular/npm, and .NET/dotnet, with a pruned bounded fallback.
- [x] Made the shared pytest runner select configured component commands and
  report an explicit `no_gates` state instead of silently changing runners.
- [x] Added the canonical PowerShell agent/product gate and removed duplicate
  Bash gate logic.
- [x] Updated catch-up and library inventory helpers for nested manifests,
  adjacent lockfiles, and repository exclusions.
- [x] Removed every `.agents/hooks` file because no Claude hook runtime remains.
- [x] Added focused tests for configuration, fallback, exclusions, CI tracking,
  command selection/failure/no-gates, and shared-script references.
- [x] Replaced every active Python-helper `python3` example with the canonical
  repo-root `uv run --project FrameWeb --locked --extra dev python ...` form.
- [x] Replaced the Codex global-install hint with official-documentation guidance
  and registered DESIGN exceptions through the real Key Decisions mechanism.
- [x] Added an active-surface hygiene contract for mojibake, deleted agent names,
  missing templates, mutating Codex installation, and noncanonical Python calls.
- [x] Corrected the Python product gate to run from the declared `FrameWeb`
  component directory and rebased shared-runner targets into that coordinate
  system without changing the repo-root CLI contract.
- [x] Reworked `.agents/check.ps1` to emit one parseable JSON contract, persist
  detailed output to a reported log, and honor the shared `0/1/2/3` exit-code
  vocabulary, including a non-executing `-ListGates` mode.
- [x] Added mandatory product-scope isolation across committed, staged,
  unstaged, and untracked changes, with explicit baseline and allowlist inputs.
- [x] Routed `simplify_gate.py` through the canonical PowerShell JSON contract
  and removed its executable dependency on the deleted Bash verifier.
- [x] Confined `run_tests.py` targets to the resolved repository boundary,
  including traversal, absolute-outside, and symlink-escape rejection.
- [x] Made Git scope enumeration preserve non-ASCII paths even when the local
  repository enables `core.quotePath`, with an execution-level Japanese-path
  rejection and allowlist regression.

## Files Modified

- Added: `.agents/repository.toml`, `.agents/check.ps1`,
  `.agents/tests/test_detect_stack.py`, `.agents/tests/test_run_tests.py`, and
  `.agents/tests/test_shared_script_contract.py`.
- Modified: `.agents/skills/init/detect_stack.py`,
  `.agents/skills/_shared/run_tests.py`, `.agents/skills/_shared/gather_diff.py`,
  `.agents/skills/_shared/README.md`,
  `.agents/skills/catchup/collect_repo_state.py`, and
  `.agents/skills/update-lib-docs/lib_inventory.py`.
- Follow-up documentation-only edits: all 18 `.agents/skills/**/*.py` files that
  still had `python3` usage examples, including shared wrappers plus context,
  checkpointing, catch-up, simplify, troubleshoot, team-execute, and inventory
  helpers. `_shared/cli_consult.py` and `_shared/README.md` now label the external
  peer-CLI wrapper as legacy compatibility rather than a repository runtime.
- Product-gate follow-up: `.agents/repository.toml`, shared `run_tests.py` and
  README, detector/runner/shared-contract tests, plus the two canonical pytest
  examples in `.agents/skills/troubleshoot/repro.py`.
- Review-fix follow-up: `.agents/check.ps1`, shared `run_tests.py` and README,
  `skills/simplify/simplify_gate.py`, runner/shared-contract tests, and this log.
- Deleted: `.agents/check.sh`, `.agents/skills/_shared/verify.sh`, and all files
  under `.agents/hooks/`.

## Key Decisions

- `repository.toml` is authoritative when present. Invalid or missing declared
  paths are contract errors and never trigger a silent discovery fallback.
- Fallback discovery is capped at four levels and prunes generated, vendored,
  cached, submodule, and repository-specific third-party paths.
- CI is reported only from tracked workflow files; an empty `.github/workflows`
  directory does not count.
- Codex bootstrap validity requires nonempty `AGENTS.md` and a valid
  `.agents/STATE.md`; Claude symlinks and hooks are not requirements.
- Product gates remain separately classified. `-AgentOnly` skips them visibly;
  the local smoke gate runs only with `-IncludeOptionalProduct`.
- Active-surface hygiene covers Markdown, Python, and TOML while excluding
  historical/generated logs, checkpoints, plans, research, and review artifacts.
  Test needles are assembled at runtime so the regression test cannot match its
  own forbidden literals.
- Python gates use `uv --directory FrameWeb run ...` so cwd-sensitive product
  fixtures resolve under `FrameWeb`. `run_tests.py` still accepts repo-root
  `--target` values and converts them to component-relative arguments, including
  `../.agents/...` for framework tests and preserved pytest `::node` suffixes.
- The PowerShell gate owns command execution and the JSON result contract;
  consumers parse stdout while human-readable subprocess detail stays in the
  reported log. `-ListGates` lists commands without invoking unavailable tools.
- Scope isolation treats `FrameWeb`, `FrameWebforJS`, `FramePrintPDF`,
  `FrameGConverter`, and `tools` as product roots. A `-BaselineRef` adds
  committed changes to the staged/unstaged/untracked union; approved paths come
  from `-ScopeAllowlist` JSON or semicolon-separated `-AllowProductPath` input.
- Every scope-enumeration Git call sets `core.quotePath=false` locally so path
  classification receives literal Unicode names without changing repository or
  global Git configuration.
- Pytest targets are resolved before component rebasing. Only paths confined to
  the repository are rendered relative to `working_directory`; pytest node ids
  are preserved after that boundary check.

## Communication

- The lead confirmed that the runtime-docs teammate owns live SKILL/rule
  reference rewrites and that the lead will remove existing `.agents` bytecode
  caches during final integration.
- Status reported to the lead after implementation and before final verification.
- Reported the stale product-gate command found in runtime-owned root/rule docs;
  the runtime-docs owner aligned those surfaces without an ownership overlap.
- The lead supplied the fifth cwd-sensitive node id; all five exact failures
  were rerun under the corrected prefix and passed.
- Review findings were implemented only in the lead-assigned gate/runner files;
  detector, configuration, migration, catch-up, inventory, checkpointing, and
  review-report ownership remained untouched.

## Communication with Teammates

- → `/root`: reported implementation status, passing focused tests, and the
  single deferred cache cleanup needed for a clean `-AgentOnly` run.
- ← `/root`: confirmed cache cleanup ownership and requested no scope expansion.
- ↔ `/root/runtime_docs`: coordinated the broad contract with its SKILL encoding
  repair and the obsolete checkpoint format reference without crossing ownership.
- ← `/root`: requested the final Codex-focused description of the retained legacy
  peer-CLI compatibility helper; the README and helper docstring were aligned.

## Verification

- Focused detector/runner/shared-contract command after the cwd correction:
  `19 passed in 1.47s`.
- Real detector: exit `0`; source `.agents/repository.toml`; stack families
  `python`, `angular`, `.net`; managers `uv`, `npm`, `dotnet`; CI list empty.
- `collect_repo_state.py`: exit `0`; 4 components, 5 gates, 4 manifests, no
  errors.
- `lib_inventory.py`: exit `0`; 6 manifest/lock sources, 91 dependencies, no
  manifest errors or warnings.
- PowerShell parser: `.agents/check.ps1` parsed without errors.
- Removed-path check: no `.agents/check.sh`, shared `verify.sh`, or hook files.
- `git diff --check`: exit `0` (line-ending warnings only).
- `.agents/check.ps1 -AgentOnly`: every executed contract passed except the
  strict bytecode-cache check, which found pre-existing caches owned by final
  integration cleanup.
- Full agent suite after the product-gate correction and final docstring update:
  `28 passed in 1.80s`.
- Corrected cwd-sensitive product rerun: the exact five formerly failing nodes
  passed (`5 passed in 1.88s`). The two affected test files also passed as a
  broader check (`21 passed in 3.47s`).
- Real detector preserved the exact Python prefix and gate:
  `uv --directory FrameWeb run --locked --extra dev python -m pytest tests -q`.
- Broad active-surface hygiene scan: no mojibake markers, deleted-agent names,
  missing-template references, global Codex install command, or `python3 ` calls.
- Codex-focused runtime-doc scan: no `Claude Code` runtime claim in active skills,
  rules, index, state, or design documentation.
- Review-fix focused suite: `22 passed in 6.75s`, covering JSON/exit behavior,
  non-executing gate listing, log failure, no gates, bad arguments, full
  committed/staged/unstaged/untracked scope isolation and allowlisting, plus
  absolute/traversal/symlink pytest-target confinement.
- The focused suite also executes `simplify_gate.run_verify` with a captured
  subprocess, proving it invokes `check.ps1`, parses the JSON contract, and
  forwards declared product scope through the PowerShell-safe direct allowlist.
- Full agent suite after review fixes: `57 passed in 9.11s`.
- Final `-ListGates`: exit `0`, JSON `overall=pass`, scope isolation passed,
  and all five product gates were listed without execution.
- Final `-AgentOnly`: valid JSON and exit `2`; scope isolation and all executed
  checks passed except the intentionally strict cache-absence gate. Detailed
  log: `.agents/logs/check-20260918T101047007Z-38868.log`.
- Executable-surface scan found no Python reference to the deleted Bash verifier
  and no Bash argv in `simplify_gate.py`; PowerShell parsing and Python compile
  checks succeeded.
- Non-ASCII scope follow-up: focused shared-contract suite `14 passed in 5.92s`;
  full agent suite `57 passed in 8.81s`. The fixture forces
  `core.quotePath=true`, rejects `FrameWebforJS/日本語-未追跡.js`, and then permits
  the exact path through the explicit JSON allowlist.
- Post-follow-up `.agents/check.ps1 -AgentOnly`: exit `0`, `ok=true`, all agent
  checks including scope isolation and cache absence passed. Detailed log:
  `.agents/logs/check-20260918T102631333Z-30828.log`.

## Remaining Work

- Lead: rerun the complete Python product suite with the corrected command. This
  agent reran only the five prior failures plus their two containing files,
  avoiding another 25-minute full-suite run as requested.

## Issues Encountered

- Windows output capture initially exposed a `cp932` encoding mismatch; shared
  JSON output now reconfigures stdout to UTF-8 and tests decode subprocess output
  explicitly.
- One multi-file patch used stale README context and applied nothing; the files
  were reread and patched with exact smaller hunks.
- The new broad contract initially exposed 582 mojibake matches in parallel-owned
  SKILL files; it was kept strict and passed after the runtime-docs repair landed.
- The first product-gate patch briefly added a duplicate TOML `command` key; the
  stanza was inspected immediately, the obsolete assignment removed, and TOML
  parsing plus detector/config contract tests passed afterward.
- A focused Ruff command could not run because the locked FrameWeb dev extra does
  not install Ruff (`No module named ruff`). No dependency or environment was
  mutated; pytest, live imports, contract tests, whitespace scanning, and
  `git diff --check` supplied the verification evidence instead.
- Early `-ListGates` probes exposed PowerShell runtime differences not caught by
  parsing alone: empty mandatory arrays, `if` used as an argument expression,
  stderr records under `ErrorActionPreference=Stop`, JSON-array coercion, and
  Windows PowerShell `-File` array binding. The implementation now uses empty
  defaults, precomputed values, native exit codes, explicit array conversion,
  and a single semicolon-delimited direct allowlist value; behavior tests pin
  the resulting JSON contract.
