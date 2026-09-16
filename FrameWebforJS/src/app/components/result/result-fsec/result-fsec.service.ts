import { Injectable } from "@angular/core";
import { DataHelperModule } from "../../../providers/data-helper.module";
import { InputLoadService } from "../../input/input-load/input-load.service";
import { InputMembersService } from "../../input/input-members/input-members.service";
import { ThreeSectionForceService } from "../../three/geometry/three-section-force/three-section-force.service";
import { ResultCombineFsecService } from "../result-combine-fsec/result-combine-fsec.service";
import { forEach } from "jszip";

@Injectable({
  providedIn: "root",
})
export class ResultFsecService {
  public isCalculated: boolean;
  public fsec: any[];
  private worker1: Worker;
  private worker2: Worker;
  private worker3: Worker;
  private worker4: Worker;
  private columns: any;

  public column3Ds: any[] = [
    { title: "result.result-fsec.memberNo", id: "m", format: "", width: -40 },
    { title: "result.result-fsec.nodeNo", id: "n", format: "", width: -40 },
    { title: "result.result-fsec.stationLocation", id: "l", format: "#.000" },
    { title: "result.result-fsec.axialForce", id: "fx", format:'#.00' },
    { title: "result.result-fsec.y_shear", id: "fy", format:'#.00' },
    { title: "result.result-fsec.z_shear", id: "fz", format:'#.00' },
    { title: "result.result-fsec.x_torsion", id: "mx", format:'#.00' },
    { title: "result.result-fsec.y_moment", id: "my", format:'#.00' },
    { title: "result.result-fsec.z_moment", id: "mz", format:'#.00' },
    { title: "dummy", id: "dummy", hidden: true },
  ];

  public column2Ds: any[] = [
    { title: "result.result-fsec.memberNo", id: "m", format: "", width: -40 },
    { title: "result.result-fsec.nodeNo", id: "n", format: "", width: -40 },
    { title: "result.result-fsec.stationLocation", id: "l", format: "#.000" },
    { title: "result.result-fsec.axialForce", id: "fx", format:'#.00' },
    { title: "result.result-fsec.shear", id: "fy", format:'#.00' },
    { title: "result.result-fsec.moment", id: "mz", format:'#.00' },
    { title: "dummy", id: "dummy", hidden: true },
  ];


  public LL_flg = [];

  constructor(
    public member: InputMembersService,
    private load: InputLoadService,
    public comb: ResultCombineFsecService,
    private three: ThreeSectionForceService,
    private helper: DataHelperModule
  ) {
    this.clear();
    this.worker1 = new Worker(
      new URL("./result-fsec1.worker", import.meta.url),
      { name: "result-fsec1", type: "module" }
    );
    this.worker2 = new Worker(
      new URL("./result-fsec2.worker", import.meta.url),
      { name: "result-fsec2", type: "module" }
    );
    // 連行荷重の集計
    this.worker3 = new Worker(
      new URL(
        "../result-combine-fsec/result-combine-fsec1.worker",
        import.meta.url
      ),
      { name: "LL-fsec1", type: "module" }
    );
    this.worker4 = new Worker(
      new URL(
        "../result-combine-fsec/result-combine-fsec2.worker",
        import.meta.url
      ),
      { name: "LL-fsec2", type: "module" }
    );
    this.LL_flg = new Array();
  }

  public clear(): void {
    // this.fsec = {};
    this.isCalculated = false;
    this.fsec =  new Array();
  }

  public clearGradient(){
    this.three.ClearDataGradient();
  }

  public getDataColumns(currentPage:number, row: number, mode: string = null):any{

    let result = {
      n: "",
      m: "",
      l:"",
      mx: "",
      my: "",
      mz: "",
      fx: "",
      fy: "",
      fz: "",
      case: "",
      nonDummyRowIndex: undefined,
    };

    let results: any = this.fsec[currentPage];
    if(results == undefined){
      return result;
    };

    if (mode in results) {
      let modes = results[mode] != undefined ? results[mode] : undefined;
      if (modes != undefined) {
        results = modes;
      }
    }
    const idx = results.findIndex(m => 'nonDummyRowIndex' in m && m['nonDummyRowIndex'] === row);
    if (idx >= 0) {
      result = results[idx];
    }

    return result;
  }

