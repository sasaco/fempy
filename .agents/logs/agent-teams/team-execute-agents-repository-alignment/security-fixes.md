# Work Log: security-fixes

## Summary

Implemented the repository-configuration and runtime-source hardening requested
after security review. Repository-declared and derived paths now share one
containment validator, declared malformed manifests fail detection, fallback
discovery exclusions are covered at their boundaries, and catchup/checkpointing
no longer read user-global runtime history by default.

## Tasks Completed

- Added a shared validator that rejects absolute paths, parent traversal, wrong
  path kinds, and symlink escapes before repository-configured files are read.
- Applied the validator to detector, catchup, and dependency-inventory manifest,
  project, working-directory, documentation, README, and adjacent-lockfile paths.
- Made malformed declared TOML, malformed JSON, and non-object JSON fatal
  detector evidence with a nonzero exit status.
- Added fallback-discovery boundary coverage for VCS, environment, output,
  cache, vendor, excluded asset/tool, submodule, pruned-repository, and depth
  exclusions.
- Replaced implicit user-global Claude history with opt-in
  `--external-agent-history`; in-repository agent-team work logs remain enabled
  by default.
- Preserved repository-relative dependency source reporting after validated paths
  are resolved to their physical targets.

## Files Modified

- `.agents/skills/_shared/repository_config.py`
- `.agents/skills/init/detect_stack.py`
- `.agents/skills/catchup/collect_repo_state.py`
- `.agents/skills/update-lib-docs/lib_inventory.py`
- `.agents/skills/checkpointing/checkpoint.py`
- `.agents/tests/test_detect_stack.py`
- `.agents/tests/test_repository_config_security.py`

## Verification

- Focused security tests: `21 passed`.
- Full agent test suite: `56 passed`.
- Detector: exit `0`, `ok=true`, no evidence errors.
- Catchup collector: exit `0`, `ok=true`, no collection errors, no external
  team sessions by default.
- Checkpoint collectors: no collection errors, no external teams by default,
  in-repository work-log groups collected.
- Library inventory: exit `0`, `ok=true`, no manifest errors, no absolute source
  paths.
- `git diff --check` passed for all owned files.

## Communication with Teammates

No direct teammate communication was required; all changes stayed within the
exclusive paths assigned by the lead agent.

## Issues Encountered

- One multi-hunk patch initially missed its context and applied no changes; it
  was split into exact patches.
- Direct checkpoint collection under the Windows default CP932 mode hit a
  pre-existing Git-output decoding issue. Re-running with `PYTHONUTF8=1`
  completed successfully with no collector errors; the encoding behavior was
  not changed because it is outside this fix scope.
- A supplemental Ruff invocation was unavailable in the locked development
  environment. No package or tool was installed implicitly; the repository's
  pytest suite, runtime probes, document validator, and diff check were used.
