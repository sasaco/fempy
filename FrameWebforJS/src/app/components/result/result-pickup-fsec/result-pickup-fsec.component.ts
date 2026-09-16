import { Component, OnInit, OnDestroy, ViewChild } from '@angular/core';
import { ResultPickupFsecService } from './result-pickup-fsec.service';
import { InputPickupService } from '../../input/input-pickup/input-pickup.service';
import { ThreeService } from '../../three/three.service';

import { ResultCombineFsecService } from '../result-combine-fsec/result-combine-fsec.service';
import { DataHelperModule } from 'src/app/providers/data-helper.module';
import { ResultDataService } from '../../../providers/result-data.service';
import { Subscription } from 'rxjs';
import { PagerDirectionService } from '../../input/pager-direction/pager-direction.service';
import { PagerService } from '../../input/pager/pager.service';
import { DocLayoutService } from 'src/app/providers/doc-layout.service';
import pq from "pqgrid";
import { SheetComponent } from '../../input/sheet/sheet.component';
import { AppComponent } from 'src/app/app.component';
import { ColorPaletteService } from '../../three/color-palette/color-palette.service';
import { ThreeSectionForceService } from '../../three/geometry/three-section-force/three-section-force.service';
import { TranslateService } from '@ngx-translate/core';

@Component({
  selector: 'app-result-pickup-fsec',
  templateUrl: './result-pickup-fsec.component.html',
  styleUrls: [
    '../result-combine-fsec/result-combine-fsec.component.scss',
    '../../../app.component.scss',
    '../../../floater.component.scss',
  ],
})
export class ResultPickupFsecComponent implements OnInit, OnDestroy {
  private directionSubscription: Subscription;
  private subscription: Subscription;
  public KEYS: string[];
  public TITLES: string[];
  public height: any;
  dataset: any[];
  page: number;
  load_name: string;
  btnCombine: string;
  tableHeight: number;
  dimension: number;
  cal: number = 0;

  circleBox = new Array();

  private column3Ds: any[] = [
    { title: "result.result-pickup-fsec.memberNo", id: "m" , format: "", width: -40 },
    { title: "result.result-pickup-fsec.nodeNo", id: "n" , format: "", width: -40 },
    { title: "result.result-pickup-fsec.stationLocation", id: "l", format: "#.000" },
    { title: "result.result-pickup-fsec.axialForce", id: "fx", format: "#.00" },
    { title: "result.result-pickup-fsec.y_shear", id: "fy", format: "#.00" },
    { title: "result.result-pickup-fsec.z_shear", id: "fz", format: "#.00" },
    { title: "result.result-pickup-fsec.x_torsion", id: "mx", format: "#.00" },
    { title: "result.result-pickup-fsec.y_moment", id: "my", format: "#.00" },
    { title: "result.result-pickup-fsec.z_moment", id: "mz", format: "#.00" },
    { title: "result.result-pickup-fsec.comb", id: "case", format: "#.00", width: 60 },
  ];
  private columnHeaders3D = this.result.initColumnTable(this.column3Ds, 80);

  private column2Ds: any[] = [
    { title: "result.result-pickup-fsec.memberNo", id: "m", format: "", width: -40 },
    { title: "result.result-pickup-fsec.nodeNo", id: "n", format: "", width: -40 },
    { title: "result.result-pickup-fsec.stationLocation", id: "l", format: "#.000" },
    { title: "result.result-pickup-fsec.axialForce", id: "fx", format: "#.00" },
    { title: "result.result-pickup-fsec.shear", id: "fy", format: "#.00" },
    { title: "result.result-pickup-fsec.moment", id: "mz", format: "#.00" },
    { title: "result.result-pickup-fsec.comb", id: "case", format: "#.00", width: 60 },
  ];
  private columnHeaders2D = this.result.initColumnTable(this.column2Ds, 80);

  private currentKey: any = 0;

  constructor(
    private app: AppComponent,
    private data: ResultPickupFsecService,
    private pickup: InputPickupService,
    private three: ThreeService,
    private result: ResultDataService,
    private comb: ResultCombineFsecService,
    private helper: DataHelperModule,
    private pagerDirectionService: PagerDirectionService,
    private pagerService: PagerService,
    public docLayout: DocLayoutService,
    public colorPaletteService: ColorPaletteService,
    private three_fesc: ThreeSectionForceService,
    private translate: TranslateService,
  ) {
    this.dataset = new Array();
    this.KEYS = this.comb.fsecKeys;
    this.TITLES = this.comb.titles;
    for (let i = 0; i < this.TITLES.length; i++) {
      this.circleBox.push(i);
    }
    this.dimension = this.helper.dimension;

    if (this.result.case != 'pic') {
      this.result.page = 1;
      this.result.case = 'pic';
    }
    this.directionSubscription =
      this.pagerDirectionService.pageSelected$.subscribe((text) => {
        this.calPage(text - 1);
        this.onChangeKey(text);
      });
    this.subscription = this.pagerService.pageSelected$.subscribe((text) => {
      this.onReceiveEventFromChild(text);
    });
  }