  // three-section-force.service から呼ばれる
  public getFsecJson(): object {
    return this.fsec;
  }

  // サーバーから受領した 解析結果を集計する
  public setFsecJson(
    jsonData: {},
    defList: any,
    combList: any,
    pickList: any
  ): void {
    const startTime = performance.now(); // 開始時間

    if (typeof Worker !== "undefined") {
      // Create a new

      this.worker1.onmessage = ({ data }) => {
        if (data.error === null) {
          console.log(
            "断面力の集計が終わりました",
            performance.now() - startTime
          );
          const fsec = data.fsec;
          const max_values = data.max_values;
          const value_ranges = data.value_ranges;
          // 組み合わせの集計処理を実行する
          this.comb.setFsecCombineJson(fsec, defList, combList, pickList);

          // 断面力テーブルの集計
          this.worker2.onmessage = ({ data }) => {
            if (data.error === null) {
              console.log(
                "断面力テーブルの集計が終わりました",
                performance.now() - startTime
              );
              this.columns = data.table;
              this.set_LL_columns(fsec, Object.keys(jsonData), max_values, value_ranges);
            } else {
              console.log("断面力テーブルの集計に失敗しました", data.error);
            }
          };
          // 連行荷重の子データは除外する
          const keys = Object.keys(fsec).filter((e) => !e.includes("."));
          for (const k of keys) {
            // 仮の着目点(バックエンドが追加した着目点)のデータ行をスキップした行インデックスをnonDummyRowIndexに設定しておく
            let nonDummyRowIndex = 0;
            fsec[k].forEach(r => {
              r['nonDummyRowIndex'] = 'dummy' in r && r['dummy'] === false ? nonDummyRowIndex++ : undefined;
            });

            this.fsec[k] = fsec[k];
          }
          this.worker2.postMessage({ fsec: this.fsec });
        } else {
          console.log("断面力の集計に失敗しました", data.error);
        }
      };

      this.worker1.postMessage({
        jsonData,
        member: this.member.member,
        dimension: this.helper.dimension,
      });
      const a = this.worker1_test({jsonData})
    } else {
      console.log("断面力の生成に失敗しました");
      // Web workers are not supported in this environment.
      // You should add a fallback so that your program still executes correctly.
    }
  }

