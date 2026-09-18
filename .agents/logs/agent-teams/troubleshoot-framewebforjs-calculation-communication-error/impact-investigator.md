# Work Log: Impact Investigator

## Summary

Traced the transport regression to `29df328`, enumerated calculation and print wire contracts, audited the missing cross-stack coverage, and evaluated compatibility/security/performance risks without changing product code.

## Tasks Completed

- [x] History: used `git show`, `git log`, and `git blame` to identify the strict-parser change, its security intent, the prior permissive behavior, and the later docs/tests that masked the real browser producer.
- [x] Blast radius: classified calculation, print, ordinary JSON, canonical compressed, response, launcher, cached-client, and deployment-skew paths.
- [x] Coverage audit: identified four backend tests that recreate only the canonical envelope, zero Angular unit specs, and the local smoke test's failure to exercise compressed browser calculation.
- [x] Compatibility/risk: proved the old/new frontend/backend matrix, assessed four fix candidates, quantified the high-water preset's allocation risk, and specified exact regressions.
- [x] Codex protocol: ran both mandatory read-only consultations with 300-second bounds and medium reasoning, read every response file, recorded unusable/timeout outcomes as unavailable evidence, and independently verified two usable single-line supplemental risk assessments.
- [x] Durable output: wrote `.agents/docs/research/troubleshoot-framewebforjs-calculation-communication-error-impact.md`.

## Git History

- Introducing commit: `29df328eb29d1a17b91f706eb1b7f6dd16a0fa78` - replaced `eval(b)` with `json.loads(b)` in the compressed calculation decoder, intentionally eliminating request-text execution but narrowing the accepted envelope.
- Related commits: `d111a02` documented the canonical JSON byte array; `f275b4e` added Python-generated canonical compressed tests; `be59bd4` imported the already-legacy frontend into the monorepo; `971f98f` added a smoke test that skipped compressed calculation; `90ea69c` exposed local anonymous calculation without changing the encoder.
- Available history cannot date the frontend producer before `be59bd4`; the combined repository was definitely incompatible from that import onward.

## Blast Radius

- Affected code paths: every `AppComponent.calcrate()` request against the current backend; every other bracketless-CSV calculation client against post-`29df328` backend.
- Affected features/users: all calculation models/presets for authenticated and allowed-local-anonymous users; Ct is only the reported example.
- Potentially affected: cached/deployed older frontend bundles and external clients if paired with a newer backend.
- Unaffected: ordinary JSON, canonical compressed clients, GET/OPTIONS, response decompression, and the separate print API. Print must remain bracketless CSV because its C# consumers split on commas.
- Secondary blocker: current backend result keys do not match the frontend's per-case `disg`/`reac`/`fsec` expectation, so transport success alone does not establish UI success.

## External Research

None. Repository code, git history, and executable probes completely resolve the transport defect; no dependency or upstream issue research was necessary.

## Regression Risk

- Existing test coverage: four Python request tests cover only a hand-built canonical array; there are no Angular specs; the local smoke test uses ordinary JSON for calculation and CSV only for print.
- Preferred change: calculation-only canonical array output using a calc-specific helper; leave print unchanged. The semantic wire recommendation is low risk with old and current calculation backends.
- Performance correction: avoid `Array.from` for the 2,025,295-byte compressed high-water preset; a typed-array `join(',')` wrapped in JSON brackets avoids boxing roughly two million numbers.
- Optional compatibility branch: medium risk and rollout-only; safe only with strict non-executable grammar/range/size/gzip validation, telemetry, and removal plan.
- Rejected broad changes: shared calculation/print canonical helper would break print; raw-gzip protocol replacement creates unnecessary coordinated-deployment risk.
- Required tests: real JS producer to Python decoder, print contract preservation, actual Ct UI/result consumption, large-preset browser memory/performance, and strict malformed-input tests if fallback is added.

## Codex Risk Analysis

- Required-label status: `troubleshoot-frameweb-regression` returned only context-loader status, its follow-up timed out, and `troubleshoot-frameweb-fix-safety` received only the prompt's first physical line. These responses are unavailable evidence; every response file was read and the timed-out call was not retried.
- Regression-risk supplemental (`troubleshoot-frameweb-regression-oneline`): rated the unresolved mismatch HIGH overall, the calculation-only canonical producer LOW risk, a strict temporary server fallback MEDIUM risk, and shared calculation/print encoding HIGH risk. It also required the real JS-to-Python contract, large-preset, print-isolation, route-isolation, mixed-version, and malformed-input regressions.
- Fix-safety supplemental (`troubleshoot-frameweb-fix-safety-oneline`): rated the calc-only typed-array `join` producer functionally/security safe but the release CAUTION because transport repair may expose the independent result-schema mismatch. It endorsed a frontend-only rollout unless legacy cached clients require a bounded, instrumented, time-limited fallback.
- Both usable supplements completed read-only within 300 seconds at medium reasoning. Their details were independently checked against repository code/history; the primary findings remain evidence-led rather than consultation-dependent.

## Communication with Teammates

- -> `/root/root_cause_analyst`: sent the introducing commit, old/new parser proof, full producer/consumer classification, print incompatibility, compatibility matrix, live canonical-envelope result, downstream result-schema risk, and large-preset memory measurement.
- <- `/root/root_cause_analyst`: confirmed the root cause and recommended calculation-only canonical output, an executable JS-to-Python contract test, print isolation, optional staged strict fallback for cached clients, and full UI result validation.
- -> `/root`: sent staged history/blast-radius findings, the downstream response blocker, final root-cause recommendation, and the quantified `Array.from` high-water memory risk.

## Issues Encountered

- Codex prompt delivery on Windows exposed only the first physical Markdown line despite the wrapper storing the full prompt; one follow-up also timed out. Those responses were treated as unavailable, while single-line supplemental prompts completed successfully and supplied independently verified risk corroboration.
- A PowerShell `rg` path containing a Unix-style wildcard produced Windows error 123; reran with directory roots plus `-g` filters.
- The repository lacks pre-import frontend history, so the exact original introduction date of `btoa(Uint8Array)` cannot be recovered from available commits.
