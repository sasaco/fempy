import { Injectable } from "@angular/core";
import {
  AnalysisResultSetIndex,
  ResultStateKind,
  analysisResultSelectionKey,
} from "../../../providers/analysis-result-set";
import {
  buildAnalysisResultPages,
  buildMovingLoadAbsoluteRows,
  buildMovingLoadEnvelope,
  createSafeRecord,
} from "../../../providers/analysis-result-presentation";
import { ResultWorkerReply, runResultWorker } from "../../../providers/result-worker-pipeline";
import { ThreeReactService } from "../../three/geometry/three-react.service";
import { ResultCombineReacService } from "../result-combine-reac/result-combine-reac.service";

interface ReactionRow extends Record<string, unknown> {
  readonly id: string;
  readonly tx: number;
  readonly ty: number;
  readonly tz: number;
  readonly mx: number;
  readonly my: number;
  readonly mz: number;
}

interface ScalarRange {
  max_d: number; min_d: number; max_r: number; min_r: number;
  max_d_m: string; min_d_m: string; max_r_m: string; min_r_m: string;
}

interface ReactionEntry {
  readonly selectionKey: string;
  readonly caseId: string;
  readonly stateKind: ResultStateKind;
  readonly rows: readonly ReactionRow[];
  readonly maxValue: number;
  readonly valueRange: ScalarRange;
}

interface ReactionReply extends ResultWorkerReply { readonly entries: readonly ReactionEntry[]; }
interface ReactionTableReply extends ResultWorkerReply { readonly table: readonly (readonly Record<string, unknown>[])[]; }

const REACTION_COMPONENTS = ["tx", "ty", "tz", "mx", "my", "mz"] as const;

function mergeRanges(entries: readonly ReactionEntry[]): ScalarRange {
  if (entries.length === 0) {
    return { max_d: 0, min_d: 0, max_r: 0, min_r: 0, max_d_m: "0", min_d_m: "0", max_r_m: "0", min_r_m: "0" };
  }
  const result = { ...entries[0].valueRange };
  entries.slice(1).forEach(({ valueRange: range }) => {
    if (range.max_d > result.max_d) { result.max_d = range.max_d; result.max_d_m = range.max_d_m; }
    if (range.min_d < result.min_d) { result.min_d = range.min_d; result.min_d_m = range.min_d_m; }
    if (range.max_r > result.max_r) { result.max_r = range.max_r; result.max_r_m = range.max_r_m; }
    if (range.min_r < result.min_r) { result.min_r = range.min_r; result.min_r_m = range.min_r_m; }
  });
  return result;
}

function formatRows(rows: readonly ReactionRow[]): readonly Record<string, unknown>[] {
  return rows.map((item) => ({
    ...item,
    tx: item.tx.toFixed(2), ty: item.ty.toFixed(2), tz: item.tz.toFixed(2),
    mx: item.mx.toFixed(2), my: item.my.toFixed(2), mz: item.mz.toFixed(2),
  }));
}

@Injectable({ providedIn: "root" })
export class ResultReacService {
  public isCalculated = false;
  public reac: any[] = [];
  public LL_flg: boolean[] = [];
  private columns: any[] = [];
  private readonly worker1: Worker;
  private readonly worker2: Worker;

  public column3Ds: any[] = [
    { title: "result.result-reac.nodeNo", id: "id", format: "", width: -40 },
    { title: "result.result-reac.x_SupportReaction", id: "tx", format: "#.00" },
    { title: "result.result-reac.y_SupportReaction", id: "ty", format: "#.00" },
    { title: "result.result-reac.z_SupportReaction", id: "tz", format: "#.00" },
    { title: "result.result-reac.x_RotationalReaction", id: "mx", format: "#.00" },
    { title: "result.result-reac.y_RotationalReaction", id: "my", format: "#.00" },
    { title: "result.result-reac.z_RotationalReaction", id: "mz", format: "#.00" },
  ];
  public column2Ds: any[] = [
    { title: "result.result-reac.nodeNo", id: "id", format: "", width: -40 },
    { title: "result.result-reac.x_SupportReaction", id: "tx", format: "#.00" },
    { title: "result.result-reac.y_SupportReaction", id: "ty", format: "#.00" },
    { title: "result.result-reac.rotationalRestraint", id: "mz", format: "#.00" },
  ];

