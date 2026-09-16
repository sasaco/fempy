import { Injectable } from "@angular/core";
import { ThreeService } from "../three/three.service";
import { LanguagesService } from "src/app/providers/languages.service";
import { DataHelperModule } from "src/app/providers/data-helper.module";
import { InputDataService } from "src/app/providers/input-data.service";
import { ResultDataService } from "src/app/providers/result-data.service";
import { PrintCustomFsecService } from "../print/custom/print-custom-fsec/print-custom-fsec.service";
import { PrintService } from "../print/print.service";
import { TranslateService } from "@ngx-translate/core";
import { SceneService } from "../three/scene.service";
import { NgbModal } from "@ng-bootstrap/ng-bootstrap";
import { WaitDialogComponent } from "../wait-dialog/wait-dialog.component";
import internal from "stream";

@Injectable({
  providedIn: "root",
})
export class MenuService {
  public fileName = "";
  constructor(
    private helper: DataHelperModule,
    public language: LanguagesService,
    private three: ThreeService,
    private InputData: InputDataService,
    public ResultData: ResultDataService,
    private CustomFsecData: PrintCustomFsecService,
    private PrintData: PrintService,
    private translate: TranslateService,
    public printCustomFsecService: PrintCustomFsecService,
    private scene: SceneService,
    private modalService: NgbModal,
  ) {
    this.fileName = '';
  }

  async renew(): Promise<void> {
      this.InputData.clear();
      this.ResultData.clear();
      this.PrintData.clear();
      this.CustomFsecData.clear();
      this.three.ClearData();
      this.fileName = "";
      this.three.fileName = "";
      this.three.mode = "";
  
      // "新規作成"のとき、印刷パネルのフラグをリセットする
      this.printCustomFsecService.flg = undefined;
  }
  open(evt){
    this.InputData.clear();
    this.ResultData.clear();
    this.PrintData.clear();
    this.CustomFsecData.clear();
    this.three.ClearData();
    // this.countArea.clear();
    const modalRef = this.modalService.open(WaitDialogComponent);

    const file = evt.target.files[0];
    this.fileName =file.name;
    this.three.fileName = file.name;
    evt.target.value = "";
    this.fileToText(file)
      .then((text) => {
        // "ファイルを開く"のとき、印刷パネルのフラグをリセットする
        this.printCustomFsecService.flg = undefined;

        // this.app.dialogClose(); // 現在表示中の画面を閉じる
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
      })
      .catch((err) => {
        this.helper.alert(err);
        modalRef.close();
      });
  }

  public fileToText(file): any {
    const reader = new FileReader();
    reader.readAsText(file);
    return new Promise((resolve, reject) => {
      reader.onload = () => {
        resolve(reader.result);
      };
      reader.onerror = () => {
        reject(reader.error);
      };
    });
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
    // this.app.dialogClose(); // 現在表示中の画面を閉じる
    this.scene.changeGui(this.helper.dimension);
  }

  public getFileNameFromUrl(url) {
    return url.replace(/^.*[\\/]/, '')
  }

  public shortenFilename(filename: string, maxLength: number = 30) {
    let tempName = filename;
    tempName = this.getFileNameFromUrl(tempName);
    return tempName.length <= maxLength ? tempName : '...'+ tempName.slice(tempName.length - maxLength);
  }
}
