import { Injectable, NgZone } from '@angular/core';
// import { Subject } from 'rxjs/Subject';

@Injectable({
  providedIn: 'root'
})
export class MaxMinService {

  private parent: any; // MaxMinComponent に通知する用
  setParent(arg0: any) {
    this.parent = arg0;
  }

  public visible: boolean = false;

  public max_Three: string;
  public min_Three: string;
  public max_Three_m: string;
  public min_Three_m: string;
  public maxminFlag: boolean;

  public Three_unit: string;
  public ModeName: any;
  public index: any;
  public max: any;
  public min: any;
  public max_m: any;
  public min_m: any;
  public max2: any;
  public min2: any;
  public max2_m: any;
  public min2_m: any;
  public radio: any;

  constructor(private ngZone: NgZone) { }

  public getStatus(mode, currentIndex) {
    this.ModeName = mode;
    this.index = currentIndex;
    if (this.max !== undefined && this.min !== undefined &&
      this.max_m !== undefined && this.min_m !== undefined &&
      this.max2 !== undefined && this.min2 !== undefined &&
      this.max2_m !== undefined && this.min2_m !== undefined) {
      this.getMaxMin(this.max, this.min, this.max_m, this.min_m,
        this.max2, this.min2, this.max2_m, this.min2_m);
    }
  }

  public getMaxMinValue(value_range, mode, currentPage, key1, key2 = null): any {
    if (!(mode in value_range)) {
      return
    }
    const a = value_range[mode];
    if (!(currentPage in a)) {
      return
    }
    const b = a[currentPage];
    if (b == undefined) {
      this.maxMinClear();
      return
    }
    const c = key2 == null ? b : b[key2];
    if (c === undefined) {
      return
    }

    return this._getMaxMinValue(
      c,
      mode,
      key1
    ) ?? '';
  }

  public _getMaxMinValue(value_range, mode, radio): any {
    if (value_range === undefined) {
      this.maxMinClear();
      return;
    }

    this.max = (value_range.max_d != undefined) ? value_range.max_d : 0;
    this.min = (value_range.min_d != undefined) ? value_range.min_d : 0;
    this.max_m = (value_range.max_d_m != undefined) ? value_range.max_d_m : '0';
    this.min_m = (value_range.min_d_m != undefined) ? value_range.min_d_m : '0';
    this.max2 = (value_range.max_r != undefined) ? value_range.max_r : 0;
    this.min2 = (value_range.min_r != undefined) ? value_range.min_r : 0;
    this.max2_m = (value_range.max_r_m != undefined) ? value_range.max_r_m : '0';
    this.min2_m = (value_range.min_r_m != undefined) ? value_range.min_r_m : '0';
    this.radio = radio;

    return this.getMaxMin(this.max, this.min, this.max_m, this.min_m,
      this.max2, this.min2, this.max2_m, this.min2_m);
  }

  public getMaxMin(max, min, max_m, min_m, max2, min2, max2_m, min2_m): any {
    if (
      this.ModeName === "fsec" ||
      this.ModeName === "comb_fsec" ||
      this.ModeName === "pick_fsec"
    ) {
      if (this.radio.includes("Force")) {
        this.Three_unit = "kN";
        this.max_Three = Number(max).toFixed(2)
          + this.Three_unit
          + '(' + max_m.toString() + '部材)';
        this.min_Three = Number(min).toFixed(2)
          + this.Three_unit
          + '(' + min_m.toString() + '部材)';
      } else if (this.radio.includes("oment")) {
        this.Three_unit = "kN・m";
        this.max_Three = Number(max2).toFixed(2)
          + this.Three_unit
          + '(' + max2_m.toString() + '部材)';
        this.min_Three = Number(min2).toFixed(2)
          + this.Three_unit
          + '(' + min2_m.toString() + '部材)';
      }
      this.visible = true;

    } else if (
      this.ModeName === "disg" ||
      this.ModeName === "comb_disg" ||
      this.ModeName === "pik_disg"
    ) {
      this.Three_unit = "mm";
      this.max_Three = Number(max).toFixed(4)
        + this.Three_unit
        + '(' + max_m.toString() + '節点), '
        + Number(max2).toFixed(4)
        + '‰rad'
        + '(' + max2_m.toString() + '節点)';
      this.min_Three = Number(min).toFixed(4)
        + this.Three_unit
        + '(' + min_m.toString() + '節点), '
        + Number(min2).toFixed(4)
        + '‰rad'
        + '(' + min2_m.toString() + '節点)';

      this.visible = true;
    } else if (
      this.ModeName === "reac" ||
      this.ModeName === "comb_reac" ||
      this.ModeName === "pik_reac"
    ) {
      this.Three_unit = "kN";
      this.max_Three = Number(max).toFixed(2)
        + this.Three_unit
        + '(' + max_m.toString() + '節点), '
        + Number(max2).toFixed(2)
        + 'kN・m'
        + '(' + max2_m.toString() + '節点)';
      this.min_Three = Number(min).toFixed(2)
        + this.Three_unit
        + '(' + min_m.toString() + '節点), '
        + Number(min2).toFixed(2)
        + 'kN・m'
        + '(' + min2_m.toString() + '節点)';
      this.visible = true;
    }

    this.parent.setValue(this.max_Three, this.min_Three);

    return { max: this.max_Three, min: this.min_Three };
  }

  public maxMinClear() {
    this.visible = false;
    this.max = undefined;
    this.min = undefined;
    this.max_m = undefined;
    this.min_m = undefined;
    this.max2 = undefined;
    this.min2 = undefined;
    this.max2_m = undefined;
    this.min2_m = undefined;
    // document.getElementById("max-min").style.display = "none";
  }
}
