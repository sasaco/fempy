import { Injectable } from '@angular/core';
import { ThreeDisplacementService } from '../../three/geometry/three-displacement.service';

@Injectable({
  providedIn: 'root'
})
export class ResultPickupDisgService {

  public disgPickup: any;
  public value_range: any;
  public isCalculated: boolean;
  private worker1: Worker;
  private worker2: Worker;
  private columns: any;

  constructor(private three: ThreeDisplacementService) {
    this.clear();
    this.isCalculated = false;
    this.worker1 = new Worker(new URL('./result-pickup-disg1.worker', import.meta.url), { name: 'pickup-disg1', type: 'module' });
    this.worker2 = new Worker(new URL('./result-pickup-disg2.worker', import.meta.url), { name: 'pickup-disg2', type: 'module' });
  }

  public clear(): void {
    this.disgPickup = {};
  }

  // three.js から呼ばれる
  public getFsecJson(): object {
    return this.disgPickup;
  }

  public getPickupDisgColumns(combNo: number, mode: string): any {
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

  public setDisgPickupJson(pickList: any, disgCombine: any): void {

    this.isCalculated = false;
    const startTime = performance.now(); // 開始時間
    if (typeof Worker !== 'undefined') {
      // Create a new
      this.worker1.onmessage = ({ data }) => {
        this.disgPickup = data.disgPickup;
        this.value_range = data.value_range;
        console.log('変位disg の ピックアップ PickUp 集計が終わりました', performance.now() - startTime);

        // 断面力テーブルの集計
        this.worker2.onmessage = ({ data }) => {
          console.log('変位disg の ピックアップ PickUp テーブル集計が終わりました', performance.now() - startTime);
          this.columns = data.result;
          this.isCalculated = true;
        };
        this.worker2.postMessage({ disgPickup: this.disgPickup });
        this.three.setCombPickResultData(this.value_range, 'pik_disg');

      };
      this.worker1.postMessage({ pickList, disgCombine});
      // const a = this.worker1_test({ pickList, disgCombine});
    } else {
      // Web workers are not supported in this environment.
      // You should add a fallback so that your program still executes correctly.
    }

  }

  private worker1_test (data) {
    
  const pickList = data.pickList;
  const disgCombine = data.disgCombine;
  const disgPickup = {};

  // pickupのループ
  for (const pickNo of Object.keys(pickList)) {
    const combines: any[] = pickList[pickNo];
    let tmp: {} = null;
    for (const combNo of combines) {
      const com = JSON.parse(
        JSON.stringify({
          temp: disgCombine[combNo]
        })
      ).temp;
      if (tmp == null) {
        tmp = com;
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
      }
    }
    if (tmp !== null) {
      disgPickup[pickNo] = tmp;
    }
  }

  const value_range = {};
  // CombineNoごとの最大最小を探す
  for (const combNo of Object.keys(disgPickup)) {
    const caseData = disgPickup[combNo];
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

    // const values2 = {
    //   max_d : Math.max( values['dx_max'].max, values['dy_max'].max, values['dz_max'].max ),
    //   min_d : Math.min( values['dx_min'].max, values['dy_min'].max, values['dz_min'].max ),
    //   max_r : Math.max( values['rx_max'].max, values['ry_max'].max, values['rz_max'].max ),
    //   min_r : Math.min( values['rx_min'].max, values['ry_min'].max, values['rz_min'].max ),
    // };
    // // dの最大値の部材番号を探す
    // if (values2.max_d === values['dx_max'].max) {
    //   values2['max_d_m'] = values['dx_max'].max_m;
    // } else if (values2.max_d === values['dy_max'].max) {
    //   values2['max_d_m'] = values['dy_max'].max_m;
    // } else if (values2.max_d === values['dz_max'].max) {
    //   values2['max_d_m'] = values['dz_max'].max_m;
    // } 
    // // dの最小値の部材番号を探す
    // if (values2.max_d === values['dx_max'].max) {
    //   values2['min_d_m'] = values['dx_max'].max_m;
    // } else if (values2.max_d === values['dy_max'].max) {
    //   values2['min_d_m'] = values['dy_max'].max_m;
    // } else if (values2.max_d === values['dz_max'].max) {
    //   values2['min_d_m'] = values['dz_max'].max_m;
    // } 
    // // rの最大値の部材番号を探す
    // if (values2.max_r === values['rx_max'].max) {
    //   values2['max_r_m'] = values['rx_max'].max_m;
    // } else if (values2.max_r === values['ry_max'].max) {
    //   values2['max_r_m'] = values['ry_max'].max_m;
    // } else if (values2.max_r === values['rz_max'].max) {
    //   values2['max_r_m'] = values['rz_max'].max_m;
    // }
    // // rの最小値の部材番号を探す
    // if (values2.max_r === values['rx_max'].max) {
    //   values2['min_r_m'] = values['rx_max'].max_m;
    // } else if (values2.max_r === values['ry_max'].max) {
    //   values2['min_r_m'] = values['ry_max'].max_m;
    // } else if (values2.max_r === values['rz_max'].max) {
    //   values2['min_r_m'] = values['rz_max'].max_m;
    // }
    value_range[combNo] = values2;
  }

  }

}
