import { Injectable } from "@angular/core";

@Injectable({
  providedIn: "root",
})
export class PrintCustomService {
  public LL_flg = [];
  public LL_exist: boolean;

  constructor() {}

  public LL() {
    this.LL_exist =
      this.LL_flg.filter((n) => n === true).length > 0 ? true : false;
  }
}
