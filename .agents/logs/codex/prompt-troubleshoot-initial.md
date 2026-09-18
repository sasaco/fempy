Objective: Analyze the FrameWebforJS Ct-girder calculation HTTP 400 and generate ranked initial root-cause hypotheses.

Context:
- UI reproduction: open `FrameWebforJS/src/assets/preset/サンプル（Ct桁）.json`, click calculation, confirm; the UI shows the generic transmission error.
- Browser console reports Angular `HttpErrorResponse`.
- Direct reproduction matching the frontend request returns HTTP 400 with `{"error":"Extra data: line 1 column 3 (char 2)","error_code":"invalid_input"}`.
- Frontend request creation at `FrameWebforJS/src/app/app.component.ts:229-247`: `const compressed = pako.gzip(json); const base64Encoded = btoa(compressed);`, sent with `Content-Encoding: gzip,base64`.
- `pako.gzip` returns a Uint8Array. JavaScript string coercion makes `btoa(new Uint8Array([31,139,8,0]))` encode the ASCII string `31,139,8,0` without brackets.
- Backend path at `FrameWeb/main.py:91-108,153-160` base64-decodes the body, calls `json.loads` expecting an integer JSON array, constructs `bytes(decoded_array)`, then gzip-decompresses. The bracketless input raises JSONDecodeError before model parsing.
- Existing backend compressed-input tests use `json.dumps(list(compressed))` and pass, so they do not exercise the browser producer.

Constraints:
- Read-only diagnosis; do not modify files.
- Focus on root cause categories: transport contract/type coercion, boundary validation, dependency behavior, and unrelated alternatives.
- Distinguish the initiating defect from the generic UI symptom.
- Rank at least three hypotheses and state evidence that confirms or eliminates each.

Relevant files:
- `FrameWebforJS/src/app/app.component.ts`
- `FrameWeb/main.py`
- `FrameWeb/src/fem/diagnostics.py`
- `FrameWeb/tests/io/test_axial_force_input.py`
- `.agents/docs/research/troubleshoot-framewebforjs-calculation-communication-error-context.md`

Acceptance checks:
- Explain why HTTP 400 contains `Extra data: line 1 column 3`.
- State whether calculation/model content can be the immediate cause.
- Identify the most useful next evidence for Phase 2.

Output format:
## Error Pattern Recognition
## Hypotheses (ranked by likelihood)
## Investigation Plan (per hypothesis)
## Known Similar Patterns
