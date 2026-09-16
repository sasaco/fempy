import { Component, OnInit, ViewChild } from "@angular/core";
import { InputLoadService } from "./input-load.service";
import { ThreeService } from "../../three/three.service";
import { DataHelperModule } from "../../../providers/data-helper.module";
import { SheetComponent } from "../sheet/sheet.component";
import pq from "pqgrid";
import { AppComponent } from "src/app/app.component";
import { ThreeLoadService } from "../../three/geometry/three-load/three-load.service";
import { TranslateService } from "@ngx-translate/core";
import { DocLayoutService } from "src/app/providers/doc-layout.service";
import { LanguagesService } from "src/app/providers/languages.service";

@Component({
  selector: "app-input-load-name",
  templateUrl: "./input-load-name.component.html",
  styleUrls: [
    "./input-load-name.component.scss",
    "../../../app.component.scss",
  ],
})
export class InputLoadNameComponent implements OnInit {
  @ViewChild("grid") grid: SheetComponent;

  private dataset = [];
  private columnHeaders = [
    {
      title: this.translate.instant("input.input-load-name.cf"),
      dataType: "float",
      format: "#.000",
      dataIndx: "rate",
      sortable: false,
      width: 100,
      align: "right",
    },
    {
      title: this.translate.instant("input.input-load-name.symbol"),
      dataType: "string",
      dataIndx: "symbol",
      sortable: false,
      width: 80,
      align: "left",
    },
    {
      title: this.translate.instant("input.input-load-name.name"),
      dataType: "string",
      dataIndx: "name",
      sortable: false,
      width: 300,
      align: "left",
    },
    {
      title: this.translate.instant("input.input-load-name.fixNode"),
      dataType: "integer",
      dataIndx: "fix_node",
      sortable: false,
      width: 30,
      align: "right",
    },
    {
      title: this.translate.instant("input.input-load-name.crossSection"),
      dataType: "integer",
      dataIndx: "element",
      sortable: false,
      width: 30,
      align: "right",
    },
    {
      title: this.translate.instant("input.input-load-name.fixMember"),
      dataType: "integer",
      dataIndx: "fix_member",
      sortable: false,
      width: 30,
      align: "right",
    },
    {
      title: this.translate.instant("input.input-load-name.joint"),
      dataType: "integer",
      dataIndx: "joint",
      sortable: false,
      width: 30,
      align: "right",
    },
  ];

  private ROWS_COUNT = 15;

  constructor(
    private data: InputLoadService,
    private three: ThreeService,
    private app: AppComponent,
    private helper: DataHelperModule,
    private threeload: ThreeLoadService,
    private language: LanguagesService,
    private translate: TranslateService, public docLayout:DocLayoutService
  ) {
    this.loadData(this.ROWS_COUNT);
  }

  ngOnInit() {
    this.ROWS_COUNT = this.rowsCount();
    // three.js にモードの変更を通知する
    this.three.ChangeMode("load_names");
    this.three.ChangePage(1);
  }
  ngAfterViewInit() {
    this.docLayout.handleMove.subscribe(data => {
    this.options.height = data - 60;
    })
    this.language.tranText();
  }
  // 指定行row 以降のデータを読み取る
  private loadData(row: number): void {
    for (let i = this.dataset.length + 1; i <= row; i++) {
      const load_name = this.data.getLoadNameColumns(i);
      this.dataset.push(load_name);
    }
  }

  // 表の高さを計算する
  private tableHeight(): string {
    const containerHeight = this.app.getPanelElementContentContainerHeight() - 10;
    return containerHeight.toString();
  }
  // 表高さに合わせた行数を計算する
  private rowsCount(): number {
    const containerHeight = this.app.getDialogHeight();
    return Math.round(containerHeight / 30);
  }

