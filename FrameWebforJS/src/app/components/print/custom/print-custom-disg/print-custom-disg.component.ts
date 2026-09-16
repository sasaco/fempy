import { Component, OnInit } from "@angular/core";
import { DataHelperModule } from "src/app/providers/data-helper.module";
import { PrintService } from "../../print.service";
import { PrintCustomDisgService } from "./print-custom-disg.service";

@Component({
  selector: "app-print-custom-disg",
  templateUrl: "./print-custom-disg.component.html",
  styleUrls: [
    "./print-custom-disg.component.scss",
    "../print-custom.component.scss",
  ],
})
export class PrintCustomDisgComponent implements OnInit {
  constructor(
    public printCustomDisgService: PrintCustomDisgService,
    public printService: PrintService,
    public helper: DataHelperModule
  ) {}

  ngOnInit(): void {
    if( this.helper.dimension == 2){
      this.printCustomDisgService.disgEditable.dz_max = false;
      this.printCustomDisgService.disgEditable.dz_min = false;
      this.printCustomDisgService.disgEditable.rx_max = false;
      this.printCustomDisgService.disgEditable.rx_min = false;
      this.printCustomDisgService.disgEditable.ry_max = false;
      this.printCustomDisgService.disgEditable.ry_min = false;
    }
  }
}
