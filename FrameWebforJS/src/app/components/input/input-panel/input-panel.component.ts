import { Component, ViewChild } from '@angular/core';
import { InputNodesService } from '../input-nodes/input-nodes.service';
import { InputPanelService } from './input-panel.service';
import { DataHelperModule } from '../../../providers/data-helper.module';
import { ThreeService } from '../../three/three.service';
import { SheetComponent } from '../sheet/sheet.component';
import pq from "pqgrid";
import { AppComponent } from "src/app/app.component";
import { TranslateService } from "@ngx-translate/core";
import { DocLayoutService } from 'src/app/providers/doc-layout.service';
import { ThreePanelService } from '../../three/geometry/three-panel.service';
import { Console } from 'console';

@Component({
  selector: 'app-input-panel',
  templateUrl: './input-panel.component.html',
  styleUrls: ['./input-panel.component.scss']
})
export class InputPanelComponent {
  @ViewChild('grid') grid: SheetComponent;

  private dataset = [];
  private columnKeys = ['e'];
  private columnHeaders: any = [
    //{ title: "パネルID", dataType: "integer", dataIndx: "panelID",  sortable: false, width: 40 },
    {
      title: this.translate.instant("input.input-panel.materialNo"),
      dataType: "integer",
      dataIndx: this.columnKeys[0],
      sortable: false,
      width: 40
    },
    {
      title: this.translate.instant("input.input-panel.nodeNo"),
      colModel: []
    }
  ];

  private ROWS_COUNT = 15;

  private currentRow: string;
  private currentColumn: string;

  constructor(private data: InputPanelService,
    private node: InputNodesService,
    private helper: DataHelperModule,
    private app: AppComponent,
    private three: ThreeService,
    private translate: TranslateService,
    public docLayout: DocLayoutService,
    public threePanelService: ThreePanelService,
  ) {

    for (let i = 1; i <= this.data.PANEL_VERTEXS_COUNT; i++) {
      // this.columnKeysの情報追加
      this.columnKeys.push('point-' + i.toString());
      // this.columnHeadersの情報追加
      this.columnHeaders[1].colModel.push({
        title: i.toString(),
        dataType: "integer",
        dataIndx: "point-" + i,
        sortable: false,
        minwidth: 30,
        width: 35
      });
    }
    this.currentRow = null;
  }

  ngOnInit() {
    this.ROWS_COUNT = this.rowsCount();
    // three.js にモードの変更を通知する
    this.three.ChangeMode('panel');
    this.three.ChangePage(1);
  }

  ngAfterViewInit() {
    this.docLayout.handleMove.subscribe(data => {
      this.options.height = data - 60;
    });

    this.threePanelService.panelSelected$.subscribe((item: any) => {
      var indexRow = item.name.replace("panel-", "");
      if (indexRow >= 29) {
        let d = Math.ceil(indexRow / 29);
        this.grid.grid.scrollY((d * this.grid.div.nativeElement.clientHeight), () => {
          // this.grid.grid.goToPage({ rowIndx: indexRow, page: 2 } )
          this.grid.grid.setSelection({ rowIndx: indexRow - 1, rowIndxPage: 1, colIndx: 0, focus: true });
        });
      } else {
        this.grid.grid.setSelection({ rowIndx: indexRow - 1, rowIndxPage: 1, colIndx: 0, focus: true });
      }
    });
  }

  // 指定行row 以降のデータを読み取る
  private loadData(row: number): void {
    for (let i = this.dataset.length + 1; i <= row; i++) {
      const panel = this.data.getPanelColumns(i);
      this.dataset.push(panel);
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
    scrollModel: {
      horizontal: true
    },
    locale: "jp",
    height: this.tableHeight(),
    numberCell: {
      show: true, // 行番号
      width: 45
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
    selectEnd: (evt, ui) => {
      const range = ui.selection.iCells.ranges;
      const row = range[0].r1 + 1;
      const column = this.columnKeys[range[0].c1];
      if (this.currentRow !== row) {
        //選択行の変更があるとき，ハイライトを実行する
        this.three.selectChange('panel', row, column);
      }
      this.currentRow = row;
      this.currentColumn = column;
    },
    change: (evt, ui) => {
      const changes = ui.updateList;
      for (const target of changes) {
        const row: number = target.rowIndx;
        const key = Object.keys(target.newRow);
        const m: string = target.newRow[key.toString()];
        if (m === null) {
          this.dataset[row]['len'] = null;
        }
        //this.grid.refreshDataAndView(); //This event prevents undo event
      }
      // copy&pasteで入力した際、超過行が消えてしまうため、addListのループを追加.
      for (const target of ui.addList) {
        const no: number = target.rowIndx;
        const panel = this.data.getPanelColumns(no + 1);
        const newRow = target.newRow;
        panel['e'] = (newRow.e != undefined) ? newRow.e : '';
        panel['row'] = (newRow.e != undefined) ? newRow.e.toString() : '';
        for (let num = 1; num <= this.data.PANEL_VERTEXS_COUNT; num++) {
          const key = "point-" + num.toString();
          panel[key] = (newRow[key] != undefined) ? newRow[key] : '';
        }
        this.dataset.splice(no, 1, panel);
      }
      this.three.changeData('panel');

      // ハイライト処理を再度実行する
      const row = changes[0].rowIndx + 1;
      let column: string; // 複数の時は左上に合わせる
      for (const key of this.columnKeys) {
        if (key in ui.updateList[0].newRow) {
          column = key;
          break;
        }
      }
      this.three.selectChange("panel", row, column);
    }
  };

}
