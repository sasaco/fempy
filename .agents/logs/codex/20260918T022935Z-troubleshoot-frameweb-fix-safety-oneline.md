## Safety Assessment (CAUTION)

The calculation-only producer change is functionally and security-wise safe:

```ts
btoa(`[${compressed.join(",")}]`)
```

It emits the canonical JSON integer-array envelope required by the current Python decoder, remains accepted by the older `eval` decoder, and preserves the security hardening introduced by `29df328`.

The overall release remains **CAUTION**, not SAFE, because fixing transport only gets requests past decompression. The current backend returns `node_displacements`, `reaction_forces`, and `element_stresses`, while the frontend consumes per-case `disg`, `reac`, and `fsec` structures. These differ in nesting, field names, and representation—not merely key names—so calculations may return HTTP 200 while the UI shows empty or unusable results.

## New Issues Identified

- The frontend result workers expect structures such as `CaseN.disg`, `CaseN.reac`, and `CaseN.fsec`. The backend supplies solver-native top-level structures.
- Reaction components differ: the frontend expects `tx/ty/tz`; the backend exposes `fx/fy/fz`.
- Element stresses are endpoint arrays, while the frontend expects member/P-point records with fields such as `fxi`, `fxj`, and `L`.
- The accepted-envelope live probe proved transport acceptance but used only an approximation of `InputData.getInputJson(0)` and failed later during model processing. It therefore does not establish calculation or UI compatibility.
- The current decoder has no explicit request-size or decompressed-size bounds. That is pre-existing, but adding a compatibility branch without limits would enlarge its attack surface.
- Any generic/shared transport-helper change could accidentally alter printing. The C# print functions Base64-decode to bracketless CSV and call `Split(',')`; brackets would make the first and last byte tokens invalid.

## Side Effects

- Functional blast radius today: every compressed FrameWebforJS calculation against the current backend fails before model parsing, regardless of model contents.
- Unaffected paths: ordinary JSON requests, already-canonical compressed clients, GET, OPTIONS, and compressed response decoding.
- The proposed producer change adds only two envelope characters before Base64 encoding. It does not change the gzip bytes or calculation JSON.
- For the largest preset, the 2,025,295-byte gzip output expands to roughly 7.2 million decimal-CSV characters and about 9.65 MB of Base64. Encoding remains synchronous and can pause the UI, but this is materially the same work already performed implicitly by `btoa(Uint8Array)`.
- `compressed.join(",")` avoids the additional large `Array.from(compressed)` allocation and associated array-slot/boxing pressure. Peak memory will still include the gzip buffer, CSV string, Base64 string, and HTTP body.
- Do not use spread-based byte conversion such as `String.fromCharCode(...compressed)`; a two-million-element argument list can exceed engine limits.
- Transport success may expose the separate response-contract mismatch and change the visible failure from “communication error” to empty, partial, or post-processing failure.

## Compatibility Matrix

| Frontend producer | Older backend (`eval`) | Current backend (`json.loads`) | Current backend with strict fallback |
|---|---:|---:|---:|
| Older/current legacy bracketless CSV | Works, but backend executes untrusted input | Fails with `Extra data` | Works temporarily |
| Fixed canonical JSON-array producer | Works | Works | Works |
| Existing canonical compressed clients | Works | Works | Works |
| Ordinary uncompressed JSON client | Works | Works | Works |
| Print producer with bracketless CSV | Works with C# print consumer | Independent service | Must remain unchanged |
| Print producer accidentally changed to JSON array | Breaks C# `Split(',')`/`Convert.ToByte` | Independent service | Still breaks printing |

The canonical frontend is therefore deployment-skew tolerant. A frontend-first rollout works with both backend generations. A server fallback is needed only if cached or independently deployed legacy frontend bundles must continue working against the current backend before clients refresh.

## Security Conditions for Optional Server Fallback

A temporary server fallback is acceptable without executable parsing only if all of these conditions are met:

