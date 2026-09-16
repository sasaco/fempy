import { Injectable } from '@angular/core';
import { DataHelperModule } from '../../../providers/data-helper.module';
import { InputNodesService } from '../input-nodes/input-nodes.service';

@Injectable({
  providedIn: 'root'
})
export class InputMembersService {
  public member: any[];
  public points: any[];

  constructor(private node: InputNodesService,
              private helper: DataHelperModule) {
    this.clear();
  }

  public clear(): void {
    this.member = new Array();
    this.points = new Array();
  }
  
  public getMemberColumns(index: number): any {

    let result: any = null;
    for ( const tmp of this.member) {
      if (tmp['id'].toString() === index.toString()) {
        result = tmp;
        break;
      }
    }
    // 対象データが無かった時に処理
    if (result == null) {
      result = { id: index.toString(), L: '', ni: '', nj: '', e: '', cg: '', n: '' };
      this.member.push(result);
    }
    return result;
  }

  public setMemberJson(jsonData: {}): void {
    if (!('member' in jsonData)) {
      return;
    }
    const json: {} = jsonData['member'];
    for (const index of Object.keys(json)) {

      const item = json[index];

      const ni = this.helper.toNumber(item['ni']);
      const nj = this.helper.toNumber(item['nj']);
      const e = this.helper.toNumber(item['e']);
      const cg = this.helper.toNumber(item['cg']);

      const result = {
        id: index,
        L: '',
        ni: (ni == null) ? '' : ni.toFixed(0),
        nj: (nj == null) ? '' : nj.toFixed(0),
        e: (e == null) ? '' : e.toFixed(0),
        cg: (cg == null) ? '' : cg.toFixed(0),
        n: '',
      };
      
      this.member.push(result);
    }
  }

  public getMemberJson(empty: number = null): object {

    const jsonData: object = {};

    for (let i = 0; i < this.member.length; i++) {

      const columns = this.member[i];
      const ni = this.helper.toNumber(columns['ni']);
      const nj = this.helper.toNumber(columns['nj']);
      const e = this.helper.toNumber(columns['e']);
      const cg = this.helper.toNumber(columns['cg']);

      if (ni == null && nj == null && e == null && cg == null) {
        continue;
      }

      const key: string = columns.id;

      jsonData[key] = {
        'ni': (ni == null) ? empty : ni,
        'nj': (nj == null) ? empty : nj,
        'e': e,
        'cg': (cg == null) ? empty : cg
      };

    }

    return jsonData;
  }

  // 補助関数 ///////////////////////////////////////////////////////////////
  private getMember(memberNo: string) {

    const member = this.member.find((columns) => {
      return columns.id === memberNo;
    })

    if (member === undefined) {
      return { ni: null, nj: null };
    }

    return member;

  }
  public getMemberNo(memberNo: string) {

    const member = this.member.find((columns) => {
      return columns.id === memberNo;
    })

    if (member === undefined) {
      return { ni: null, nj: null };
    }

    return member;

  }

  public getAxis(memberNo: string, m_len: number, m_length: number):any {

    const memb = this.getMember(memberNo.toString());
    if (memb.ni === undefined || memb.nj === undefined) {
      return null;
    }

    const ni: string = memb.ni;
    const nj: string = memb.nj;
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

    const x: string = (xi + (xj - xi) * m_length / m_len).toFixed(3);
    const y: string = (yi + (yj - yi) * m_length / m_len).toFixed(3);
    const z: string = (zi + (zj - zi) * m_length / m_len).toFixed(3);

    return {
      x,
      y,
      z
    };
  }

  public getMemberLength(memberNo: string): number {

    const memb = this.getMember(memberNo.toString());
    if (memb.ni === undefined || memb.nj === undefined) {
      return null;
    }
    const ni: string = memb.ni;
    const nj: string = memb.nj;
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

  public sameNodeMember(temp, list):boolean {
    let result = false;

    const ni = temp.ni;
    const nj = temp.nj;

    for (const a of Object.keys(list)) {
      const item = list[a];
      if ((ni === item.ni && nj === item.nj) || (ni === item.nj && nj === item.ni)) {
        result = true;
        break;
      }
    }

    // 同じ(仮想)部材があればtrue, 無ければfalse
    return result
  }

 

}
