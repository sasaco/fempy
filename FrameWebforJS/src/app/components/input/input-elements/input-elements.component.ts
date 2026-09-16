import { Component, OnDestroy, OnInit, ViewChild } from "@angular/core";
import { InputElementsService } from "./input-elements.service";
import { DataHelperModule } from "../../../providers/data-helper.module";
import { ThreeService } from "../../three/three.service";
import { SheetComponent } from "../sheet/sheet.component";
import pq from "pqgrid";
import { AppComponent } from "src/app/app.component";
import { TranslateService } from "@ngx-translate/core";
import { Subscription } from "rxjs";
import { PagerService } from "../pager/pager.service";
import { DocLayoutService } from "src/app/providers/doc-layout.service";

@Component({
  selector: "app-input-elements",
  templateUrl: "./input-elements.component.html",
  styleUrls: ["./input-elements.component.scss", "../../../app.component.scss"],
})
export class InputElementsComponent implements OnInit, OnDestroy {
  @ViewChild("grid") grid: SheetComponent;

  private subscription: Subscription;
  private dataset = [];
  private columnKeys = ["E", "G", 'Xp', 'A', 'J', 'Iy', 'Iz', 'n']
  private columnHeaders3D = [
    {
      title: this.translate.instant("input.input-elements.elastic"),
      align: "center",
      colModel: [
        {
          title: "E(kN/m2)",
          dataType: "float",
          dataIndx: this.columnKeys[0],
          sortable: false,
          width: 120,
        },
      ],
    },
    {
      title: this.translate.instant("input.input-elements.shear_elastic"),
      align: "center",
      colModel: [
        {
          title: "G(kN/m2)",
          dataType: "float",
          dataIndx: this.columnKeys[1],
          sortable: false,
          width: 130,
        },
      ],
    },
    {
      title: this.translate.instant("input.input-elements.cte"),
      align: "center",
      colModel: [
        {
          title: "",
          dataType: "float",
          dataIndx: this.columnKeys[2],
          sortable: false,
          width: 100,
        },
      ],
    },
    {
      title: this.translate.instant("input.input-elements.area"),
      align: "center",
      colModel: [
        {
          title: "A(m2)",
          dataType: "float",
          format: "#.0000",
          dataIndx: this.columnKeys[3],
          sortable: false,
          width: 100,
        },
      ],
    },
    {
      title: this.translate.instant("input.input-elements.torsionConstant"),
      align: "center",
      colModel: [
        {
          title: "J(m4)",
          dataType: "float",
          format: "#.000000",
          dataIndx: this.columnKeys[4],
          sortable: false,
          width: 100,
        },
      ],
    },
    {
      title: this.translate.instant("input.input-elements.inertia"),
      align: "center",
      colModel: [
        {
          title: "Iy (m4)",
          dataType: "float",
          format: "#.000000",
          dataIndx: this.columnKeys[5],
          sortable: false,
          width: 100,
        },
        {
          title: "Iz (m4)",
          dataType: "float",
          format: "#.000000",
          dataIndx: this.columnKeys[6],
          sortable: false,
          width: 100,
        },
      ],
    },
    {
      title: this.translate.instant("input.input-elements.material_name"),
      align: "center",
      dataType: "string",
      format: "#.000000",
      dataIndx: this.columnKeys[7],
      sortable: false,
      width: 100,
    },
  ];
  private columnHeaders2D = [
    {
      title: this.translate.instant("input.input-elements.elastic"),
      align: "center",
      colModel: [
        {
          title: "E(kN/m2)",
          dataType: "float",
          dataIndx: this.columnKeys[0],
          sortable: false,
          width: 120,
        },
      ],
    },
    {
      title: this.translate.instant("input.input-elements.cte"),
      align: "center",
      colModel: [
        {
          title: "",
          dataType: "float",
          dataIndx: this.columnKeys[2],
          sortable: false,
          width: 100,
        },
      ],
    },
    {
      title: this.translate.instant("input.input-elements.area"),
      align: "center",
      colModel: [
        {
          title: "A(m2)",
          dataType: "float",
          format: "#.0000",
          dataIndx: this.columnKeys[3],
          sortable: false,
          width: 100,
        },
      ],
    },
    {
      title: this.translate.instant("input.input-elements.inertia"),
      align: "center",
      colModel: [
        {
          title: "I(m4)",
          dataType: "float",
          format: "#.000000",
          dataIndx: this.columnKeys[6],
          sortable: false,
          width: 100,
        },
      ],
    },
    {
      title: this.translate.instant("input.input-elements.material_name"),
      align: "center",
      dataType: "string",
      format: "#.000000",
      dataIndx: this.columnKeys[7],
      sortable: false,
      width: 100,
    },
  ];

  private ROWS_COUNT = 15;
  private page = 1;
  private before_page = 1;

  private currentRow: string;

  constructor(
    private data: InputElementsService,
    private helper: DataHelperModule,
    private app: AppComponent,
    private three: ThreeService,
    private translate: TranslateService,
    private pagerService: PagerService, public docLayout:DocLayoutService
  ) {

    this.currentRow = null;
    this.subscription = this.pagerService.pageSelected$.subscribe((text) => {
      this.onReceiveEventFromChild(text);
    });
  }

  ngOnInit() {
    this.ROWS_COUNT = this.rowsCount();
    this.loadPage(1, this.ROWS_COUNT);
    this.three.ChangeMode("elements");
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
      const fix_node = this.data.getElementColumns(currentPage, i);
      this.dataset.push(fix_node);
    }
    this.page = currentPage;

    // nameを検索し、this.datasetに反映させる
    for (let i = 0; i < this.dataset.length; i++) {
      const name = this.data.getAlignName(this.before_page, i);
      this.dataset[i].n = name;
    }
    this.before_page = currentPage;
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
      const column = this.columnKeys[range[0].c1];
      if (this.currentRow !== row){
        //選択行の変更があるとき，ハイライトを実行する
        this.three.selectChange("elements", row, '');
      }
      this.currentRow = row;
    },
    change: (evt, ui) => {
      // copy&pasteで入力した際、超過行が消えてしまうため、addListのループを追加.
      for (const target of ui.addList) {
        const no: number = target.rowIndx;
        const newRow = target.newRow;
        const element = this.data.getElementColumns(this.page, no + 1);
        element['E']  = (newRow.E  !== undefined) ? newRow.E  : '';
        element['G']  = (newRow.G  !== undefined) ? newRow.G  : '';
        element['Xp'] = (newRow.Xp !== undefined) ? newRow.Xp : '';
        element['A']  = (newRow.A  !== undefined) ? newRow.A  : '';
        element['J']  = (newRow.J  !== undefined) ? newRow.J  : '';
        element['Iy'] = (newRow.Iy !== undefined) ? newRow.Iy : '';
        element['Iz'] = (newRow.Iz !== undefined) ? newRow.Iz : '';
        element['n']  = (newRow.n  !== undefined) ? newRow.n  : '';
        this.dataset.splice(no, 1, element)
      }
      this.three.changeData("elements", this.page);
      // 名称が変更されたら、変更を内部データ全体に反映させる
      if ("n" in ui.updateList[0].newRow) {
        this.data.matchName(ui.updateList[0].rowData);
      }
    },
  };

  width = this.helper.dimension === 3 ? 950 : 620;
}
