import { Component, OnInit, ViewChild } from "@angular/core";
import { InputCombineService } from "./input-combine.service";
import { InputDefineService } from "../input-define/input-define.service";
import { InputLoadService } from "../input-load/input-load.service";
import { ResultDataService } from "../../../providers/result-data.service";
import { DataHelperModule } from "../../../providers/data-helper.module";
import { SheetComponent } from "../sheet/sheet.component";
import pq from "pqgrid";
import { AppComponent } from "src/app/app.component";
import { TranslateService } from "@ngx-translate/core";
import { DocLayoutService } from "src/app/providers/doc-layout.service";

@Component({
  selector: "app-input-combine",
  templateUrl: "./input-combine.component.html",
  styleUrls: ["./input-combine.component.scss", "../../../app.component.scss"],
})
export class InputCombineComponent implements OnInit {
  
  @ViewChild("grid") grid: SheetComponent;

  private dataset = [];
  private columnHeaders: object[] = [];

  private ROWS_COUNT = 15;
  private COLUMNS_COUNT = 5;

  constructor(
    private data: InputCombineService,
    private define: InputDefineService,
    private load: InputLoadService,
    private result: ResultDataService,
    private helper: DataHelperModule,
    private app: AppComponent,
    private translate: TranslateService, public docLayout:DocLayoutService
    ) { }

  ngOnInit() {
    let head = "D";
    this.COLUMNS_COUNT = this.define.getDefineCaseCount();
    if (this.COLUMNS_COUNT <= 0) {
      this.COLUMNS_COUNT = this.load.getLoadCaseCount();
      head = "C";
    }
    if (this.COLUMNS_COUNT <= 5) {
      this.COLUMNS_COUNT = 5;
    }
    for (let i = 1; i <= this.COLUMNS_COUNT; i++) {
      const id = "C" + i;
      const title = head + i;
      this.columnHeaders.push({
        title: title,
        dataType: "float",
        format: '#.000',
        dataIndx: id,
        sortable: false,
        width: 30,
      });
    }
    this.columnHeaders.push({
      title: this.translate.instant("input.input-combine.name"),
      dataType: "string",
      dataIndx: "name",
      sortable: false,
      align: 'left',
      width: 800,
    });
    this.ROWS_COUNT = this.rowsCount();
 
    //const datasheet_inner = document.getElementById("datasheet_inner");
    // datasheet_inner.style.width = String(window.innerWidth - 40 ) + "px";
  }

  ngAfterViewInit() {
    this.docLayout.handleMove.subscribe(data => {
    this.options.height = data - 60;
    })
  }
  // 指定行row 以降のデータを読み取る
  private loadData(row: number): void {
    for (let i = this.dataset.length + 1; i <= row; i++) {
      const combine = this.data.getCombineDataColumns(i, this.COLUMNS_COUNT);
      this.dataset.push(combine);
    }
  }

  // 表の高さを計算する
  private tableHeight(): string {
    const containerHeight = this.app.getDialogHeight();
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
    scrollModel: {
      horizontal: true
    },
    locale: "jp",
    height: this.tableHeight(),
    numberCell: {
      show: true, // 行番号
      width:45
    },
    colModel: this.columnHeaders,
    dataModel: {
      data: this.dataset
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
    change: (evt, ui) => {
      debugger
      for (const target of ui.addList) {
        const no: number = target.rowIndx;
        const newRow = target.newRow;
        const combine = this.data.getCombineDataColumns(no + 1, this.COLUMNS_COUNT);
        combine['name'] = (newRow['name'] != undefined) ? newRow['name'] : '';
        for (let i = 1; i <= this.COLUMNS_COUNT; i++) { 
          const key = "C" + i.toString();
          combine[key] = (newRow[key] != undefined) ? newRow[key] : null;
        }
        this.dataset.splice(no, 1, combine);
      }
    }
  };

}
