import { Component, OnInit, OnDestroy, ViewChild } from '@angular/core';
import { ResultFsecService } from './result-fsec.service';
import { InputLoadService } from '../../input/input-load/input-load.service';
import { ThreeService } from '../../three/three.service';

import { ResultDataService } from '../../../providers/result-data.service';
import { ResultCombineFsecService } from '../result-combine-fsec/result-combine-fsec.service';
import { ResultPickupFsecService } from '../result-pickup-fsec/result-pickup-fsec.service';
import { AppComponent } from 'src/app/app.component';
import { DataHelperModule } from 'src/app/providers/data-helper.module';
import { Subscription } from 'rxjs';
import { PagerService } from '../../input/pager/pager.service';
import { DocLayoutService } from 'src/app/providers/doc-layout.service';
import { SheetComponent } from '../../input/sheet/sheet.component';
import pq from "pqgrid";
import { TranslateService } from '@ngx-translate/core';
import { InputPanelService } from '../../input/input-panel/input-panel.service';
import { forEach } from 'jszip';
import { InputNodesService } from '../../input/input-nodes/input-nodes.service';
import { ThreeSectionForceService } from '../../three/geometry/three-section-force/three-section-force.service';
import { ThreePanelService } from '../../three/geometry/three-panel.service';
import { ColorPaletteService } from '../../three/color-palette/color-palette.service';
import { PagerDirectionService } from '../../input/pager-direction/pager-direction.service';

@Component({
  selector: 'app-result-fsec',
  templateUrl: './result-fsec.component.html',
  styleUrls: [
    './result-fsec.component.scss',
    '../../../app.component.scss',
    '../../../floater.component.scss',
  ],
})
export class ResultFsecComponent implements OnInit, OnDestroy {
  private subscription: Subscription;
  private directionSubscription: Subscription;
  public KEYS: string[];
  page: number = 1;
  currentKey: any = 0;
  LL_page: boolean;

  private columnHeaders3D = this.result.initColumnTable(this.data.column3Ds, 80);
  private columnHeaders3D_LL = this.result.initColumnTable(this.comb.column3Ds, 80);

  private columnHeaders2D = this.result.initColumnTable(this.data.column2Ds, 80);
  private columnHeaders2D_LL = this.result.initColumnTable(this.comb.column2Ds, 80);


  constructor(
    private panel: InputPanelService,
    private nodes: InputNodesService,
    private data: ResultFsecService,
    private app: AppComponent,
    private result: ResultDataService,
    private load: InputLoadService,
    private three: ThreeService,
    private comb: ResultCombineFsecService,
    private pic: ResultPickupFsecService,
    private helper: DataHelperModule,
    private pagerDirectionService: PagerDirectionService,
    private pagerService: PagerService,
    public docLayout: DocLayoutService,
    private translate: TranslateService  ,
    private three_fesc: ThreeSectionForceService,
    private three_panel: ThreePanelService,
    public colorPaletteService: ColorPaletteService
  ) {
    this.KEYS = this.comb.fsecKeys;

    if (this.result.case != 'basic') {
      this.result.page = 1;
      this.result.case = 'basic';
    }
    this.directionSubscription =
      this.pagerDirectionService.pageSelected$.subscribe((text) => {
        this.onChangeKey(text);
      });
    this.subscription = this.pagerService.pageSelected$.subscribe((text) => {
      this.onReceiveEventFromChild(text);
    });
  }

  ngOnInit() {
    this.colorPaletteService.setControlShowHide(true);
    this.ROWS_COUNT = this.rowsCount();
    this.loadData(this.page || 1, this.ROWS_COUNT);
  }

  ngOnDestroy() {
    this.subscription.unsubscribe();
    this.directionSubscription.unsubscribe();
    this.colorPaletteService.setControlShowHide(false);
    // コンポーネント破棄時にLL_pageをリセット
    this.helper.LL_page = false;
  }

  //　pager.component からの通知を受け取る
  onReceiveEventFromChild(eventData: number) {
    let pageNew: number = eventData;
    // this.loadPage(pageNew);
    this.datasetNew.splice(0);
    this.loadData(pageNew, this.ROWS_COUNT);
    this.grid.refreshDataAndView();
    this.three.ChangePage(pageNew);
  }

  onChangeKey(text: any) {
    this.currentKey = text - 1;

    this.datasetNew.splice(0);
    this.ROWS_COUNT = this.rowsCount();
    const currentPage = this.page || 1;
    this.loadData(currentPage, this.ROWS_COUNT);
    this.grid.refreshDataAndView();
    this.three.ChangePage(currentPage);
  }

   @ViewChild('grid') grid: SheetComponent;

  private datasetNew = [];
  private ROWS_COUNT = 15;

  private loadData(currentPage: number, row: number): void {
    this.three_panel.ClearData();
    this.three_fesc.ClearDataGradient();

    // 連行荷重`LL`か判定
    if (currentPage <= this.data.LL_flg.length) {
      this.LL_page = this.data.LL_flg[currentPage - 1];
    } else {
      this.LL_page = false;
    }

    // DataHelperModuleのLL_page状態を更新
    this.helper.LL_page = this.LL_page;

    // データロード
    if (this.LL_page === true) {
      this.options.colModel = this.helper.dimension === 3 ? this.columnHeaders3D_LL : this.columnHeaders2D_LL;

      let key = this.KEYS[this.currentKey];
      for (let i = this.datasetNew.length; i <= row; i++) {
        const define = this.data.getDataColumns(currentPage, i, key);
        this.datasetNew.push(define);
      }
    } else {
      this.options.colModel = this.helper.dimension === 3 ? this.columnHeaders3D : this.columnHeaders2D;

      for (let i = this.datasetNew.length; i <= row; i++) {
        const define = this.data.getDataColumns(currentPage, i);
        this.datasetNew.push(define);
      }
    }

    this.page = currentPage;

    // three.jsの表示を変更
    this.three.ChangeMode("fsec");
    this.three.ChangePage(currentPage);
    this.three_fesc.drawGradientPanel();
  }

  private tableHeight(): string {
    const containerHeight = this.app.getPanelElementContentContainerHeight() - 10;
    return containerHeight.toString();
  }
  // 表高さに合わせた行数を計算する
  private rowsCount(): number {
    const containerHeight = this.app.getDialogHeight();
    return Math.round(containerHeight / 30);
  }

  public options: pq.gridT.options = {
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
      width:40
    },
    colModel: this.helper.dimension === 3 ? this.columnHeaders3D : this.columnHeaders2D,
    dataModel: {
      data: this.datasetNew
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
        }
      ]
    },
    beforeTableView: (evt, ui) => {
      const finalV = ui.finalV;
      const dataV = this.datasetNew.length;
      if (ui.initV == null) {
        return;
      }
      if (finalV >= dataV - 1) {
        this.loadData(this.page, dataV + this.ROWS_COUNT);
        this.grid.refreshDataAndView();
      }
    },
  };
}
