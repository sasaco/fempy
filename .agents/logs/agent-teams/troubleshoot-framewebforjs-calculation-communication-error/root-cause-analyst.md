# Work Log: Root Cause Analyst

## Summary

Established the exact producer/consumer wire-contract regression, proved the parser boundary with a same-byte accepted-envelope control, evaluated every initial hypothesis, and compared secure compatibility strategies without changing product code.

## Tasks Completed

- [x] Loaded repository rules, state/design context, troubleshoot instructions, Bug Report, Phase-1 context, and reproduction artifact.
- [x] Traced `calcrate()` through `post_compress()`, Flask routing, `Compressor.decompress()`, diagnostic classification, and the Angular error callback.
- [x] Proved why `json.loads()` reports character 2 / column 3.
- [x] Ran a same-byte Ct-derived accepted-envelope control and bounded Ct model content out of the observed transport failure.
- [x] Inspected commit `29df328`, existing compressed tests, calculation/print consumers, current result schema, and large-preset encoding size.
- [x] Completed all four mandatory uniquely labelled Codex consultations; marked the timed-out hypothesis run unavailable as evidence.
- [x] Communicated root-cause, compatibility, print isolation, performance, and post-transport findings bidirectionally with the Impact Investigator.
- [x] Wrote the detailed root-cause artifact.

## Hypotheses Evaluated

- [confirmed] Compressed-request contract regression: current calculation producer sends Base64 of bracketless decimal CSV, while current backend requires Base64 of a JSON integer array.
- [eliminated as required current cause; historically inconclusive] Deployment-version skew: old backend behavior explains prior compatibility, but mixed versions are unnecessary for the checked-in current failure and deployed version inventory is absent.
- [eliminated for observed error] Ct-girder content/model validation: parsing fails before gzip or model inspection.
- [eliminated] URL/service availability: the application receives and answers the POST.
- [eliminated] CORS: backend parsing is reached and a non-browser client reproduces the same application error.
- [eliminated] Authentication/anonymous UID: the endpoint performs no authentication before decompression and empty UID reproduces the same failure.
- [eliminated] Green backend tests refute the mismatch: the tests construct the canonical envelope and do not exercise the JavaScript producer.

## Root Cause

- Defect: `btoa(pako.gzip(json))` stringifies `Uint8Array` as `31,139,...`; the backend's secure `json.loads()` decoder requires `[31,139,...]`.
- Location: `FrameWebforJS/src/app/app.component.ts:233-245` and `FrameWeb/main.py:153-160`; regression trigger `29df328eb29d1a17b91f706eb1b7f6dd16a0fa78`.
- Trigger condition: any current compressed calculation request sent to a post-`29df328` backend. The comma after the valid JSON number `31` is zero-based char 2 / one-based column 3.

## Proposed Fixes

- Approach A: calculation-only canonical producer using `[${compressed.join(',')}]` before `btoa` — directly conforms to the documented contract, works with old/current backends, avoids the `Array.from` boxed-number spike, and leaves print unchanged; does not repair cached legacy clients.
- Approach B: temporary strict backend dual parser — restores legacy clients but adds grammar, validation, decompression-limit, observability, and removal obligations; never use `eval` or `literal_eval`.
- Approach C: versioned Base64(raw gzip) or standards-based gzip — more efficient long-term but requires a coordinated protocol migration and is too broad for the incident fix.
- Recommended: Approach A as the durable fix; deploy B first only when supported legacy/cached calculation clients require a mixed-version rollout. Full completion also requires resolving or explicitly scoping the separate result-schema/load-case incompatibility.

## Codex Consultations

- Execution flow (`troubleshoot-frameweb-flow`): exit 0; confirmed exact transforms and line/column calculation.
- Hypothesis evaluation (`troubleshoot-frameweb-hypothesis`): timed out after the 300-second bound; response read but treated as unavailable evidence.
- Fix design (`troubleshoot-frameweb-fix-design`): exit 0; favored backend-first dual compatibility then canonical producer when old deployed clients must work.
- Fix correctness (`troubleshoot-frameweb-fix-verify`): exit 0, verdict INCOMPLETE; transport fix is correct, but exact Angular-normalized Ct plus result-consumption/UI verification is mandatory before claiming the user outcome fixed.

## Communication with Teammates

- → `/root/impact_investigator`: shared the confirmed root cause, exact parser boundary, conditional producer/server compatibility recommendation, print isolation, and required end-to-end Ct/result verification.
- ← `/root/impact_investigator`: received introducing/history evidence, blast radius, separate C# print contract, current response-schema incompatibility, and quantified large-preset memory/wire-size evidence.
- → `/root`: sent status, confirmed state transforms, control result, compatibility recommendation, large-preset encoder refinement, and post-transport blocker.

## Issues Encountered

- First inline `node -e` control attempt lost quoting across PowerShell/native argument parsing and failed before reaching product code. Recorded the caveat and replaced it with an explicit `.agents/logs/*.cjs` artifact created via `apply_patch`.
- Hypothesis Codex consultation exceeded the 300-second bound. Per instruction, it was not retried and is not treated as validation evidence.
- The accepted-envelope control only approximates Angular provider normalization. Its later `float(None)` response is bounded as a separate downstream input issue, not a successful end-to-end Ct result.
