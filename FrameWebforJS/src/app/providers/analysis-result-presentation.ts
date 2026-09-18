import {
  AnalysisResult,
  AnalysisResultSetIndex,
  ResultCase,
  StaticAnalysisResult,
} from "./analysis-result-set";

export interface AnalysisResultPage {
  readonly resultCase: ResultCase;
  readonly result: AnalysisResult;
  readonly sourceResults: readonly AnalysisResult[];
  readonly sourceCaseIds: readonly string[];
  readonly movingLoad: boolean;
}

export interface MovingLoadSource<Row> {
  readonly caseId: string;
  readonly rows: readonly Row[];
}

export type MovingLoadEnvelopeRow<Row> = Row & {
  readonly case: string;
  readonly comb: string;
};

export function createSafeRecord<Value>(): Record<string, Value> {
  return Object.create(null) as Record<string, Value>;
}

function isMovingLoadCase(resultCase: ResultCase): boolean {
  return resultCase.analysis_type === "static" && resultCase.symbol.toUpperCase().includes("LL");
}

export function buildAnalysisResultPages(index: AnalysisResultSetIndex): readonly AnalysisResultPage[] {
  const pages: AnalysisResultPage[] = [];
  const consumed = new Set<string>();

  index.value.cases.forEach((resultCase) => {
    if (consumed.has(resultCase.case_id)) return;
    const owned = index.resultsByCase.get(resultCase.case_id) ?? [];
    const childCases = isMovingLoadCase(resultCase)
      ? index.value.cases.filter(
          (candidate) =>
            candidate.case_id.startsWith(`${resultCase.case_id}.`) &&
            isMovingLoadCase(candidate)
        )
      : [];

    if (isMovingLoadCase(resultCase)) {
      const sourceCaseIds = [resultCase.case_id, ...childCases.map((candidate) => candidate.case_id)];
      const sourceIdSet = new Set(sourceCaseIds);
      const sourceResults = index.resultsInOrder.filter(
        (result): result is StaticAnalysisResult =>
          sourceIdSet.has(result.case_id) && result.state.kind === "static"
      );
      if (owned.length > 0 && sourceResults.length === sourceCaseIds.length) {
        sourceCaseIds.forEach((caseId) => consumed.add(caseId));
        pages.push({
          resultCase,
          result: owned[0],
          sourceResults,
          sourceCaseIds,
          movingLoad: true,
        });
        return;
      }
    }

    consumed.add(resultCase.case_id);
    owned.forEach((result) => {
      pages.push({
        resultCase,
        result,
        sourceResults: [result],
        sourceCaseIds: [resultCase.case_id],
        movingLoad: false,
      });
    });
  });
  return pages;
}

export function buildMovingLoadEnvelope<Row extends Record<string, unknown>>(
  parentCaseId: string,
  sources: readonly MovingLoadSource<Row>[],
  components: readonly string[],
  identity: (row: Row, rowIndex: number) => string
): Record<string, readonly MovingLoadEnvelopeRow<Row>[]> {
  const orderedIdentities: string[] = [];
  const sourceMaps = sources.map((source) => {
    const rows = new Map<string, Row>();
    source.rows.forEach((row, rowIndex) => {
      const key = identity(row, rowIndex);
      if (!rows.has(key)) {
        rows.set(key, row);
        if (!orderedIdentities.includes(key)) orderedIdentities.push(key);
      }
    });
    return { caseId: source.caseId, rows };
  });
  const envelope = createSafeRecord<readonly MovingLoadEnvelopeRow<Row>[]>();

  components.forEach((component) => {
    (["max", "min"] as const).forEach((direction) => {
      const rows: MovingLoadEnvelopeRow<Row>[] = [];
      orderedIdentities.forEach((rowIdentity) => {
        let selected: { caseId: string; row: Row; value: number } | null = null;
        sourceMaps.forEach((source) => {
          const row = source.rows.get(rowIdentity);
          const value = row?.[component];
          if (row === undefined || typeof value !== "number") return;
          if (
            selected === null ||
            (direction === "max" ? value > selected.value : value < selected.value)
          ) {
            selected = { caseId: source.caseId, row, value };
          }
        });
        if (selected !== null) {
          rows.push({ ...selected.row, case: selected.caseId, comb: parentCaseId });
        }
      });
      envelope[`${component}_${direction}`] = rows;
    });
  });
  return envelope;
}

