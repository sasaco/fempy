import { Injectable } from "@angular/core";

@Injectable({
  providedIn: "root",
})
export class PrintCustomReacService {
  public reacEditable = {
      tx_max: true,
      tx_min: true,
      ty_max: true,
      ty_min: true,
      tz_max: true,
      tz_min: true,
      mx_max: true,
      mx_min: true,
      my_max: true,
      my_min: true,
      mz_max: true,
      mz_min: true
    };

  constructor() {}

  public reset_check() {
    this.reacEditable["tx_max"] = true;
    this.reacEditable["tx_min"] = true;
    this.reacEditable["ty_max"] = true;
    this.reacEditable["ty_min"] = true;
    this.reacEditable["tz_max"] = true;
    this.reacEditable["tz_min"] = true;
    this.reacEditable["mx_max"] = true;
    this.reacEditable["mx_min"] = true;
    this.reacEditable["my_max"] = true;
    this.reacEditable["my_min"] = true;
    this.reacEditable["mz_max"] = true;
    this.reacEditable["mz_min"] = true;
  };
}
