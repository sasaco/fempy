import { Injectable } from "@angular/core";
import { InputLoadService } from "../../input/input-load/input-load.service";
import { PrintCustomService } from "../../print/custom/print-custom.service";
import { ThreeDisplacementService } from "../../three/geometry/three-displacement.service";
import { ResultCombineDisgService } from "../result-combine-disg/result-combine-disg.service";

@Injectable({
  providedIn: "root",
})
export class ResultDisgService {
  public isCalculated: boolean;
  public disg: any[];
  private worker1: Worker;
  private worker2: Worker;
  private worker3: Worker;
  private worker4: Worker;
  private columns: any; // 表示用

  public column3Ds: any[] = [
    { title: "result.result-disg.No", id: "id", format: "" },
    { title: "result.result-disg.x_movement", id: "dx", format:'#.0000' },
    { title: "result.result-disg.y_movement", id: "dy", format:'#.0000' },
    { title: "result.result-disg.z_movement", id: "dz", format:'#.0000' },
    { title: "result.result-disg.x_rotation", id: "rx", format:'#.0000' },
    { title: "result.result-disg.y_rotation", id: "ry", format:'#.0000' },
    { title: "result.result-disg.z_rotation", id: "rz", format:'#.0000' },
  ];
  public column2Ds: any[] = [
    { title: "result.result-disg.No", id: "id", format:'' },
    { title: "result.result-disg.x_movement", id: "dx", format:'#.0000' },
    { title: "result.result-disg.y_movement", id: "dy", format:'#.0000' },
    { title: "result.result-disg.z_rotation", id: "rz", format:'#.0000' },
  ];

  public LL_flg = [];

  constructor(
    private comb: ResultCombineDisgService,
    private load: InputLoadService,
    private three: ThreeDisplacementService,
    public printCustomService: PrintCustomService
  ) {
    this.clear();
    this.worker1 = new Worker(
      new URL("./result-disg1.worker", import.meta.url),
      { name: "result-disg1", type: "module" }
    );
    this.worker2 = new Worker(
      new URL("./result-disg2.worker", import.meta.url),
      { name: "result-disg2", type: "module" }
    );
    // 連行荷重の集計
    this.worker3 = new Worker(
      new URL(
        "../result-combine-disg/result-combine-disg1.worker",
        import.meta.url
      ),
      { name: "LL-disg1", type: "module" }
    );
    this.worker4 = new Worker(
      new URL(
        "../result-combine-disg/result-combine-disg2.worker",
        import.meta.url
      ),
      { name: "LL-disg2", type: "module" }
    );
    this.LL_flg = new Array();
  }

  public clear(): void {
    this.disg = [];
    this.isCalculated = false;
  }

  public getDisgColumns(typNo: number, mode: string = null): any {
    const key: string = typNo.toString();
    if (!(key in this.columns)) {
      return new Array();
    }
    const col = this.columns[key];
    if (mode === null) {
      return col;
    } else {
      if (mode in col) {
        return col[mode]; // 連行荷重の時は combine のようになる
      }
    }

    return new Array();
  }

  public getDataColumns(currentPage:number, row: number, mode: string = null):any{

    let result = {
      id : "",
      dx : "", 
      dy : "", 
      dz : "", 
      rx : "", 
      ry : "", 
      rz : "",
      comb: "",
      case: ""
    };

    let results: any = this.disg[currentPage];
    if(results == undefined){
      return result;
    };

    if (mode in results) {
      let modes = results[mode] != undefined ? results[mode] : undefined;
      if (modes != undefined) {
        const keys = Object.keys(modes);
        const key = keys[row];
        if(modes[key] != undefined){
          result = modes[key];
          result["id"] = key;
        }
      }
    } else if(results[row] != undefined) {
      result = results[row];
    }

    return result;
  }


  // three-section-force.service から呼ばれる
  public getDisgJson(): object {
    return this.disg;
  }

