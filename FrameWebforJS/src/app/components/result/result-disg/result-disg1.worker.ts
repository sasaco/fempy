/// <reference lib="webworker" />

import { AnalysisResult, analysisResultSelectionKey, isForceAnalysisResult } from "../../../providers/analysis-result-set";
import { workerErrorMessage } from "../../../providers/result-worker-pipeline";

function rowsFor(result: AnalysisResult) {
  const rows = isForceAnalysisResult(result) ? result.node_displacements : result.node_mode_shapes;
  return rows.map((row) => ({ id: row.node_id, ...row.components }));
}

addEventListener("message", ({ data }) => {
  let error: unknown = null;
  try {
    const entries = (data.results as readonly AnalysisResult[]).map((result) => {
      const rows = rowsFor(result);
      let maxDisplacement = Number.NEGATIVE_INFINITY;
      let minDisplacement = Number.POSITIVE_INFINITY;
      let maxRotation = Number.NEGATIVE_INFINITY;
      let minRotation = Number.POSITIVE_INFINITY;
      let maxDisplacementNode = "0";
      let minDisplacementNode = "0";
      let maxRotationNode = "0";
      let minRotationNode = "0";
      rows.forEach((row) => {
        [row.dx, row.dy, row.dz].forEach((value) => {
          if (value > maxDisplacement) { maxDisplacement = value; maxDisplacementNode = row.id; }
          if (value < minDisplacement) { minDisplacement = value; minDisplacementNode = row.id; }
        });
        [row.rx, row.ry, row.rz].forEach((value) => {
          if (value > maxRotation) { maxRotation = value; maxRotationNode = row.id; }
          if (value < minRotation) { minRotation = value; minRotationNode = row.id; }
        });
      });
      if (rows.length === 0) {
        maxDisplacement = minDisplacement = maxRotation = minRotation = 0;
      }
      return {
        selectionKey: analysisResultSelectionKey(result),
        caseId: result.case_id,
        stateKind: result.state.kind,
        rows,
        maxValue: Math.max(Math.abs(maxDisplacement), Math.abs(minDisplacement)),
        valueRange: {
          max_d: maxDisplacement,
          min_d: minDisplacement,
          max_r: maxRotation,
          min_r: minRotation,
          max_d_m: maxDisplacementNode,
          min_d_m: minDisplacementNode,
          max_r_m: maxRotationNode,
          min_r_m: minRotationNode,
        },
      };
    });
    postMessage({ entries, error });
  } catch (caught) {
    error = workerErrorMessage(caught);
    postMessage({ entries: [], error });
  }
});
