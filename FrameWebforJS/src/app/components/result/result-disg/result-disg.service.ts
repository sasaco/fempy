import { Injectable } from "@angular/core";
import {
  AnalysisResultSetIndex,
  ResultStateKind,
  analysisResultSelectionKey,
} from "../../../providers/analysis-result-set";
import {
  buildAnalysisResultPages,
  buildMovingLoadEnvelope,
  createSafeRecord,
} from "../../../providers/analysis-result-presentation";
import {
  ResultWorkerReply,
  runResultWorker,
} from "../../../providers/result-worker-pipeline";
import { PrintCustomService } from "../../print/custom/print-custom.service";
import { ThreeDisplacementService } from "../../three/geometry/three-displacement.service";
import { ResultCombineDisgService } from "../result-combine-disg/result-combine-disg.service";

interface DisplacementRow extends Record<string, unknown> {
  readonly id: string;
  readonly dx: number;
  readonly dy: number;
  readonly dz: number;
  readonly rx: number;
  readonly ry: number;
  readonly rz: number;
}

interface ScalarRange {
  max_d: number;
  min_d: number;
  max_r: number;
  min_r: number;
  max_d_m: string;
  min_d_m: string;
  max_r_m: string;
  min_r_m: string;
}

interface DisplacementEntry {
  readonly selectionKey: string;
  readonly caseId: string;
  readonly stateKind: ResultStateKind;
  readonly rows: readonly DisplacementRow[];
  readonly maxValue: number;
  readonly valueRange: ScalarRange;
}

interface DisplacementReply extends ResultWorkerReply {
  readonly entries: readonly DisplacementEntry[];
}

interface DisplacementTableReply extends ResultWorkerReply {
  readonly table: readonly (readonly Record<string, unknown>[])[];
}

const DISPLACEMENT_COMPONENTS = ["dx", "dy", "dz", "rx", "ry", "rz"] as const;

function mergeRanges(entries: readonly DisplacementEntry[]): ScalarRange {
  if (entries.length === 0) {
    return { max_d: 0, min_d: 0, max_r: 0, min_r: 0, max_d_m: "0", min_d_m: "0", max_r_m: "0", min_r_m: "0" };
  }
  const result = { ...entries[0].valueRange };
  entries.slice(1).forEach((entry) => {
    const range = entry.valueRange;
    if (range.max_d > result.max_d) { result.max_d = range.max_d; result.max_d_m = range.max_d_m; }
    if (range.min_d < result.min_d) { result.min_d = range.min_d; result.min_d_m = range.min_d_m; }
    if (range.max_r > result.max_r) { result.max_r = range.max_r; result.max_r_m = range.max_r_m; }
    if (range.min_r < result.min_r) { result.min_r = range.min_r; result.min_r_m = range.min_r_m; }
  });
  return result;
}

function formatRows(rows: readonly DisplacementRow[]): readonly Record<string, unknown>[] {
  return rows.map((item) => ({
    ...item,
    dx: item.dx.toFixed(4), dy: item.dy.toFixed(4), dz: item.dz.toFixed(4),
    rx: item.rx.toFixed(4), ry: item.ry.toFixed(4), rz: item.rz.toFixed(4),
  }));
}

@Injectable({ providedIn: "root" })
export class ResultDisgService {
  public isCalculated = false;
  public disg: any[] = [];
  public LL_flg: boolean[] = [];
  private columns: any[] = [];
  private readonly worker1: Worker;
  private readonly worker2: Worker;

  public column3Ds: any[] = [
    { title: "result.result-disg.No", id: "id", format: "" },
    { title: "result.result-disg.x_movement", id: "dx", format: "#.0000" },
    { title: "result.result-disg.y_movement", id: "dy", format: "#.0000" },
    { title: "result.result-disg.z_movement", id: "dz", format: "#.0000" },
    { title: "result.result-disg.x_rotation", id: "rx", format: "#.0000" },
    { title: "result.result-disg.y_rotation", id: "ry", format: "#.0000" },
    { title: "result.result-disg.z_rotation", id: "rz", format: "#.0000" },
  ];
  public column2Ds: any[] = [
    { title: "result.result-disg.No", id: "id", format: "" },
    { title: "result.result-disg.x_movement", id: "dx", format: "#.0000" },
    { title: "result.result-disg.y_movement", id: "dy", format: "#.0000" },
    { title: "result.result-disg.z_rotation", id: "rz", format: "#.0000" },
  ];

