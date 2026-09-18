import { AnalysisResultSetValidationError } from "./analysis-result-set";
import { ResultDataService } from "./result-data.service";
import singleStatic from "../../../../FrameWeb/tests/data/contracts/positive/single-static.json";

describe("ResultDataService contract boundary", () => {
  it("keeps calculated false and stops before worker dispatch on invalid data", () => {
    const service = Object.create(ResultDataService.prototype) as ResultDataService;
    service.isCalculated = true;
    const previous = { value: "previous" } as any;
    service.resultSet = previous;

    expect(() => service.loadResultData({})).toThrowError(AnalysisResultSetValidationError);
    expect(service.isCalculated).toBeFalse();
    expect(service.resultSet).toBe(previous);
  });

  it("rejects only a derived operation that references a non-static operand", () => {
    const service = Object.create(ResultDataService.prototype) as any;
    service.helper = {
      toNumber: (value: string) => {
        const parsed = Number(value);
        return Number.isFinite(parsed) ? parsed : null;
      },
    };
    const allCases = new Set(["1", "2"]);
    const staticCases = new Set(["1"]);

    expect(() => service.assertStaticDerivedOperands(
      { "10": { C1: 1 } }, {}, {}, allCases, staticCases
    )).not.toThrow();
    expect(() => service.assertStaticDerivedOperands(
      { "10": { C2: 2 } }, {}, {}, allCases, staticCases
    )).toThrowError("DEFINE 10 references non-static case 2.");
  });

  it("marks results calculated only after all three pipelines complete", async () => {
    const service = Object.create(ResultDataService.prototype) as any;
    const resolvers: Array<() => void> = [];
    const pipeline = () => new Promise<void>((resolve) => resolvers.push(resolve));
    const resultService = () => ({ isCalculated: false, clear: jasmine.createSpy("clear") });
    service.disg = { ...resultService(), setDisgJson: pipeline };
    service.reac = { ...resultService(), setReacJson: pipeline };
    service.fsec = { ...resultService(), setFsecJson: pipeline };
    service.define = { getDefineJson: () => ({}), validate: () => null };
    service.combine = { getCombineJson: () => ({}), validate: () => null };
    service.pickup = { getPickUpJson: () => ({}), validate: () => null };
    service.load = { getLoadNameJson: () => ({ D: { symbol: "D" } }) };
    service.helper = {
      toNumber: (value: string) => {
        const parsed = Number(value);
        return Number.isFinite(parsed) ? parsed : null;
      },
    };
    service.resultSelections = [];
    service.resultSet = null;
    service.isCalculated = false;

    const completion = service.loadResultData(JSON.parse(JSON.stringify(singleStatic)));
    expect(resolvers.length).toBe(3);
    expect(service.isCalculated).toBeFalse();
    expect(service.disg.isCalculated).toBeFalse();
    resolvers[0]();
    resolvers[1]();
    await Promise.resolve();
    expect(service.isCalculated).toBeFalse();
    resolvers[2]();
    await completion;

    expect(service.isCalculated).toBeTrue();
    expect(service.disg.isCalculated).toBeTrue();
    expect(service.reac.isCalculated).toBeTrue();
    expect(service.fsec.isCalculated).toBeTrue();
  });
});
