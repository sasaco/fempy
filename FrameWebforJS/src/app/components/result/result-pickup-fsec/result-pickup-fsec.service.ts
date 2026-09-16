import { Injectable } from "@angular/core";
import { ThreeSectionForceService } from "../../three/geometry/three-section-force/three-section-force.service";
import { log } from "console";

@Injectable({
  providedIn: "root",
})
export class ResultPickupFsecService {
  public fsecPickup: any;
  public value_range: any;
  public isCalculated: boolean;
  private worker1: Worker;
  private worker2: Worker;
  public fsecKeys3D = [
    "fx_max",
    "fx_min",
    "fy_max",
    "fy_min",
    "fz_max",
    "fz_min",
    "mx_max",
    "mx_min",
    "my_max",
    "my_min",
    "mz_max",
    "mz_min",
  ];
  public fsecKeys2D = [
    "fx_max",
    "fx_min",
    "fy_max",
    "fy_min",
    "mz_max",
    "mz_min",
  ];
  public titles3D = [
    "軸方向力 最大",
    "軸方向力 最小",
    "y方向のせん断力 最大",
    "y方向のせん断力 最小",
    "z方向のせん断力 最大",
    "z方向のせん断力 最小",
    "ねじりモーメント 最大",
    "ねじりモーメント 最小",
    "y軸回りの曲げモーメント 最大",
    "y軸回りの曲げモーメント力 最小",
    "z軸回りの曲げモーメント 最大",
    "z軸回りの曲げモーメント 最小",
  ];
  public titles2D = [
    "軸方向力 最大",
    "軸方向力 最小",
    "y方向のせん断力 最大",
    "y方向のせん断力 最小",
    "z軸回りの曲げモーメント 最大",
    "z軸回りの曲げモーメント 最小",
  ];
  public fsecKeys = this.fsecKeys3D || this.fsecKeys2D;
  public titles = this.titles3D || this.titles2D;
  private columns: any;

  constructor(private three: ThreeSectionForceService) {
    this.clear();
    this.isCalculated = false;
    this.worker1 = new Worker(
      new URL("./result-pickup-fsec1.worker", import.meta.url),
      { name: "pickup-fsec1", type: "module" }
    );
    this.worker2 = new Worker(
      new URL("./result-pickup-fsec2.worker", import.meta.url),
      { name: "pickup-fsec2", type: "module" }
    );
  }

  public clear(): void {
    this.fsecPickup = {};
  }

  // three.js から呼ばれる
  public getFsecJson(): object {
    return this.fsecPickup;
  }

  public getPickupFsecColumns(combNo: number, mode: string): any {
    if (!(combNo in this.columns))
      return null;
    if (!(mode in this.columns[combNo]))
      return null;
    return this.columns[combNo][mode];
  }

  public getDataColumns(currentPage: number, row: number, mode: string): any {
    let results: any = this.columns[currentPage];
    if (results == undefined) {
      return {
        m: "",
        n: "",
        l: "",
        fx: "",
        fy: "",
        fz: "",
        mx: "",
        my: "",
        mz: "",
        case: "",
      };
    }
    let result = undefined;
    let modes = results[mode] != undefined ? results[mode] : undefined;
    if (modes != undefined) {
      result = modes[row];
    }
    // 対象データが無かった時に処理
    if (result === undefined) {
      result = {
        m: "",
        n: "",
        l: "",
        fx: "",
        fy: "",
        fz: "",
        mx: "",
        my: "",
        mz: "",
        case: "",
      };
    }
    return result;
  }

  public setFsecPickupJson(pickList: any, fsecCombine: any): void {
    this.isCalculated = false;
    const startTime = performance.now(); // 開始時間
    if (typeof Worker !== "undefined") {
      // Create a new
      this.worker1.onmessage = ({ data }) => {
        console.log(
          "断面力fsec の ピックアップ PickUp 集計が終わりました",
          performance.now() - startTime
        );
        this.fsecPickup = data.fsecPickup;
        const max_values = data.max_values;
        this.value_range = data.value_range;

        // 断面力テーブルの集計
        this.worker2.onmessage = ({ data }) => {
          console.log(
            "断面fsec の ピックアップ PickUp テーブル集計が終わりました",
            performance.now() - startTime
          );
          this.columns = data.result;
          this.isCalculated = true;
        };
        // this.columns = this.worker2_test(this.fsecPickup );
        this.worker2.postMessage({ fsecPickup: this.fsecPickup });
        this.three.setPickupResultData(this.fsecPickup, max_values, this.value_range);
      };
      this.worker1_test({ pickList, fsecCombine });
      this.worker1.postMessage({ pickList, fsecCombine });
    } else {
      // Web workers are not supported in this environment.
      // You should add a fallback so that your program still executes correctly.
    }
  }

