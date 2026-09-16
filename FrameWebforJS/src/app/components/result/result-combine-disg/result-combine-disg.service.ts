import { Injectable } from '@angular/core';
import { ResultDisgService } from '../result-disg/result-disg.service';
import { ResultPickupDisgService } from '../result-pickup-disg/result-pickup-disg.service';
import { ThreeDisplacementService } from '../../three/geometry/three-displacement.service';
import { DataHelperModule } from '../../../providers/data-helper.module';
import { data } from 'jquery';

@Injectable({
  providedIn: 'root'
})
export class ResultCombineDisgService {

  public disgCombine: any[];
  public value_range: any;
  public isCalculated: boolean;
  private worker1: Worker;
  private worker2: Worker;
  public disgKeys3D = [
    "dx_max",
    "dx_min",
    "dy_max",
    "dy_min",
    "dz_max",
    "dz_min",
    "rx_max",
    "rx_min",
    "ry_max",
    "ry_min",
    "rz_max",
    "rz_min",
  ];
  public disgKeys2D = [
    "dx_max",
    "dx_min",
    "dy_max",
    "dy_min",
    "rz_max",
    "rz_min",
  ];
  public titles3D = [
    "x方向の移動量 最大",
    "x方向の移動量 最小",
    "y方向の移動量 最大",
    "y方向の移動量 最小",
    "z方向の移動量 最大",
    "Z方向の移動量 最小",
    "x軸回りの回転角 最大",
    "x軸回りの回転角 最小",
    "y軸回りの回転角 最大",
    "y軸回りの回転角 最小",
    "z軸回りの回転角 最大",
    "Z軸回りの回転角 最小",
  ];
  public titles2D = [
    "x方向の移動量 最大",
    "x方向の移動量 最小",
    "y方向の移動量 最大",
    "y方向の移動量 最小",
    "z軸回りの回転角 最大",
    "Z軸回りの回転角 最小",
  ];
  public disgKeys = this.disgKeys3D || this.disgKeys2D;
  public titles = this.titles3D || this.titles2D

  public column3Ds: any[] = [
    { title: "result.result-combine-disg.No", id: "id", format: "" },
    { title: "result.result-combine-disg.x_movement", id: "dx", format: "#.0000" },
    { title: "result.result-combine-disg.y_movement", id: "dy", format: "#.0000" },
    { title: "result.result-combine-disg.z_movement", id: "dz", format: "#.0000" },
    { title: "result.result-combine-disg.x_rotation", id: "rx", format: "#.0000" },
    { title: "result.result-combine-disg.y_rotation", id: "ry", format: "#.0000" },
    { title: "result.result-combine-disg.z_rotation", id: "rz", format: "#.0000" },
    { title: "result.result-combine-disg.comb", id: "case", format: "", width: 40 },
  ];

  public column2Ds: any[] = [
    { title: "result.result-combine-disg.No", id: "id", format: "" },
    { title: "result.result-combine-disg.x_movement", id: "dx", format: "#.0000" },
    { title: "result.result-combine-disg.y_movement", id: "dy", format: "#.0000" },
    { title: "result.result-combine-disg.z_rotation", id: "rz", format: "#.0000" },
    { title: "result.result-combine-disg.comb", id: "case", format: "", width: 40 },
  ];

  private columns: any;

  constructor(private pickdisg: ResultPickupDisgService,
              private three: ThreeDisplacementService,
              private helper: DataHelperModule,) {
    this.clear();
    this.isCalculated = false;
    this.worker1 = new Worker(new URL('./result-combine-disg1.worker', import.meta.url), { name: 'combine-disg1', type: 'module' });
    this.worker2 = new Worker(new URL('./result-combine-disg2.worker', import.meta.url), { name: 'combine-disg2', type: 'module' });
  }

  public clear(): void {
    this.disgCombine = [];
  }

  // three.js で必要
  public getDisgJson(): object {
    return this.disgCombine;
  }

  public getCombineDisgColumns(combNo: number, mode: string): any {
    if(!(combNo in this.columns))
      return null;
    if(!(mode in this.columns[combNo]))
      return null;
    return this.columns[combNo][mode];
  }

