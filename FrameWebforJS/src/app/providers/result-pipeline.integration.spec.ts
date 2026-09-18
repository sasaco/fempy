import * as pako from "pako";
import { Subject } from "rxjs";

import { AppComponent } from "../app.component";
import { PagerComponent } from "../components/input/pager/pager.component";
import { PagerService } from "../components/input/pager/pager.service";
import { ResultDisgService } from "../components/result/result-disg/result-disg.service";
import { ResultFsecService } from "../components/result/result-fsec/result-fsec.service";
import { ResultReacService } from "../components/result/result-reac/result-reac.service";
import { calculateMemberForceMetrics } from "./analysis-result-presentation";
import { analysisResultSelectionKey } from "./analysis-result-set";
import { InputDataService } from "./input-data.service";
import { ResultDataService } from "./result-data.service";
import { movingLoadAnalysisResultSetFixture } from "./testing/moving-load-analysis-result-set.fixture";

// AppComponent registers an unrelated screenshot callback on window.onload.
// The focused pipeline fixture has no #target element, so keep that global side
// effect outside this service-boundary integration test.
const applicationOnLoad = window.onload;
window.onload = null;

function scalarMetrics(
  rows: readonly any[],
  displacementKeys: readonly string[],
  rotationKeys: readonly string[]
): any {
  let maxDisplacement = Number.NEGATIVE_INFINITY;
  let minDisplacement = Number.POSITIVE_INFINITY;
  let maxRotation = Number.NEGATIVE_INFINITY;
  let minRotation = Number.POSITIVE_INFINITY;
  let maxDisplacementNode = "0";
  let minDisplacementNode = "0";
  let maxRotationNode = "0";
  let minRotationNode = "0";
  rows.forEach((row) => {
    displacementKeys.forEach((key) => {
      if (row[key] > maxDisplacement) { maxDisplacement = row[key]; maxDisplacementNode = row.id; }
      if (row[key] < minDisplacement) { minDisplacement = row[key]; minDisplacementNode = row.id; }
    });
    rotationKeys.forEach((key) => {
      if (row[key] > maxRotation) { maxRotation = row[key]; maxRotationNode = row.id; }
      if (row[key] < minRotation) { minRotation = row[key]; minRotationNode = row.id; }
    });
  });
  if (rows.length === 0) {
    maxDisplacement = minDisplacement = maxRotation = minRotation = 0;
  }
  return {
    maxValue: Math.max(Math.abs(maxDisplacement), Math.abs(minDisplacement)),
    valueRange: {
      max_d: maxDisplacement,
      min_d: minDisplacement,
      max_r: maxRotation,
      min_r: minRotation,
      max_d_m: maxDisplacementNode,
      min_d_m: minDisplacementNode,
      max_r_m: maxRotationNode,
      min_r_m: minRotationNode,
    },
  };
}

class FakeResultWorker {
  public static postedWorkerNames: string[] = [];
  private readonly listeners = new Map<string, Set<(event: any) => void>>();
  private readonly workerName: string;

  constructor(url: URL | string) {
    const match = String(url).match(/result-(?:disg|reac|fsec)[12]\.worker/);
    if (match === null) throw new Error(`Unexpected worker URL: ${url}`);
    this.workerName = match[0];
  }

  public addEventListener(type: string, listener: (event: any) => void): void {
    if (!this.listeners.has(type)) this.listeners.set(type, new Set());
    this.listeners.get(type)!.add(listener);
  }

  public removeEventListener(type: string, listener: (event: any) => void): void {
    this.listeners.get(type)?.delete(listener);
  }

  public postMessage(request: any): void {
    FakeResultWorker.postedWorkerNames.push(this.workerName);
    Promise.resolve().then(() => this.emit("message", { data: this.reply(request) }));
  }

  public terminate(): void {}

  private emit(type: string, event: any): void {
    this.listeners.get(type)?.forEach((listener) => listener(event));
  }

