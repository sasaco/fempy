## TL;DR

The leading hypothesis is effectively confirmed: **a frontend/backend compressed-request contract regression causes the HTTP 400 before Ct-girder parsing begins**.

Commit `29df328` replaced a permissive `eval()` decoder with `json.loads()`, but the frontend still sends a bracketless comma-separated byte sequence.

## Analysis

### Ranked hypotheses

1. **Compressed-envelope compatibility regression — ~99%, confirmed**

   - Frontend performs `btoa(pako.gzip(json))` in [app.component.ts](C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/app.component.ts:233). Because `pako.gzip()` returns `Uint8Array`, `btoa` receives the coerced string:

     `31,139,8,...`

   - Backend Base64-decodes and executes `json.loads()` before gzip decompression in [main.py](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/main.py:153). Valid JSON would require:

     `[31,139,8,...]`

   - Parsing the bracketless string produces the observed error exactly:

     `JSONDecodeError: Extra data: line 1 column 3 (char 2)`

   - Direct reproduction returned:

     `HTTP 400 / invalid_input / Extra data: line 1 column 3`

   - Historical trigger: commit `29df328` changed `eval(b)` to `json.loads(b)`. The old decoder accepted `31,139,...` as a Python tuple; the hardened decoder does not.

2. **Frontend/backend deployment-version skew — low confidence**

   A backend before `29df328` accepts the current frontend format, while a current backend rejects it. This could explain environment-to-environment differences, but the current local failure is already explained by hypothesis 1.

3. **Ct-girder model validation failure — excluded as the immediate cause**

   The exception happens before `_read_json_model()` or `FemModel.run()` in [main.py](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/main.py:104). No Ct-specific field has been inspected when the observed 400 is generated.

   A corrected-envelope experiment advanced beyond decompression, confirming the original failure boundary. Its later `float(None)` error came from sending the raw preset without Angular’s normalization; the actual UI converts those null support values to zero in [input-fix-node.service.ts](C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/components/input/input-fix-node/input-fix-node.service.ts:131).

4. **URL, CORS, authentication, proxy, or service availability — very low confidence**

   The endpoint responds to GET, receives the POST, and returns a structured application-generated 400. This is not a refused connection or CORS-preflight failure.

### Why tests missed it

The compressed backend test explicitly sends `json.dumps(list(compressed))`, producing the bracketed format the server expects, in [test_axial_force_input.py](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/tests/io/test_axial_force_input.py:173). No test uses the real Angular `btoa(Uint8Array)` producer.

## Plan

The next discriminating validation should use the exact Angular-normalized Ct payload with a server-accepted envelope, then classify any subsequent model or result-schema failure separately.

## Patch Strategy

No patch was applied. A future fix should establish one canonical wire format and add a TypeScript-to-Python contract test; backward compatibility for already-deployed clients must be decided explicitly.

## Validation

- Reproduced current request: HTTP 400 with the exact `Extra data` diagnostic.
- Verified old `eval("31,139,8,0")` produces gzip bytes `1f8b0800`.
- Verified `json.loads("31,139,8,0")` produces the exact observed exception.
- Two independent read-only investigations reached the same conclusion.

## Risks

The same `btoa(pako.gzip(...))` pattern also exists in the printing path, but it targets another service and should be audited separately rather than changed automatically.
