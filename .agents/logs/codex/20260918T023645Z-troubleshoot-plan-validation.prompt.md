Objective: Validate the proposed fix plan for the FrameWebforJS calculation communication error for completeness, correctness, and honest user-outcome verification.

Context:
- Proven root cause: `FrameWebforJS/src/app/app.component.ts` sends Base64 of bracketless `Uint8Array.toString()` CSV, while post-`29df328` `FrameWeb/main.py` securely expects Base64 of a JSON byte array. The strict parser returns the observed HTTP 400 `Extra data: line 1 column 3` before model parsing.
- Recommended transport fix: calculation-only canonical body `btoa(`[${compressed.join(',')}]`)` (or an equivalently tested helper), preserving the current backend parser and leaving the print encoder untouched.
- Compatibility: the fixed producer works with old and current backends. Add a temporary strict legacy CSV server branch only if deployment inventory proves cached/deployed old calculation bundles must remain supported. Never restore `eval`/`literal_eval`.
- Blast radius: every current compressed JS calculation is affected; ordinary JSON and canonical compressed clients are unaffected; print deliberately uses bracketless CSV for separate C# consumers.
- Large preset: gzip output can be 2,025,295 bytes and Base64 about 9.65 MB; avoid `Array.from` boxing and measure browser peak memory/latency.
- Separate blocker: after transport repair, current backend returns flat `node_displacements/reaction_forces/element_stresses`, while FrameWebforJS result workers expect per-case `disg/reac/fsec`. Ct contains multiple load cases. Therefore HTTP 200 alone cannot prove calculation works.

Proposed tasks:
1. Add a failing regression that invokes the actual production JavaScript calculation encoder and feeds its body to Python `Compressor.decompress`/Flask, asserting exact original-object equality and the existing `gzip,base64` header.
2. Add focused encoder tests for empty/ASCII/Japanese/Ct/large-preset inputs, and a print-isolation test that pins the print producer's existing bracketless CSV contract.
3. Implement a calculation-only pure encoder using brackets around `compressed.join(',')`; do not change the backend decoder, print producer, headers, or introduce `Array.from` high-water allocation.
4. Re-run the original browser Ct flow with the exact `InputData.getInputJson(0)` payload. Require no transmission dialog and capture the actual post-transport response/result-consumption behavior.
5. Gate completion on the response contract: write a failing UI/integration regression for case count plus non-empty displacement/reaction/section-force consumption. Before implementing an adapter/schema change, explicitly decide authoritative multi-load-case behavior; do not treat a field rename as sufficient without that decision.
6. If legacy deployed clients are in scope, add a separate temporary strict CSV fallback with Base64/token/range/body/decompressed-size/gzip/UTF-8/top-level-object validation, telemetry, and removal criteria; otherwise omit it.
7. Verify large-preset time/peak memory, print/PDF behavior, TypeScript build/tests, focused Python HTTP tests, and the named Ct browser outcome. Do not declare the user issue fixed on decoder success or HTTP 200 alone.

Constraints:
- Check that tasks address root cause rather than suppressing the generic error.
- Identify missing edge cases or unsafe compatibility behavior.
- Treat the unresolved multi-case response contract as an explicit gate.
- Prefer minimal, backward-compatible changes.

Relevant artifacts:
- `.agents/docs/research/troubleshoot-framewebforjs-calculation-communication-error-root-cause.md`
- `.agents/docs/research/troubleshoot-framewebforjs-calculation-communication-error-impact.md`

Acceptance checks for this validation:
- Return PASS only if the plan is safe to execute without falsely claiming end-to-end success.
- If the transport change can proceed but the result contract needs a separate decision, state how the gate should be represented.

Output format:
## Validation Result (PASS / NEEDS_REVISION)
## Missing Coverage
## Potential New Issues
## Additional Test Cases Recommended
## Revised Task List (if needed)
