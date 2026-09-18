import {
  LegacyCasesResultValidationError,
  ResultDataService,
  validateLegacyCasesResult,
} from "./result-data.service";

describe("validateLegacyCasesResult", () => {
  const validResult = {
    "1": { disg: { "1": {} }, reac: {}, fsec: {} },
    "2": { disg: {}, reac: { "2": {} }, fsec: { "1": {} } },
  };

  it("accepts a non-empty case map with object-valued result fields", () => {
    expect(() =>
      validateLegacyCasesResult(validResult, ["1", "2"])
    ).not.toThrow();
  });

  [
    { disg: { "1": {} }, reac: {}, fsec: {} },
    { disg: {}, reac: { "1": {} }, fsec: {} },
    { disg: {}, reac: {}, fsec: { "1": {} } },
  ].forEach((caseResult) => {
    it(`accepts individually empty result maps: ${JSON.stringify(caseResult)}`, () => {
      expect(() =>
        validateLegacyCasesResult({ "1": caseResult })
      ).not.toThrow();
    });
  });

  it("rejects a case when all required result maps are empty", () => {
    expect(() =>
      validateLegacyCasesResult({ "1": { disg: {}, reac: {}, fsec: {} } })
    ).toThrowError(LegacyCasesResultValidationError);
  });

  it("rejects the current flat backend result", () => {
    const flatResult = {
      node_displacements: {},
      reaction_forces: {},
      element_stresses: {},
    };

    expect(() => validateLegacyCasesResult(flatResult)).toThrowError(
      LegacyCasesResultValidationError
    );
  });

  it("rejects an empty object", () => {
    expect(() => validateLegacyCasesResult({})).toThrowError(
      LegacyCasesResultValidationError
    );
  });

  [null, [], [validResult["1"]]].forEach((value) => {
    it(`rejects a non-object top level: ${JSON.stringify(value)}`, () => {
      expect(() => validateLegacyCasesResult(value)).toThrowError(
        LegacyCasesResultValidationError
      );
    });
  });

  [null, []].forEach((value) => {
    it(`rejects a non-object case: ${JSON.stringify(value)}`, () => {
      expect(() =>
        validateLegacyCasesResult({ "1": value })
      ).toThrowError(LegacyCasesResultValidationError);
    });
  });

  it("rejects missing expected case IDs", () => {
    expect(() =>
      validateLegacyCasesResult({ "1": validResult["1"] }, ["1", "2"])
    ).toThrowError(LegacyCasesResultValidationError);
  });

  it("rejects extra case IDs", () => {
    expect(() =>
      validateLegacyCasesResult(validResult, ["1"])
    ).toThrowError(LegacyCasesResultValidationError);
  });

  it("rejects case IDs returned in a different order", () => {
    expect(() =>
      validateLegacyCasesResult(validResult, ["2", "1"])
    ).toThrowError(LegacyCasesResultValidationError);
  });

  ["disg", "reac", "fsec"].forEach((field) => {
    it(`rejects a case missing ${field}`, () => {
      const caseResult = { disg: {}, reac: {}, fsec: {} };
      delete caseResult[field];

      expect(() =>
        validateLegacyCasesResult({ "1": caseResult })
      ).toThrowError(LegacyCasesResultValidationError);
    });

    [null, []].forEach((value) => {
      it(`rejects ${field} when it is ${JSON.stringify(value)}`, () => {
        const caseResult = { disg: {}, reac: {}, fsec: {}, [field]: value };

        expect(() =>
          validateLegacyCasesResult({ "1": caseResult })
        ).toThrowError(LegacyCasesResultValidationError);
      });
    });
  });

  it("keeps calculated false and stops before worker dispatch on invalid data", () => {
    const service = Object.create(ResultDataService.prototype) as ResultDataService;
    service.isCalculated = true;

    expect(() => service.loadResultData({})).toThrowError(
      LegacyCasesResultValidationError
    );
    expect(service.isCalculated).toBeFalse();
  });
});
