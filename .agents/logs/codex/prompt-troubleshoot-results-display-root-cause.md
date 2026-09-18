Objective: Read-only review of the FrameWebforJS result-display defect; verify execution flow, ranked hypotheses, and safest fix boundary.

Read only these files:
- `.agents/docs/research/troubleshoot-framewebforjs-results-not-displayed-bug-report.md`
- `.agents/docs/research/troubleshoot-framewebforjs-results-not-displayed-context.md`
- `FrameWeb/main.py`
- `FrameWeb/src/fem/file_io.py`
- `FrameWeb/src/fem/legacy_beam.py`
- `FrameWeb/tests/support/section_cut_view.py`
- `FrameWebforJS/src/app/app.component.ts`
- `FrameWebforJS/src/app/providers/result-data.service.ts`
- the three `result-disg1.worker.ts`, `result-reac1.worker.ts`, `result-fsec1.worker.ts`
- `C:/Users/sasai/Documents/FrameWeb2/main.py`, `app/controller.py`, `app/result.py`

Known facts: current HTTP returns one flat modern result; the old endpoint returns an insertion-ordered case map whose values contain `disg/reac/fsec/shell_fsec/size`; legacy parsing selects only the first case. Evaluate (1) schema mismatch, (2) first-case loss, (3) async timing. Compare A replacing the modern endpoint schema, B adapting the frontend, C an explicit/versioned legacy response adapter while retaining the flat contract. Check exact projection correctness: IDs/order, `rate`, displacement units, reaction names/signs, and fsec i/j ordering/signs/keys/length. Identify omissions or unsafe assumptions in `section_cut_view.py` as a production basis.

Return at most 700 words with:
## Verdict and defect location
## Hypotheses
## A/B/C comparison
## Recommended boundary
## Conversion correctness and remaining unknowns
Every material claim must cite file:line. Do not edit files or run long tests.
