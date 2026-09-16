import { Component, OnInit, ViewChild } from "@angular/core";
import { SheetComponent } from "../../../input/sheet/sheet.component";
import pq from "pqgrid";
import { AppComponent } from "src/app/app.component";
import { PrintService } from "../../print.service";
import { InputMembersService } from "src/app/components/input/input-members/input-members.service";
import { InputElementsService } from "src/app/components/input/input-elements/input-elements.service";
import { PrintCustomFsecService } from "./print-custom-fsec.service";
import { PrintCustomService } from "../print-custom.service";
import { TranslateService } from "@ngx-translate/core";
import { DataHelperModule } from "src/app/providers/data-helper.module";

@Component({
  selector: "app-print-custom-fsec",
  templateUrl: "./print-custom-fsec.component.html",
  styleUrls: [
    "./print-custom-fsec.component.scss",
    "../print-custom.component.scss",
  ],
})
export class PrintCustomFsecComponent implements OnInit {
  @ViewChild("grid") grid: SheetComponent;
  private columnHeaders: any = [
    //{ title: "パネルID", dataType: "integer", dataIndx: "panelID",  sortable: false, width: 40 },
    {
      title: this.translate.instant("print.custom.print-custom-fsec.distance"),
      align: "center",
      dataType: "float",
      format: "#.000",
      dataIndx: "L",
      sortable: false,
      width: 100,
      editable: false,
      style: { background: "#dae6f0" },
    },
    {
      title: this.translate.instant("print.custom.print-custom-fsec.material_name"),
      align: "center",
      dataType: "string",
      dataIndx: "n",
      sortable: false,
      width: 150,
      editable: false,
      style: { background: "#dae6f0" },
    },
    {
      title: this.translate.instant("print.custom.print-custom-fsec.target"),
      width: 100,
      dataIndx: "check",
      align: "center",
      resizable: false,
      menuIcon: false,
      type: "checkBoxSelection",
      cls: "ui-state-default",
      sortable: false,
      editor: false,
      dataType: "bool",
      cb: {
        all: false, //checkbox selection in the header affect current page only.
        header: true, //show checkbox in header.
      },
    },
  ];

  public judgeArr: boolean[];

  constructor(
    private app: AppComponent,
    public printCustomFsecService: PrintCustomFsecService,
    public printCustomService: PrintCustomService,
    public printService: PrintService,
    private translate: TranslateService,
    public helper: DataHelperModule
  ) { }

  ngOnInit(): void {
    this.printCustomFsecService.clear();
    if (!this.printCustomFsecService.flg) {
      this.printCustomFsecService.checkReverse();
    }
    if (this.helper.dimension == 2) {
      this.printCustomFsecService.fsecEditable.fz_max = false;
      this.printCustomFsecService.fsecEditable.fz_max = false;
      this.printCustomFsecService.fsecEditable.mx_max = false;
      this.printCustomFsecService.fsecEditable.mx_min = false;
      this.printCustomFsecService.fsecEditable.my_max = false;
      this.printCustomFsecService.fsecEditable.my_min = false;
    }

  }

  // 表の高さを計算する
  private tableHeight(): string {
    const containerHeight = this.app.getDialogHeight() - 100;
    return containerHeight.toString();
  }

  // グリッドの設定this.flg = Number(this.printOption[0].id);
  options: pq.gridT.options = {
    showTop: false,
    reactive: true,
    sortable: false,
    locale: "jp",
    height: 400,
    width: 418,
    rowBorders: true,
    columnBorders: false,
    numberCell: {
      show: true, // 行番号
      width: 45,
    },
    toolbar: {
      items: [
        {
          type: "button",
          label: "Get Row ID of checkboxes",
          listener: function () {
            var checked = this.Checkbox("state")
              .getCheckedNodes()
              .map(function (rd) {
                return rd.id;
              });
            this.helper.alert(checked);
          },
        },
      ],
    },
    colModel: this.columnHeaders,
    dataModel: {
      data: this.printCustomFsecService.dataset,
    },

    change: (evt, ui) => {
      this.printCustomFsecService.flg = true;
    },
  };

  width = 1000;
}
