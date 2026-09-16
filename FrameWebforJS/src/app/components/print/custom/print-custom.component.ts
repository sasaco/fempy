import { Component, OnInit } from "@angular/core";
import { PrintService } from "../print.service";
import { PrintCustomService } from "./print-custom.service";

@Component({
  selector: "app-print-custom",
  templateUrl: "./print-custom.component.html",
  styleUrls: ["./print-custom.component.scss", "../print.component.scss"],
})
export class PrintCustomComponent implements OnInit {
  // public flg: number;
  public flg = this.printService.flg;

  constructor(
    public printService: PrintService,
    public printCustomService: PrintCustomService
  ) {}

  ngOnInit(): void {
    this.onSelectChange(this.printService.flg);
  }

  public onSelectChange(value) {
    this.printService.flg = Number(value);
  }
}
