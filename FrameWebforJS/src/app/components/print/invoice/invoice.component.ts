import { Component, OnInit } from "@angular/core";
import { PrintService } from "../print.service";
import { AfterViewInit } from "@angular/core";

@Component({
  selector: "app-invoice",
  templateUrl: "./invoice.component.html",
  styleUrls: ["./invoice.component.scss", "../../../app.component.scss"],
})
export class InvoiceComponent implements OnInit, AfterViewInit {
  constructor(public printService: PrintService) {}

  ngOnInit() {}

  ngAfterViewInit() {
    this.printService.onDataReady();
  }
}