  private reply(request: any): any {
    if (this.workerName === "result-disg1.worker") {
      const entries = request.results.map((result: any) => {
        const rows = result.node_displacements.map((row: any) => ({ id: row.node_id, ...row.components }));
        return {
          selectionKey: analysisResultSelectionKey(result),
          caseId: result.case_id,
          stateKind: result.state.kind,
          rows,
          ...scalarMetrics(rows, ["dx", "dy", "dz"], ["rx", "ry", "rz"]),
        };
      });
      return { entries, error: null };
    }
    if (this.workerName === "result-disg2.worker") {
      const table = request.entries.map((entry: any) => entry.rows.map((row: any) => ({
        id: row.id,
        dx: row.dx.toFixed(4), dy: row.dy.toFixed(4), dz: row.dz.toFixed(4),
        rx: row.rx.toFixed(4), ry: row.ry.toFixed(4), rz: row.rz.toFixed(4),
      })));
      return { table, error: null };
    }
    if (this.workerName === "result-reac1.worker") {
      const entries = request.results.map((result: any) => {
        const rows = result.support_reactions.map((row: any) => ({
          id: row.node_id,
          tx: row.components.fx, ty: row.components.fy, tz: row.components.fz,
          mx: row.components.mx, my: row.components.my, mz: row.components.mz,
        }));
        return {
          selectionKey: analysisResultSelectionKey(result),
          caseId: result.case_id,
          stateKind: result.state.kind,
          rows,
          ...scalarMetrics(rows, ["tx", "ty", "tz"], ["mx", "my", "mz"]),
        };
      });
      return { entries, error: null };
    }
    if (this.workerName === "result-reac2.worker") {
      const table = request.entries.map((entry: any) => entry.rows.map((row: any) => ({
        ...row,
        tx: row.tx.toFixed(2), ty: row.ty.toFixed(2), tz: row.tz.toFixed(2),
        mx: row.mx.toFixed(2), my: row.my.toFixed(2), mz: row.mz.toFixed(2),
      })));
      return { table, error: null };
    }
    if (this.workerName === "result-fsec1.worker") {
      const members = new Map(request.topology.members.map((member: any) => [member.member_id, member]));
      const entries = request.results.map((result: any) => {
        const rows: any[] = [];
        result.member_section_forces.forEach((memberResult: any) => {
          const member: any = members.get(memberResult.member_id);
          memberResult.segments.forEach((segment: any, segmentIndex: number) => {
            rows.push({
              memberId: member.member_id,
              m: segmentIndex === 0 ? member.member_id : "",
              n: segmentIndex === 0 ? member.node_i : "",
              l: member.stations[segmentIndex].position,
              ...segment.i_end,
              dummy: false,
            });
            rows.push({
              memberId: member.member_id,
              m: "",
              n: segmentIndex === memberResult.segments.length - 1 ? member.node_j : "",
              l: member.stations[segmentIndex + 1].position,
              ...segment.j_end,
              dummy: false,
            });
          });
        });
        const metrics = calculateMemberForceMetrics(rows);
        return {
          selectionKey: analysisResultSelectionKey(result),
          caseId: result.case_id,
          stateKind: result.state.kind,
          rows,
          ...metrics,
        };
      });
      return { entries, error: null };
    }
    const table = request.entries.map((entry: any) => entry.rows.map((row: any) => ({
      ...row,
      l: row.l.toFixed(3),
      fx: row.fx.toFixed(2), fy: row.fy.toFixed(2), fz: row.fz.toFixed(2),
      mx: row.mx.toFixed(2), my: row.my.toFixed(2), mz: row.mz.toFixed(2),
    })));
    return { table, error: null };
  }
}

function encodedResponse(value: unknown): string {
  const compressed = pako.gzip(JSON.stringify(value)) as Uint8Array;
  const binary = Array.from(compressed, (byte) => String.fromCharCode(byte)).join("");
  return btoa(binary);
}

function createClearable(methodName: string): any {
  return {
    clear: jasmine.createSpy(`clear ${methodName}`),
    [methodName]: jasmine.createSpy(methodName),
  };
}

