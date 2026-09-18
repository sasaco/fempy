# Objective

Evaluate regression and backward-compatibility risk for candidate fixes to the FrameWebforJS calculation request encoding mismatch.

# Constraints

- Read-only analysis. Do not edit any source or test file.
- Use only repository code/history and the facts below; do not perform web research.
- Preserve the security hardening in commit `29df328`: never recommend restoring `eval` or executing request text.
- Distinguish the Python calculation API from the separate C# PDF print API.
- Rank the candidates and identify the minimum safe fix and required regression tests.

# Relevant files and facts

- `FrameWebforJS/src/app/app.component.ts:229-247`: calculation request is `btoa(pako.gzip(JSON.stringify(jsonData)))` with `Content-Encoding: gzip,base64`. JavaScript coerces the `Uint8Array` to bracketless decimal CSV (`31,139,...`) before Base64.
- `FrameWeb/main.py:140-160`: calculation backend Base64-decodes and calls `json.loads` on the decoded ASCII before `bytes(...)` and gzip decompression. It requires a JSON integer array (`[31,139,...]`).
- Commit `29df328eb29d1a17b91f706eb1b7f6dd16a0fa78` changed the decoder from `eval(b)` to `json.loads(b)` with the comment `legacy JSON byte-array transport, never execute input`. This intentionally removed code execution but narrowed the accepted wire format.
- A compatibility probe shows old `eval` accepts both `31,139,8,0` (tuple) and `[31,139,8,0]` (list), while current `json.loads` rejects the bracketless form and accepts the JSON array.
- Current backend tests and `FrameWeb/docs/wiki/endpoints.md` produce `base64(json.dumps(list(gzip(...))))`, so they cover only the canonical bracketed form and miss the actual browser producer.
- `FrameWebforJS/src/app/components/print/print.component.ts:1001-1011` deliberately has the same JavaScript `btoa(pako.gzip(...))` expression, but it calls the separate C# PDF service.
- `FramePrintPDF/FramePrintAzure/Function1.cs:34-48` and `Function2.cs:34-48` Base64-decode ASCII, split it on commas, convert each item to a byte, then gzip-decompress. The print service therefore requires the current bracketless CSV form.
- `scripts/smoke-local.py:92-99` also sends bracketless CSV to the print API and verifies a PDF; it is not a calculation request.
- No Angular `*.spec.ts` exists. Four Python integration/regression locations exercise compressed calculation requests, all with the bracketed JSON array.

# Candidate fixes to assess

1. Calculation-only frontend change: serialize the compressed byte array before Base64, e.g. `btoa(JSON.stringify(Array.from(compressed)))`, leaving the print path unchanged.
2. Backend compatibility fallback: retain `json.loads` first, then accept only a strictly validated comma-separated decimal byte list (no `eval`, no `literal_eval`; bounded count and each integer 0..255).
3. Shared frontend compression helper used by both calculation and print, emitting the canonical bracketed JSON array.
4. Replace the calculation protocol with direct Base64 of raw gzip bytes and update both frontend and backend.

# Acceptance checks

- Cover current/older frontend against current/older backend compatibility.
- Explain whether any server tolerance can remain safe without reintroducing executable parsing.
- Identify print regressions, deployment-skew risks, malformed-input/security risks, and test gaps.

# Output format

## Risk Assessment (HIGH / MEDIUM / LOW)
## Candidate Comparison
## Affected Code Paths
## Implicit Contracts at Risk
## Compatibility Matrix
## Recommended Safeguards
## Required Regression Tests