- Attempt canonical JSON-array decoding first; enter the legacy branch only on a JSON syntax failure.
- Accept only canonical bracketless decimal CSV, with no whitespace, signs, exponents, brackets, expressions, names, strings, empty fields, or trailing comma. A suitable grammar is equivalent to `0|[1-9][0-9]{0,2}`, comma-separated.
- Reject an empty list unless explicitly required by the protocol.
- Require the parsed envelope to be exactly a list and every item to satisfy `type(value) is int` and `0 <= value <= 255`.
- Use neither `eval`, `ast.literal_eval`, dynamic imports, nor any other executable/general-purpose parser.
- Use strict Base64 validation and reject invalid alphabet, padding, or trailing junk.
- Bound the HTTP/Base64 body, decoded envelope, parsed byte count, and decompressed JSON independently.
- Decompress incrementally with an output ceiling. Do not rely solely on `gzip.decompress`, which may allocate the full expansion before a size check.
- Require a valid gzip member, completed stream, valid CRC/trailer, no concatenated members, and no trailing compressed data.
- Validate the decompressed payload as UTF-8 JSON and require the expected top-level object type before model construction.
- Return a generic 400 diagnostic without echoing large or attacker-controlled payloads.
- Instrument fallback usage by client/build version where available, but never log request bodies.
- Time-box the fallback with an explicit removal criterion. It must not become a permanent alternate protocol.

If the backend is modified, these limits and exact type/range checks should cover both canonical and legacy envelopes so the fallback is not more constrained than the primary path.

## Mitigation Recommendations

1. Change only the calculation producer or a calculation-specific pure helper to:

   ```ts
   export function encodeCalculationBytes(compressed: Uint8Array): string {
     return btoa(`[${compressed.join(",")}]`);
   }
   ```

2. Do not modify the print producer, introduce a global gzip/Base64 helper, or change response decoding.

3. Prefer a frontend-only rollout when legacy cached bundles can be invalidated. It is compatible with both old and current backends and preserves the strict backend parser.

4. If legacy bundles must survive, use this rollout sequence:

   1. Deploy the bounded, instrumented decimal-CSV fallback.
   2. Deploy/cache-bust the canonical frontend.
   3. Measure fallback traffic.
   4. Deprecate and remove the fallback after the agreed compatibility window.

5. Keep the result-schema problem separate from this minimal transport patch, but treat it as a release blocker for claiming full calculation functionality. Define an explicit adapter between solver-native output and the frontend’s per-case result contract.

6. Avoid `Array.from(compressed)` for the large preset. The explicit typed-array `join` produces the same decimal sequence without constructing another multi-million-entry array.

## Required Validation

- Add an Angular unit spec for the pure calculation helper asserting that `Uint8Array([31,139,8,0])` produces Base64 of exactly `[31,139,8,0]`, round-trips to the original bytes, and does not mutate its input.
- Add an executable cross-runtime regression: compile/import the real JavaScript/TypeScript producer in Node, generate the request body there, pass that exact output to Python’s current `Compressor.decompress`, and assert equality with the original JSON object. Do not recreate the producer in Python.
- Preserve explicit backend coverage that canonical JSON-array envelopes succeed and legacy bracketless envelopes fail when fallback is disabled.
- If fallback is enabled, test valid legacy CSV plus invalid Base64, whitespace, signs, leading zeros, floats, booleans, expressions, out-of-range values, empty tokens, trailing commas, excess encoded/decoded/decompressed sizes, truncated gzip, bad CRC, concatenated members, trailing data, zip bombs, invalid UTF-8, and non-object JSON.
- Run the largest preset through the real helper and browser runtime. Confirm 2,025,295 gzip bytes, approximately 9.65 MB Base64, no `InvalidCharacterError`, acceptable peak memory/UI pause, and successful backend decoding.
- Add a print regression proving its request remains bracketless CSV and is accepted by the actual C# consumer.
- Revalidate ordinary JSON POST, canonical compressed POST, GET, OPTIONS, compressed response decode, and error handling.
- Perform an end-to-end test using the real Angular preset-loading and provider path—not manual key deletion:
  1. Load the Ct preset through `PresetComponent`.
  2. Obtain the actual `InputData.getInputJson(0)` output.
  3. Add the normal `uid` and `production` fields.
  4. Encode through the new calculation helper.
  5. POST to the current backend.
  6. Decode the actual response through the Angular success path.
  7. Exercise `InputData.getResult` and `ResultData.loadResultData`.
  8. Wait for all displacement, reaction, and section-force workers.
  9. Assert non-empty, numerically correct per-case tables and 3D result consumption, with no worker, console, dialog, or runtime errors.

Transport repair is validated when step 5 succeeds. The user-visible calculation flow is validated only when all nine steps succeed.
