import { Injectable } from '@angular/core';
import { ThreeReactService } from '../../three/geometry/three-react.service';

@Injectable({
  providedIn: 'root'
})
export class ResultPickupReacService {

  public reacPickup: any;
  public value_range: any;
  public isCalculated : boolean;
  private worker1: Worker;
  private worker2: Worker;
  private columns: any;

  constructor(private three: ThreeReactService) {
    this.clear();
    this.isCalculated  = false;
    this.worker1 = new Worker(new URL('./result-pickup-reac1.worker', import.meta.url), { name: 'pickup-reac1', type: 'module' });
    this.worker2 = new Worker(new URL('./result-pickup-reac2.worker', import.meta.url), { name: 'pickup-reac2', type: 'module' });
  }

  public clear(): void {
    this.reacPickup = {};
  }

  // three.js で必要
  public getReacJson(): object {
    return this.reacPickup;
  }

  public getPickupReacColumns(combNo: number, mode: string): any {
    if(!(combNo in this.columns))
      return null;
    if(!(mode in this.columns[combNo]))
      return null;
    return this.columns[combNo][mode];
  }

  public getDataColumns(currentPage: number, row: number, mode: string): any {
    let results: any = this.columns[currentPage];
    if (results == undefined) {
      return {
        id: "",
        tx: "",
        ty: "",
        tz: "",
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
        id: "",
        tx: "",
        ty: "",
        tz: "",
        mx: "",
        my: "",
        mz: "",
        case: "",
      };
    }
    return result;
  }

  public setReacPickupJson(pickList: any, reacCombine: any): void {

    this.isCalculated = false;
    const startTime = performance.now(); // 開始時間
    if (typeof Worker !== 'undefined') {
      // Create a new
      this.worker1.onmessage = ({ data }) => {
        this.reacPickup = data.reacPickup;
        this.value_range = data.value_range
        console.log('反力reac の ピックアップ PickUp 集計が終わりました', performance.now() - startTime);

        // 断面力テーブルの集計
        this.worker2.onmessage = ({ data }) => {
          console.log('反力reac の ピックアップ PickUp テーブル集計が終わりました', performance.now() - startTime);
          this.columns = data.result;
          this.isCalculated = true;
        };
        this.worker2.postMessage({ reacPickup: this.reacPickup });
        this.three.setCombPickResultData(this.value_range, 'pik_reac');
        //this.columns = this.work2_test({ reacPickup: this.reacPickup });
      };
      // this.worker1_test({ pickList, reacCombine });
      this.worker1.postMessage({ pickList, reacCombine });
    } else {
      // Web workers are not supported in this environment.
      // You should add a fallback so that your program still executes correctly.
    }

  }

  worker1_test(data){

  const reacCombine = data.reacCombine;
  const pickList = data.pickList;
  const reacPickup = {};

  // pickupのループ
  for (const pickNo of Object.keys(pickList)) {
    const combines: any[] = pickList[pickNo];
    let tmp: {} = null;
    for (const combNo of combines) {
      const com = JSON.parse(
        JSON.stringify({
          temp: reacCombine[combNo]
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
      reacPickup[pickNo] = tmp;
    }
  }

  const value_range = {};
  // CombineNoごとの最大最小を探す
  for (const combNo of Object.keys(reacPickup)) {
    const caseData = reacPickup[combNo];
    const key_list = Object.keys(caseData);
    const values_t = {};
    const values_r = {};
    // dx～rzの最大最小をそれぞれ探す
    for (const key of key_list) {
      const datas = caseData[key];
      let key2: string;
      let is_t = false;
      if (key.includes('tx')) {
        key2 = 'tx';
        is_t = true;
      } else if (key.includes('ty')) {
        key2 = 'ty';
        is_t = true;
      } else if (key.includes('tz')) {
        key2 = 'tz';
        is_t = true;
      } else if (key.includes('mx')) {
        key2 = 'mx';
      } else if (key.includes('my')) {
        key2 = 'my';
      } else if (key.includes('mz')) {
        key2 = 'mz';
      }
      let targetValue = (key.includes('max')) ? Number.MIN_VALUE : Number.MAX_VALUE;
      let targetValue_m = '0';
      if (key.includes('max')) { // 最大値の判定
        for (const row of Object.keys(datas)) {
          const data = datas[row][key2];
          if (data >= targetValue) {
            targetValue = data;
            targetValue_m = row;
          }
        }
      } else {  // 最小値の判定
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
      if(is_t){
        values_t[key] = {max: targetValue, max_m: targetValue_m};
      } else{
        values_r[key] = {max: targetValue, max_m: targetValue_m};
      }
    }
    if (Object.keys(values_t).length === 0) {
      continue;
    }
    const values2 = {
      max_d : Number.MIN_VALUE, max_d_m: 0,
      min_d : Number.MAX_VALUE, min_d_m: 0,
      max_r : Number.MIN_VALUE, max_r_m: 0,
      min_r : Number.MAX_VALUE, min_r_m: 0
    };
    for(const key of Object.keys(values_t)){
      const value = values_t[key];
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
  return { reacPickup, value_range };
  }
}
