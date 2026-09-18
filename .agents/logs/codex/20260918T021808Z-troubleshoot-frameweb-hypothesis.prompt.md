Objective: Evaluate every initial hypothesis for the FrameWebforJS Ct-girder calculation communication error against repository and control-experiment evidence.

Constraints:
- Read-only analysis; do not edit files or restart services.
- Apply causal reasoning. Separate the cause of the observed line/column-3 HTTP 400 from possible later calculation errors.
- Give one of CONFIRMED, ELIMINATED, or INCONCLUSIVE for every numbered hypothesis and every sub-hypothesis listed below.
- Treat a possible explanation of older behavior separately from proof of current deployment skew.

Observed facts:
- Browser UI reaches the POST and shows the generic transmission dialog after a non-2xx response.
- Direct Node HTTP reproduction reaches `127.0.0.1:8080` and returns HTTP 400 with `invalid_input` and `Extra data: line 1 column 3 (char 2)`.
- Current producer: `btoa(pako.gzip(JSON.stringify(jsonData)))`.
- `pako.gzip` returns `Uint8Array`; direct `btoa` coercion yields base64 of ASCII comma-separated decimals without brackets.
- Current consumer base64-decodes, then runs `json.loads(b)`, then `gzip.decompress(bytes(l))`, then parses the model JSON.
- An identical-byte accepted-envelope control using `btoa(JSON.stringify(Array.from(compressed)))` round-trips exactly through array parse and ungzip. Over HTTP it advances past decompression to a later model-level error in the approximate payload.
- Commit `29df328eb29d1a17b91f706eb1b7f6dd16a0fa78` replaced `eval(b)` with `json.loads(b)`. `eval(b'31,139,8,0')` returns a tuple, whereas `json.loads` raises the exact observed error; both accept `[31,139,8,0]`.
- Existing backend compressed-route tests all construct `base64(json.dumps(list(gzip_bytes)))`, not the real browser producer.

Hypotheses to evaluate:
1. Compressed-request transport contract regression/type-coercion mismatch is the direct root cause.
2. Frontend/backend deployment-version skew is required to explain the current local incident.
3a. Ct-girder model validation/content is the cause of the observed line/column-3 response.
3b. Wrong URL or service unavailability is the cause.
3c. CORS is the cause.
3d. Authentication/anonymous UID is the cause.
4. Existing green backend compressed-route tests contradict the transport mismatch hypothesis.

Relevant files:
- `.agents/docs/research/troubleshoot-framewebforjs-calculation-communication-error-bug-report.md`
- `.agents/docs/research/troubleshoot-framewebforjs-calculation-communication-error-context.md`
- `FrameWebforJS/src/app/app.component.ts`
- `FrameWeb/main.py`
- `FrameWeb/tests/integration/test_input_routes.py`
- `FrameWeb/tests/integration/test_spatial_general_plane.py`
- `FrameWeb/tests/io/test_axial_force_input.py`
- `FrameWeb/tests/regression/test_slip_support_history.py`

Output format:
## Hypothesis 1
### Verdict
### Evidence For
### Evidence Against
### Reasoning
### Remaining Unknowns
(repeat for 2, 3a, 3b, 3c, 3d, and 4)
## Overall Causal Conclusion

