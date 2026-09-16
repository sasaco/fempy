import { Component, OnDestroy, OnInit, ViewChild } from "@angular/core";
import { FixNodeColumns, InputFixNodeService } from "./input-fix-node.service";
import { DataHelperModule } from "../../../providers/data-helper.module";
import { ThreeService } from "../../three/three.service";
import { SheetComponent } from "../sheet/sheet.component";
import pq from "pqgrid";
import { AppComponent } from "src/app/app.component";
import { TranslateService } from "@ngx-translate/core";
import { Subscription } from "rxjs";
import { PagerService } from "../pager/pager.service";
import { DocLayoutService } from "src/app/providers/doc-layout.service";
import { ThreeFixNodeService } from "../../three/geometry/three-fix-node.service";
import { LanguagesService } from "src/app/providers/languages.service";

@Component({
  selector: "app-input-fix-node",
  templateUrl: "./input-fix-node.component.html",
  styleUrls: ["./input-fix-node.component.scss", "../../../app.component.scss"],
})
export class InputFixNodeComponent implements OnInit, OnDestroy {
  @ViewChild("grid") grid: SheetComponent;

  private subscription: Subscription;
  private ROWS_COUNT = 15;
  private page = 1;
  private dataset: FixNodeColumns[] = [];

  private columnKeys3D: string[] = ["n", "tx", "ty", "tz", "rx", "ry", "rz"];
  private columnKeys2D: string[] = ["n", "tx", "ty", "rz"];
  private columnHeaders3D = [
    {
      title: this.translate.instant("input.input-fix-node.node"),
      colModel: [
        {
          title: this.translate.instant("input.input-fix-node.No"),
          align: "center",
          dataType: "string",
          dataIndx: this.columnKeys3D[0],
          sortable: false,
          width: 30,
        },
      ],
    },
    {
      title: this.translate.instant(
        "input.input-fix-node.displacementRestraint"
      ),
      align: "center",
      colModel: [
        {
          title: this.translate.instant("input.input-fix-node.x_direction"),
          dataType: "string",
          align: "right",
          dataIndx: this.columnKeys3D[1],
          sortable: false,
          width: 100,
        },
        {
          title: this.translate.instant("input.input-fix-node.y_direction"),
          dataType: "string",
          align: "right",
          dataIndx: this.columnKeys3D[2],
          sortable: false,
          width: 100,
        },
        {
          title: this.translate.instant("input.input-fix-node.z_direction"),
          dataType: "string",
          align: "right",
          dataIndx: this.columnKeys3D[3],
          sortable: false,
          width: 100,
        },
      ],
    },
    {
      title: this.translate.instant("input.input-fix-node.rotationalRestraint"),
      align: "center",
      colModel: [
        {
          title: this.translate.instant("input.input-fix-node.x_around"),
          dataType: "string",
          align: "right",
          dataIndx: this.columnKeys3D[4],
          sortable: false,
          width: 100,
        },
        {
          title: this.translate.instant("input.input-fix-node.y_around"),
          dataType: "string",
          align: "right",
          dataIndx: this.columnKeys3D[5],
          sortable: false,
          width: 100,
        },
        {
          title: this.translate.instant("input.input-fix-node.z_around"),
          dataType: "string",
          align: "right",
          dataIndx: this.columnKeys3D[6],
          sortable: false,
          width: 100,
        },
      ],
    },
  ];
  private columnHeaders2D = [
    {
      title: this.translate.instant("input.input-fix-node.node"),
      colModel: [
        {
          title: this.translate.instant("input.input-fix-node.No"),
          align: "center",
          dataType: "string",
          dataIndx: this.columnKeys2D[0],
          sortable: false,
          width: 30,
        },
      ],
    },
    {
      title: this.translate.instant(
        "input.input-fix-node.displacementRestraint"
      ),
      align: "center",
      colModel: [
        {
          title: this.translate.instant("input.input-fix-node.x_direction"),
          dataType: "string",
          align: "right",
          dataIndx: this.columnKeys2D[1],
          sortable: false,
          width: 100,
        },
        {
          title: this.translate.instant("input.input-fix-node.y_direction"),
          dataType: "string",
          align: "right",
          dataIndx: this.columnKeys2D[2],
          sortable: false,
          width: 100,
        },
      ],
    },
    {
      title: this.translate.instant("input.input-fix-node.rotationalRestraint"),
      align: "center",
      colModel: [
        {
          title: " (kN・m/rad)",
          dataType: "string",
          align: "right",
          dataIndx: this.columnKeys2D[3],
          sortable: false,
          width: 100,
        },
      ],
    },
  ];

