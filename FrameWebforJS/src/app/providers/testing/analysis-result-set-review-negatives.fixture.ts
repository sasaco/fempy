import singleStatic from "../../../../../FrameWeb/tests/data/contracts/positive/single-static.json";
import nonlinearSteps from "../../../../../FrameWeb/tests/data/contracts/positive/nonlinear-steps.json";
import modal from "../../../../../FrameWeb/tests/data/contracts/positive/modal.json";

function mutated<T>(source: T, mutation: (copy: any) => void): unknown {
  const copy = JSON.parse(JSON.stringify(source));
  mutation(copy);
  return copy;
}

export const reviewNegativeFixtures: readonly [string, unknown][] = [
  ["whitespace case ID", mutated(singleStatic, (copy) => {
    copy.cases[0].case_id = "   ";
    copy.results[0].case_id = "   ";
  })],
  ["whitespace case name", mutated(singleStatic, (copy) => { copy.cases[0].name = "   "; })],
  ["whitespace case symbol", mutated(singleStatic, (copy) => { copy.cases[0].symbol = "   "; })],
  ["empty solid result locations", mutated(singleStatic, (copy) => {
    copy.topology.nodes.push(
      { node_id: "3", coordinates: { x: 0, y: 1, z: 0 }, source_node_id: "3", generated: false },
      { node_id: "4", coordinates: { x: 0, y: 0, z: 1 }, source_node_id: "4", generated: false }
    );
    copy.topology.solid_elements.push({
      element_id: "SOLID-1",
      element_type: "tetra4",
      node_ids: ["1", "2", "3", "4"],
      coordinate_frame: "global",
      result_locations: [],
    });
  })],
  ["non-consecutive nonlinear iteration", mutated(nonlinearSteps, (copy) => {
    copy.results[1].diagnostics.iterations[1].index = 2;
  })],
  ["negative nonlinear norm", mutated(nonlinearSteps, (copy) => {
    copy.results[0].diagnostics.iterations[0].residual_norm = -0.01;
  })],
  ["descending modal eigenvalue", mutated(modal, (copy) => {
    copy.results[1].state.eigenvalue = 1;
    copy.results[1].state.frequency = 1 / (2 * Math.PI);
  })],
  ["non-contiguous modal group", mutated(modal, (copy) => {
    copy.results[1].state.degeneracy_group = 2;
  })],
  ["inconsistent modal frequency", mutated(modal, (copy) => {
    copy.results[1].state.frequency = 42;
  })],
];