  public setDisgJson(
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
            "変位量の集計が終わりました",
            performance.now() - startTime
          );
          const disg = data.disg;
          const max_values = data.max_value;
          const value_range = {};
          value_range['disg'] = data.value_range;

          // 組み合わせの集計処理を実行する
          this.comb.setDisgCombineJson(disg, defList, combList, pickList);
          value_range['comb_disg'] = this.comb.value_range;
          value_range['pik_disg'] = this.comb.value_range;

          // 変位量テーブルの集計
          this.worker2.onmessage = ({ data }) => {
            if (data.error === null) {
              console.log(
                "変位量テーブルの集計が終わりました",
                performance.now() - startTime
              );
              this.columns = data.table;
              this.set_LL_columns(disg, Object.keys(jsonData), max_values, value_range['disg']);
            } else {
              console.log("変位量テーブルの集計に失敗しました", data.error);
            }
          };
          // 連行荷重の子データは除外する
          const keys = Object.keys(disg).filter((e) => !e.includes("."));
          for (const k of keys) {
            this.disg[k] = disg[k];
          }
          this.worker2.postMessage({ disg: this.disg });
        } else {
          console.log("変位量の集計に失敗しました", data.error);
        }
      };
      this.worker1.postMessage({ jsonData });
      // const a = this.woker1_test({ jsonData })
    } else {
      console.log("変位量の生成に失敗しました");
      // Web workers are not supported in this environment.
      // You should add a fallback so that your program still executes correctly.
    }
  }

  // 連行荷重の断面力を集計する
  private set_LL_columns(disg: any, load_keys: string[], org_max_values: {}, org_value_range: {}) {
    this.LL_flg = new Array();

    const load_name = this.load.getLoadNameJson(0);
    const defList: any = {};
    const combList: any = {};
    const max_values: any = {};
    const value_range: any = {};

    let flg = false;

    for (const caseNo of Object.keys(load_name)) {
      const caseLoad: any = load_name[caseNo];
      if (caseLoad.symbol !== "LL") {
        this.LL_flg.push(false);
        max_values[caseNo] = org_max_values[caseNo];
        value_range[caseNo] = org_value_range[caseNo];
      } else {
        // 連行荷重の場合
        flg = true;
        this.LL_flg.push(true);

        const target_LL_Keys: string[] = load_keys.filter((e) => {
          return e.indexOf(caseNo + ".") === 0;
        });
        const caseList: string[] = [caseNo];
        const key0: string = target_LL_Keys[0];
        let tmp_max_values = (caseNo in org_max_values) ? org_max_values[caseNo] : org_max_values[key0];

        for (const k of target_LL_Keys) {
          // ケースを追加
          caseList.push(k);

          // max_valuesを更新
          const target_max_values = Math.abs(org_max_values[k]);
          if (tmp_max_values < target_max_values) {
            tmp_max_values = target_max_values;
          }
        }
        defList[caseNo] = caseList;
        combList[caseNo] = [{ caseNo, coef: 1 }];
        max_values[caseNo] = tmp_max_values;
      }
    }

    // 集計が終わったら three.js に通知
    this.three.setResultData(disg, max_values, value_range, 'disg');
    this.printCustomService.LL_flg = this.LL_flg;
    this.printCustomService.LL();
    if (flg === false) {
      this.isCalculated = true;
      return; // 連行荷重がなければ ここまで
    }

    // combine のモジュールを利用して 連行荷重の組合せケースを集計する
    this.worker3.onmessage = ({ data }) => {
      console.log(data);
      const disgCombine = data.disgCombine;

      this.worker4.onmessage = ({ data }) => {
        const LL_columns = data.result;

        for (const k of Object.keys(LL_columns)) {
          this.columns[k] = LL_columns[k];
          this.disg[k] = disgCombine[k];
        }
        this.isCalculated = true;
      };
      this.worker4.postMessage({ disgCombine });
    };

    this.worker3.postMessage({
      defList,
      combList,
      disg: disg,
      disgKeys: this.comb.disgKeys,
    });
  }


}
