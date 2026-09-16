import { AfterViewInit } from '@angular/core';
import { Component, OnInit, ViewChild } from '@angular/core';
import { InputNodesService } from './input-nodes.service';
import { DataHelperModule } from '../../../providers/data-helper.module';

import { ThreeService } from '../../three/three.service';
import { SheetComponent } from '../sheet/sheet.component';
import pq from 'pqgrid';
import { AppComponent } from 'src/app/app.component';
import { DocLayoutService } from 'src/app/providers/doc-layout.service';
import { Subscription } from 'rxjs';
import { ThreeNodesService } from '../../three/geometry/three-nodes.service';
import { TranslateService } from '@ngx-translate/core';
import { LanguagesService } from 'src/app/providers/languages.service';

@Component({
  selector: 'app-input-nodes',
  templateUrl: './input-nodes.component.html',
  styleUrls: ['./input-nodes.component.scss', '../../../app.component.scss'],
})
export class InputNodesComponent implements OnInit, AfterViewInit {
  @ViewChild('grid') grid: SheetComponent;

  private dataset = [];
  private columnKeys = ['X', 'Y', 'Z'];
  private columnHeaders3D = [
    {
      title: this.columnKeys[0],
      dataType: 'float',
      format: '#.000',
      dataIndx: 'x',
      sortable: false,
      width: 90,
    },
    {
      title: this.columnKeys[1],
      dataType: 'float',
      format: '#.000',
      dataIndx: 'y',
      sortable: false,
      width: 90,
    },
    {
      title: this.columnKeys[2],
      dataType: 'float',
      format: '#.000',
      dataIndx: 'z',
      sortable: false,
      width: 90,
    },
  ];
  private columnHeaders2D = [
    {
      title: this.columnKeys[0],
      dataType: 'float',
      format: '#.000',
      dataIndx: 'x',
      sortable: false,
      width: 90,
    },
    {
      title: this.columnKeys[1],
      dataType: 'float',
      format: '#.000',
      dataIndx: 'y',
      sortable: false,
      width: 90,
    },
  ];

  private ROWS_COUNT = 15;
  public inner_width = 290;

  private currentRow: string;

  constructor(
    private data: InputNodesService,
    public helper: DataHelperModule,
    private app: AppComponent,
    private three: ThreeService,
    private threeNodesService : ThreeNodesService,
    private translate: TranslateService,
    private language: LanguagesService,
    public docLayout: DocLayoutService
  ) {
    this.currentRow = null;
  }

  ngOnInit() {
    this.ROWS_COUNT = this.rowsCount();
    // three.js にモードの変更を通知する
    this.three.ChangeMode('nodes');
  }
  private subscription: Subscription;
  ngAfterViewInit() {
    this.docLayout.handleMove.subscribe((data) => {
      this.options.height = data - 80;
    });

    this.subscription = this.threeNodesService.nodeSelected$.subscribe((position : any) => {
      var pX = position.x.toFixed(3);
      var pY = position.y.toFixed(3);
      var pZ = position.z.toFixed(3);
      var dataNode = this.data.node.filter(n => n.x === pX && n.y === pY && n.z === pZ)[0];
      if(dataNode.pq_ri === undefined){
        let indexRow = dataNode.id;
        if(indexRow >= 29){
          let d = Math.ceil(indexRow / 29);
          this.grid.grid.scrollY((d * this.grid.div.nativeElement.clientHeight), () => {
            // this.grid.grid.goToPage({ rowIndx: indexRow, page: 2 } )
            this.grid.grid.setSelection({rowIndx: indexRow,rowIndxPage:1,colIndx:1, focus: true});
          });
        }else{
          this.grid.grid.setSelection({rowIndx: indexRow,rowIndxPage:1,colIndx:1, focus: true});
        }
      }else{
        let indexRow = dataNode.pq_ri;
        this.grid.grid.setSelection({rowIndx: indexRow,rowIndxPage:1,colIndx:1, focus: true});
      }
    });
    this.language.tranText();
  }

  test(){
    // this.grid.grid.setSelection({rowIndx: 2,rowIndxPage:3,colIndx:1, focus: true});
    this.grid.grid.scrollY(this.grid.div.nativeElement.clientHeight);
  }
  // 指定行row 以降のデータを読み取る
  private loadData(row: number): void {
    for (let i = this.dataset.length + 1; i <= row; i++) {
      const node = this.data.getNodeColumns(i);
      this.dataset.push(node);
    }
  }

  // 表の高さを計算する
  private tableHeight(): string {
    const containerHeight =
      this.app.getPanelElementContentContainerHeight() - 20;
    return containerHeight.toString();
  }
  // 表高さに合わせた行数を計算する
  private rowsCount(): number {
    const containerHeight = this.app.getDialogHeight();
    return Math.round(containerHeight / 30);
  }
  // グリッドの設定
  public options: pq.gridT.options = {
    showTop: false,
    reactive: true,
    sortable: false,
    locale: 'jp',
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
      console.log('ui', ui);
      console.log('evt', evt);
      const finalV = ui.finalV;
      console.log('finalV', finalV);
      console.log('this.dataset', this.dataset);
      const dataV = this.dataset.length;
      console.log('dataV', dataV);
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
      // if (this.currentRow !== row){
      //選択行の変更があるとき，ハイライトを実行する
      this.three.selectChange('nodes', row, '');
      // }
      // this.currentRow = row;
    },
    change: (evt, ui) => {
      const changes = ui.updateList;
      // copy&pasteで入力した際、超過行が消えてしまうため、addListのループを追加.
      for (const target of ui.addList) {
        const no: number = target.rowIndx;
        const node = this.data.getNodeColumns(no + 1);
        node['x'] = target.newRow.x !== undefined ? target.newRow.x : '';
        node['y'] = target.newRow.y !== undefined ? target.newRow.y : '';
        node['z'] = target.newRow.z !== undefined ? target.newRow.z : '';
        this.dataset.splice(no, 1, node);
      }
      this.three.changeData('nodes');

      // ハイライト処理を再度実行する
      const row = changes[0].rowIndx + 1;
      this.three.selectChange('nodes', row, '');
    },
  };

  width = this.helper.dimension === 3 ? 380 : 290;
}
