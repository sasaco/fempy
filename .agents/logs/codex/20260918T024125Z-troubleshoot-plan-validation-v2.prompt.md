Objective: Re-validate the revised two-milestone fix plan for the FrameWebforJS calculation communication error.

Resolved findings:
- Milestone T (transport): current calculation producer sends Base64 of bracketless gzip-byte CSV; secure backend expects Base64 of a JSON integer array. The minimal compatible correction is calculation-only `btoa(`[${compressed.join(',')}]`)` in an exported pure encoder. Print remains bracketless CSV.
- Milestone U (user outcome): current backend response is flat `node_displacements/reaction_forces/element_stresses`; FrameWebforJS expects ordered load cases containing `disg/reac/fsec`. Ct has multiple load cases. No transport patch may be reported as complete user restoration until this separate contract is decided and implemented.

Revised plan:

Milestone T — independently implementable and reportable only as “transport repaired”:
1. Extract an exported calculation-only pure TypeScript encoder used directly by `AppComponent.post_compress()`.
2. Add an Angular Karma/Jasmine unit spec and run it with the existing Angular test builder in ChromeHeadless. The spec calls the production encoder and verifies Base64 decode -> JSON integer array -> gzip decode equals `JSON.parse(JSON.stringify(input))` for empty, ASCII, Japanese, Ct-normalized, and large-preset inputs; it asserts the unchanged `gzip,base64` header contract.
3. Add a focused Python integration regression that spawns the repository-local Node 18 runtime with `ts-node/register` (already a dev dependency) to call that same exported TypeScript encoder, submits the body to Flask/`Compressor.decompress`, and asserts exact serialized-object equality. Do not recreate the desired envelope in Python.
4. Implement brackets around `compressed.join(',')`; do not change backend decoding, printing, response encoding, headers, or use `Array.from`.
5. Add a print isolation regression proving its request still decodes to bracketless decimal CSV and retain an actual PDF smoke check.
6. Use the exact live Angular `InputData.getInputJson(0)` Ct flow to verify the original `Extra data`/transmission dialog is gone. Record this milestone only as transport repaired, including any next failure.
7. Large preset: enforce deterministic safeguards (production source contains no `Array.from`/spread of compressed bytes; exact round-trip; Chrome execution completes without crash). Record elapsed time and peak memory as informational baseline because the repository has no stable CI memory budget yet.

Blocking decision gate between milestones:
8. Before any result adapter/schema code, define and approve the authoritative multi-load-case contract: cases calculated, stable identifiers/order, backend-vs-frontend adaptation ownership, exact `node_displacements/reaction_forces/element_stresses` to `disg/reac/fsec` mappings, units/signs/member-end conventions, and node/member key formats. Until approved, Milestone U remains blocked and the overall user issue remains open.

Milestone U — separately approved after the gate:
9. Add a failing result-consumption test in the selected harness for the approved contract. It must reject flat incompatible output and assert expected case count/IDs, finite non-empty displacement/reaction/section-force data, no worker errors, and functioning result navigation.
10. Implement only the approved backend response transformation or frontend adapter.
11. Run a local Chrome end-to-end workflow using the existing local launcher/services and the named Ct preset. Acceptance is: no transmission dialog, expected case count/IDs, populated `disg/reac/fsec`, working result pages, finite displayed values, and no console/worker error. Also run TypeScript build/Karma tests, focused Python HTTP/integration tests, and print/PDF smoke.

Optional deployment branch:
12. Add a temporary strict server legacy-CSV fallback only if an explicit deployment inventory shows supported cached/deployed legacy calculation bundles. It requires strict Base64/token/integer/range/body/decompressed-size/gzip/UTF-8/top-level-object validation, telemetry, and removal criteria. Never use `eval` or `literal_eval`.

Completion reporting:
- Milestone T PASS must be called “transport repaired,” never “calculation fixed.”
- Overall issue remains open/blocked at the contract decision if Milestone U is not approved and passed.
- User-visible completion requires Milestone U end-to-end PASS.

Constraints:
- Minimal, backward-compatible, no security regression.
- Do not conflate transport success, HTTP 200, `isCalculated`, or empty worker output with user success.

Output format:
## Validation Result (PASS / NEEDS_REVISION)
## Missing Coverage
## Potential New Issues
## Additional Test Cases Recommended
## Revised Task List (if needed)