  private currentRow: number;
  private currentColumn: string;

  constructor(
    private data: InputFixNodeService,
    private helper: DataHelperModule,
    private app: AppComponent,
    private three: ThreeService,
    private translate: TranslateService,
    private threeFixNodeService: ThreeFixNodeService,
    private pagerService: PagerService,
    private language: LanguagesService,
    public docLayout: DocLayoutService
  ) {
    this.currentRow = null;
    this.currentColumn = null;
    this.subscription = this.pagerService.pageSelected$.subscribe((text) => {
      this.onReceiveEventFromChild(text);
    });
  }

  ngOnInit() {
    this.ROWS_COUNT = this.rowsCount();
    // this.loadPage(1, this.ROWS_COUNT);
    this.three.ChangeMode("fix_nodes");
    this.three.ChangePage(1);
  }
  ngAfterViewInit() {
    this.docLayout.handleMove.subscribe((data) => {
      this.options.height = data - 60;
    });

    this.threeFixNodeService.fixNodeSelected$.subscribe((item: any) => {
      var name = item.name.replace("fixnode", "");
      var indexRow = name.match(/\d+/g)[0];
      var col = name.match(/[a-zA-Z]+/g)[0];
      var indexCol;
      if (this.helper.dimension === 3) {
        indexCol = this.columnKeys3D.findIndex((x) => x === col);
      } else {
        indexCol = this.columnKeys2D.findIndex((x) => x === col);
      }
      if (indexRow >= 29) {
        let d = Math.ceil(indexRow / 29);
        this.grid.grid.scrollY(
          d * this.grid.div.nativeElement.clientHeight,
          () => {
            this.grid.grid.setSelection({
              rowIndx: indexRow - 1,
              rowIndxPage: 1,
              colIndx: indexCol,
              focus: true,
            });
          }
        );
      } else {
        this.grid.grid.setSelection({
          rowIndx: indexRow - 1,
          rowIndxPage: 1,
          colIndx: indexCol,
          focus: true,
        });
      }
    });
    this.language.tranText();
  }
  ngOnDestroy() {
    this.subscription.unsubscribe();
  }

  //　pager.component からの通知を受け取る
  onReceiveEventFromChild(eventData: number) {
    this.dataset.splice(0);
    this.loadPage(eventData, this.ROWS_COUNT);
    this.grid.refreshDataAndView();
    this.three.ChangePage(eventData);
  }

  //
  loadPage(currentPage: number, row: number) {
    for (let i = this.dataset.length + 1; i <= row; i++) {
      const fix_node = this.data.getFixNodeColumns(currentPage, i);
      const fixed = this.fixRowData(fix_node);
      this.dataset.push(fixed);
    }
    this.page = currentPage;
  }

  // 表の高さを計算する
  private tableHeight(): string {
    const containerHeight = this.app.getDialogHeight() - 70; // pagerの分減じる
    return containerHeight.toString();
  }
  // 表高さに合わせた行数を計算する
  private rowsCount(): number {
    const containerHeight = this.app.getDialogHeight();
    return Math.round(containerHeight / 30);
  }

  private colModel(): any {
    this.helper.dimension === 3 ? this.columnHeaders3D : this.columnHeaders2D;
  }

