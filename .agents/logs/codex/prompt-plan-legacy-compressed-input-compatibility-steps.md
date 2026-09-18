Objective: Decompose a backend compatibility repair into an ordered, independently testable implementation plan.

Context:
- Purpose: Make the current FrameWeb backend accept the same bracketless decimal gzip-byte CSV that C:\Users\sasai\Documents\FrameWeb2\main.py accepted, while preserving current canonical JSON byte-array support and never restoring executable parsing.
- Scope in:
  - FrameWeb/main.py Compressor.decompress and focused backend tests.
  - Strict dual-envelope parsing: canonical JSON integer array first; on JSONDecodeError only, legacy ASCII decimal CSV.
  - Validation that every item has exact type int (reject bool) and lies in 0..255.
  - Stable HTTP 400 invalid_input behavior for malformed transport input.
  - Existing uncompressed JSON and canonical compressed request regressions.
- Scope out:
  - FrameWebforJS producer changes.
  - Print C# contracts.
  - Frontend/backend calculation-result schema and multi-load-case mapping.
  - Raw-gzip protocol redesign, dependency changes, and broad backend rewrites.
- Constraints:
  - Never use eval or ast.literal_eval.
  - Reject empty tokens, whitespace-only input, signs, decimals, exponents, non-ASCII, bool, float, string, nested values, and values outside 0..255.
  - Preserve current JSON-array clients and error diagnostics.
  - Treat only expected transport/decode failures as input errors; do not hide unexpected defects.
  - User requested backend behavior aligned with old FrameWeb2/main.py for this incident.

Current state (with file evidence):
- Old C:\Users\sasai\Documents\FrameWeb2\main.py:107-113 Base64-decodes, calls eval(b), reconstructs bytes, ungzips, then JSON-decodes. eval accidentally accepts bracketless `31,139,8,...` as a tuple but is unsafe.
- Current FrameWeb/main.py:91-102 routes non-`json` requests to Compressor.decompress.
- Current FrameWeb/main.py:153-160 Base64-decodes and calls json.loads(b), so it only accepts bracketed JSON arrays such as `[31,139,8,...]`.
- Current FrameWeb/main.py:130-132 sends exceptions through diagnostic_payload.
- FrameWeb/src/fem/diagnostics.py:22-25 defines InputValidationError as HTTP 400 invalid_input; lines 52-80 map generic ValueError/KeyError/TypeError to 400 and unexpected exceptions to 500.
- Actual Angular producer FrameWebforJS/src/app/app.component.ts:233-245 emits Base64 of bracketless decimal CSV by passing Uint8Array directly to btoa.
- Existing canonical compressed tests build JSON byte arrays, not the browser envelope: FrameWeb/tests/integration/test_input_routes.py:86-89, integration/test_spatial_general_plane.py:98-101, io/test_axial_force_input.py:173-181, regression/test_slip_support_history.py:33-39.
- Direct probes: canonical JSON byte-array with the real compression header reaches normal processing; the same gzip bytes in legacy CSV currently return HTTP 400 Extra data; JSON `[true]` currently reaches bytes conversion and becomes an unexpected 500, proving type validation is missing.
- Separate known issue: backend result fields and frontend per-case result consumers may differ; this must remain a separate follow-up and cannot be counted as fixed by transport success.

Constraints for your response:
- Order steps by dependency.
- Each step must be independently testable and name its verification.
- Put parser contract/security tests before implementation (TDD).
- Keep product changes minimal and backend-only.
- Explicitly define fallback conditions and error mapping.
- Include rejection tests for code-like payloads demonstrating no execution path.
- Define a transport completion gate distinct from full Ct calculation completion.

Output format:
## Implementation Steps (ordered by dependency)
## Verification per Step
## Risks and Mitigations
## Open Questions
