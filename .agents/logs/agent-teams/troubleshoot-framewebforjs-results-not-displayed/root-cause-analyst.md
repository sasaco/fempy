# Work Log: Root Cause Analyst

## Summary
Traced the old and current backend-to-frontend result paths, confirmed a silent response-schema mismatch plus first-case loss, eliminated async timing as the primary cause, and specified the legacy projection and safest compatibility boundary.

## Tasks Completed
- [x] Traced old execution: all surviving load cases are solved and returned as an ordered case map.
- [x] Traced current execution: one selected legacy case is solved and returned as a flat modern result.
- [x] Verified frontend failure mode: all three workers skip incompatible entries and report empty success.
- [x] Defined exact case IDs/order, rate, displacement, reaction, and beam section-force projection semantics.
- [x] Compared unversioned replacement, frontend adaptation, and an explicit versioned compatibility adapter.
- [x] Wrote `.agents/docs/research/troubleshoot-framewebforjs-results-not-displayed-root-cause.md`.

## Hypotheses Evaluated
- [confirmed] Response-schema mismatch: the backend emits flat `node_displacements/reaction_forces/element_stresses`; the frontend requires an outer case map containing `disg/reac/fsec`.
- [confirmed] First-case loss: `_read_json_model` invokes `select_case` without an ID, so Ct cases 2–11 are absent before calculation.
- [eliminated] Async readiness timing as primary cause: it affects status sequencing, but waiting cannot restore fields already skipped by workers.

## Root Cause
- Defect: An unversioned producer/consumer contract mismatch is compounded by single-case selection before solve.
- Location: `FrameWeb/main.py:106-118`; `FrameWeb/src/fem/file_io.py:70-79`; `FrameWeb/src/fem/legacy_beam.py:6-20`; `FrameWebforJS/src/app/components/result/result-disg/result-disg1.worker.ts:29-40` (same pattern in reaction and section-force workers).
- Trigger condition: Any successful legacy FrameWebforJS request reaches the modern flat response; multi-case input additionally loses every case after the first.

## Proposed Fixes
- Approach A: Replace the current `/` response with the old case map — minimal frontend change, but breaks documented modern HTTP consumers and tests; rejected.
- Approach B: Adapt the frontend to flat modern fields — preserves the modern API, but cannot recover cases discarded before solve and duplicates mesh-derived conversion; rejected as primary fix.
- Approach C: Add an explicit/versioned `legacy-cases-v1` backend adapter requested by FrameWebforJS — preserves the flat default, owns per-case solve/projection at the correct boundary, and is independently testable; recommended.
- Recommended: Approach C, plus frontend fail-fast validation when no valid legacy case is returned.

## Codex Consultations
- Asked Codex to review the execution flow, three hypotheses, A/B/C tradeoffs, and exact projection correctness. The bounded low-effort read-only call timed out and left an empty response at `.agents/logs/codex/20260918T053932Z-troubleshoot-results-display-root-cause.md`; per lead instruction, Codex was marked unavailable and not retried. No Codex output was used as evidence.

## Communication with Teammates
- → `/root/result_impact`: Shared exact `rate`, displacement-unit, reaction-name/sign, and fsec end-sign mappings; warned that `section_cut_view.py` is not production-equivalent for rate, shell key, or 2D reaction key.
- ← `/root/result_impact`: Received history that flat HTTP predates the recent transport fix, first-case selection was introduced later, and replacing the flat schema has broad documented/test/tool blast radius; teammate independently recommended an explicit compatibility boundary.
- → `/root/result_impact`: Sent completed report path and final boundary recommendation.

## Issues Encountered
- Codex CLI timed out after the single bounded attempt; recorded exact prompt/empty response artifacts and continued from direct repository evidence without retry.
- Linksee-memory MCP operations were not available in this delegated runtime; no product files were edited.
