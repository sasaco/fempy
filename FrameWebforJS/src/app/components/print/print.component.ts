import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Component, OnDestroy, OnInit } from "@angular/core";
import { InputDataService } from "src/app/providers/input-data.service";
import { ResultDataService } from "src/app/providers/result-data.service";
import { ThreeService } from "../three/three.service";
import { PrintService } from "./print.service";
import printJS from "print-js";
import * as pako from "pako";
import { ElectronService } from "src/app/providers/electron.service";
import { MaxMinService } from "../three/max-min/max-min.service";
import { DataHelperModule } from "src/app/providers/data-helper.module";
import { TranslateService } from "@ngx-translate/core";
import packageJson from "../../../../package.json";
import { AppComponent } from "../../app.component";
import { environment } from "src/environments/environment";
import { Router } from "@angular/router";

@Component({
  selector: "app-print",
  templateUrl: "./print.component.html",
  styleUrls: ["./print.component.scss", "../../app.component.scss"],
})
export class PrintComponent implements OnInit, OnDestroy {
  public contentEditable2;
  public combineJson: any;
  public pickupJson: any;
  public id;
  private url = environment.printURL;
  public PrintScreen: string;

  constructor(
    private router: Router,
    public InputData: InputDataService,
    public printService: PrintService,
    public ResultData: ResultDataService,
    private three: ThreeService,
    private http: HttpClient,
    public max_min: MaxMinService,
    public electronService: ElectronService,
    public helper: DataHelperModule,
    private translate: TranslateService,
    private app: AppComponent
  ) { }

  private a: boolean;
  ngOnInit() {

    this.printService.selectCheckbox(0);
    this.printService.isCheckAll = false;
    this.id = document.getElementById("printButton");
    this.a = this.max_min.visible;
    this.max_min.visible = false;
    this.PrintScreen = this.translate.instant("print.PrintScreen");
    this.printService.printTargetValues = [
      { value: false }, // 軸方向力
      { value: false }, // y軸方向のせん断力
      { value: false }, // z軸方向のせん断力
      { value: false }, // ねじりモーメント
      { value: false }, // y軸回りのモーメント
      { value: false }, // z軸回りのモーメント
      { value: false }, // 変位図
    ];
  }

  ngOnDestroy(): void {
    this.max_min.visible = this.a;
    this.printService.arrFlg = [];
  }

  public options = {
    headers: new HttpHeaders({
      "Content-Type": "application/json",
      responseType: "text",
      Accept: "*/*",
    }),
  };

  // 経過時間計測用
  private last_ts: number;
  private reset_ts(): void {
    this.last_ts = performance.now();
  }

  private check_ts(): number {
    const tmp: number = this.last_ts;
    this.last_ts = performance.now();
    return this.last_ts - tmp;
  }

  public onPageBack(): void {
    this.app.dialogClose();
  }

