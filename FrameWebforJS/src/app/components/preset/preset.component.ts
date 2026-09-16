import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Component, OnDestroy, OnInit } from "@angular/core";
import { ResultDataService } from "src/app/providers/result-data.service";
import { ThreeService } from "../three/three.service";
import { PresetService } from "./preset.service";
import { DataHelperModule } from "src/app/providers/data-helper.module";
import { TranslateService } from "@ngx-translate/core";
import { AppComponent } from '../../app.component';
import { Router } from "@angular/router";
import { LanguagesService } from "src/app/providers/languages.service";
import { PrintCustomFsecService } from "../print/custom/print-custom-fsec/print-custom-fsec.service";
import { NgbModal } from "@ng-bootstrap/ng-bootstrap";
import { SceneService } from "../three/scene.service";
import { InputDataService } from "src/app/providers/input-data.service";
import { WaitDialogComponent } from "../wait-dialog/wait-dialog.component";
import { MenuService } from "../menu/menu.service";
import { AppService } from "src/app/app.service";

@Component({
  selector: "app-preset",
  templateUrl: "./preset.component.html",
  styleUrls: ["./preset.component.scss", "../../app.component.scss"],
})
export class PresetComponent implements OnInit, OnDestroy {
  
  constructor(
    private router: Router,
    public presetService: PresetService,
    public menuService: MenuService,
    public ResultData: ResultDataService,
    private http: HttpClient,
    public helper: DataHelperModule,
    private translate: TranslateService,
    private app: AppComponent,
    private language: LanguagesService,
    private three: ThreeService,
    private CustomFsecData: PrintCustomFsecService,
    private modalService: NgbModal,
    private scene: SceneService,
    private appService: AppService,
    public InputData: InputDataService,
    public printCustomFsecService: PrintCustomFsecService
  ) {}

  ngOnInit() {
    this.presetService.isDisabled = true;
    this.presetService.bindData();
  }

  ngOnDestroy(): void {}

  public onPageBack(): void {
    this.presetService.fileSelected = {};
    this.helper.isContentsDailogShow = false;
    this.appService.addHiddenFromElements();
  }

  openFile(){
    this.helper.isContentsDailogShow = false;
    this.appService.addHiddenFromElements();
    this.InputData.clear();
    this.ResultData.clear();
    this.CustomFsecData.clear();
    this.three.ClearData();
    // this.countArea.clear();
    const modalRef = this.modalService.open(WaitDialogComponent);
    this.http.get(this.presetService.presetLink + this.presetService.fileSelected.fileName, {responseType: 'text'}).subscribe(text => {
      this.menuService.fileName = this.presetService.fileSelected.fileName;
      this.three.fileName = this.presetService.fileSelected.fileName;
      this.printCustomFsecService.flg = undefined;
      this.ResultData.clear(); // 解析結果を削除
      const old = this.helper.dimension;
      const jsonData: {} = JSON.parse(text);
      let resultData: {} = null;
      if ("result" in jsonData) {
        resultData = jsonData["result"];
        delete jsonData["result"];
      }
      this.InputData.loadInputData(jsonData); // データを読み込む
      if (resultData !== null) {
        this.ResultData.loadResultData(resultData); // 解析結果を読み込む
        this.ResultData.isCalculated = true;
      } else {
        this.ResultData.isCalculated = false;
      }
      if (old !== this.helper.dimension) {
        this.setDimension(this.helper.dimension);
      }
      this.three.fileload();
      modalRef.close();
    });
    // this.presetService.fileSelected = {};
  }

  public setDimension(dim: number = null) {
    this.scene.changeGui(this.helper.dimension);
    if (dim === null) {
      if (this.helper.dimension === 2) {
        this.helper.dimension = 3;
      } else {
        this.helper.dimension = 2;
      }
    } else {
      this.helper.dimension = dim;
    }
    this.app.dialogClose(); // 現在表示中の画面を閉じる
    this.scene.changeGui(this.helper.dimension);
  }
}