  constructor(
    private readonly comb: ResultCombineDisgService,
    private readonly three: ThreeDisplacementService,
    public readonly printCustomService: PrintCustomService
  ) {
    this.worker1 = new Worker(new URL("./result-disg1.worker", import.meta.url), { name: "result-disg1", type: "module" });
    this.worker2 = new Worker(new URL("./result-disg2.worker", import.meta.url), { name: "result-disg2", type: "module" });
  }

  public clear(): void {
    this.disg = [];
    this.columns = [];
    this.LL_flg = [];
    this.isCalculated = false;
  }

  public getDisgColumns(typNo: number, mode: string = null): any {
    const columns = this.columns[typNo];
    if (columns === undefined) return [];
    return mode !== null && mode in columns ? columns[mode] : columns;
  }

  public getDataColumns(currentPage: number, row: number, mode: string = null): any {
    const empty = { id: "", dx: "", dy: "", dz: "", rx: "", ry: "", rz: "", comb: "", case: "" };
    const results = this.disg[currentPage];
    if (results === undefined) return empty;
    if (mode !== null && mode in results) return results[mode][row] ?? empty;
    return results[row] ?? empty;
  }

  public getDisgJson(): object {
    return this.disg;
  }

  public async setDisgJson(
    index: AnalysisResultSetIndex,
    defList: any,
    combList: any,
    pickList: any,
    allowDerived: boolean
  ): Promise<void> {
    this.clear();
    const prepared = await runResultWorker<
      { results: AnalysisResultSetIndex["resultsInOrder"] },
      DisplacementReply
    >(this.worker1, "result-disg1", { results: index.resultsInOrder });
    const table = await runResultWorker<
      { entries: readonly DisplacementEntry[] },
      DisplacementTableReply
    >(this.worker2, "result-disg2", { entries: prepared.entries });

    const entriesByKey = new Map(prepared.entries.map((entry) => [entry.selectionKey, entry]));
    const tableByKey = new Map(prepared.entries.map((entry, position) => [entry.selectionKey, table.table[position]]));
    const baseRowsBySelection = createSafeRecord<readonly DisplacementRow[]>();
    const staticByCaseId = createSafeRecord<readonly DisplacementRow[]>();
    const maxValues = createSafeRecord<number>();
    const valueRanges = createSafeRecord<ScalarRange>();
    prepared.entries.forEach((entry) => {
      baseRowsBySelection[entry.selectionKey] = entry.rows;
      maxValues[entry.selectionKey] = entry.maxValue;
      valueRanges[entry.selectionKey] = entry.valueRange;
      if (entry.stateKind === "static") staticByCaseId[entry.caseId] = entry.rows;
    });

    const pages = buildAnalysisResultPages(index);
    pages.forEach((page, pageIndex) => {
      const pageNumber = pageIndex + 1;
      const pageKey = analysisResultSelectionKey(page.result);
      const entries = page.sourceResults.map((result) => entriesByKey.get(analysisResultSelectionKey(result))!);
      if (page.movingLoad) {
        const sources = entries.map((entry) => ({ caseId: entry.caseId, rows: entry.rows }));
        const envelope = buildMovingLoadEnvelope(page.result.case_id, sources, DISPLACEMENT_COMPONENTS, (row) => row.id);
        const formatted = createSafeRecord<readonly Record<string, unknown>[]>();
        Object.keys(envelope).forEach((mode) => (formatted[mode] = formatRows(envelope[mode])));
        this.disg[pageNumber] = envelope;
        this.columns[pageNumber] = formatted;
        maxValues[pageKey] = Math.max(...entries.map((entry) => entry.maxValue));
        valueRanges[pageKey] = mergeRanges(entries);
      } else {
        const entry = entries[0];
        this.disg[pageNumber] = entry.rows;
        this.columns[pageNumber] = tableByKey.get(entry.selectionKey) ?? [];
      }
    });

    this.LL_flg = pages.map((page) => page.movingLoad);
    this.printCustomService.LL_flg = [...this.LL_flg];
    this.printCustomService.LL();
    this.three.setResultData(
      baseRowsBySelection,
      maxValues,
      valueRanges,
      "disg",
      pages.map((page) => analysisResultSelectionKey(page.result)),
      pages.filter((page) => page.movingLoad).map((page) => page.result.case_id)
    );
    if (allowDerived) this.comb.setDisgCombineJson(staticByCaseId, defList, combList, pickList);
  }
}