  //OLD LOGIC SINGLE PRINT
  public onPrintPDF(): void {
    this.reset_ts();
    console.log("starting onPrintPDF...: 0 msec");

    // 印刷ケースをセッ�
    let mode = "";
    if (this.printService.printCase === "PrintScreen") {
      // 画面印刷
      mode = "default";
    } else if (this.printService.printCase === "PrintLoad") {
      // 荷重図
      mode = "PrintLoad";
    } else if (this.printService.printCase === "PrintDiagram") {
      // 断面力図
      mode = "fsec";
    } else if (this.printService.printCase === "disgDiagram") {
      // 変�
      mode = "disg";
    } else if (this.printService.printCase === "CombPrintDiagram") {
      // Combine 断面力図
      mode = "comb_fsec";
    } else if (this.printService.printCase === "PickPrintDiagram") {
      // Pickup 断面力図
      mode = "pick_fsec";
    } else if (this.printService.printCase === "ReactionDiagram") {
      // Pickup 断面力図
      mode = "reac";
    }

    this.three.ChangeMode(mode);

    this.three.mode = mode;

    // 印刷対象を取得して、セッ�
    if (
      (this.printService.flg === 14 && this.helper.dimension === 3) ||
      this.printService.flg === 10 ||
      this.printService.flg === 15
    ) {
      for (let i = 0; i < this.printService.printTargetValues.length; i++) {
        if (i < this.printService.printTargetValues.length - 1)
          this.printService.customThree.threeEditable[i] = true;
      }
    } else {
      for (let i = 0; i < this.printService.printTargetValues.length; i++) {
        const isSelected = this.printService.printTargetValues[i].value;

        this.printService.customThree.threeEditable[i] = isSelected;
      }
    }

    if (this.printService.is_printing_screen()) {
      // 図の印刷
      if (
        this.helper.dimension === 2 &&
        ["fsec", "comb_fsec", "pick_fsec", "PrintLoad"].includes(
          this.three.mode
        )
      ) {
        console.log("図の印刷: " + this.check_ts() + " msec");

        // loadingの表示
        this.loadind_enable();

        // �タの雨
        console.log("�タを集計中...: " + this.check_ts() + " msec");
        this.printService.optionList["input"].value = true;
        this.printService.optionList[this.three.mode].value = true;
        this.printService.getPrintDatas();
        console.log("�タの雨�完�: " + this.check_ts() + " msec");

        // PDFサーバ�に送る
        const json = {};
        for (var key of ["node", "member", "dimension", "language"]) {
          json[key] = this.printService.json[key];
        }

        // 印刷ケースを選�ここから
        if (this.three.mode == "fsec") {
          json["load"] = this.printService.json["load"];
          json["fsec"] = this.printService.json["fsec"];
        }
        if (this.three.mode === "comb_fsec") {
          json["combine"] = this.printService.json["combine"];
          json["fsecCombine"] = this.printService.json["fsecCombine"];
        }
        if (this.three.mode === "pick_fsec") {
          json["pickup"] = this.printService.json["pickup"];
          json["fsecPickup"] = this.printService.json["fsecPickup"];
        }
        // 印刷ケースの選�ここまで

        //console.log("印刷ケースの選�ここまで: " + this.check_ts() + " msec");

        // 印刷対象を選�ここから
        // 断面力図の種類を挮�す�
        const output = [];
        var selected: boolean = false;
        if (
          this.printService.customThree.threeEditable[5] &&
          this.printService.flg !== 15
        ) {
          // z軸周り�モーメント図
          output.push("mz");
          selected = true;
        }

        if (
          this.printService.customThree.threeEditable[1] &&
          this.printService.flg !== 15
        ) {
          // y方向�せん断力図
          output.push("fy");
          selected = true;
        }

        if (
          this.printService.customThree.threeEditable[0] &&
          this.printService.flg !== 15
        ) {
          // 軸力図
          output.push("fx");
          selected = true;
        }

        if (
          11 == this.printService.flg &&
          this.printService.customThree.threeEditable[6]
        ) {
          output.push("disg"); // 変位図
          selected = true;

          // こ�場合�変位��タも忦�にな�
          json["disg"] = this.printService.json["disg"];
          json["disgName"] = this.printService.json["disgName"];
        }

        //For printLoad
        if (this.printService.flg == 15) {
          output.push("axis"); // default load, axis
          output.push("load");
          json["load"] = this.printService.json["load"];
          json["loadName"] = this.printService.json["loadName"];
          json["fix_node"] = this.printService.json["fix_node"];
          json["element"] = this.printService.json["element"];
          json["fix_member"] = this.printService.json["fix_member"];
          selected = true;
        }

        // 印刷対象を選�ここまで

        //console.log("印刷対象を選�ここまで: " + this.check_ts() + " msec");

        if (!selected) {
          this.helper.alert(this.translate.instant("print.selectTarget"));
          this.loadind_desable();
          return;
        }

        json["pageOrientation"] = this.printService.pageOrientation;
        if (this.printService.flg == 15) {
          json["diagramInput"] = {
            layout: this.printService.printLayout,
            output,
            //single_layout_cases: [1, 2, 3,]
          };
          if (null !== this.printService.axis_scale_x.value)
            json["diagramInput"].scaleX =
              1.0 / Number(this.printService.axis_scale_x.value);

          if (null !== this.printService.axis_scale_y.value)
            json["diagramInput"].scaleY =
              1.0 / Number(this.printService.axis_scale_y.value);
        } else {
          json["diagramResult"] = {
            layout: this.printService.printLayout,
            output,
          };

          if (null !== this.printService.axis_scale_x.value)
            json["diagramResult"].scaleX =
              1.0 / Number(this.printService.axis_scale_x.value);

          if (null !== this.printService.axis_scale_y.value)
            json["diagramResult"].scaleY =
              1.0 / Number(this.printService.axis_scale_y.value);
        }

        json["ver"] = packageJson.version;
        const base64Encoded = this.getPostJson(json);

        //console.log("base64EncodedをgetPostJsonしたところまで: " + this.check_ts() + " msec");

        this.pdfPreView(base64Encoded);

        //console.log("this.pdfPreView(base64Encoded);が終� " + this.check_ts() + " msec");
      } else {
        // 3D図の印刷
        if (
          this.printService.customThree.threeEditable.filter((x) => x === true)
            .length > 0
        ) {
          console.log("3D図の印刷: " + this.check_ts() + " msec");
          this.router.navigate(["/"]);
          this.three.getCaptureImage().then((print_target) => {
            console.log(
              "getCaptureImage.then start: " + this.check_ts() + " msec"
            );

            this.printService.print_target = print_target;
            this.printService.printDocument("invoice", [""]);

            console.log(
              "getCaptureImage.then last: " + this.check_ts() + " msec"
            );
          });
        } else {
          this.helper.alert(this.translate.instant("print.selectTarget"));
          return;
        }
      }
    } else {
      // 図以外�数字だけ�ージの印刷
      // this.helper.alert(this.translate.instant("print.selectTarget"));
      // return;

      const json: any = this.printService.json;
      if (Object.keys(json).length !== 0) {
        var checkSelectItem = false;
        switch (this.printService.flg) {
          case 2: {
            var checkSelect = json.disgCombine;
            checkSelectItem = this.checkDataExisted(checkSelect);
            break;
          }
          case 3: {
            var checkSelect = json.disgPickup;
            checkSelectItem = this.checkDataExisted(checkSelect);
            break;
          }
          case 5: {
            var checkSelect = json.reacCombine;
            checkSelectItem = this.checkDataExisted(checkSelect);
            break;
          }
          case 6: {
            var checkSelect = json.reacPickup;
            checkSelectItem = this.checkDataExisted(checkSelect);
            break;
          }
          case 7: {
            var checkSelect = json.fsec;
            checkSelectItem = this.checkDataExisted(checkSelect);
            break;
          }
          case 8: {
            var checkSelect = json.fsecCombine;
            checkSelectItem = this.checkDataExisted(checkSelect);
            if (!checkSelectItem) {
              const checkChild = Object.values(checkSelect).map((v) => {
                return Object.values(v)
                  .map((v2) => Object.values(v2)?.length > 0)
                  .filter((v2) => v2 === true);
              });
              checkSelectItem = this.checkDataExisted(checkChild);
            }
            break;
          }
          case 9: {
            var checkSelect = json.fsecPickup;
            checkSelectItem = this.checkDataExisted(checkSelect);
            if (!checkSelectItem) {
              const checkChild = Object.values(checkSelect).map((v) => {
                return Object.values(v)
                  .map((v2) => Object.values(v2)?.length > 0)
                  .filter((v2) => v2 === true);
              });
              checkSelectItem = this.checkDataExisted(checkChild);
            }
            break;
          }
        }

        if (checkSelectItem) {
          this.helper.alert(this.translate.instant("print.selectTarget"));
          return;
        }

        // loadingの表示
        this.loadind_enable();
        // PDFサーバ�に送る
        this.pdfPreView(this.getPostJson(json));
        this.router.navigate(["/"]);
      }
    }
  }