  // 連行荷重の断面力を集計する
  private set_LL_columns(fsec: any, load_keys: string[], org_max_values: {}, org_value_ranges: {}) {
    this.LL_flg = new Array();

    const load_name = this.load.getLoadNameJson(0);
    const defList: any = {};
    const combList: any = {};
    const max_values: any = {};
    const value_ranges: any = {};

    let flg = false;

    for (const caseNo of Object.keys(load_name)) {
      const caseLoad: any = load_name[caseNo];
      if (caseLoad.symbol !== "LL") {
        this.LL_flg.push(false);
        max_values[caseNo] = org_max_values[caseNo];
        value_ranges[caseNo] = org_value_ranges[caseNo];
      } else {
        // 連行荷重の場合
        flg = true;
        this.LL_flg.push(true);

        const target_LL_Keys: string[] = load_keys.filter((e) => {
          return e.indexOf(caseNo + ".") === 0;
        });

        const caseList: string[] = [caseNo];
        const key0: string = target_LL_Keys[0];
        const tmp_max_values = (caseNo in org_max_values) ? org_max_values[caseNo] : org_max_values[key0];
        
        // value_rangesの初期値を設定（最初のLL_Keyの値を使用）
        const tmp_value_ranges = JSON.parse(JSON.stringify(org_value_ranges[key0]));

        for (const k of target_LL_Keys) {
          // ケースを追加
          caseList.push(k);

          // max_valuesを更新
          const target_max_values = org_max_values[k];
          const target_value_ranges = org_value_ranges[k];
          
          for (const kk of Object.keys(tmp_max_values)) {
            if (tmp_max_values[kk] < target_max_values[kk]) {
              tmp_max_values[kk] = target_max_values[kk];
            }
          }
          
          // value_rangesを更新
          for (const axis of ['x', 'y', 'z']) {
            // max_dの比較と更新
            if (tmp_value_ranges[axis].max_d < target_value_ranges[axis].max_d) {
              tmp_value_ranges[axis].max_d = target_value_ranges[axis].max_d;
              tmp_value_ranges[axis].max_d_m = target_value_ranges[axis].max_d_m;
            }
            
            // min_dの比較と更新
            if (tmp_value_ranges[axis].min_d > target_value_ranges[axis].min_d) {
              tmp_value_ranges[axis].min_d = target_value_ranges[axis].min_d;
              tmp_value_ranges[axis].min_d_m = target_value_ranges[axis].min_d_m;
            }
            
            // max_rの比較と更新
            if (tmp_value_ranges[axis].max_r < target_value_ranges[axis].max_r) {
              tmp_value_ranges[axis].max_r = target_value_ranges[axis].max_r;
              tmp_value_ranges[axis].max_r_m = target_value_ranges[axis].max_r_m;
            }
            
            // min_rの比較と更新
            if (tmp_value_ranges[axis].min_r > target_value_ranges[axis].min_r) {
              tmp_value_ranges[axis].min_r = target_value_ranges[axis].min_r;
              tmp_value_ranges[axis].min_r_m = target_value_ranges[axis].min_r_m;
            }
          }
        }
        defList[caseNo] = caseList;
        combList[caseNo] = [{ caseNo, coef: 1 }];
        max_values[caseNo] = tmp_max_values;
        value_ranges[caseNo] = tmp_value_ranges;
      }
    }

    if (flg === false) {
      this.three.setResultData(this.fsec, max_values, value_ranges);
      this.isCalculated = true;
      return; // 連行荷重がなければ ここまで
    }

    // combine のモジュールを利用して 連行荷重の組合せケースを集計する
    this.worker3.onmessage = ({ data }) => {
      console.log(data);
      const fsecCombine = data.fsecCombine;

      this.worker4.onmessage = ({ data }) => {
        const LL_columns = data.result;

        for (const k of Object.keys(LL_columns)) {
          this.columns[k] = LL_columns[k];
          this.fsec[k] = fsecCombine[k];
        }
        // 集計が終わったら three.js に通知
        this.three.setResultData(this.fsec, max_values, value_ranges);
        this.isCalculated = true;
      };

      this.worker4.postMessage({ fsecCombine });
    };

    this.worker3.postMessage({
      defList,
      combList,
      fsec: fsec,
      fsecKeys: this.comb.fsecKeys,
    });
  }
  
