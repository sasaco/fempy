/// <reference lib="webworker" />

import { AnalysisResult, analysisResultSelectionKey, isForceAnalysisResult } from "../../../providers/analysis-result-set";
import { workerErrorMessage } from "../../../providers/result-worker-pipeline";

addEventListener("message", ({ data }) => {
  let error: unknown = null;
  try {
    const entries = (data.results as readonly AnalysisResult[]).map((result) => {
      const rows = isForceAnalysisResult(result)
        ? result.support_reactions.map((row) => ({
            id: row.node_id,
            tx: row.components.fx,
            ty: row.components.fy,
            tz: row.components.fz,
            mx: row.components.mx,
            my: row.components.my,
            mz: row.components.mz,
          }))
        : [];
      let maxForce = Number.NEGATIVE_INFINITY;
      let minForce = Number.POSITIVE_INFINITY;
      let maxMoment = Number.NEGATIVE_INFINITY;
      let minMoment = Number.POSITIVE_INFINITY;
      let maxForceNode = "0";
      let minForceNode = "0";
      let maxMomentNode = "0";
      let minMomentNode = "0";
      rows.forEach((row) => {
        [row.tx, row.ty, row.tz].forEach((value) => {
          if (value > maxForce) { maxForce = value; maxForceNode = row.id; }
          if (value < minForce) { minForce = value; minForceNode = row.id; }
        });
        [row.mx, row.my, row.mz].forEach((value) => {
          if (value > maxMoment) { maxMoment = value; maxMomentNode = row.id; }
          if (value < minMoment) { minMoment = value; minMomentNode = row.id; }
        });
      });
      if (rows.length === 0) {
        maxForce = minForce = maxMoment = minMoment = 0;
      }
      return {
        selectionKey: analysisResultSelectionKey(result),
        caseId: result.case_id,
        stateKind: result.state.kind,
        rows,
        maxValue: Math.max(Math.abs(maxForce), Math.abs(minForce)),
        valueRange: {
          max_d: maxForce,
          min_d: minForce,
          max_r: maxMoment,
          min_r: minMoment,
          max_d_m: maxForceNode,
          min_d_m: minForceNode,
          max_r_m: maxMomentNode,
          min_r_m: minMomentNode,
        },
      };
    });
    postMessage({ entries, error });
  } catch (caught) {
    error = workerErrorMessage(caught);
    postMessage({ entries: [], error });
  }
});
