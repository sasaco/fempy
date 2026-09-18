/// <reference lib="webworker" />

import {
  AnalysisResult,
  ResultTopology,
  analysisResultSelectionKey,
  isForceAnalysisResult,
} from "../../../providers/analysis-result-set";
import {
  MemberForceDisplayRow,
  calculateMemberForceMetrics,
} from "../../../providers/analysis-result-presentation";
import { workerErrorMessage } from "../../../providers/result-worker-pipeline";

addEventListener("message", ({ data }) => {
  let error: unknown = null;
  try {
    const topology = data.topology as ResultTopology;
    const membersById = new Map(topology.members.map((member) => [member.member_id, member]));
    const entries = (data.results as readonly AnalysisResult[]).map((result) => {
      const rows: MemberForceDisplayRow[] = [];
      if (isForceAnalysisResult(result)) {
        result.member_section_forces.forEach((memberResult) => {
          const member = membersById.get(memberResult.member_id)!;
          memberResult.segments.forEach((segment, segmentIndex) => {
            const stationI = member.stations[segmentIndex];
            const stationJ = member.stations[segmentIndex + 1];
            rows.push({
              memberId: member.member_id,
              m: segmentIndex === 0 ? member.member_id : "",
              n: segmentIndex === 0 ? member.node_i : "",
              l: stationI.position,
              ...segment.i_end,
              dummy: false,
            });
            rows.push({
              memberId: member.member_id,
              m: "",
              n: segmentIndex === memberResult.segments.length - 1 ? member.node_j : "",
              l: stationJ.position,
              ...segment.j_end,
              dummy: false,
            });
          });
        });
      }
      const { maxValue, valueRange } = calculateMemberForceMetrics(rows);
      return {
        selectionKey: analysisResultSelectionKey(result),
        caseId: result.case_id,
        stateKind: result.state.kind,
        rows,
        maxValue,
        valueRange,
      };
    });
    postMessage({ entries, error });
  } catch (caught) {
    error = workerErrorMessage(caught);
    postMessage({ entries: [], error });
  }
});