function createHarness(response: unknown): any {
  const combineDisg = createClearable("setDisgCombineJson");
  const combineReac = createClearable("setReacCombineJson");
  const combineFsec = createClearable("setFsecCombineJson");
  const pickupDisg = createClearable("setDisgPickupJson");
  const pickupReac = createClearable("setReacPickupJson");
  const pickupFsec = createClearable("setFsecPickupJson");
  const threeDisg = {
    ClearData: jasmine.createSpy("clear displacement 3D data"),
    setResultData: jasmine.createSpy("publish displacement 3D data"),
  };
  const threeReac = {
    ClearData: jasmine.createSpy("clear reaction 3D data"),
    setResultData: jasmine.createSpy("publish reaction 3D data"),
  };
  const threeFsec = {
    ClearData: jasmine.createSpy("clear member-force 3D data"),
    ClearDataGradient: jasmine.createSpy("clear member-force gradient"),
    setResultData: jasmine.createSpy("publish member-force 3D data"),
  };
  const printCustom = {
    LL_flg: [] as boolean[],
    LL: jasmine.createSpy("refresh moving-load print controls"),
  };

  const disg = new ResultDisgService(combineDisg, threeDisg as any, printCustom as any);
  const reac = new ResultReacService(combineReac, threeReac as any);
  const fsec = new ResultFsecService(combineFsec, threeFsec as any);

  const define = {
    getDefineJson: () => ({}),
    validate: () => null,
  };
  const combine = {
    getCombineJson: () => ({}),
    getCombineName: (page: number) => `Combine ${page}`,
    validate: () => null,
  };
  const pickup = {
    getPickUpJson: () => ({}),
    getPickUpName: (page: number) => `Pickup ${page}`,
    validate: () => null,
  };
  const load = {
    getLoadName: (page: number) => `Load ${page}`,
    getLoadNameJson: () => ({
      "1": { symbol: "LL" },
      "2": { symbol: "D" },
      "1.1": { symbol: "LL" },
      "1.2": { symbol: "LL" },
    }),
  };
  const helper = {
    alert: jasmine.createSpy("alert"),
    toNumber: (value: unknown) => {
      const parsed = Number(value);
      return Number.isFinite(parsed) ? parsed : null;
    },
  };
  const translate = {
    instant: (key: string) => key,
  };

  const inputData = Object.create(InputDataService.prototype) as InputDataService;
  inputData.result = { value: "previous" };
  spyOn(inputData, "getResult").and.callThrough();

  const resultData = new ResultDataService(
    inputData,
    combine as any,
    define as any,
    load as any,
    pickup as any,
    disg,
    reac,
    fsec,
    combineDisg,
    combineReac,
    combineFsec,
    pickupDisg,
    pickupReac,
    pickupFsec,
    threeFsec as any,
    threeReac as any,
    threeDisg as any,
    translate as any,
    helper as any
  );

  const http = {
    post: jasmine.createSpy("post canonical response").and.returnValue({
      subscribe: (next: (body: string) => void) => next(encodedResponse(response)),
    }),
  };
  const app = Object.create(AppComponent.prototype) as AppComponent;
  Object.assign(app as any, {
    ResultData: resultData,
    InputData: inputData,
    http,
    helper,
    translate,
    language: { browserLang: "en" },
  });

  return {
    app,
    combine,
    disg,
    fsec,
    helper,
    http,
    inputData,
    load,
    pickup,
    printCustom,
    reac,
    resultData,
    threeDisg,
    threeFsec,
    threeReac,
  };
}

async function postAndWaitForClose(app: AppComponent): Promise<jasmine.Spy> {
  let close: (() => void) | null = null;
  const closed = new Promise<void>((resolve) => (close = resolve));
  const closeSpy = jasmine.createSpy("close calculation wait dialog").and.callFake(() => close!());
  (app as any).post_compress({}, { close: closeSpy });
  await closed;
  return closeSpy;
}

