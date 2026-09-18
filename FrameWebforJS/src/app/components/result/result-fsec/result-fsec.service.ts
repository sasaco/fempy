import { Injectable } from "@angular/core";
import {
  AnalysisResultSetIndex,
  ResultStateKind,
  analysisResultSelectionKey,
} from "../../../providers/analysis-result-set";
import {
  MemberForceDisplayRow,
  MemberForceMetrics,
  buildAnalysisResultPages,
  buildMovingLoadEnvelope,
  calculateMemberForceMetrics,
  createSafeRecord,
} from "../../../providers/analysis-result-presentation";
import { ResultWorkerReply, runResultWorker } from "../../../providers/result-worker-pipeline";
import { ThreeSectionForceService } from "../../three/geometry/three-section-force/three-section-force.service";
import { ResultCombineFsecService } from "../result-combine-fsec/result-combine-fsec.service";

interface MemberForceTableRow extends MemberForceDisplayRow {
  readonly nonDummyRowIndex: number;
}

interface MemberForceEntry {
  readonly selectionKey: string;
  readonly caseId: string;
  readonly stateKind: ResultStateKind;
  readonly rows: readonly MemberForceDisplayRow[];
  readonly maxValue: MemberForceMetrics["maxValue"];
  readonly valueRange: MemberForceMetrics["valueRange"];
}

interface MemberForceReply extends ResultWorkerReply {
  readonly entries: readonly MemberForceEntry[];
}

interface MemberForceTableReply extends ResultWorkerReply {
  readonly table: readonly (readonly Record<string, unknown>[])[];
}

const MEMBER_FORCE_COMPONENTS = ["fx", "fy", "fz", "mx", "my", "mz"] as const;

function indexedRows(rows: readonly MemberForceDisplayRow[]): readonly MemberForceTableRow[] {
  return rows.map((row, nonDummyRowIndex) => ({ ...row, nonDummyRowIndex }));
}

function formatRows(rows: readonly MemberForceTableRow[]): readonly Record<string, unknown>[] {
  return rows.map((item) => ({
    ...item,
    l: item.l.toFixed(3),
    fx: item.fx.toFixed(2),
    fy: item.fy.toFixed(2),
    fz: item.fz.toFixed(2),
    mx: item.mx.toFixed(2),
    my: item.my.toFixed(2),
    mz: item.mz.toFixed(2),
  }));
}

@Injectable({ providedIn: "root" })
export class ResultFsecService {
  public isCalculated = false;
  public fsec: any[] = [];
  public LL_flg: boolean[] = [];
  private columns: any[] = [];
  private readonly worker1: Worker;
  private readonly worker2: Worker;

  public column3Ds: any[] = [
    { title: "result.result-fsec.memberNo", id: "m", format: "", width: -40 },
    { title: "result.result-fsec.nodeNo", id: "n", format: "", width: -40 },
    { title: "result.result-fsec.stationLocation", id: "l", format: "#.000" },
    { title: "result.result-fsec.axialForce", id: "fx", format: "#.00" },
    { title: "result.result-fsec.y_shear", id: "fy", format: "#.00" },
    { title: "result.result-fsec.z_shear", id: "fz", format: "#.00" },
    { title: "result.result-fsec.x_torsion", id: "mx", format: "#.00" },
    { title: "result.result-fsec.y_moment", id: "my", format: "#.00" },
    { title: "result.result-fsec.z_moment", id: "mz", format: "#.00" },
  ];
  public column2Ds: any[] = [
    { title: "result.result-fsec.memberNo", id: "m", format: "", width: -40 },
    { title: "result.result-fsec.nodeNo", id: "n", format: "", width: -40 },
    { title: "result.result-fsec.stationLocation", id: "l", format: "#.000" },
    { title: "result.result-fsec.axialForce", id: "fx", format: "#.00" },
    { title: "result.result-fsec.shear", id: "fy", format: "#.00" },
    { title: "result.result-fsec.moment", id: "mz", format: "#.00" },
  ];

  constructor(
    private readonly comb: ResultCombineFsecService,
    private readonly three: ThreeSectionForceService
  ) {
    this.worker1 = new Worker(new URL("./result-fsec1.worker", import.meta.url), { name: "result-fsec1", type: "module" });
    this.worker2 = new Worker(new URL("./result-fsec2.worker", import.meta.url), { name: "result-fsec2", type: "module" });
  }