  public getDataColumns(currentPage:number, row: number, mode: string):any{
    let results: any = this.columns[currentPage];
    if(results == undefined){
      return {
        id : "",
        dx : "", 
        dy : "", 
        dz : "", 
        rx : "", 
        ry : "", 
        rz : "",
        case: ""
      }
    };
    let result = undefined;
    let modes = results[mode] != undefined ? results[mode] : undefined;
    if(modes != undefined){
      result = modes[row];
    }
    // 対象データが無かった時に処理
    if (result === undefined) {
      result = {
        id : "",
        dx : "", 
        dy : "", 
        dz : "", 
        rx : "", 
        ry : "", 
        rz : "",
        case: ""
      };
    }
    return result;
  }

  public setDisgCombineJson(disg: any, defList: any, combList: any, pickList: any): void {

    this.disgKeys = (this.helper.dimension === 3) ? this.disgKeys3D : this.disgKeys2D ;
    this.titles = (this.helper.dimension === 3) ? this.titles3D : this.titles2D ;

    const startTime = performance.now(); // 開始時間
    if (typeof Worker !== 'undefined') {
      // Create a new
      this.worker1.onmessage = ({ data }) => {
        this.disgCombine = data.disgCombine;
        this.value_range = data.value_range;
        console.log('変位disg の 組み合わせ Combine 集計が終わりました', performance.now() - startTime);

        // ピックアップの集計処理を実行する
        this.pickdisg.setDisgPickupJson(pickList, this.disgCombine);

        // 断面力テーブルの集計
        this.worker2.onmessage = ({ data }) => {
          console.log('変位disg の 組み合わせ Combine テーブル集計が終わりました', performance.now() - startTime);
          this.columns = data.result;
          this.isCalculated = true;
        };
        this.worker2.postMessage({ disgCombine: this.disgCombine });
        this.three.setCombPickResultData(this.value_range, 'comb_disg');

      };
      // const a = this.worker1_test({ defList, combList, disg, disgKeys: this.disgKeys } );
      this.worker1.postMessage({ defList, combList, disg, disgKeys: this.disgKeys });
    } else {
      // Web workers are not supported in this environment.
      // You should add a fallback so that your program still executes correctly.
    }

  }