  // グリッドの設定
  options: pq.gridT.options = {
    showTop: false,
    reactive: true,
    sortable: false,
    locale: "jp",
    height: this.tableHeight(),
    numberCell: {
      show: true, // 行番号
      width: 45,
    },
    colModel: this.columnHeaders,
    dataModel: {
      data: this.dataset,
    },
    contextMenu: {
      on: true,
      items: [
        {
          name: this.translate.instant("action_key.copy"),
          shortcut: 'Ctrl + C',
          action: function (evt, ui, item) {
            this.copy();
          }
        },
        {
          name: this.translate.instant("action_key.paste"),
          shortcut: 'Ctrl + V',
          action: function (evt, ui, item) {
            this.paste();
          }
        },
        {
          name: this.translate.instant("action_key.cut"),
          shortcut: 'Ctrl + X',
          action: function (evt, ui, item) {
            this.cut();
          }
        },
        {
          name: this.translate.instant("action_key.undo"),
          shortcut: 'Ctrl + Z',
          action: function (evt, ui, item) {
            this.History().undo();
          }
        }
      ]
    },
    beforeTableView: (evt, ui) => {
      const finalV = ui.finalV;
      const dataV = this.dataset.length;
      if (ui.initV == null) {
        return;
      }
      if (finalV >= dataV - 1) {
        this.loadData(dataV + this.ROWS_COUNT);
        this.grid.refreshDataAndView();
      }
    },
    selectEnd: (evt, ui) => {
      const range = ui.selection.iCells.ranges;
      const row = range[0].r1;
      const column = range[0].c1;
      const caseNo = row + 1;

      const target = this.dataset[row];
      const fm = this.helper.toNumber(target["fix_member"]);
      const fn = this.helper.toNumber(target["fix_node"]);
      const option = {
        fixMemberPage: fm !== null ? fm : -1,
        fixNodePage: fn !== null ? fn : -1,
      };
      this.three.ChangePage(caseNo, option);

      this.three.selectChange("load_names", row, column);
    },
    change: (evt, ui) => {
      let target: any = null;
      for (let i = 0; i < ui.updateList.length; i++) {
        target = ui.updateList[i];

        const r = this.helper.toNumber(target.rowData["rate"]);
        let s = target.rowData["symbol"];
        if(s === undefined){
          s = null;
        }
        const fm = this.helper.toNumber(target.rowData["fix_member"]);
        const fn = this.helper.toNumber(target.rowData["fix_node"]);
        const e = this.helper.toNumber(target.rowData["element"]);
        const j = this.helper.toNumber(target.rowData["joint"]);
        const n = target.rowData["name"];

        if (
          r === null &&
          s === null &&
          (n === "" || n === undefined) &&
          fm === null &&
          fn === null &&
          e === null &&
          j === null
        ) {
          this.setNewList(target.rowIndx + 1);
        }
      }
      // copy&pasteで入力した際、超過行が消えてしまうため、addListのループを追加.
      for (const target of ui.addList) {
        const no: number = target.rowIndx;
        const newRow = target.newRow;
        const load_name = this.data.getLoadNameColumns(no + 1);
        load_name['rate']  = (newRow.rate != undefined) ? newRow.rate  : null;
        load_name['symbol'] = (newRow.symbol != "") ? newRow.symbol : null;
        load_name['name'] = (newRow.name != "") ? newRow.name : '';
        load_name['fix_node'] = (newRow.fix_node != undefined) ? newRow.fix_node : null;
        load_name['element'] = (newRow.element != undefined) ? newRow.element : null;
        load_name['fix_member'] = (newRow.fix_member != undefined) ? newRow.fix_member : null;
        load_name['joint'] = (newRow.joint != undefined) ? newRow.joint : null;
        this.dataset.splice(no, 1, load_name);

        if (
          load_name['rate'] === null &&
          load_name['symbol'] === null &&
          (load_name['name'] === "" || load_name['name'] === undefined) &&
          load_name['fix_node'] === null &&
          load_name['element'] === null &&
          load_name['fix_member'] === null &&
          load_name['joint'] === null
        ) {
          this.setNewList(no + 1);
        }
      }

      if (target !== null) {
        this.three.changeData("load_names", target.rowIndx + 1);
      }
    },
  };

  public setNewList(index) {
    this.data.partClear(index);
    this.threeload.removeCase(index);
  }
}