  private worker1_test(data) {

    console.log("Start wk");
    const pickList = data.pickList;
    const fsecCombine = data.fsecCombine;
    const fsecPickup = {};
    const max_values = {};

    // pickupのループ
    for (const pickNo of Object.keys(pickList)) {
      const max_value = {
        fx: 0, fy: 0, fz: 0,
        mx: 0, my: 0, mz: 0
      }

      const combines: any[] = pickList[pickNo];
      let tmp: {} = null;
      for (const combNo of combines) {
        const com = JSON.parse(
          JSON.stringify({
            temp: fsecCombine[combNo]
          })
        ).temp;

        if (com == null || com == undefined) {
          console.log('Combine' + combNo + 'がない');
          continue;
        }

        if (tmp == null) {
          tmp = com;
          for (const k of Object.keys(com)) { // 最大値を 集計する
            for (const value of tmp[k]) {
              max_value.fx = Math.max(Math.abs(value.fx), max_value.fx);
              max_value.fy = Math.max(Math.abs(value.fy), max_value.fy);
              max_value.fz = Math.max(Math.abs(value.fz), max_value.fz);
              max_value.mx = Math.max(Math.abs(value.mx), max_value.mx);
              max_value.my = Math.max(Math.abs(value.my), max_value.my);
              max_value.mz = Math.max(Math.abs(value.mz), max_value.mz);
            }
          }
          continue;
        }

        for (const k of Object.keys(com)) {
          const key = k.split('_');
          const target = com[k];
          const comparison = tmp[k];
          for (const id of Object.keys(comparison)) {
            const a = comparison[id];
            if (!(id in target)) {
              continue;
            }
            const b = target[id];
            if (key[1] === 'max') {
              if (b[key[0]] > a[key[0]]) {
                tmp[k][id] = com[k][id];
              }
            } else {
              if (b[key[0]] < a[key[0]]) {
                tmp[k][id] = com[k][id];
              }
            }
          }

          // 最大値を 集計する
          for (const value of tmp[k]) {
            max_value.fx = Math.max(Math.abs(value.fx), max_value.fx);
            max_value.fy = Math.max(Math.abs(value.fy), max_value.fy);
            max_value.fz = Math.max(Math.abs(value.fz), max_value.fz);
            max_value.mx = Math.max(Math.abs(value.mx), max_value.mx);
            max_value.my = Math.max(Math.abs(value.my), max_value.my);
            max_value.mz = Math.max(Math.abs(value.mz), max_value.mz);
          }
        }

      }
      fsecPickup[pickNo] = tmp;
      max_values[pickNo] = max_value;
      tmp = null;
    }

    const value_range = {};
    // CombineNoごとの最大最小を探す
    for (const combNo of Object.keys(fsecPickup)) {
      const caseData = fsecPickup[combNo];
      if (caseData == null || caseData == undefined) {
        console.log('CaseData ' + combNo + 'がない');
        continue;
      }
      const key_list = Object.keys(caseData);
      const values = {};
      // dx～rzの最大最小をそれぞれ探す
      for (const key of key_list) {
        const datas = caseData[key];
       /* */  let key2: string;
        if (key.includes('fx')) {
          key2 = 'fx';
        } else if (key.includes('fy')) {
          key2 = 'fy';
        } else if (key.includes('fz')) {
          key2 = 'fz';
        } else if (key.includes('mx')) {
          key2 = 'mx';
        } else if (key.includes('my')) {
          key2 = 'my';
        } else if (key.includes('mz')) {
          key2 = 'mz';
        }
        let targetValue = (key.includes('max')) ? Number.MIN_VALUE : Number.MAX_VALUE;
        let targetValue_m = '0';
        if (key.includes('max')) {  // 最大値を探す
          //for (const row of Object.keys(datas)) {
          for (let num = 0; num < datas.length; num++) {
            const row = num.toString();
            const data = datas[row][key2];
            if (data >= targetValue) {
              targetValue = data;
              // memberNoがないとき(着目点が最大)の分岐
              if (datas[row].m === '') {
                let m_no: string
                for (let num2 = num - 1; num2 > 0; num2--) {
                  const row2 = num2.toString();
                  if (datas[row2].m !== '') {
                    m_no = datas[row2].m;
                    break;
                  }
                }
                targetValue_m = m_no;
              } else {
                targetValue_m = datas[row].m;
              }
            }
          }
        } else {  // 最小値を探す
          // for (const row of Object.keys(datas)) {
          for (let num = 0; num < datas.length; num++) {
            const row = num.toString();
            const data = datas[row][key2];
            if (data <= targetValue) {
              targetValue = data;
              // memberNoがないとき(着目点が最小)の分岐
              if (datas[row].m === '') {
                let m_no: string
                for (let num2 = num - 1; num2 > 0; num2--) {
                  const row2 = num2.toString();
                  if (datas[row2].m !== '') {
                    m_no = datas[row2].m;
                    break;
                  }
                }
                targetValue_m = m_no;
              } else {
                targetValue_m = datas[row].m;
              }
            }
          }
        }
        if (Math.abs(targetValue) === Number.MAX_VALUE) {
          continue;
        }
        values[key] = { max: targetValue, max_m: targetValue_m };
      }
      if (Object.keys(values).length === 0) {
        continue;
      }

      const values2 = {
        x: {
          max_d: 0, max_d_m: 0,
          min_d: 0, min_d_m: 0,
          max_r: 0, max_r_m: 0,
          min_r: 0, min_r_m: 0,
        },
        y: {
          max_d: 0, max_d_m: 0,
          min_d: 0, min_d_m: 0,
          max_r: 0, max_r_m: 0,
          min_r: 0, min_r_m: 0,
        },
        z: {
          max_d: 0, max_d_m: 0,
          min_d: 0, min_d_m: 0,
          max_r: 0, max_r_m: 0,
          min_r: 0, min_r_m: 0,
        }
      }
      for (const key of Object.keys(values2)) {
        let kf = 'f' + key + '_max';
        if (kf in values) {
          values2[key].max_d = values[kf].max;
          values2[key].max_d_m = values[kf].max_m
        }
        kf = 'f' + key + '_min';
        if (kf in values) {
          values2[key].min_d = values[kf].max;
          values2[key].min_d_m = values[kf].max_m
        }
        let km = 'm' + key + '_max';
        if (km in values) {
          values2[key].max_r = values[km].max;
          values2[key].max_r_m = values[km].max_m
        }
        km = 'm' + key + '_min';
        if (km in values) {
          values2[key].min_r = values[km].max;
          values2[key].min_r_m = values[km].max_m
        }
      }

      value_range[combNo] = values2;
    }

    return { fsecPickup, max_values, value_range };
  }
}
