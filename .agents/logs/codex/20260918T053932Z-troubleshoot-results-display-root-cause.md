## TL;DR

The result-display defect is confirmed by static evidence: the backend returns one flat solver result, while FrameWebforJS expects a map of load cases containing `disg`, `reac`, and `fsec`. Workers silently discard every incompatible field, producing an empty “successful” result.

## Analysis

Execution flow:

1. Backend selects only the first legacy load case at [file_io.py](/C:/Users/sasai/Documents/FrameWeb3/FrameWeb/src/fem/file_io.py:70) and [legacy_beam.py](/C:/Users/sasai/Documents/FrameWeb3/FrameWeb/src/fem/legacy_beam.py:6).
2. It runs `FemModel` once and returns the flat `result_to_jsonable(result)` object at [main.py](/C:/Users/sasai/Documents/FrameWeb3/FrameWeb/main.py:106).
3. Angular decodes the HTTP 200 response and forwards it unchanged at [app.component.ts](/C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/app.component.ts:249).
4. `ResultDataService` dispatches that object to the three result processors without schema validation at [result-data.service.ts](/C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/providers/result-data.service.ts:87).
5. The workers expect each top-level value to contain legacy fields and silently `continue` otherwise:
   - [result-disg1.worker.ts](/C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/components/result/result-disg/result-disg1.worker.ts:29)
   - [result-reac1.worker.ts](/C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/components/result/result-reac/result-reac1.worker.ts:27)
   - [result-fsec1.worker.ts](/C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/components/result/result-fsec/result-fsec1.worker.ts:41)
6. Empty worker outputs are accepted, and calculated state is set, leaving enabled but blank result pages.

Ranked hypotheses:

1. **Confirmed, very high confidence — response-contract mismatch.**  
   Producer emits flat fields such as `node_displacements`, `reaction_forces`, and `element_stresses`; consumers require `{caseId: {disg, reac, fsec}}`. This fully explains HTTP 200 with no displayed results.

2. **Confirmed, high confidence — missing multi-load-case execution.**  
   `select_case()` defaults to the first case, while legacy behavior calculated every case. Merely wrapping the current result under case `"1"` would conceal data loss and fail cases such as the 11-case Ct preset.

3. **Secondary, medium confidence — premature readiness and silent failure handling.**  
   Global `isCalculated` is set immediately after asynchronous processing starts, and incompatible worker input becomes empty success. This makes the symptom confusing but does not create the missing data.

4. **Low confidence / rejected as primary causes — rendering, timing, routing, or transport.**  
   Rendering never receives compatible result data. The existing uncommitted transport compatibility work in `FrameWeb/main.py` does not repair this separate result contract.

## Safest Fix Boundary

The safest ownership boundary is the backend compatibility/orchestration layer immediately around legacy-input execution and HTTP response projection:

- Enumerate and solve every requested legacy load case.
- Preserve original case IDs and ordering.
- Convert each solver result into the established `disg`/`reac`/`fsec` contract.
- Keep `FemModel.run()` and its native flat result schema unchanged.
- Keep existing frontend result tables, combination/pickup logic, and workers unchanged except for adding fail-fast schema validation.

Avoid:

- A frontend-only flat-result adapter: it cannot recover load cases already discarded by `select_case()`.
- Changing `FemModel.run()` globally: its flat schema has current tests and other consumers.
- A shape-only wrapper around the first result.
- Combining this with transport or print-protocol changes.

## Validation

Required acceptance checks:

1. Endpoint response contains every expected case ID—11 for the Ct fixture.
2. Every case has non-empty, finite `disg`, `reac`, and `fsec`.
3. Reaction names, force signs, units, member-end conventions, and case ordering are explicitly pinned.
4. Workers reject incompatible flat responses instead of returning empty success.
5. Browser E2E opens displacement, reaction, and section-force pages for multiple cases without worker or console errors.
6. Existing native flat-result tests remain green.

The bundled HTTP reproduction could not be rerun in this read-only environment because importing the backend opens `FrameWeb/src/fem/frame.log` in write mode and raises `PermissionError`. Therefore runtime reconfirmation is unavailable in this pass; the conclusion is supported by direct source inspection and the previously captured HTTP-200/zero-compatible-cases evidence.

No files were changed.
