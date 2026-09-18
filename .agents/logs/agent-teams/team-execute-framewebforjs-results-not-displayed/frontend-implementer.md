# Work Log: frontend-implementer
## Summary
Implemented the FrameWebforJS opt-in for `legacy-cases-v1` and added fail-fast validation before result storage or worker dispatch. Added focused validator coverage and kept invalid results uncalculated.
## Tasks Completed
- [x] Added the exact `Accept: application/vnd.frameweb.legacy-cases-v1+json` calculation request header without changing the existing request content headers or response decoding.
- [x] Added an exported pure validator for non-empty legacy case maps, required object-valued `disg`/`reac`/`fsec`, and exact ordered expected case IDs.
- [x] Applied validation before `InputData.getResult`, `ResultDataService.loadResultData`, worker dispatch, and `isCalculated = true`; direct service callers receive the generic shape check too.
- [x] Hardened calculation error handling for `Error` and unknown thrown values while preserving Japanese server JSON-error handling and using a safe validation message.
- [x] Added focused Jasmine cases for valid data, flat/empty/null/array inputs, invalid case values, missing/extra/reordered IDs, missing fields, and null/array fields.
- [x] Review fix: rejected cases whose `disg`, `reac`, and `fsec` maps are all empty while preserving valid partial-empty cases.
- [x] Ran scoped TypeScript and diff checks; documented test/build/verifier infrastructure blockers.
## Files Modified
- `FrameWebforJS/src/app/app.component.ts`: requested the versioned response, checked request case IDs against the decoded response, and made the catch path type-safe.
- `FrameWebforJS/src/app/providers/result-data.service.ts`: added the result types, validation error, pure validator, and the service-level validation guard.
- `FrameWebforJS/src/app/providers/result-data.service.spec.ts`: added focused validator and invalid-dispatch guard tests.
## Key Decisions
- Empty individual `disg`, `reac`, or `fsec` objects remain valid because some valid models can have no entries in a category; each case must still contain at least one entry across the three required maps.
- Expected case IDs are compared in exact request insertion order so missing, extra, and reordered responses fail before any consumer mutates state.
- Validation errors expose only the fixed user-facing message `計算結果の形式が不正です。`, not response contents.
- `npm run build:prod -- --no-progress` was not run because the script invokes Electron webpack and file-copy packaging outside this task; a packaging-free Angular production build was attempted instead.
## Review Fix
- Addressed the test review HIGH finding by adding a per-case aggregate non-empty invariant and explicit all-empty rejection plus displacement-only, reaction-only, and section-force-only acceptance tests.
## Communication with Teammates
None
## Issues Encountered
- `npm test -- --watch=false --browsers=ChromeHeadless --include=src/app/providers/result-data.service.spec.ts` exited 1 before test execution: `src/test.ts` and `src/polyfills.ts` are missing from the spec compilation and `angular.json` references missing `@fortawesome/some-free/js/all.min.js`.
- `npx ng build --configuration production --no-progress` exited 1 because configured `src/environments/environment.prod.ts` does not exist.
- `bash .agents/skills/_shared/verify.sh` exited 1 because this Windows WSL environment has no `/bin/bash`.
- `npx tsc -p tsconfig.app.json --noEmit`, `npx tsc -p tsconfig.spec.json --noEmit`, and scoped `git diff --check` each exited 0.
