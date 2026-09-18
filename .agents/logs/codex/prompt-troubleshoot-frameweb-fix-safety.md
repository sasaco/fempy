# Objective

Analyze whether the Root Cause Analyst's recommended fix for the FrameWebforJS calculation transport regression is safe and identify all necessary safeguards.

# Continuation requirement

Repository context loading may run first. After it finishes, you MUST immediately perform the requested analysis in this same run. Do not stop at a context status message, do not ask for more input, and do not delegate.

# Constraints

- Read-only analysis. Do not edit source or tests.
- Do not perform web research; repository evidence is sufficient.
- Never recommend restoring `eval` or using `literal_eval` on request data.
- Distinguish the calculation request path from the independent print request path.
- Treat successful transport decoding as necessary but not sufficient for full application success.

# Root cause

`FrameWebforJS/src/app/app.component.ts:229-247` passes the `Uint8Array` from `pako.gzip` directly to `btoa`, producing Base64 of bracketless decimal CSV. Since commit `29df328`, `FrameWeb/main.py:153-160` uses `json.loads` and requires Base64 of a bracketed JSON integer array. The request fails before model parsing.

# Root Cause Analyst's final recommendation

Correct only the calculation producer (`app.component.ts` or a calculation-specific pure helper) so it emits:

`base64(JSON.stringify(Array.from(pako.gzip(json))))`

Add an executable JavaScript-producer-to-Python-decoder contract regression. Leave the print producer unchanged because its C# consumers require bracketless CSV.

If already deployed or cached legacy calculation bundles must continue to work, use a phased rollout: first add a temporary backend branch that accepts only a strictly validated decimal-CSV byte list (no `eval` or `literal_eval`; validate grammar, byte integers 0..255, encoded/decoded sizes, and gzip), then deploy the canonical calculation producer, measure legacy use, and later remove the compatibility branch.

# Blast radius and dependencies

- Every FrameWebforJS calculation uses the failing producer, so all models/presets are affected against the current backend; Ct data is not the trigger.
- Ordinary uncompressed JSON clients, canonical compressed backend clients/tests, GET, OPTIONS, and response decompression are unaffected.
- The print producer at `FrameWebforJS/src/app/components/print/print.component.ts:1001-1011` uses the same current JavaScript expression but calls C# `FramePrintPDF/FramePrintAzure/Function1.cs`/`Function2.cs`, which Base64-decode bracketless CSV and split on commas. A shared canonical encoder would break print.
- Pre-29df328 `eval` accepts both legacy CSV and canonical JSON arrays. Current `json.loads` accepts only canonical arrays.
- No Angular unit spec covers the calculation producer. Four Python test locations generate canonical arrays in Python and therefore miss the browser producer.
- Current backend result keys (`node_displacements`, `reaction_forces`, `element_stresses`) do not match the frontend workers' per-load-case `disg`/`reac`/`fsec` expectation. This is a separate response-contract issue that can appear after request transport is fixed.
- A live canonical-envelope probe bypassed the `Extra data` decoder error but the approximate, non-Angular-normalized Ct payload then failed later in model input. Full UI validation must therefore use actual `InputData.getInputJson(0)` output.

# Acceptance checks

- Assess new functional, security, performance/memory, deployment-skew, and compatibility failure modes.
- Explicitly assess current/older frontend against current/older backend.
- State whether a strict server fallback can be safe without reintroducing executable parsing.
- Require exact regression tests and an end-to-end acceptance check that distinguishes transport success from calculation/result-consumption success.

# Output format

## Safety Assessment (SAFE / CAUTION / UNSAFE)
## New Issues Identified
## Side Effects
## Compatibility Matrix
## Security Conditions for Optional Server Fallback
## Mitigation Recommendations
## Required Validation