export function buildMovingLoadAbsoluteRows<Row extends Record<string, unknown>>(
  parentCaseId: string,
  sources: readonly MovingLoadSource<Row>[],
  components: readonly string[],
  identity: (row: Row, rowIndex: number) => string
): readonly Row[] {
  const childSources = sources.filter((source) => source.caseId.startsWith(`${parentCaseId}.`));
  const activeSources = childSources.length > 0 ? childSources : sources;
  const orderedIdentities: string[] = [];
  const sourceMaps = activeSources.map((source) => {
    const rows = new Map<string, Row>();
    source.rows.forEach((row, rowIndex) => {
      const key = identity(row, rowIndex);
      rows.set(key, row);
      if (!orderedIdentities.includes(key)) orderedIdentities.push(key);
    });
    return rows;
  });

  return orderedIdentities.map((rowIdentity) => {
    const template = sourceMaps.find((rows) => rows.has(rowIdentity))!.get(rowIdentity)!;
    const combined: Record<string, unknown> = { ...template };
    components.forEach((component) => {
      let selected: number | undefined;
      sourceMaps.forEach((rows) => {
        const value = rows.get(rowIdentity)?.[component];
        if (typeof value !== "number") return;
        if (selected === undefined || Math.abs(value) > Math.abs(selected)) selected = value;
      });
      if (selected !== undefined) combined[component] = selected;
    });
    return combined as Row;
  });
}

export interface MemberForceDisplayRow extends Record<string, unknown> {
  readonly memberId: string;
  readonly m: string;
  readonly n: string;
  readonly l: number;
  readonly fx: number;
  readonly fy: number;
  readonly fz: number;
  readonly mx: number;
  readonly my: number;
  readonly mz: number;
  readonly dummy: boolean;
}

export interface MemberForceMetrics {
  readonly maxValue: Record<"fx" | "fy" | "fz" | "mx" | "my" | "mz", number>;
  readonly valueRange: Record<"x" | "y" | "z", {
    max_d: number;
    min_d: number;
    max_d_m: string;
    min_d_m: string;
    max_r: number;
    min_r: number;
    max_r_m: string;
    min_r_m: string;
  }>;
}

const FORCE_COMPONENTS = ["fx", "fy", "fz", "mx", "my", "mz"] as const;

export function calculateMemberForceMetrics(
  rows: readonly MemberForceDisplayRow[]
): MemberForceMetrics {
  const maxValue = { fx: 0, fy: 0, fz: 0, mx: 0, my: 0, mz: 0 };
  const valueRange = createSafeRecord<any>() as MemberForceMetrics["valueRange"];
  (["x", "y", "z"] as const).forEach((axis) => {
    valueRange[axis] = {
      max_d: Number.NEGATIVE_INFINITY,
      min_d: Number.POSITIVE_INFINITY,
      max_d_m: "0",
      min_d_m: "0",
      max_r: Number.NEGATIVE_INFINITY,
      min_r: Number.POSITIVE_INFINITY,
      max_r_m: "0",
      min_r_m: "0",
    };
  });

  rows.forEach((row) => {
    FORCE_COMPONENTS.forEach((component) => {
      maxValue[component] = Math.max(maxValue[component], Math.abs(row[component]));
    });
    if (row.dummy) return;
    (["x", "y", "z"] as const).forEach((axis) => {
      const force = row[`f${axis}` as "fx" | "fy" | "fz"];
      const moment = row[`m${axis}` as "mx" | "my" | "mz"];
      const range = valueRange[axis];
      if (force > range.max_d) {
        range.max_d = force;
        range.max_d_m = row.memberId;
      }
      if (force < range.min_d) {
        range.min_d = force;
        range.min_d_m = row.memberId;
      }
      if (moment > range.max_r) {
        range.max_r = moment;
        range.max_r_m = row.memberId;
      }
      if (moment < range.min_r) {
        range.min_r = moment;
        range.min_r_m = row.memberId;
      }
    });
  });

  (["x", "y", "z"] as const).forEach((axis) => {
    const range = valueRange[axis];
    if (!Number.isFinite(range.max_d)) range.max_d = 0;
    if (!Number.isFinite(range.min_d)) range.min_d = 0;
    if (!Number.isFinite(range.max_r)) range.max_r = 0;
    if (!Number.isFinite(range.min_r)) range.min_r = 0;
  });
  return { maxValue, valueRange };
}
