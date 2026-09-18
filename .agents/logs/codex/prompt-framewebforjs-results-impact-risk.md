# FrameWebforJS result compatibility: regression-risk review

Read-only review. Do not edit files.

Objective: assess regression risk and fix safety for restoring the old FrameWeb2 multi-case `disg`/`reac`/`fsec` response needed by FrameWebforJS while preserving modern flat-result clients.

Evidence:

- Old contract: `C:/Users/sasai/Documents/FrameWeb2/main.py:64-78`, `app/controller.py:90-102`, and `app/result.py:135-180` return an insertion-ordered load-case map; every case contains `disg`, `reac`, `fsec`, `shell_fsec`, and `size`.
- Current endpoint: `FrameWeb/main.py:106-118` returns one flat `FemModel.run()` result. This shape dates to commit `12b8ab9` (2026-01-30), is documented by `FrameWeb/docs/wiki/results.md:35-43` in `d111a02`, and is pinned by `FrameWeb/tests/io/test_http.py:18-34` from `46441c5`.
- Current legacy loader: `FrameWeb/src/fem/file_io.py:70-79` calls `select_case(data)`; `FrameWeb/src/fem/legacy_beam.py:6-20`, introduced by `29b9c32` (2026-09-08), defaults to and retains only the first case.
- Ct preset has ordered load-case IDs `1..11`, each with distinct loads. A shape-only wrapper loses cases 2-11.
- Frontend workers silently skip top-level objects without `disg`/`reac`/`fsec`: `result-disg1.worker.ts:29-39`, `result-reac1.worker.ts:27-40`, `result-fsec1.worker.ts:41-69`.
- Flat fields are referenced in 43 backend test files and 11 source/tool/script files. The test-only adapter `FrameWeb/tests/support/section_cut_view.py:6-104` is used by sample/reference tests but is not a production API.

Alternatives:

A. Replace the root endpoint response for every caller with the old case map.
B. Adapt the frontend to the existing flat response.
C. Preserve the default flat response and add an explicit versioned/negotiated legacy case-map representation, implemented by backend per-case solve plus a production projection; make the frontend request it and fail fast on schema mismatch.

Required output:

## Verdict
Choose A/B/C and rate risk.

## Contracts at Risk
Identify implicit contracts and affected/unaffected paths.

## Fix-Safety Safeguards
Cover case order/IDs, all 11 Ct cases, non-empty `disg`/`reac`/`fsec`, unit/sign/member-point ordering, `rate`, error behavior, and modern client preservation.

## Minimum Executable Tests
Separate transport success from response-schema ingestion and browser display.