describe("canonical result pipeline integration", () => {
  const nativeWorker = globalThis.Worker;

  beforeEach(() => {
    FakeResultWorker.postedWorkerNames = [];
    Object.defineProperty(globalThis, "Worker", { configurable: true, writable: true, value: FakeResultWorker });
  });

  afterEach(() => {
    Object.defineProperty(globalThis, "Worker", { configurable: true, writable: true, value: nativeWorker });
  });

  afterAll(() => {
    window.onload = applicationOnLoad;
  });

  it("runs the real result services through worker completion before persistence and preserves page order", async () => {
    const fixture = JSON.parse(JSON.stringify(movingLoadAnalysisResultSetFixture));
    const harness = createHarness(fixture);
    const completionOrder: string[] = [];
    (harness.inputData.getResult as jasmine.Spy).and.callFake((value: object) => {
      expect(harness.resultData.isCalculated).toBeTrue();
      harness.inputData.result = value;
      completionOrder.push("persist");
    });
    spyOn(console, "log");

    const modalClose = await postAndWaitForClose(harness.app);
    completionOrder.push("close");

    expect(modalClose).toHaveBeenCalledTimes(1);
    expect(completionOrder).toEqual(["persist", "close"]);
    expect(harness.inputData.getResult).toHaveBeenCalledTimes(1);
    expect(harness.resultData.getResultPageCount()).toBe(2);
    expect(harness.resultData.getResultAtPage(1)?.case_id).toBe("1");
    expect(harness.resultData.getResultAtPage(2)?.case_id).toBe("2");
    expect(FakeResultWorker.postedWorkerNames).toEqual([
      "result-disg1.worker",
      "result-reac1.worker",
      "result-fsec1.worker",
      "result-disg2.worker",
      "result-reac2.worker",
      "result-fsec2.worker",
    ]);
    expect(harness.disg.LL_flg).toEqual([true, false]);
    expect(harness.reac.LL_flg).toEqual([true, false]);
    expect(harness.fsec.LL_flg).toEqual([true, false]);

    expect(harness.disg.getDataColumns(1, 1, "dx_max").dx).toBeCloseTo(0.003);
    expect(harness.disg.getDataColumns(2, 1).dx).toBeCloseTo(0.004);
    expect(harness.reac.getDataColumns(1, 0, "tx_min").tx).toBe(-30);
    expect(harness.reac.getDataColumns(2, 0).tx).toBe(-40);
    expect(harness.fsec.getDataColumns(1, 0, "fx_max").fx).toBe(30);
    expect(harness.fsec.getDataColumns(2, 0).fx).toBe(40);
    expect(harness.disg.getDisgColumns(2)[1].dx).toBe("0.0040");
    expect(harness.reac.getReacColumns(2)[0].tx).toBe("-40.00");

    expect(harness.threeDisg.setResultData).toHaveBeenCalledTimes(1);
    expect(harness.threeReac.setResultData).toHaveBeenCalledTimes(1);
    expect(harness.threeFsec.setResultData).toHaveBeenCalledTimes(1);
    const reactionPages = harness.threeReac.setResultData.calls.mostRecent().args[0];
    expect(reactionPages[1][0].tx).toBe(-30);
    expect(harness.printCustom.LL_flg).toEqual([true, false]);
    expect(harness.printCustom.LL).toHaveBeenCalledTimes(1);

    const pagerService = new PagerService();
    const selectedPages: number[] = [];
    pagerService.pageSelected$.subscribe((page) => selectedPages.push(page));
    const router = {
      url: "/result-reac",
      events: new Subject<unknown>(),
    };
    const pager = new PagerComponent(
      router as any,
      pagerService,
      harness.load,
      harness.resultData,
      harness.combine,
      harness.pickup
    );
    pager.ngOnInit();

    expect(pager.pages).toEqual([1, 2]);
    expect(pager.pages_name).toEqual(["1.Moving load", "2.Following dead load"]);
    expect(selectedPages).toEqual([1]);
    pager.nextPage();
    expect(pager.selectedPage).toBe(2);
    expect(selectedPages).toEqual([1, 2]);
  });

  it("rejects an invalid response before real workers or persisted input are touched", async () => {
    const harness = createHarness({});
    const previous = harness.inputData.result;
    const setDisg = spyOn(harness.disg, "setDisgJson").and.callThrough();
    const setReac = spyOn(harness.reac, "setReacJson").and.callThrough();
    const setFsec = spyOn(harness.fsec, "setFsecJson").and.callThrough();
    spyOn(console, "log");

    const modalClose = await postAndWaitForClose(harness.app);

    expect(modalClose).toHaveBeenCalledTimes(1);
    expect(setDisg).not.toHaveBeenCalled();
    expect(setReac).not.toHaveBeenCalled();
    expect(setFsec).not.toHaveBeenCalled();
    expect(harness.inputData.getResult).not.toHaveBeenCalled();
    expect(harness.inputData.result).toBe(previous);
    expect(harness.resultData.resultSet).toBeNull();
    expect(harness.resultData.isCalculated).toBeFalse();
    expect(harness.helper.alert).toHaveBeenCalledTimes(1);
    expect(harness.helper.alert.calls.mostRecent().args[0]).toContain("$");
  });
});
