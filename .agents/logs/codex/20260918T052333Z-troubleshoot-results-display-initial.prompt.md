Objective: Analyze the FrameWebforJS HTTP-200-but-no-results-display defect and rank initial root-cause hypotheses.

Context:
- Direct reproduction succeeds with HTTP 200 but returns top-level keys: analysis_type, constitutive_element_stresses, displacement, displacement_correction, element_stresses, force_recovery, metadata, node_displacements, reaction_forces.
- No response value is a legacy case containing disg, reac, and fsec.
- Old FrameWeb2 returns `{caseId: {disg, reac, fsec, shell_fsec, size}}` for every load case.
- Current FrameWeb legacy input selects only the first load case and serializes the flat FemModel result.
- Angular forwards the response unchanged. Its three workers skip every top-level value lacking disg/reac/fsec and return empty maps with no error, so the UI marks calculation complete but displays nothing.

Relevant files:
- .agents/docs/research/troubleshoot-framewebforjs-results-not-displayed-context.md
- FrameWeb/main.py
- FrameWeb/src/fem/file_io.py
- FrameWeb/src/fem/legacy_beam.py
- FrameWeb/tests/support/section_cut_view.py
- FrameWebforJS/src/app/app.component.ts
- FrameWebforJS/src/app/providers/result-data.service.ts
- FrameWebforJS/src/app/components/result/result-disg/result-disg1.worker.ts
- FrameWebforJS/src/app/components/result/result-reac/result-reac1.worker.ts
- FrameWebforJS/src/app/components/result/result-fsec/result-fsec1.worker.ts
- C:/Users/sasai/Documents/FrameWeb2/main.py
- C:/Users/sasai/Documents/FrameWeb2/app/controller.py
- C:/Users/sasai/Documents/FrameWeb2/app/result.py

Constraints:
- Read-only analysis; do not edit files.
- Distinguish immediate silent-display cause from the deeper multi-load-case/result-projection defect.
- Evaluate whether backend compatibility projection, frontend adaptation, or a versioned API boundary is the safest fix direction.
- Account for all 11 Ct load cases, original case IDs/order, reaction component renaming, member end-force structure, and existing consumers of the modern flat API.

Output format:
## Error Pattern Recognition
## Hypotheses (ranked by likelihood)
## Investigation Plan (per hypothesis)
## Known Similar Patterns
## Preliminary Fix Direction and Risks
