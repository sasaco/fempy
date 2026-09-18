import singleStatic from "../../../../../FrameWeb/tests/data/contracts/positive/single-static.json";

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value));
}

function snapshot(caseId: string, factor: number): any {
  const result = clone(singleStatic.results[0]);
  result.case_id = caseId;
  result.node_displacements.forEach((row) => {
    row.components.dx *= factor;
  });
  result.support_reactions.forEach((row) => {
    row.components.fx *= factor;
  });
  result.member_section_forces.forEach((member) => {
    member.segments.forEach((segment) => {
      segment.i_end.fx *= factor;
      segment.j_end.fx *= factor;
    });
  });
  return result;
}

export const movingLoadAnalysisResultSetFixture: unknown = {
  ...clone(singleStatic),
  cases: [
    { case_id: "1", name: "Moving load", symbol: "LL", analysis_type: "static", support_node_ids: ["1"] },
    { case_id: "2", name: "Following dead load", symbol: "D", analysis_type: "static", support_node_ids: ["1"] },
    { case_id: "1.1", name: "Moving load position 1", symbol: "LL", analysis_type: "static", support_node_ids: ["1"] },
    { case_id: "1.2", name: "Moving load position 2", symbol: "LL", analysis_type: "static", support_node_ids: ["1"] },
  ],
  results: [
    snapshot("1", 1),
    snapshot("2", 4),
    snapshot("1.1", 3),
    snapshot("1.2", -2),
  ],
};
