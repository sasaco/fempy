import { Injectable } from '@angular/core';
import { InputNodesService } from '../input-nodes/input-nodes.service';
import { DataHelperModule } from 'src/app/providers/data-helper.module';
import { InputMembersService } from '../input-members/input-members.service';
import { ThreeService } from '../../three/three.service';
import { ThreeMembersService } from '../../three/geometry/three-members.service';
import { TranslateService } from "@ngx-translate/core";

@Injectable({
  providedIn: 'root'
})
export class InputRigidZoneService {
  public rigid_zone: any[];

  constructor(private node: InputNodesService,
    private helper: DataHelperModule,
    private member: InputMembersService,
    private translate: TranslateService,) {
    this.clear();
  }
  public clear(): void {
    this.rigid_zone = new Array();
  }
  public getRigidZoneColums(mem: number): any {
    let result: any = null;
    let json = this.member.getMemberJson()  
    for (const tmp of this.rigid_zone) {
      if (tmp["m"].toString() === mem.toString() && json[mem] != null) {
        tmp['e'] = json[mem]['e'];
        result = tmp;
        break;
      }
    }
    if (result === null) {
      result = { m: "", Ilength: "", Jlength: "", e: "", L: '', e1: '', n: '' };
      this.rigid_zone.push(result);
    }
    return result;
  }

  public setRigidJson(jsonData: {}): void {
    let json = this.member.getMemberJson()
    if (!('rigid' in jsonData)) {
      for (const index of Object.keys(json)) {
        const item = json[index];
        const e = this.helper.toNumber(item['e']);
        const result = {
          m: index,
          Ilength: '',
          Jlength: '',
          e: (e == null) ? '' : e.toFixed(0),
          L: '',
          e1: '',
          n: ''
        };
        this.rigid_zone.push(result);
      }
    } else {
      const json1: any = jsonData['rigid'];
      for (const index of Object.keys(json)) {

        const item = json[index];
        const e = this.helper.toNumber(item['e']);
        const rigid = json1.find(x => x['m'] === index);
        if (rigid !== undefined) {
          const result = {
            m: index,
            Ilength: rigid['Ilength'],
            Jlength: rigid['Jlength'],
            e: (e == null) ? '' : e.toFixed(0),
            L: '',
            e1: rigid['e'],
            n: ''
          };
          console.log("result", result);
          this.rigid_zone.push(result);
        } else {
          const result = {
            m: index,
            Ilength: '',
            Jlength: '',
            e: (e == null) ? '' : e.toFixed(0),
            L: '',
            e1: '',
            n: ''
          };
          this.rigid_zone.push(result);
        }
      }
    }

  }
  public getRigidJson() {
    const result = new Array();

    for (const row of this.rigid_zone) {
      const m = this.helper.toNumber(row["m"]);
      const Ilength = this.helper.toNumber(row["Ilength"]);
      const Jlength = this.helper.toNumber(row["Jlength"]);
      const e = this.helper.toNumber(row["e1"]);
      if (m == null) {
        continue;
      }
      if (e != null) {
        result.push({
          m: row['m'],
          Ilength: (Ilength == null) ? 0 : Ilength,
          Jlength: (Jlength == null) ? 0 : Jlength,
          e: e,
        });
      }
    }
    return result;
  }
}