  worker1_test( data) {
    const defList = data.defList;
    const combList = data.combList;
    const disg = data.disg;
    const disgKeys = data.disgKeys;

    // defineのループ
    const disgDefine = {};
    for(const defNo of Object.keys(defList)) {
      const temp = {};
      //
      for(const caseInfo of defList[defNo]) {
        let baseNo: string = '';
        if(typeof caseInfo === "number"){
          baseNo = Math.abs(caseInfo).toString();
        } else {
          baseNo = caseInfo;
        }
        const coef: number = Math.sign(caseInfo);

        if (!(baseNo in disg)) {
          if(caseInfo === 0 ){
            // 値が全て0 の case 0 という架空のケースを用意する
            // 値は coef=0 であるため 0 となる
            disg['0'] = Object.values(disg)[0];
          } else {
            continue;
          }
        }

        // カレントケースを集計する
        for (const key of disgKeys) {
          // 節点番号のループ
          const obj = {};
          for (const d of disg[baseNo]) {
            obj[d.id] = {
              dx: coef * d.dx,
              dy: coef * d.dy,
              dz: coef * d.dz,
              rx: coef * d.rx,
              ry: coef * d.ry,
              rz: coef * d.rz,
              case: caseInfo,
            };
          }
          if (key in temp) {
            // 大小を比較する
            const kk = key.split('_');
            const k1 = kk[0]; // dx, dy, dz, rx, ry, rz
            const k2 = kk[1]; // max, min
            for (const nodeNo of Object.keys(temp[key])) {
              if(k2==='max'){
                if(temp[key][nodeNo][k1] < obj[nodeNo][k1]){
                  temp[key][nodeNo] = obj[nodeNo];
                }
              } else if (k2==='min'){
                if(temp[key][nodeNo][k1] > obj[nodeNo][k1]){
                  temp[key][nodeNo] = obj[nodeNo];
                }
              }
            }

          } else {
            temp[key] = obj;
          }
        }
      }
      disgDefine[defNo] = temp;
    }


    // combineのループ
    const disgCombine = {};
    for (const combNo of Object.keys(combList)) {
      const temp = {};
      //
      for (const caseInfo of combList[combNo]) {
        const caseNo = Number(caseInfo.caseNo);
        const defNo: string = caseInfo.caseNo.toString();
        const coef: number = caseInfo.coef;

        if (!(defNo in disgDefine)) {
          continue;
        }
        if (coef === 0) {
          continue;
        }

        const disgs = disgDefine[defNo];
        if(Object.keys(disgs).length < 1) continue;

        // カレントケースを集計する
        const c2 = Math.abs(caseNo).toString().trim();
        for (const key of disgKeys){
          // 節点番号のループ
          const obj = {};
          for (const nodeNo of Object.keys(disgs[key])) {
            const d = disgs[key][nodeNo];
            const c1 = Math.sign(coef) < 0 ? -1 : 1 * d.case;
            let caseStr = '';
            if (c1 !== 0){
              caseStr = (c1 < 0 ? "-" : "+") + c2;
            }
            obj[nodeNo] = {
              dx: coef * d.dx,
              dy: coef * d.dy,
              dz: coef * d.dz,
              rx: coef * d.rx,
              ry: coef * d.ry,
              rz: coef * d.rz,
              case: caseStr
            };
          }

          if (key in temp) {
            for (const nodeNo of Object.keys(disgs[key])) {
                for(const k of Object.keys(obj[nodeNo])){
                  temp[key][nodeNo][k] += obj[nodeNo][k];
                }
                temp[key][nodeNo]['comb']= combNo;
              }
          } else {
            for (const nodeNo of Object.keys(obj)) {
              obj[nodeNo]['comb']= combNo;
            }
            temp[key] = obj;
          }
        }
      }
      disgCombine[combNo] = temp;
    }

    const value_range = {};
    // CombineNoごとの最大最小を探す
    for (const combNo of Object.keys(disgCombine)) {
      const caseData = disgCombine[combNo];
      const key_list = Object.keys(caseData);
      const values_d = {};
      const values_r = {};
      // dx～rzの最大最小をそれぞれ探す
      for (const key of key_list) {
        const datas = caseData[key];
        let key2: string;
        let is_d = false;
        if (key.includes('dx')) {
          key2 = 'dx';
          is_d = true;
        } else if (key.includes('dy')) {
          key2 = 'dy';
          is_d = true;
        } else if (key.includes('dz')) {
          key2 = 'dz';
          is_d = true;
        } else if (key.includes('rx')) {
          key2 = 'rx';
        } else if (key.includes('ry')) {
          key2 = 'ry';
        } else if (key.includes('rz')) {
          key2 = 'rz';
        }
        let targetValue = (key.includes('max')) ? Number.MIN_VALUE : Number.MAX_VALUE;
        let targetValue_m = '0';
        if (key.includes('max')) {
          for (const row of Object.keys(datas)) {
            const data = datas[row][key2];
            if (data >= targetValue) {
              targetValue = data;
              targetValue_m = row;
            }
          }
        } else {
          for (const row of Object.keys(datas)) {
            const data = datas[row][key2];
            if (data <= targetValue) {
              targetValue = data;
              targetValue_m = row;
            }
          }
        }
        if (Math.abs(targetValue) === Number.MAX_VALUE) {
          continue;
        }
        if(is_d){
          values_d[key] = {max: targetValue, max_m: targetValue_m};
        } else{
          values_r[key] = {max: targetValue, max_m: targetValue_m};
        }
      }
      if (Object.keys(values_d).length === 0) {
        continue;
      }
      const values2 = {
        max_d : Number.MIN_VALUE, max_d_m: 0,
        min_d : Number.MAX_VALUE, min_d_m: 0,
        max_r : Number.MIN_VALUE, max_r_m: 0,
        min_r : Number.MAX_VALUE, min_r_m: 0
      };
      for(const key of Object.keys(values_d)){
        const value = values_d[key];
        if(value.max > values2.max_d){
          values2.max_d = value.max;
          values2.max_d_m = value.max_m;
        } else if(value.max < values2.min_d){
          values2.min_d = value.max;
          values2.min_d_m = value.max_m;
        }
      }
      for(const key of Object.keys(values_r)){
        const value = values_r[key];
        if(value.max > values2.max_r){
          values2.max_r = value.max;
          values2.max_r_m = value.max_m;
        } else if(value.max < values2.min_r){
          values2.min_r = value.max;
          values2.min_r_m = value.max_m;
        }
      }

      value_range[combNo] = values2;
    }

    return{ disgCombine, value_range };
  }



}
