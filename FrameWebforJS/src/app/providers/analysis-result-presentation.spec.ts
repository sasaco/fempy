import multipleStatic from "../../../../FrameWeb/tests/data/contracts/positive/multiple-static.json";
import {
  AnalysisResultSetValidationError,
  isForceAnalysisResult,
  validateAndIndexAnalysisResultSet,
} from "./analysis-result-set";
import {
  buildAnalysisResultPages,
  buildMovingLoadAbsoluteRows,
  buildMovingLoadEnvelope,
  calculateMemberForceMetrics,
  createSafeRecord,
} from "./analysis-result-presentation";
import { reviewNegativeFixtures } from "./testing/analysis-result-set-review-negatives.fixture";
import { movingLoadAnalysisResultSetFixture } from "./testing/moving-load-analysis-result-set.fixture";

describe("AnalysisResultSet review regressions", () => {
  reviewNegativeFixtures.forEach(([name, fixture]) => {
    it(`rejects review negative fixture: ${name}`, () => {
      expect(() => validateAndIndexAnalysisResultSet(fixture)).toThrowError(
        AnalysisResultSetValidationError
      );
    });
  });

  it("groups moving-load children without shifting the following ordinary page", () => {
    const index = validateAndIndexAnalysisResultSet(movingLoadAnalysisResultSetFixture);
    const pages = buildAnalysisResultPages(index);

    expect(pages.map((page) => page.result.case_id)).toEqual(["1", "2"]);
    expect(pages[0].sourceCaseIds).toEqual(["1", "1.1", "1.2"]);
    expect(pages[0].movingLoad).toBeTrue();
    expect(pages[1].resultCase.symbol).toBe("D");
    expect(pages[1].movingLoad).toBeFalse();
  });

  it("derives moving-load extrema without mutating the canonical snapshots", () => {
    const index = validateAndIndexAnalysisResultSet(movingLoadAnalysisResultSetFixture);
    const page = buildAnalysisResultPages(index)[0];
    const childResult = page.sourceResults[1];
    if (!isForceAnalysisResult(childResult)) throw new Error("moving-load fixture must be static");
    const original = childResult.node_displacements[1].components.dx;
    const sources = page.sourceResults.map((result) => {
      if (!isForceAnalysisResult(result)) throw new Error("moving-load fixture must be static");
      return {
        caseId: result.case_id,
        rows: result.node_displacements.map((row) => ({ id: row.node_id, dx: row.components.dx })),
      };
    });
    const envelope = buildMovingLoadEnvelope("1", sources, ["dx"], (row) => row.id);

    expect(envelope.dx_max[1].dx).toBe(0.003);
    expect(envelope.dx_max[1].case).toBe("1.1");
    expect(envelope.dx_min[1].dx).toBe(-0.002);
    expect(envelope.dx_min[1].case).toBe("1.2");
    expect(childResult.node_displacements[1].components.dx).toBe(original);
  });

  it("aggregates moving-load reaction rows component-wise by child absolute maximum", () => {
    const parent = { id: "N1", tx: 100, ty: 100, tz: 0, mx: 0, my: 0, mz: 0 };
    const child1 = { id: "N1", tx: -30, ty: 2, tz: 3, mx: 4, my: -5, mz: 6 };
    const child2 = { id: "N1", tx: 20, ty: -40, tz: -1, mx: -8, my: 3, mz: -7 };

    const rows = buildMovingLoadAbsoluteRows(
      "1",
      [
        { caseId: "1", rows: [parent] },
        { caseId: "1.1", rows: [child1] },
        { caseId: "1.2", rows: [child2] },
      ],
      ["tx", "ty", "tz", "mx", "my", "mz"],
      (row) => row.id
    );

    expect(rows).toEqual([
      { id: "N1", tx: -30, ty: -40, tz: 3, mx: -8, my: -5, mz: -7 },
    ]);
    expect(parent.tx).toBe(100);
    expect(child1.ty).toBe(2);
    expect(child2.tx).toBe(20);
  });

  it("reports signed member-force extrema and their member IDs", () => {
    const rows = [
      { memberId: "M-positive", m: "M-positive", n: "1", l: 0, fx: 7, fy: 1, fz: 2, mx: 3, my: 4, mz: 5, dummy: false },
      { memberId: "M-negative", m: "M-negative", n: "2", l: 0, fx: -11, fy: -2, fz: -3, mx: -13, my: -5, mz: -6, dummy: false },
    ];
    const metrics = calculateMemberForceMetrics(rows);

    expect(metrics.maxValue.fx).toBe(11);
    expect(metrics.valueRange.x.max_d).toBe(7);
    expect(metrics.valueRange.x.max_d_m).toBe("M-positive");
    expect(metrics.valueRange.x.min_d).toBe(-11);
    expect(metrics.valueRange.x.min_d_m).toBe("M-negative");
    expect(metrics.valueRange.x.min_r).toBe(-13);
    expect(metrics.valueRange.x.min_r_m).toBe("M-negative");
  });

  it("indexes reserved case IDs without prototype pollution", () => {
    const fixture = JSON.parse(JSON.stringify(multipleStatic));
    fixture.cases[0].case_id = "__proto__";
    fixture.results[0].case_id = "__proto__";
    fixture.cases[1].case_id = "constructor";
    fixture.results[1].case_id = "constructor";

    const index = validateAndIndexAnalysisResultSet(fixture);
    const record = createSafeRecord<number>();
    record["__proto__"] = 1;
    record["constructor"] = 2;

    expect(index.caseOrder).toEqual(["__proto__", "constructor"]);
    expect(index.resultsByCase.get("__proto__")?.length).toBe(1);
    expect(Object.getPrototypeOf(record)).toBeNull();
    expect(record["__proto__"]).toBe(1);
    expect(record["constructor"]).toBe(2);
  });
});