  ngOnInit() {
    this.colorPaletteService.setControlShowHide(true);
    // this.loadPage(this.result.page);
    this.calPage(0);

    this.ROWS_COUNT = this.rowsCount();
    this.loadData(1, this.ROWS_COUNT);

    // コンバインデータがあればボタンを表示する
    if (this.comb.isCalculated === true) {
      this.btnCombine = 'btn-change';
    } else {
      this.btnCombine = 'btn-change disabled';
    }

    // テーブルの高さを計算する
    this.tableHeight = (this.dataset[0].length + 1) * 30;
  }
  ngAfterViewInit() {
    this.docLayout.handleMove.subscribe((data) => {
      // this.height = 400;// data - 100;
      this.options.height = data - 60;
    });
  }
  ngOnDestroy() {
    this.directionSubscription.unsubscribe();
    this.subscription.unsubscribe();
    this.colorPaletteService.setControlShowHide(false);
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
    this.loadData(this.page, this.ROWS_COUNT);
    this.grid.refreshDataAndView();
    this.three.ChangePage(1);
  }

  loadPage(currentPage: number) {
    if (currentPage !== this.result.page) {
      this.result.page = currentPage;
    }
    this.dataset = new Array();
    for (const key of this.KEYS) {
      const d = this.data.getPickupFsecColumns(this.result.page, key);
      if (d == null) {
        this.dataset = new Array();
        break;
      }
      this.dataset.push(d);
    }
    this.load_name = this.pickup.getPickUpName(currentPage);

    this.three.ChangeMode('pick_fsec');
    this.three.ChangePage(currentPage);
  }

  calPage(calPage: any) {
    const carousel = document.getElementById('carousel');
    if (carousel != null) {
      carousel.classList.add('add');
    }
    const time = this.TITLES.length;
    let cal = this.cal;
    setTimeout(() => {
      this.calcal(calPage);
    }, 100);
    setTimeout(function () {
      if (carousel != null) {
        carousel.classList.remove('add');
      }
    }, 500);
  }

  calcal(calpage: any) {
    if (calpage === '-1' || calpage === '1') {
      this.cal += Number(calpage);
      if (this.cal >= this.TITLES.length) {
        this.cal = 0;
      }
      if (this.cal < 0) {
        this.cal = this.TITLES.length - 1;
      }
    } else {
      this.cal = calpage;
    }
    setTimeout(() => {
      const circle = document.getElementById(String(this.cal + 20));
      if (circle !== null) {
        circle.classList.add('active');
      }
    }, 10);
  }
  

  @ViewChild("grid") grid: SheetComponent;
  private datasetNew = [];
  private ROWS_COUNT = 15;

  private loadData(currentPage: number, row: number): void {
    let key = this.KEYS[this.currentKey];
    for (let i = this.datasetNew.length; i <= row; i++) {
      const define = this.data.getDataColumns(currentPage, i, key);
      this.datasetNew.push(define);
    }
    this.page = currentPage;
    this.three.ChangeMode("pick_fsec");
    this.three.ChangePage(currentPage);
    this.three_fesc.drawGradientPanel();
  }

  private tableHeightf(): string {
    const containerHeight =
      this.app.getPanelElementContentContainerHeight() - 10;
    return containerHeight.toString();
  }
  // 表高さに合わせた行数を計算する
  private rowsCount(): number {
    const containerHeight = this.app.getDialogHeight();
    return Math.round(containerHeight / 30);
  }

  options: pq.gridT.options = {
    showTop: false,
    reactive: true,
    sortable: false,
    scrollModel: {
      horizontal: true,
    },
    locale: "jp",
    height: this.tableHeightf(),
    numberCell: {
      show: true, // 行番号
      width: 40,
    },
    colModel:
      this.helper.dimension === 3 ? this.columnHeaders3D : this.columnHeaders2D,
    dataModel: {
      data: this.datasetNew,
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