  worker1_test( data ) {

    const fsec = {};
    let error: any = null;

    const jsonData = data.jsonData;
    const member: any[] = data.member;

    // 同じidをもつ部材を探す
    const getMember = (memberNo: string) => {
      if (member === undefined) return { ni: null, nj: null };
      const m = member.find((columns) => {
        return columns.id === memberNo;
      })
      if (m === undefined) {
        return { ni: null, nj: null };
      }
      return m;
    }

    // 文字列string を数値にする
    const toNumber = (num: string) => {
      let result: number = null;
      try {
        const tmp: string = num.toString().trim();
        if (tmp.length > 0) {
          result = ((n: number) => isNaN(n) ? null : n)(+tmp);
        }
      } catch {
        result = null;
      }
      return result;
    };


    const max_values = {};
    const value_ranges = {};
    // 断面力の集計
    try {
      for (const caseNo of Object.keys(jsonData)) {
        const max_value = {
          fx: 0, fy: 0, fz: 0,
          mx: 0, my: 0, mz: 0
        }
        const value_range = {
          x: {
            max_d: Number.MIN_VALUE, min_d: Number.MAX_VALUE, max_d_m: '0', min_d_m: '0',
            max_r: Number.MIN_VALUE, min_r: Number.MAX_VALUE, max_r_m: '0', min_r_m: '0',
          },
          y: {
            max_d: Number.MIN_VALUE, min_d: Number.MAX_VALUE, max_d_m: '0', min_d_m: '0',
            max_r: Number.MIN_VALUE, min_r: Number.MAX_VALUE, max_r_m: '0', min_r_m: '0',
          },
          z: {
            max_d: Number.MIN_VALUE, min_d: Number.MAX_VALUE, max_d_m: '0', min_d_m: '0',
            max_r: Number.MIN_VALUE, min_r: Number.MAX_VALUE, max_r_m: '0', min_r_m: '0',
          },
        };

        const target = new Array();
        const caseData: {} = jsonData[caseNo];
        if (typeof (caseData) !== 'object') {
          continue;
        }
        if (!('fsec' in caseData)) {
          continue;
        }
        const json: {} = caseData['fsec'];
        let row = 0;
        let memberNo = '';
        for (const m of Object.keys(json)) {

          let noticePoint = 0.0;
          memberNo = m.replace('member', '');
          const js: {} = json[m];

          let result = {};
          const memb = getMember(memberNo);
          let ni: string = memb.ni;
          let nj = '';
          let counter = 1;
          const data_length: number = Object.keys(js).length;
          while (counter <= data_length) {
            const p = 'P' + counter.toString();
            if (!(p in js)) {
              break;
            }
            const item: {} = js[p];
            let fxi: number = toNumber(item['fxi']);
            let fyi: number = toNumber(item['fyi']);
            let fzi: number = toNumber(item['fzi']);
            let mxi: number = toNumber(item['mxi']);
            let myi: number = toNumber(item['myi']);
            let mzi: number = toNumber(item['mzi']);
            fxi = (fxi == null) ? 0 : Math.round(fxi * 100) / 100;
            fyi = (fyi == null) ? 0 : Math.round(fyi * 100) / 100;
            fzi = (fzi == null) ? 0 : Math.round(fzi * 100) / 100;
            mxi = (mxi == null) ? 0 : Math.round(mxi * 100) / 100;
            myi = (myi == null) ? 0 : Math.round(myi * 100) / 100;
            mzi = (mzi == null) ? 0 : Math.round(mzi * 100) / 100;

            result = {
              m: memberNo,
              n: ni,
              l: noticePoint,
              fx: fxi,
              fy: fyi,
              fz: fzi,
              mx: mxi,
              my: myi,
              mz: mzi
            };

            row++;
            result['row'] = row;

            const check_target = (target[target.length - 1] !== undefined) ? target[target.length - 1] :
              { l: '', m: '', n: '', row: '' };
            const check_result = result;
            check_result['row'] -= 1;
            if (check_result['l'] !== check_target.l || check_result['m'] !== check_target.m ||
              check_result['n'] !== check_target.n || check_result['row'] !== check_target.row) {
              target.push(result);
            }

            memberNo = '';
            ni = '';
            if (counter === data_length) {
              nj = memb.nj;
            }

            const l = toNumber(item['L']);
            let fxj: number = toNumber(item['fxj']);
            let fyj: number = toNumber(item['fyj']);
            let fzj: number = toNumber(item['fzj']);
            let mxj: number = toNumber(item['mxj']);
            let myj: number = toNumber(item['myj']);
            let mzj: number = toNumber(item['mzj']);
            noticePoint += Math.round(l * 1000) / 1000;
            fxj = (fxj == null) ? 0 : Math.round(fxj * 100) / 100;
            fyj = (fyj == null) ? 0 : Math.round(fyj * 100) / 100;
            fzj = (fzj == null) ? 0 : Math.round(fzj * 100) / 100;
            mxj = (mxj == null) ? 0 : Math.round(mxj * 100) / 100;
            myj = (myj == null) ? 0 : Math.round(myj * 100) / 100;
            mzj = (mzj == null) ? 0 : Math.round(mzj * 100) / 100;

            result = {
              m: '',
              n: nj,
              l: noticePoint,
              fx: fxj,
              fy: fyj,
              fz: fzj,
              mx: mxj,
              my: myj,
              mz: mzj
            };

            row++;
            result['row'] = row;
            target.push(result);
            counter++;

            max_value.fx = Math.max(Math.abs(fxi), Math.abs(fxj), max_value.fx);
            max_value.fy = Math.max(Math.abs(fyi), Math.abs(fyj), max_value.fy);
            max_value.fz = Math.max(Math.abs(fzi), Math.abs(fzj), max_value.fz);
            max_value.mx = Math.max(Math.abs(mxi), Math.abs(mxj), max_value.mx);
            max_value.my = Math.max(Math.abs(myi), Math.abs(myj), max_value.my);
            max_value.mz = Math.max(Math.abs(mzi), Math.abs(mzj), max_value.mz);

            // 断面力の最大最小とその部材番号を調べる
            // fx //
            value_range.x.max_d = Math.max(fxi, fxj, value_range.x.max_d);
            value_range.x.min_d = Math.min(fxi, fxj, value_range.x.min_d);
            if (value_range.x.max_d === fxi || value_range.x.max_d === fxj) { 
              value_range.x.max_d_m = m;}
            if (value_range.x.min_d === fxi || value_range.x.min_d === fxj) { 
              value_range.x.min_d_m = m;}
            // fy //
            value_range.y.max_d = Math.max(fyi, fyj, value_range.y.max_d);
            value_range.y.min_d = Math.min(fyi, fyj, value_range.y.min_d);
            if (value_range.y.max_d === fyi || value_range.y.max_d === fyj) { 
              value_range.y.max_d_m = m;}
            if (value_range.y.min_d === fyi || value_range.y.min_d === fyj) { 
              value_range.y.min_d_m = m;}
            // fz //
            value_range.z.max_d = Math.max(fzi, fzj, value_range.z.max_d);
            value_range.z.min_d = Math.min(fzi, fzj, value_range.z.min_d);
            if (value_range.z.max_d === fzi || value_range.z.max_d === fzj) { 
              value_range.z.max_d_m = m;}
            if (value_range.z.min_d === fzi || value_range.z.min_d === fzj) { 
              value_range.z.min_d_m = m;}
            // mx //
            value_range.x.max_r = Math.max(mxi, mxj, value_range.x.max_r);
            value_range.x.min_r = Math.min(mxi, mxj, value_range.x.min_r);
            if (value_range.x.max_r === mxi || value_range.x.max_r === mxj) { 
              value_range.x.max_r_m = m;}
            if (value_range.x.min_r === mxi || value_range.x.min_r === mxj) { 
              value_range.x.min_r_m = m;}
            // my //
            value_range.y.max_r = Math.max(myi, myj, value_range.y.max_r);
            value_range.y.min_r = Math.min(myi, myj, value_range.y.min_r);
            if (value_range.y.max_r === myi || value_range.y.max_r === myj) { 
              value_range.y.max_r_m = m;}
            if (value_range.y.min_r === myi || value_range.y.min_r === myj) { 
              value_range.y.min_r_m = m;}
            // mz //
            value_range.z.max_r = Math.max(mzi, mzj, value_range.z.max_r);
            value_range.z.min_r = Math.min(mzi, mzj, value_range.z.min_r);
            if (value_range.z.max_r === mzi || value_range.z.max_r === mzj) { 
              value_range.z.max_r_m = m;}
            if (value_range.z.min_r === mzi || value_range.z.min_r === mzj) { 
              value_range.z.min_r_m = m;}
          }
        }
        const key = caseNo.replace('Case', '');
        fsec[key] = target;
        max_values[key] = max_value;
        value_ranges[key] = value_range;
      }

    } catch (e) {
      error = e;
    }

    return { fsec, max_values, error };
  }
}
