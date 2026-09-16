import { Injectable } from '@angular/core';
import { DataHelperModule } from '../../../providers/data-helper.module';
import { InputNodesService } from '../input-nodes/input-nodes.service';

@Injectable({
  providedIn: 'root'
})
export class InputPanelService {


  public PANEL_VERTEXS_COUNT = 4;
  public panel_points: any[];

  constructor(private node: InputNodesService,
    private helper: DataHelperModule) {
    this.clear();
  }

  public clear(): void {
    this.panel_points = new Array();
  }

  public getPanelColumns(row: number): any {
    let result: any = this.panel_points.find((e) => e.row.toString() === row.toString());

    // 対象データが無かった時に処理
    if (result == undefined) {
      result = { row, e: '' };// , len: '' };
      for (let i = 1; i <= this.PANEL_VERTEXS_COUNT; i++) {
        result['point-' + i] = '';
      }
      this.panel_points.push(result);
    } else {
      // データの不足を補う
      for (let i = 1; i <= this.PANEL_VERTEXS_COUNT; i++) {
        if (!(('point-' + i) in result)) {
          result['point-' + i] = '';
        }
      }
    }

    return result;
  }

  public setPanelJson(jsonData: {}): void {
    // console.log('setPanelJson', jsonData);
    if (!('shell' in jsonData)) {
      return;
    }
    const json: {} = jsonData['shell'];
    for (const index of Object.keys(json)) {
      const item = json[index];

      const row: string = index;

      const e = item['e'];
      const Points: any[] = item.nodes;

      const result = {
        row: row,
        e: e
      };
      for (let j = 0; j < Points.length; j++) {
        const key = 'point-' + (j + 1).toString();
        const pos: number = this.helper.toNumber(Points[j]);
        result[key] = (pos === null) ? '' : pos.toFixed(0);
      }
      this.panel_points.push(result);
    }
  }

  public getPanelJson(empty: number = null) {

    const result: object = {};

    // console.log('this.panel_points', this.panel_points);
    for (const row of this.panel_points) {
      const r = row['row'];

      const points = new Array();
      for (let j = 1; j < this.PANEL_VERTEXS_COUNT + 1; j++) {
        const key = 'point-' + j;
        if (key in row) {
          const pos: number = this.helper.toNumber(row[key]);
          if (pos != null) {
            points.push(pos);
          }
        }
      }

      const e = this.helper.toNumber(row['e']);

      if (e == null || Object.keys(points).length === 0) {
        continue;
      }

      const key: string = r;
      result[key] = {
        e: row.e,
        nodes: points
      };
    }
    return result;
  }

  public getPanelLength(target: any): number {

    const ni: string = target.ni;
    const nj: string = target.nj;
    if (ni === null || nj === null) {
      return null;
    }

    const iPos = this.node.getNodePos(ni)
    const jPos = this.node.getNodePos(nj)
    if (iPos == null || jPos == null) {
      return null;
    }

    const xi: number = iPos['x'];
    const yi: number = iPos['y'];
    const zi: number = iPos['z'];
    const xj: number = jPos['x'];
    const yj: number = jPos['y'];
    const zj: number = jPos['z'];

    const result: number = Math.sqrt((xi - xj) ** 2 + (yi - yj) ** 2 + (zi - zj) ** 2);
    return result;
  }

  // gmsh サーバーにpost する用の文字列を追加する
  public getGeoString(): string {

    let tmp: string[] = new Array();

    tmp.push('Point(1) = {0, 0, 0}');
    tmp.push('Point(2) = {1, 0, 0}');
    tmp.push('Point(3) = {1, 1, 0}');
    tmp.push('Point(4) = {0, 1, 0}');

    tmp.push('Line(1) = {1,2}');
    tmp.push('Line(2) = {3,2}');
    tmp.push('Line(3) = {3,4}');
    tmp.push('Line(4) = {4,1}');

    tmp.push('Curve Loop(5) = {4,1,-2,3}');
    tmp.push('Plane Surface(6) = {5}');

    tmp.push('Transfinite Curve{1:4} = 10');
    tmp.push('Transfinite Surface{6} Alternate');
    tmp.push('Mesh.ElementOrder = 2');

    let result = '';
    for (const str of tmp) {
      result += str;
      result += ';\n'; // 改行コードを追加
    }

    return result;
  }
}
