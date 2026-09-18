The recommended model is to retire “old/new” and “flat/legacy” as primary terminology.

### Formal terminology

| Concept | Name |
|---|---|
| Canonical solver output for one selected load case | `CaseAnalysisResult` |
| Ordered, all-or-nothing collection of case outputs | `LoadCaseResultSet<T>` |
| `disg/reac/fsec/shell_fsec/size` UI representation | `FrameWebForJsBeamCaseProjectionV1` |

Thus the current compatibility response is formally:

`LoadCaseResultSet<FrameWebForJsBeamCaseProjectionV1>`

It is not a collection of literal `CaseAnalysisResult` objects because projection changes names, grouping, signs, and scaling.

### API boundary

The current `Accept` branch changes both representation and computational cardinality—from one solve to every load case ([main.py](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/main.py:105)). The first-class boundary should separate those operations:

- `POST /` — unchanged; returns one `CaseAnalysisResult`.
- `POST /load-case-result-sets` — new; solves the ordered case collection.
- `Content-Encoding` remains an independent transport concern.
- New response media type:  
  `application/vnd.frameweb.load-case-result-set-v1+json`

Flow:

`LoadCase → fresh FemModel → CaseAnalysisResult → FrameWebForJsBeamCaseProjectionV1 → LoadCaseResultSet`

### Canonical v1 envelope

```json
{
  "kind": "load_case_result_set",
  "version": 1,
  "item_schema": "framewebforjs_beam_case_projection_v1",
  "case_count": 2,
  "cases": [
    {
      "case_id": "1",
      "applied_rate": 1.0,
      "projection": {
        "disg": {},
        "reac": {},
        "fsec": {},
        "shell_fsec": {},
        "size": 42
      }
    }
  ]
}
```

An array makes ordering explicit and avoids relying on JSON object-key ordering.

### Required invariants

- Validate the complete request and the `1..256` case limit before constructing any `FemModel`.
- Preserve input case order and exact string IDs; reject duplicates.
- Use a fresh model per case and never mutate caller input.
- Return every requested case or one diagnostic error containing the failing `case_id`; never return partial success.
- Missing `rate` means `1.0`; a supplied rate must be finite. Apply it exactly once to projected displacement, reaction, force, and moment values—not `L`, `size`, or metadata.
- Require `case_count === cases.length`, the expected ordered IDs, the five projection fields, finite numeric values, positive integral `size`, and at least one non-empty `disg/reac/fsec` map per case.
- Unknown result-set versions return `406`; never fall back to the single-case response.

### Compatibility migration

1. Keep `application/vnd.frameweb.legacy-cases-v1+json` and its current bare case-map body unchanged.
2. Add the new endpoint, envelope, and media type server-side.
3. Add a frontend boundary adapter that accepts both shapes and unwraps the new envelope before existing workers run ([result-data.service.ts](C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/providers/result-data.service.ts:49)).
4. Switch FrameWebforJS to the new endpoint/media type.
5. Mark the old media type deprecated with `Deprecation` and eventually `Sunset`; remove it only after downstream usage is cleared.

Internally, “legacy” should remain only in the compatibility shim. The existing projection module ([legacy_results.py](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/src/fem/legacy_results.py:35)) should eventually be split into result-set orchestration and a named FrameWebforJS projection adapter.

No repository files were changed in this read-only session. The external Codex review produced no usable response, so this recommendation is based on the implemented code, tests, and existing review evidence.
