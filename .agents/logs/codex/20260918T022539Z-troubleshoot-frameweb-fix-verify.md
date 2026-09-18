## Correctness Assessment — INCOMPLETE

The recommended strategy is correct for repairing the observed HTTP 400 transport failure, but it is not sufficient to claim that Ct-girder calculation works end-to-end.

The producer change should proceed. However, completion must also address or explicitly defer the calculation response-contract mismatch.

## Root-Cause Coverage

The proposed calculation-specific encoding is correct:

```ts
const compressed = pako.gzip(JSON.stringify(jsonData));
const body = btoa(JSON.stringify(Array.from(compressed)));
```

The current producer passes a `Uint8Array` directly to `btoa`, yielding Base64-encoded comma-separated text rather than the JSON array required by [`Compressor.decompress`](</C:/Users/sasai/Documents/FrameWeb3/FrameWeb/main.py:144>). See [`app.component.ts`](</C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/app.component.ts:229>).

The executable control confirmed:

- Current body: HTTP 400, `Extra data: line 1 column 3`.
- Canonical body: exact gzip round-trip succeeded and the request advanced beyond transport decoding.
- The subsequent `float(None)` came from the approximate payload, not the transport.
- Canonical `[31,139,…]` is accepted by both the former `eval` decoder and current `json.loads` decoder.
- Restoring `eval` is neither necessary nor acceptable.

This affects every compressed calculation request against the current backend, not only the Ct preset.

## Edge Case Coverage

Covered or structurally sound:

- Non-ASCII model JSON: gzip handles UTF-8; the outer JSON integer array is ASCII.
- Empty model objects: gzip output itself is never empty for a valid JSON string.
- Gzip integrity/CRC: checked by `gzip.decompress`.
- Print isolation: correct and essential. [`print.component.ts`](</C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/components/print/print.component.ts:1001>) must retain its current CSV encoding because the C# consumer splits decoded text on commas in [`Function1.cs`](</C:/Users/sasai/Documents/FrameWeb3/FramePrintPDF/FramePrintAzure/Function1.cs:37>).

Still required:

- Exercise the extracted production JavaScript helper, not a test-side reimplementation.
- Test the exact Angular `getInputJson(0)` result for the Ct preset.
- Benchmark the maximum supported production model. `Array.from` adds a second byte representation in memory, although wire size is almost unchanged from the current decimal CSV.
- Apply strict validation to both canonical and temporary legacy inputs:
  - `base64.b64decode(..., validate=True)`
  - top-level array only
  - integers excluding booleans
  - values `0..255`
  - compressed-body and decompressed-body limits
  - valid gzip and top-level JSON object

The current decoder does not use strict Base64 validation, has no decompression-size bound, and `bytes(...)` treats booleans as integers. Merely adding malformed-input tests without defining these expectations is insufficient.

## Compatibility Matrix

| Calculation client | Backend | Transport result |
|---|---|---|
| Legacy CSV client | Pre-`29df328` `eval` backend | Works, but backend is unsafe |
| Legacy CSV client | Current strict backend | Fails with the observed HTTP 400 |
| Canonical client | Pre-`29df328` backend | Works |
| Canonical client | Current strict backend | Works |
| Legacy CSV client | Proposed strict compatibility backend | Works temporarily |
| Canonical client | Proposed strict compatibility backend | Works |
| Print client, unchanged | Existing C# print service | Works |
| Print client changed to canonical format | Existing C# print service | Breaks |

Therefore:

- If cached legacy calculation bundles are not supported, deploy the frontend-only repair.
- If they are supported, deploy the bounded compatibility decoder first, then the frontend, observe legacy usage, and remove compatibility later.
- Do not retain the old unsafe backend merely for compatibility.

## New Failure Modes

1. **Response-contract incompatibility remains.**  
   The backend returns fields such as `node_displacements`, `reaction_forces`, and `element_stresses`. The frontend workers instead iterate `Case*` objects containing `disg`, `reac`, and `fsec`; for example [`result-disg1.worker.ts`](</C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/components/result/result-disg/result-disg1.worker.ts:25>) and [`result-reac1.worker.ts`](</C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/components/result/result-reac/result-reac1.worker.ts:27>).

2. **This is more than field renaming.**  
   The Ct preset contains 11 load cases, while current backend integration behavior deliberately ignores the second case in one contract test and returns one solver result structure. A multi-case response adapter or calculation orchestration contract must be designed.

3. **The UI may report false success.**  
   [`loadResultData`](</C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/providers/result-data.service.ts:87>) starts workers asynchronously, but the caller immediately sets `isCalculated = true`. Workers can silently skip objects without `disg/reac/fsec`, leaving empty results while displaying completion.

4. **Error handling is brittle.**  
   The catch path calls `e.includes(...)`. A non-string exception, such as a schema-related `TypeError`, can itself break the error handler.

5. **A shared encoder could break printing.**  
   The helper must be calculation-specific unless transport format is an explicit parameter.

6. **A compatibility parser can become a denial-of-service surface.**  
   Limits must apply before Base64 decoding, after byte-array parsing, and during bounded gzip expansion—not only to the legacy branch.

## Required Validation Before Claiming User Outcome Fixed

1. Add unit tests for the real calculation encoder covering ASCII, Japanese text, empty object, and a representative maximum-size model.
2. Add a JavaScript-to-Python contract test proving exact object equality through the production encoder and `Compressor.decompress`.
3. Add malformed-envelope tests for invalid Base64, non-array JSON, booleans, floats, strings, nested values, out-of-range integers, truncated gzip, bad CRC, oversized compressed input, and excessive expansion.
4. Run the actual Angular Ct preset through `getInputJson(0)` and require transport decoding plus model construction to succeed.
5. Define and test the result contract:
   - load-case cardinality,
   - `Case*` wrapping,
   - displacement/reaction/section-force mappings,
   - units and signs,
   - combine/pickup behavior.
6. Run a browser-level Ct calculation and assert:
   - no transmission or calculation dialog,
   - HTTP 200,
   - non-empty displacement, reaction, and section-force results,
   - correct case count,
   - result navigation and rendering work.
7. Verify printing independently after the calculation change.
8. If compatibility mode is deployed, add telemetry and an explicit removal criterion.

## Confidence Level

- Immediate transport root cause: **very high, 99%**
- Proposed producer fix for that root cause: **very high, 99%**
- Print-path isolation requirement: **very high, 99%**
- Strategy completeness for the full user-visible calculation outcome: **high confidence that it is incomplete, 95%**

No product files were changed.
