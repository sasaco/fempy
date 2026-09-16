import { Component, OnInit } from "@angular/core";
import { DataHelperModule } from "src/app/providers/data-helper.module";
import { PrintService } from "../../print.service";
import { PrintCustomReacService } from "./print-custom-reac.service";

@Component({
  selector: "app-print-custom-reac",
  templateUrl: "./print-custom-reac.component.html",
  styleUrls: [
    "./print-custom-reac.component.scss",
    "../print-custom.component.scss",
  ],
})
export class PrintCustomReacComponent implements OnInit {
  constructor(
    public printCustomReacService: PrintCustomReacService,
    public printService: PrintService,
    public helper: DataHelperModule
  ) {}

  ngOnInit(): void {
    if( this.helper.dimension == 2){
      this.printCustomReacService.reacEditable.tz_max = false;
      this.printCustomReacService.reacEditable.tz_min = false;
      this.printCustomReacService.reacEditable.mx_max = false;
      this.printCustomReacService.reacEditable.mx_min = false;
      this.printCustomReacService.reacEditable.my_max = false;
      this.printCustomReacService.reacEditable.my_min = false;
    }
  }
}
