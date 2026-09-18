## Risk Assessment (HIGH/MEDIUM/LOW)

**HIGH** overall until the calculation producer/consumer contract is aligned.

- Current and older FrameWebforJS calculation clients send legacy bracketless decimal CSV.
- The current Python backend accepts only a bracketed JSON byte array, so all such clients fail before gzip decompression.
- The mismatch was introduced when `eval` was correctly removed in `29df328`, exposing an undocumented compatibility dependency.
- A calculation-only canonical producer is **LOW risk** and compatible with both backend generations.
- A strictly validated legacy-CSV server fallback is **MEDIUM risk**, but safe as a temporary migration mechanism if bounded and non-executing.
- Sharing the canonical encoder with printing is **HIGH risk** because the separate C# PDF API explicitly expects CSV.
- Ordinary JSON requests, GET/OPTIONS, and compressed-response decoding are outside the affected request-decoding branch.

## Candidate Comparison

| Rank | Candidate | Risk | Assessment |
|---:|---|---|---|
| 1 | Calculation-only canonical producer | LOW | Recommended end state. Emit `btoa(\`[${compressed.join(",")}]\`)`. Works with old `eval` and current `json.loads`, while preserving the C# print protocol. Avoids explicitly boxing roughly two million numbers with `Array.from`. |
| 2 | Temporary strict backend CSV fallback | MEDIUM | Recommended migration bridge when cached or independently deployed old frontends must continue working. Deploy before candidate 1, measure legacy use, then retire after the compatibility window. |
| 3 | Coordinated raw-gzip Base64 replacement | MEDIUM–HIGH | Best possible wire-size improvement, but requires a versioned, coordinated protocol migration. Direct raw-byte Base64 would reduce the largest request substantially, but must not be folded into this incident fix. |
| 4 | Shared calculation-and-print canonical helper | HIGH | Reject. Bracketed output reaches C# as tokens such as `[31` and `0]`, which `Convert.ToByte` cannot parse. It would repair calculation while breaking PDF generation. |

Recommended rollout:

1. If stale-client compatibility matters, deploy candidate 2 first.
2. Deploy candidate 1 as the canonical calculation producer.
3. Leave printing unchanged.
4. Remove the fallback only after telemetry shows legacy traffic has disappeared.
5. Consider candidate 4 only as a separately versioned future protocol.

## Affected Code Paths

- Calculation producer: [app.component.ts](C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/app.component.ts:229) uses `pako.gzip` followed by `btoa(compressed)`, which coerces the `Uint8Array` to bracketless CSV.
- Calculation consumer: [main.py](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/main.py:140) Base64-decodes and calls `json.loads`, requiring a bracketed JSON array.
- Canonical protocol documentation: [endpoints.md](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/docs/wiki/endpoints.md:72) documents the bracketed JSON byte-array envelope.
- Print producer: [print.component.ts](C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/components/print/print.component.ts:1001) intentionally emits the same legacy CSV representation.
- Separate print consumers: [Function1.cs](C:/Users/sasai/Documents/FrameWeb3/FramePrintPDF/FramePrintAzure/Function1.cs:34) and [Function2.cs](C:/Users/sasai/Documents/FrameWeb3/FramePrintPDF/FramePrintAzure/Function2.cs:34) Base64-decode, split on commas, and convert each token to a byte.
- Four Python compressed-request tests cover only canonical arrays:
  - [test_input_routes.py](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/tests/integration/test_input_routes.py:86)
  - [test_spatial_general_plane.py](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/tests/integration/test_spatial_general_plane.py:98)
  - [test_axial_force_input.py](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/tests/io/test_axial_force_input.py:173)
  - [test_slip_support_history.py](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/tests/regression/test_slip_support_history.py:32)

## Implicit Contracts at Risk

- `btoa(Uint8Array)` is not raw-gzip Base64. JavaScript first converts the typed array to `"31,139,..."`.
- The old Python `eval` accidentally accepted both CSV tuples and JSON-style lists. That tolerance became an undeclared wire contract.
- The current backend’s `json.loads` hardening preserved the documented form but broke every deployed legacy calculation producer.
- Calculation and printing look identical at the Angular expression level but target different APIs with different input grammars.
- Frontend and backend can be deployed or cached independently. A frontend-only fix does not repair already cached clients talking to the strict backend.
- `Content-Encoding: gzip,base64` is treated merely as “not json” by the Python route; it is not normal HTTP content-coding processing.
- The largest known gzip payload is 2,025,295 bytes. `Array.from` would create approximately two million boxed numbers before serialization. `join` avoids that array but still creates a large CSV string and approximately 9.65 MB Base64 body.
- Current backend tests duplicate the canonical encoder in Python, so they prove consumer behavior but not browser-producer compatibility.

