Objective: Design and compare safe fixes for the compressed-request wire-contract regression between FrameWebforJS and FrameWeb.

Root cause:
- `FrameWebforJS/src/app/app.component.ts:233-245` passes the `Uint8Array` returned by `pako.gzip` directly to `btoa`, producing base64 of bracketless decimal CSV (`31,139,...`).
- `FrameWeb/main.py:153-160` now securely uses `json.loads` and requires a JSON byte array (`[31,139,...]`). Commit `29df328` replaced unsafe `eval`, which happened to accept both tuple-like CSV and JSON lists.
- Existing tests cover only the bracketed consumer-preferred envelope and miss the browser producer.

Constraints:
- Read-only design only; do not edit files.
- Do not recommend restoring `eval` or any executable input parsing.
- Propose at least two concrete approaches and compare correctness, minimality, maintainability, performance, security, rollout/backward compatibility, and testability.
- Analyze at least these candidates:
  A. Producer conformance: in FrameWebforJS send base64(JSON.stringify(Array.from(gzipBytes))).
  B. Secure consumer compatibility: keep JSON-list support and additionally parse bracketless decimal CSV with strict size/count/integer/range validation.
  C. Optional protocol modernization: base64 raw gzip bytes with an explicit/new encoding contract, while considering mixed-version rollout.
- Include compatibility with both pre-29df328 (`eval`) and current (`json.loads`) backends, already deployed legacy frontend bundles, and existing backend tests.
- Include an end-to-end regression-test strategy that exercises the actual JavaScript producer and Python decoder rather than duplicating only one side.

Relevant files:
- `FrameWebforJS/src/app/app.component.ts`
- `FrameWeb/main.py`
- `FrameWebforJS/src/app/components/print/print.component.ts` (similar producer targeting another service; audit separately, do not assume same contract)
- compressed route tests under `FrameWeb/tests/`

Output format:
## Approach A: Producer Conformance
## Approach B: Secure Consumer Compatibility
## Approach C: Protocol Modernization
## Comparison Matrix
## Recommendation with Rationale
## Required Regression Tests
## Rollout Notes