  public clear(): void {
    this.fsec = [];
    this.columns = [];
    this.LL_flg = [];
    this.isCalculated = false;
  }

  public clearGradient(): void {
    this.three.ClearDataGradient();
  }

  public getDataColumns(currentPage: number, row: number, mode: string = null): any {
    const empty = { n: "", m: "", l: "", mx: "", my: "", mz: "", fx: "", fy: "", fz: "", case: "", nonDummyRowIndex: undefined };
    let results = this.fsec[currentPage];
    if (results === undefined) return empty;
    if (mode !== null && mode in results) results = results[mode];
    return results.find((item) => item.nonDummyRowIndex === row) ?? empty;
  }

  public getFsecJson(): object {
    return this.fsec;
  }

  public async setFsecJson(
    index: AnalysisResultSetIndex,
    defList: any,
    combList: any,
    pickList: any,
    allowDerived: boolean
  ): Promise<void> {
    this.clear();
    const prepared = await runResultWorker<
      { results: AnalysisResultSetIndex["resultsInOrder"]; topology: AnalysisResultSetIndex["value"]["topology"] },
      MemberForceReply
    >(this.worker1, "result-fsec1", { results: index.resultsInOrder, topology: index.value.topology });
    const table = await runResultWorker<
      { entries: readonly MemberForceEntry[] },
      MemberForceTableReply
    >(this.worker2, "result-fsec2", {
      entries: prepared.entries.map((entry) => ({ ...entry, rows: indexedRows(entry.rows) })),
    });

    const entriesByKey = new Map(prepared.entries.map((entry) => [entry.selectionKey, entry]));
    const tableByKey = new Map(prepared.entries.map((entry, position) => [entry.selectionKey, table.table[position]]));
    const staticByCaseId = createSafeRecord<readonly MemberForceDisplayRow[]>();
    prepared.entries.forEach((entry) => {
      if (entry.stateKind === "static") staticByCaseId[entry.caseId] = entry.rows;
    });

    const maxValues = createSafeRecord<MemberForceMetrics["maxValue"]>();
    const valueRanges = createSafeRecord<MemberForceMetrics["valueRange"]>();
    const pages = buildAnalysisResultPages(index);
    pages.forEach((page, pageIndex) => {
      const pageNumber = pageIndex + 1;
      const entries = page.sourceResults.map((result) => entriesByKey.get(analysisResultSelectionKey(result))!);
      if (page.movingLoad) {
        const envelope = buildMovingLoadEnvelope(
          page.result.case_id,
          entries.map((entry) => ({ caseId: entry.caseId, rows: entry.rows })),
          MEMBER_FORCE_COMPONENTS,
          (_row, rowIndex) => String(rowIndex)
        );
        const indexedEnvelope = createSafeRecord<readonly MemberForceTableRow[]>();
        const formattedEnvelope = createSafeRecord<readonly Record<string, unknown>[]>();
        Object.keys(envelope).forEach((mode) => {
          indexedEnvelope[mode] = indexedRows(envelope[mode]);
          formattedEnvelope[mode] = formatRows(indexedEnvelope[mode]);
        });
        this.fsec[pageNumber] = indexedEnvelope;
        this.columns[pageNumber] = formattedEnvelope;
        const metrics = calculateMemberForceMetrics(entries.flatMap((entry) => entry.rows));
        maxValues[pageNumber] = metrics.maxValue;
        valueRanges[pageNumber] = metrics.valueRange;
      } else {
        const entry = entries[0];
        const rows = indexedRows(entry.rows);
        this.fsec[pageNumber] = rows;
        this.columns[pageNumber] = tableByKey.get(entry.selectionKey) ?? formatRows(rows);
        maxValues[pageNumber] = entry.maxValue;
        valueRanges[pageNumber] = entry.valueRange;
      }
    });

    this.LL_flg = pages.map((page) => page.movingLoad);
    this.three.setResultData(this.fsec, maxValues, valueRanges);
    if (allowDerived) this.comb.setFsecCombineJson(staticByCaseId, defList, combList, pickList);
  }
}
