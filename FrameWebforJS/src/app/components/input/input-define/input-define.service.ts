import { Injectable } from '@angular/core';
import { DataHelperModule } from '../../../providers/data-helper.module';

@Injectable({
  providedIn: 'root'
})
export class InputDefineService {

  public define: any[];

  constructor(private helper: DataHelperModule) {
    this.clear();
  }

  public clear(): void {
    this.define = new Array();
  }
  
  public getDefineDataColumns(row: number, col: number): any {

    let result: any = this.define.find(element => element.row === row);

    // 対象データが無かった時に処理
    if (result == undefined) {
      result = { row: row };
      for (let i = 1; i <= col; i++) {
        result['C' + i] = '';
      }
      this.define.push(result);
    }
    return result;
  }

  public setDefineJson(jsonData: {}): void {
    if (!('define' in jsonData)) {
      return;
    }
    const json: {} = jsonData['define'];
    for (const index of Object.keys(json)) {
      if (index == null) {
        continue;
      }
      const result: {} = json[index];
      for (const key of Object.keys(result)) {
        const value = this.helper.toNumber(result[key]);
        result[key] = value;
      }
      this.define.push(result);
    }
  }

  public getDefineJson() {

    const jsonData = {};
    for (let i = 0; i < this.define.length; i++) {
      const data = {};
      const row = this.define[i];
      const id = row['row'];
      let flg = false;
      for (let key in row) {
        if (key === 'row' || key === 'name') {
          data[key] = row[key];
        } else if (key.charAt(0) === 'C'){
          const value = row[key];
          if (this.helper.toNumber(value) != null) {
            flg = true;
            data[key] = value;
          }
        }
      }
      if (flg === true) {
        jsonData[id] = data;
      }
    }
    return jsonData;
  }

  // 補助関数 ///////////////////////////////////////////////////////////////

  /**
   * DEFINEケース番号の最大値を返す
   * @returns DEFINEケース番号の最大値
   */
  public getDefineCaseCount(): number {
    const dict = this.getDefineJson();
    return Math.max(...Object.keys(dict).map(k => Number(k)));
  }

  /**
   * 無効な情報を取り除いたDEFINE情報を返す
   * @param define DEFINE情報
   * @param keys 基本荷重ケース番号のリスト
   * @returns 無効な情報を取り除いたDEFINE情報、ただし、DEFINE情報が有効な情報を含まない場合はnull
   */
  public validate(define: { [key: string]: number | string }, keys: string[]): { [key: string]: number | string } | null {
    const result: { [key: string]: number | string } = {};
    let cnt: number = 0;
    for (const [key, value] of Object.entries(define)) {
      if (key.charAt(0) === 'C') {
        // 有効な基本荷重ケース番号と'0'だけを残してその他は破棄('0'は荷重載荷なしを意味する)
        if (Number.isInteger(value) && (keys.includes(value.toString()) || value === 0)) {
          result[key] = value;
          cnt++;
        }
      } else {
        result[key] = value;
      }
    }
    return cnt > 0 ? result : null;
  }
  
}