  //NEW LOGIC MULTI PRINT
  public async onPrintPDFNew(): Promise<void> {
    if (this.helper.dimension === 3) {
      const dataCheckCalculation = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];
      const arrFlg = this.printService.arrFlg;
      const hasPrintCalculation = arrFlg.every(data => dataCheckCalculation.includes(data));
      let json: any = {}

      if (arrFlg.includes(0)) {
        json["hasPrintInputData"] = true;
      }
      this.printService.getPrintDatas();
      json = { ...json, ...this.printService.json };

      if (Object.keys(json).length !== 0) {
        var checkSelectItem = false;
        switch (this.printService.flg) {
          case 2: {
            var checkSelect = json.disgCombine;
            checkSelectItem = this.checkDataExisted(checkSelect);
            break;
          }
          case 3: {
            var checkSelect = json.disgPickup;
            checkSelectItem = this.checkDataExisted(checkSelect);
            break;
          }
          case 5: {
            var checkSelect = json.reacCombine;
            checkSelectItem = this.checkDataExisted(checkSelect);
            break;
          }
          case 6: {
            var checkSelect = json.reacPickup;
            checkSelectItem = this.checkDataExisted(checkSelect);
            break;
          }
          case 7: {
            var checkSelect = json.fsec;
            checkSelectItem = this.checkDataExisted(checkSelect);
            break;
          }
          case 8: {
            var checkSelect = json.fsecCombine;
            checkSelectItem = this.checkDataExisted(checkSelect);
            if (!checkSelectItem) {
              const checkChild = Object.values(checkSelect).map((v) => {
                return Object.values(v)
                  .map((v2) => Object.values(v2)?.length > 0)
                  .filter((v2) => v2 === true);
              });
              checkSelectItem = this.checkDataExisted(checkChild);
            }
            break;
          }
          case 9: {
            var checkSelect = json.fsecPickup;
            checkSelectItem = this.checkDataExisted(checkSelect);
            if (!checkSelectItem) {
              const checkChild = Object.values(checkSelect).map((v) => {
                return Object.values(v)
                  .map((v2) => Object.values(v2)?.length > 0)
                  .filter((v2) => v2 === true);
              });
              checkSelectItem = this.checkDataExisted(checkChild);
            }
            break;
          }
        }
        if (checkSelectItem) {
          this.helper.alert(this.translate.instant("print.selectTarget"));
          return;
        }
      }

      if (this.printService.arrFlg.length > 1) {
        this.printService.printTargetValues = [
          { value: true },
          { value: true },
          { value: false },
          { value: false },
          { value: false },
          { value: true },
          { value: false },
        ];
      }
      this.printService.optionList["input"].value = true;
      let mode = "";
      let modes = []

      for (let key of this.printService.printCases) {
        this.printService.optionList[key].value = true;
        if (key === "PrintLoad") {
          mode = "PrintLoad";
        }
        if (key === "PrintDiagram") {
          mode = "fsec";
        }
        if (key === "disgDiagram") {
          mode = "disg"
        }
        if (key === "CombPrintDiagram") {
          mode = "comb_fsec";
        }
        if (key === "PickPrintDiagram") {
          mode = "pick_fsec";
        }
        modes.push(mode)
        this.printService.optionList[mode].value = true;
      }

      // 印刷対象を取得して、セッ�
      if (
        this.printService.flg === 14 ||
        this.printService.flg === 10 ||
        this.printService.flg === 15
      ) {
        for (let i = 0; i < this.printService.printTargetValues.length; i++) {
          if (i < this.printService.printTargetValues.length - 1)
            this.printService.customThree.threeEditable[i] = true;
        }
      } else {
        for (let i = 0; i < this.printService.printTargetValues.length; i++) {
          const isSelected = this.printService.printTargetValues[i].value;

          this.printService.customThree.threeEditable[i] = isSelected;
        }
      }

      if (
        this.printService.printCases.length !== 0 &&
        this.printService.printCase === ""
      ) {
        // 図の印刷
        if (
          this.printService.customThree.threeEditable.filter((x) => x === true).length > 0
        ) {
          this.printService.print_target = []
          if (modes.length !== 0) {
            this.router.navigate(["/"]);
            this.loadind_enable();
            let count = 0;
              const capturePromises = modes.map(mode => {
                let dataMode = mode
                if(mode==="PrintLoad"){
                  dataMode="print_load"
                }
                this.three.mode = dataMode
                count += this.three.getTotalCaptureImage()
              });
              if(count < 120){
                setTimeout(() => {
                            const capturePromises = modes.map(mode => {
                              let dataMode = mode
                              if(mode==="PrintLoad"){
                                dataMode="print_load"
                              }
                              this.three.ChangeMode(dataMode);
                              this.three.mode = dataMode
                              return this.three.getCaptureImage().then((print_target) => {
                                console.log("getCaptureImage.then start: " + this.check_ts() + " msec");
                                print_target["mode"] = dataMode;
                                this.printService.print_target.push(print_target);
                                console.log("getCaptureImage.then last: " + this.check_ts() + " msec");
                              });
                            });
                            Promise.all(capturePromises)
                              .then(() => {
                                json["PrintScreenData"] = this.printService.print_target
                                this.pdfPreView(this.getPostJson(json));
                              })
                              .catch(error => {
                                console.error("Error:", error);
                              });
                          }, 1000)
              }
              else{
                const isConfirm = await this.helper.confirm(this.translate.instant("app.alertMultiPrinting"));
                if (!isConfirm) {
                  setTimeout(() => {
                    const capturePromises = modes.map(mode => {
                      let dataMode = mode
                      if(mode==="PrintLoad"){
                        dataMode="print_load"
                      }
                      this.three.ChangeMode(dataMode);
                      this.three.mode = dataMode
                      return this.three.getCaptureImage().then((print_target) => {
                        console.log("getCaptureImage.then start: " + this.check_ts() + " msec");
                        print_target["mode"] = dataMode;
                        this.printService.print_target.push(print_target);
                        console.log("getCaptureImage.then last: " + this.check_ts() + " msec");
                      });
                    });
                    Promise.all(capturePromises)
                      .then(() => {
                        json["PrintScreenData"] = this.printService.print_target
                        this.pdfPreView(this.getPostJson(json));
                      })
                      .catch(error => {
                        console.error("Error:", error);
                      });
                  }, 1000)
                }else{
                  this.loadind_desable();
                }
              }
          }

        }
        else {
          this.helper.alert(this.translate.instant("print.selectTarget"));
          return;
        }
      }
      // loadingの表示
      if (hasPrintCalculation) {
        this.router.navigate(["/"]);
        this.loadind_enable();
        // PDFサーバ�に送る
        setTimeout(() => {
          this.pdfPreView(this.getPostJson(json));
        }, 1000)
      }
    } else {
      let json: any = {};
      const dataCheck = [1, 2, 3, 4, 5, 6, 7, 8, 9];
      json["hasPrintCalculation"] = false;
      json["hasPrintInputData"] = false;
      let count = 0;
      //print input
      if (this.printService.arrFlg.includes(0)) {
        json["hasPrintInputData"] = true;
        count++;
      }
      for (let data of this.printService.arrFlg) {
        if (dataCheck.includes(data)) {
          json["hasPrintCalculation"] = true;
          count++;
          break;
        }
      }
      if (count != 0) {
        this.printService.getPrintDatas();
        json = { ...json, ...this.printService.json };
      }
      // data Calculation Results
      if (Object.keys(json).length !== 0) {
        var checkSelectItem = false;
        switch (this.printService.flg) {
          case 2: {
            var checkSelect = json.disgCombine;
            checkSelectItem = this.checkDataExisted(checkSelect);
            break;
          }
          case 3: {
            var checkSelect = json.disgPickup;
            checkSelectItem = this.checkDataExisted(checkSelect);
            break;
          }
          case 5: {
            var checkSelect = json.reacCombine;
            checkSelectItem = this.checkDataExisted(checkSelect);
            break;
          }
          case 6: {
            var checkSelect = json.reacPickup;
            checkSelectItem = this.checkDataExisted(checkSelect);
            break;
          }
          case 7: {
            var checkSelect = json.fsec;
            checkSelectItem = this.checkDataExisted(checkSelect);
            break;
          }
          case 8: {
            var checkSelect = json.fsecCombine;
            checkSelectItem = this.checkDataExisted(checkSelect);
            if (!checkSelectItem) {
              const checkChild = Object.values(checkSelect).map((v) => {
                return Object.values(v)
                  .map((v2) => Object.values(v2)?.length > 0)
                  .filter((v2) => v2 === true);
              });
              checkSelectItem = this.checkDataExisted(checkChild);
            }
            break;
          }
          case 9: {
            var checkSelect = json.fsecPickup;
            checkSelectItem = this.checkDataExisted(checkSelect);
            if (!checkSelectItem) {
              const checkChild = Object.values(checkSelect).map((v) => {
                return Object.values(v)
                  .map((v2) => Object.values(v2)?.length > 0)
                  .filter((v2) => v2 === true);
              });
              checkSelectItem = this.checkDataExisted(checkChild);
            }
            break;
          }
        }

        if (checkSelectItem) {
          this.helper.alert(this.translate.instant("print.selectTarget"));
          return;
        }
      }
      //Print screen and Reaction diagram
      if (
        this.printService.printCases.length === 0 &&
        this.printService.printCase !== ""
      ) {
        json = {};
        if (this.printService.flg === 10) {
          for (let i = 0; i < this.printService.printTargetValues.length; i++) {
            if (i < this.printService.printTargetValues.length - 1)
              this.printService.customThree.threeEditable[i] = true;
          }
        } else {
          for (let i = 0; i < this.printService.printTargetValues.length; i++) {
            const isSelected = this.printService.printTargetValues[i].value;

            this.printService.customThree.threeEditable[i] = isSelected;
          }
        }
        if (
          this.printService.customThree.threeEditable.filter((x) => x === true)
            .length > 0
        ) {
          console.log("3D図の印刷: " + this.check_ts() + " msec");
          this.router.navigate(["/"]);
          this.three.getCaptureImage().then((print_target) => {
            console.log(
              "getCaptureImage.then start: " + this.check_ts() + " msec"
            );

            this.printService.print_target = print_target;
            this.printService.printDocument("invoice", [""]);

            console.log(
              "getCaptureImage.then last: " + this.check_ts() + " msec"
            );
          });
        } else {
          this.helper.alert(this.translate.instant("print.selectTarget"));
          return;
        }
        return;
      }
      //data Load diagram, Sectional force diagram, Combine force diagram, Pickup force diagram
      if (
        this.printService.printCases.length !== 0 &&
        this.printService.printCase === ""
      ) {
        //has multi check print screen
        if (this.printService.arrFlg.length > 1) {
          this.printService.printTargetValues = [
            { value: true },
            { value: true },
            { value: false },
            { value: false },
            { value: false },
            { value: true },
            { value: false },
          ];
        }
        this.printService.optionList["input"].value = true;
        let mode = "";
        for (let key of this.printService.printCases) {
          this.printService.optionList[key].value = true;
          if (key === "PrintLoad") {
            mode = "PrintLoad";
          }
          if (key === "PrintDiagram") {
            mode = "fsec";

            //set to get value of disg and disgname json
            this.printService.printTargetValues[6].value = true;
          }
          if (key === "CombPrintDiagram") {
            mode = "comb_fsec";
          }
          if (key === "PickPrintDiagram") {
            mode = "pick_fsec";
          }
          this.printService.optionList[mode].value = true;
        }
        this.printService.getPrintDatas();
        if (this.printService.arrFlg.length > 1) {
          // multiple check print screen
          let diagramResult = {
            layout: "single",
            output: ["mz", "fy", "fx"],
          };
          let diagramInput = {
            layout: "single",
            output: ["axis", "load"],
          };
          let initData = {
            pageOrientation: "Vertical",
            ver: packageJson.version,
          };
          for (var key of ["node", "member", "dimension", "language"]) {
            initData[key] = this.printService.json[key];
          }
          json = { ...json, ...initData };
          for (let key of this.printService.printCases) {
            if (key === "PrintLoad") {
              (json["load"] = this.printService.json["load"]  ?? {}),
              (json["fix_node"] = this.printService.json["fix_node"] ?? {}),
              (json["element"] = this.printService.json["element"] ?? {}),
              (json["fix_member"] = this.printService.json["fix_member"] ?? {}),
              (json["PrintLoad"] = {
                diagramInput,
              });
            }
            if (key === "PrintDiagram") {
              let diagramResultTemp = {
                layout: "single",
                output: ["mz", "fy", "fx", "disg"],
              };
              (json["load"] = this.printService.json["load"] ?? {}),
              (json["fsec"] = this.printService.json["fsec"] ?? {}),
              (json["PrintDiagram"] = {
                disg: this.printService.json["disg"] ?? {},
                disgName: this.printService.json["disgName"]?? {},
                diagramResult: diagramResultTemp,

              });
            }
            if (key === "CombPrintDiagram") {
              (json["combine"] = this.printService.json["combine"] ?? {}),
              (json["fsecCombine"] = this.printService.json["fsecCombine"] ?? {}),
              (json["CombPrintDiagram"] = {
                diagramResult,

              });
            }
            if (key === "PickPrintDiagram") {
              (json["pickup"] = this.printService.json["pickup"] ?? {}),
              (json["fsecPickup"] = this.printService.json["fsecPickup"] ?? {}),
              (json["PickPrintDiagram"] = {
                diagramResult,

              });
            }
          }
        } else {
          // single check print screen
          var selected: boolean = false;
          let initData = { ver: packageJson.version };
          const output = [];
          for (var key of ["node", "member", "dimension", "language"]) {
            initData[key] = this.printService.json[key];
          }
          json = { ...json, ...initData };
          json["pageOrientation"] =
            this.printService.pageOrientation;
          let keyScreen = "";
          for (let key of this.printService.printCases) {
            keyScreen = key;
            if (key === "PrintLoad") {
              (json["load"] = this.printService.json["load"] ?? {}),
              (json["fix_node"] = this.printService.json["fix_node"] ?? {}),
              (json["element"] = this.printService.json["element"] ?? {}),
              (json["fix_member"] = this.printService.json["fix_member"] ?? {}),
              (json["PrintLoad"] = {});
            }
            if (key === "PrintDiagram") {
              (json["load"] = this.printService.json["load"] ?? {}),
              (json["fsec"] = this.printService.json["fsec"] ?? {}),
              (json["PrintDiagram"] = {});
            }
            if (key === "CombPrintDiagram") {
              (json["combine"] = this.printService.json["combine"] ?? {}),
              (json["fsecCombine"] = this.printService.json["fsecCombine"] ?? {}),
              (json["CombPrintDiagram"] = {});
            }
            if (key === "PickPrintDiagram") {
              (json["pickup"] = this.printService.json["pickup"] ?? {}),
              (json["fsecPickup"] = this.printService.json["fsecPickup"] ?? {}),
              (json["PickPrintDiagram"] = {});
            }
          }
          json[keyScreen]["pageOrientation"] =
            this.printService.pageOrientation;
          if (keyScreen === "PrintLoad") {
            for (
              let i = 0;
              i < this.printService.printTargetValues.length;
              i++
            ) {
              if (i < this.printService.printTargetValues.length - 1)
                this.printService.customThree.threeEditable[i] = true;
            }
            output.push("axis"); // default load, axis
            output.push("load");
            selected = true;
            json[keyScreen]["diagramInput"] = {
              layout: this.printService.printLayout,
              output,
            };
            if (null !== this.printService.axis_scale_x.value)
              json[keyScreen]["diagramInput"].scaleX =
                1.0 / Number(this.printService.axis_scale_x.value);

            if (null !== this.printService.axis_scale_y.value)
              json[keyScreen]["diagramInput"].scaleY =
                1.0 / Number(this.printService.axis_scale_y.value);
          } else {
            for (
              let i = 0;
              i < this.printService.printTargetValues.length;
              i++
            ) {
              const isSelected = this.printService.printTargetValues[i].value;

              this.printService.customThree.threeEditable[i] = isSelected;
            }
            if (this.printService.customThree.threeEditable[5]) {
              output.push("mz");
              selected = true;
            }

            if (this.printService.customThree.threeEditable[1]) {
              output.push("fy");
              selected = true;
            }

            if (this.printService.customThree.threeEditable[0]) {
              output.push("fx");
              selected = true;
            }
            if (
              keyScreen === "PrintDiagram" &&
              this.printService.customThree.threeEditable[6]
            ) {
              output.push("disg");
              selected = true;
              json[keyScreen]["disg"] = this.printService.json["disg"] ?? {};
              json[keyScreen]["disgName"] = this.printService.json["disgName"] ?? {};
            }
            json[keyScreen]["diagramResult"] = {
              layout: this.printService.printLayout,
              output,
            };

            if (null !== this.printService.axis_scale_x.value)
              json[keyScreen]["diagramResult"].scaleX =
                1.0 / Number(this.printService.axis_scale_x.value);

            if (null !== this.printService.axis_scale_y.value)
              json[keyScreen]["diagramResult"].scaleY =
                1.0 / Number(this.printService.axis_scale_y.value);
          }
          if (!selected) {
            this.helper.alert(this.translate.instant("print.selectTarget"));
            this.loadind_desable();
            return;
          }
        }
      }
      this.router.navigate(["/"]);
      this.loadind_enable();
      setTimeout(() => {
        this.pdfPreView(this.getPostJson(json));
      }, 1000)
    }
  }

  private checkDataExisted(data: any): boolean {
    return Object.values(data).every((v: any) => Object.keys(v).length === 0);
  }

  private pdfPreView(base64Encoded: string): void {
    console.log("pdfPreView を実行中...");

    const maxFileSize = 100 * 1024 * 1024
    const size = this.getFileSizeInMb(base64Encoded)
    if (size >= maxFileSize) {
      this.loadind_desable();
      this.helper.alert(this.translate.instant("message.print-max"));
      return;
    }

    this.http.post(this.url, base64Encoded, this.options).subscribe(
      (response) => {
        console.log("pdfPreView が完�");

        this.showPDF(response.toString());
      },
      (err) => {
        try {
          if ("error" in err) {
            if ("text" in err.error) {
              this.showPDF(err.error.text.toString());
              this.router.navigate(["/"]);
              return;
            }
          }
        } catch (e) { }
        this.loadind_desable();
        console.log(err);
        this.helper.alert(this.translate.instant("message.print"));
      }
    );
  }

  private getPostJson(json: any): string {
    console.log("getPostJson を実行中...");

    const jsonStr = JSON.stringify(json);
    // pako を使ってgzip圧縮する
    const compressed = pako.gzip(jsonStr);
    //btoa() を使ってBase64エンコードす�
    const base64Encoded = btoa(compressed);

    console.log("getPostJson が完�");
    return base64Encoded;
  }

  private loadind_enable(): void {
    // loadingの表示
    document.getElementById("print-loading").style.display = "block";
    this.id.setAttribute("disabled", "true");
    this.id.style.opacity = "0.7";
  }

  // finally皁�処�
  // loadingの表示終�
  private loadind_desable() {
    document.getElementById("print-loading").style.display = "none";
    const id = document.getElementById("printButton");
    this.id.removeAttribute("disabled");
    this.id.style.opacity = "";
  }

  private showPDF(base64: string) {
    this.loadind_desable();

    if (this.electronService.isElectron) {
      // electron の場�
      const byteCharacters = atob(base64);
      let byteNumbers = new Array(byteCharacters.length);
      for (let i = 0; i < byteCharacters.length; i++) {
        byteNumbers[i] = byteCharacters.charCodeAt(i);
      }
      let byteArray = new Uint8Array(byteNumbers);
      let file = new Blob([byteArray], { type: "application/pdf;base64" });
      let fileURL = URL.createObjectURL(file);
      window.open(fileURL, "_blank");
      this.router.navigate(["/"]);
    } else {
      //Webアプリの場�
      printJS({ printable: base64, type: "pdf", base64: true });
    }
  }

  private getFileSizeInMb(base64: string) {
    const encoder = new TextEncoder();
    const encoded = encoder.encode(base64);
    const bytes = encoded.byteLength;
    return bytes;
  }
}