  // グリッドの設定
  options: pq.gridT.options = {
    showTop: false,
    reactive: true,
    sortable: false,
    locale: "jp",
    height: this.tableHeight(),
    numberCell: {
      show: false, // 行番号
    },
    colModel:
      this.helper.dimension === 3 ? this.columnHeaders3D : this.columnHeaders2D,
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
        this.loadPage(this.page, dataV + this.ROWS_COUNT);
        this.grid.refreshDataAndView();
      }
    },
    selectEnd: (evt, ui) => {
      const range = ui.selection.iCells.ranges;
      const row = range[0].r1 + 1;
      const columnList = this.getColumnList(this.helper.dimension);
      const column = columnList[range[0].c1];
      if (this.currentRow !== row || this.currentColumn !== column) {
        //選択行の変更があるとき，ハイライトを実行する
        this.three.selectChange("fix_nodes", row, column);
      }
      this.currentRow = row;
      this.currentColumn = column;
    },
    change: (evt, ui) => {
      // 入力制限
      for (const range of [...ui.addList, ...ui.updateList]) {
        range.rowData = this.fixRowData(range.rowData);
      }

      // copy&pasteで入力した際、超過行が消えてしまうため、addListのループを追加.
      for (const target of ui.addList) {
        const no: number = target.rowIndx;
        const node = this.data.getFixNodeColumns(this.page, no + 1);
        node["n"] = target.newRow.n != undefined ? target.newRow.n : "";
        node["tx"] = target.newRow.tx != undefined ? target.newRow.tx : "";
        node["ty"] = target.newRow.ty != undefined ? target.newRow.ty : "";
        node["tz"] = target.newRow.tz != undefined ? target.newRow.tz : "";
        node["rx"] = target.newRow.rx != undefined ? target.newRow.rx : "";
        node["ry"] = target.newRow.ry != undefined ? target.newRow.ry : "";
        node["rz"] = target.newRow.rz != undefined ? target.newRow.rz : "";
        this.dataset.splice(no, 1, node);
      }
      this.three.changeData("fix_nodes", this.page);

      // ハイライトの処理を再度実行する
      const row = ui.updateList[0].rowIndx + 1;
      let column: string;
      const columnList = this.getColumnList(this.helper.dimension);
      for (const key of columnList) {
        if (key in ui.updateList[0].newRow) {
          column = key;
          break;
        }
      }
      this.three.selectChange("fix_nodes", row, column);
    },
  };

  /**
   * 指定されたデータ行に対して入力制限を適用した結果を返す。現状は入力行のデータを編集しているので入力データも更新されることに注意
   * @param fixNode 処理対象のデータ行
   * @returns 入力制限を適用した結果のデータ行
   */
  private fixRowData(fixNode: FixNodeColumns): FixNodeColumns {
    // const result = structuredClone(fixNode);
    const result = fixNode;

    // n 正の整数
    if (!this.helper.isNaturalNumber(result.n)) {
      result.n = '';
    }
    // tx 0以上の実数
    if (result.tx === '') {
      // do nothing
    } else if (result.tx === null) {
      result.tx = '';
    } else {
      const value = Number(result.tx);
      result.tx = (isNaN(value) || value < 0) ? '' : value.toString();
    }
    // ty 0以上の実数
    if (result.ty === '') {
      // do nothing
    } else if (result.ty === null) {
      result.ty = '';
    } else {
      const value = Number(result.ty);
      result.ty = (isNaN(value) || value < 0) ? '' : value.toString();
    }
    // tz 0以上の実数
    if (result.tz === '') {
      // do nothing
    } else if (result.tz === null) {
      result.tz = '';
    } else {
      const value = Number(result.tz);
      result.tz = (isNaN(value) || value < 0) ? '' : value.toString();
    }
    // rx 0以上の実数
    if (result.rx === '') {
      // do nothing
    } else if (result.rx === null) {
      result.rx = '';
    } else {
      const value = Number(result.rx);
      result.rx = (isNaN(value) || value < 0) ? '' : value.toString();
    }
    // ry 0以上の実数
    if (result.ry === '') {
      // do nothing
    } else if (result.ry === null) {
      result.ry = '';
    } else {
      const value = Number(result.ry);
      result.ry = (isNaN(value) || value < 0) ? '' : value.toString();
    }
    // rz 0以上の実数
    if (result.rz === '') {
      // do nothing
    } else if (result.rz === null) {
      result.rz = '';
    } else {
      const value = Number(result.rz);
      result.rz = (isNaN(value) || value < 0) ? '' : value.toString();
    }

    return result;
  }

  width = this.helper.dimension === 3 ? 712 : 412;

  private getColumnList(dimension): string[] {
    if (dimension === 3) {
      return this.columnKeys3D;
    } else {
      return this.columnKeys2D;
    }
  }
}