  constructor(private readonly comb: ResultCombineReacService, private readonly three: ThreeReactService) {
    this.worker1 = new Worker(new URL("./result-reac1.worker", import.meta.url), { name: "result-reac1", type: "module" });
    this.worker2 = new Worker(new URL("./result-reac2.worker", import.meta.url), { name: "result-reac2", type: "module" });
  }

  public clear(): void {
    this.reac = [];
    this.columns = [];
    this.LL_flg = [];
    this.isCalculated = false;
  }

  public getReacColumns(typNo: number, mode: string = null): any {
    const columns = this.columns[typNo];
    if (columns === undefined) return [];
    return mode !== null && mode in columns ? columns[mode] : columns;
  }

  public getDataColumns(currentPage: number, row: number, mode: string = null): any {
    const empty = { id: "", tx: "", ty: "", tz: "", mx: "", my: "", mz: "", comb: "", case: "" };
    const results = this.reac[currentPage];
    if (results === undefined) return empty;
    if (mode !== null && mode in results) return results[mode][row] ?? empty;
    return results[row] ?? empty;
  }

  public getReacJson(): object { return this.reac; }

  public async setReacJson(
    index: AnalysisResultSetIndex,
    defList: any,
    combList: any,
    pickList: any,
    allowDerived: boolean
  ): Promise<void> {
    this.clear();
    const prepared = await runResultWorker<
      { results: AnalysisResultSetIndex["resultsInOrder"] }, ReactionReply
    >(this.worker1, "result-reac1", { results: index.resultsInOrder });
    const table = await runResultWorker<
      { entries: readonly ReactionEntry[] }, ReactionTableReply
    >(this.worker2, "result-reac2", { entries: prepared.entries });

    const entriesByKey = new Map(prepared.entries.map((entry) => [entry.selectionKey, entry]));
    const tableByKey = new Map(prepared.entries.map((entry, position) => [entry.selectionKey, table.table[position]]));
    const staticByCaseId = createSafeRecord<readonly ReactionRow[]>();
    prepared.entries.forEach((entry) => {
      if (entry.stateKind === "static") staticByCaseId[entry.caseId] = entry.rows;
    });
    const maxValues = createSafeRecord<number>();
    const valueRanges = createSafeRecord<ScalarRange>();
    const threeRows: any[] = [];
    const pages = buildAnalysisResultPages(index);
    pages.forEach((page, pageIndex) => {
      const pageNumber = pageIndex + 1;
      const entries = page.sourceResults.map((result) => entriesByKey.get(analysisResultSelectionKey(result))!);
      maxValues[pageNumber] = Math.max(...entries.map((entry) => entry.maxValue));
      valueRanges[pageNumber] = mergeRanges(entries);
      if (page.movingLoad) {
        threeRows[pageNumber] = buildMovingLoadAbsoluteRows(
          page.result.case_id,
          entries.map((entry) => ({ caseId: entry.caseId, rows: entry.rows })),
          REACTION_COMPONENTS,
          (row) => row.id
        );
        const envelope = buildMovingLoadEnvelope(
          page.result.case_id,
          entries.map((entry) => ({ caseId: entry.caseId, rows: entry.rows })),
          REACTION_COMPONENTS,
          (row) => row.id
        );
        const formatted = createSafeRecord<readonly Record<string, unknown>[]>();
        Object.keys(envelope).forEach((mode) => (formatted[mode] = formatRows(envelope[mode])));
        this.reac[pageNumber] = envelope;
        this.columns[pageNumber] = formatted;
      } else {
        const entry = entries[0];
        threeRows[pageNumber] = entry.rows;
        this.reac[pageNumber] = entry.rows;
        this.columns[pageNumber] = tableByKey.get(entry.selectionKey) ?? [];
      }
    });

    this.LL_flg = pages.map((page) => page.movingLoad);
    this.three.setResultData(threeRows, maxValues, { reac: valueRanges }, "reac");
    if (allowDerived) this.comb.setReacCombineJson(staticByCaseId, defList, combList, pickList);
  }
}
