import { Component, OnInit, OnDestroy, ViewChild } from "@angular/core";
import { JointColumns, InputJointService } from "./input-joint.service";
import { DataHelperModule } from "../../../providers/data-helper.module";
import { ThreeService } from "../../three/three.service";
import { SheetComponent } from "../sheet/sheet.component";
import pq from "pqgrid";
import { AppComponent } from 'src/app/app.component';
import { TranslateService } from "@ngx-translate/core";
import { Subscription } from "rxjs";
import { PagerService } from "../pager/pager.service";
import { DocLayoutService } from "src/app/providers/doc-layout.service";

@Component({
  selector: "app-input-joint",
  templateUrl: "./input-joint.component.html",
  styleUrls: ["./input-joint.component.scss", "../../../app.component.scss"],
})
export class InputJointComponent implements OnInit, OnDestroy {
  @ViewChild("grid") grid!: SheetComponent;

  private subscription: Subscription;
  private dataset: JointColumns[] = [];
  private columnKeys3D = ['m', 'xi', 'yi', 'zi', 'xj', 'yj', 'zj'];
  private columnKeys2D = ['m', 'zi', 'zj'];
  private columnHeaders3D =[
    { 
      title: this.translate.instant("input.input-joint.member"),
      align: 'center',
      colModel: [
      { 
        title: this.translate.instant("input.input-joint.No"),
        align: "center",
        dataType: "string",
        dataIndx: this.columnKeys3D[0],
        sortable: false
      },
    ]},      
    { 
      title: this.translate.instant("input.input-joint.node_i"),
      align: 'center',
      colModel: [
        {
          title: "x",
          dataType: "string",
          align: "center",
          dataIndx: this.columnKeys3D[1],
          sortable: false
        },
        {
          title: "y",
          dataType: "string",
          align: "center",
          dataIndx: this.columnKeys3D[2],
          sortable: false
        },
        {
          title: "z",
          dataType: "string",
          align: "center",
          dataIndx: this.columnKeys3D[3],
          sortable: false
        },
      ]
    },      
    { 
      title: this.translate.instant("input.input-joint.node_j"),
      align: 'center',
      colModel: [
        {
          title: "x",
          dataType: "string",
          align: "center",
          dataIndx: this.columnKeys3D[4],
          sortable: false
        },
        {
          title: "y",
          dataType: "string",
          align: "center",
          dataIndx: this.columnKeys3D[5],
          sortable: false
        },
        {
          title: "z",
          dataType: "string",
          align: "center",
          dataIndx: this.columnKeys3D[6],
          sortable: false
        }
      ]
    },      
  ];
  private columnHeaders2D =[
    { 
      title: this.translate.instant("input.input-joint.memberNo"),
      dataType: "string",
      align: "center",
      dataIndx: this.columnKeys2D[0],
      sortable: false
    },
    { 
      title: this.translate.instant("input.input-joint.node_i"),
      dataType: "string",
      align: "center",
      dataIndx: this.columnKeys2D[1],
      sortable: false
    },
    { 
      title: this.translate.instant("input.input-joint.node_j"),
      dataType: "string",
      align: "center",
      dataIndx: this.columnKeys2D[2],
      sortable: false
    }
  ];

  private ROWS_COUNT = 15;
  private page = 1;

  private currentRow: string | null;
  private currentColumn: string | null;

  constructor(
    private data: InputJointService,
    private helper: DataHelperModule,
    private app: AppComponent,
    private three: ThreeService,
    private translate: TranslateService,
    private pagerService: PagerService,
    public docLayout:DocLayoutService
  ) {

    this.currentRow = null;
    this.currentColumn = null;
    this.subscription = this.pagerService.pageSelected$.subscribe((text) => {
      this.onReceiveEventFromChild(text);
    });
  }

  ngOnInit() {
    this.ROWS_COUNT = this.rowsCount();
    this.loadPage(1, this.ROWS_COUNT);
    this.three.ChangeMode("joints");
    this.three.ChangePage(1);
  }


  ngAfterViewInit() {
    this.docLayout.handleMove.subscribe(data => {
    this.options.height = data - 60;
    })
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

  loadPage(currentPage: number, row: number) {
    for (let i = this.dataset.length + 1; i <= row; i++) {
      const jointData = this.data.getJointColumns(currentPage, i);
      const jointDataFixed = this.fixRowData(jointData);  // 入力制限のチェック
      this.dataset.push(jointDataFixed);
    }

    this.page = currentPage;
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
      const row = (range[0].r1 + 1).toString();  // 行番号
      const columnList = this.getColumnList(this.helper.dimension);
      const column = columnList[range[0].c1];  // xi, yi, zi, xj, yj, zj
      if (this.currentRow !== row || this.currentColumn !== column){
        //選択セルの変更があるとき，ハイライトを実行する
        this.three.selectChange("joints", Number(row), column);
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
        const joint = this.data.getJointColumns(this.page, no + 1);
        const newRow = target.newRow;
        joint["m"] = newRow.m != undefined ? newRow.m : "";
        joint["xi"] = newRow.xi != undefined ? newRow.xi : "";
        joint["yi"] = newRow.yi != undefined ? newRow.yi : "";
        joint["zi"] = newRow.zi != undefined ? newRow.zi : "";
        joint["xj"] = newRow.xj != undefined ? newRow.xj : "";
        joint["yj"] = newRow.yj != undefined ? newRow.yj : "";
        joint["zj"] = newRow.zj != undefined ? newRow.zj : "";
        this.dataset.splice(no, 1, joint);
      }
      this.three.changeData("joints", this.page);

      // ハイライトの処理を再度実行する
      const row = (ui.updateList[0].rowIndx + 1).toString();  // 行番号
      const columnList = this.getColumnList(this.helper.dimension);
      let column: string | null = null;
      for (const key of columnList) {
        if (key in ui.updateList[0].newRow) {
          column = key;
          break;
        }
      }
      this.three.selectChange("joints", Number(row), column);
    },
  };

  width = this.helper.dimension === 3 ? 410 : 410;

  private getColumnList (dimension): string[] {
    if (dimension === 3) {
      return this.columnKeys3D;
    } else {
      return this.columnKeys2D;
    }
  }

  /**
   * 指定されたデータ行に対して入力制限を適用した結果を返す。現状は入力行のデータを編集しているので入力データも更新されることに注意
   * @param jointRowData 処理対象のデータ行
   * @returns 入力制限を適用した結果のデータ行
   */
  private fixRowData(jointRowData: JointColumns): JointColumns {
    const result = jointRowData

    // 部材番号：自然数
    if (!this.helper.isNaturalNumber(result.m)) {
      result.m = "";
    }
    // i端x軸まわり材端条件
    if (!this.helper.isZeroOrOne(result.xi)) {
      result.xi = "";
    }
    // i端y軸まわり材端条件
    if (!this.helper.isZeroOrOne(result.yi)) {
      result.yi = "";
    }
    // i端z軸まわり材端条件
    if (!this.helper.isZeroOrOne(result.zi)) {
      result.zi = "";
    }
    // j端x軸まわり材端条件
    if (!this.helper.isZeroOrOne(result.xj)) {
      result.xj = "";
    }
    // j端y軸まわり材端条件
    if (!this.helper.isZeroOrOne(result.yj)) {
      result.yj = "";
    }
    // j端z軸まわり材端条件
    if (!this.helper.isZeroOrOne(result.zj)) {
      result.zj = "";
    }

    return result;
  }
}