## Compatibility Matrix

| Calculation frontend | Wire form | Older backend (`eval`) | Current backend (`json.loads`) |
|---|---|---:|---:|
| Older FrameWebforJS | Decimal CSV | Pass | **Fail** |
| Current FrameWebforJS | Decimal CSV | Pass | **Fail** |
| Proposed calculation-only producer | Bracketed JSON byte array | Pass | Pass |

The print path is separate:

| Print producer | Current C# PDF API |
|---|---:|
| Existing decimal CSV | Pass |
| Shared bracketed canonical array | **Fail** |

Thus the canonical calculation producer is backward-compatible with both Python backend generations, but it must not replace the print encoder.

## Recommended Safeguards

- Introduce a calculation-specific, unit-tested helper whose output is exactly `btoa(\`[${compressed.join(",")}]\`)`.
- Keep the print encoder separate and unchanged; use names that expose the protocol distinction.
- Never restore `eval`, `literal_eval`, dynamic execution, or interpretation of arbitrary request text.
- If adding server tolerance:
  - Decode Base64 with strict validation.
  - Try the canonical JSON-array grammar first.
  - Permit fallback only for ASCII decimal CSV with no signs, whitespace, floats, exponents, empty tokens, or trailing commas.
  - Require exact integers in `0..255`; reject booleans, strings, nesting, and mixed types.
  - Bound HTTP body size, decoded textual size, byte count, gzip output size, decompression ratio, and final JSON size.
  - Reject invalid/truncated gzip, unwanted concatenated members, and trailing data.
  - Record only counters for legacy use—not request bodies—and define a removal date.
- Deploy fallback before frontend when stale bundles must work. Otherwise, candidate 1 can ship independently because its output works with both backend generations.
- For any raw-gzip Base64 migration, use an explicit versioned endpoint/header and dual-read transition; do not silently reinterpret the existing envelope.
- Preserve the current ordinary-JSON, GET, OPTIONS, error-response, and successful compressed-response behavior.

## Required Regression Tests

- Browser-side calculation helper:
  - Base64-decode output and assert valid bracketed JSON.
  - Assert every value is an integer byte.
  - Gzip-decompress and compare with the original JSON.
  - Cover empty/minimal input, bytes `0` and `255`, Unicode JSON, and the largest preset.
  - Measure the 2,025,295-byte gzip case for runtime and peak-memory regression.
- Cross-version contract fixture:
  - Legacy calculation CSV → old backend passes.
  - Legacy calculation CSV → strict current backend fails, or passes only when the bounded fallback is enabled.
  - Canonical calculation array → both backend generations pass.
  - Generate at least one fixture through the actual TypeScript/JavaScript helper rather than reimplementing it in Python.
- Backend:
  - Retain all four canonical compressed tests.
  - Add legacy CSV equivalence tests rather than converting existing canonical coverage.
  - Verify canonical and fallback requests produce identical model input and results.
- Malformed/security cases:
  - Invalid Base64, non-ASCII text, empty input, whitespace, signs, floats, exponents, hex, empty/trailing tokens, out-of-range bytes, booleans, strings, nested arrays, and oversized inputs.
  - Invalid/truncated gzip, excessive expansion, concatenated members, and trailing bytes.
  - Assert deterministic 4xx responses and that no request text is executed.
- Print:
  - Assert `getPostJson` still decodes to bracketless CSV.
  - Exercise both C# functions with that envelope.
  - Add a negative test proving bracketed calculation encoding is rejected by the unchanged print API.
- Route isolation:
  - Ordinary JSON POST remains unchanged.
  - GET and OPTIONS remain unchanged.
  - Successful compressed-response Base64/gzip decoding remains unchanged.
  - Error responses remain ordinary uncompressed JSON.
- Test infrastructure:
  - Add focused Angular/helper specifications; none currently cover this producer.
  - Add a contract test that fails if calculation and print are later “deduplicated” into one encoder.
