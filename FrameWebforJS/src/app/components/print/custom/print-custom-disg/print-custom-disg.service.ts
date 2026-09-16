import { Injectable } from "@angular/core";

@Injectable({
  providedIn: "root",
})
export class PrintCustomDisgService {

  public disgEditable = {
      dx_max: true,
      dx_min: true,
      dy_max: true,
      dy_min: true,
      dz_max: true,
      dz_min: true,
      rx_max: true,
      rx_min: true,
      ry_max: true,
      ry_min: true,
      rz_max: true,
      rz_min: true
  };

  constructor(){}

  public reset_check() {
    this.disgEditable["dx_max"] = true;
    this.disgEditable["dx_min"] = true;
    this.disgEditable["dy_max"] = true;
    this.disgEditable["dy_min"] = true;
    this.disgEditable["dz_max"] = true;
    this.disgEditable["dz_min"] = true;
    this.disgEditable["rx_max"] = true;
    this.disgEditable["rx_min"] = true;
    this.disgEditable["ry_max"] = true;
    this.disgEditable["ry_min"] = true;
    this.disgEditable["rz_max"] = true;
    this.disgEditable["rz_min"] = true;
  };
}
