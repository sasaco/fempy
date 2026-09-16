import { Component, OnInit, EventEmitter, Output, ViewChild, AfterViewInit, ElementRef } from "@angular/core";
import { Router } from "@angular/router";
import { UserInfoService } from "./providers/user-info.service";
import { ResultDataService } from "./providers/result-data.service";
import { PrintService } from "./components/print/print.service";

import { ResultFsecService } from "./components/result/result-fsec/result-fsec.service";
import { ResultDisgService } from "./components/result/result-disg/result-disg.service";
import { ResultReacService } from "./components/result/result-reac/result-reac.service";
import { DataHelperModule } from "./providers/data-helper.module";
import { TranslateService } from "@ngx-translate/core";

import html2canvas from "html2canvas";
import * as pako from "pako";
import { NgbModal, NgbModalRef } from "@ng-bootstrap/ng-bootstrap";
import { WaitDialogComponent } from "./components/wait-dialog/wait-dialog.component";
import { Auth, getAuth, signInWithEmailAndPassword } from '@angular/fire/auth';
import { InputDataService } from "./providers/input-data.service";
import { environment } from "src/environments/environment";
import { SheetComponent } from "./components/input/sheet/sheet.component";
import { HttpClient, HttpHeaders } from "@angular/common/http";
import { LanguagesService } from './providers/languages.service';
import { AppService } from "./app.service";

@Component({
  selector: "app-root",
  templateUrl: "./app.component.html",
  styleUrls: ["./app.component.scss"],
})
export class AppComponent implements OnInit, AfterViewInit {
  @Output() pagerEvent = new EventEmitter<number>();
  @ViewChild("grid") grid: SheetComponent;
  translateY: string = "translateY(0)";
  btnReac!: string;
  isToggled: Boolean = true;
  eventFromChild: number;
  constructor(
    private _router: Router,
    public ResultData: ResultDataService,
    public printService: PrintService,
    public helper: DataHelperModule,
    public fsec: ResultFsecService,
    public disg: ResultDisgService,
    public reac: ResultReacService,
    public print: PrintService,
    private translate: TranslateService,
    private modalService: NgbModal,
    public auth: Auth,
    private InputData: InputDataService,
    private http: HttpClient,
    public user: UserInfoService,
    public language: LanguagesService,
    public appService: AppService,
    private elem: ElementRef
  ) {
    this.translate.setDefaultLang("ja");
    if(window.sessionStorage.getItem("openStart") === null) window.sessionStorage.setItem("openStart", "1")
  }
  ngAfterViewInit(): void {
    this.language.tranText();
  }

  ngOnInit() {
    this.helper.isContentsDailogShow = false;
  }

  onScroll(event): void {
    let scrollTop = event.srcElement.scrollTop;
    this.translateY = "translateY(-" + scrollTop + "px)";
    // Interpret the scroll event
    // Do stuff on inner div scroll
  }

  // 計算結果表示ボタンを無効にする
  public disableResultButton() {
    this.fsec.clear();
    this.disg.clear();
    this.reac.clear();
  }

  public dialogClose(): void {
    this.appService.dialogClose();
  }

  public contentsDailogShow(id): void {
    this.fsec.clearGradient();
    this.deactiveButtons();
    document.getElementById(id).classList.add("active");
    this.changePosition();

    if (this.isSameURL(id)) {
      this.toggleContentsDailogShow();
      this.toggleVisibilityOfElements();
    } else {
      this.removeHiddenFromElements();
      this.setContentsDailogShow(true);
    }
    this.print.mode = id;
  }

  private isSameURL(id: number): boolean {
    return id === this.print.mode;
  }
  private toggleContentsDailogShow(): void {
    this.helper.isContentsDailogShow = !this.helper.isContentsDailogShow;
  }
  private toggleVisibilityOfElements(): void {
    this.toggleElementVisibility(".panel-element-content-container");
    this.toggleElementVisibility("#my_dock_manager");
    this.toggleElementVisibility(".dialog-floating");
  }
  private toggleElementVisibility(selector: string): void {
    const element = document.querySelector(selector);
    if (element) {
      element.classList.toggle("hidden");
    }
  }
  private setContentsDailogShow(state: boolean): void {
    this.helper.isContentsDailogShow = state;
  }
  private removeHiddenFromElements(): void {
    this.removeHiddenFromClass(".panel-element-content-container");
    this.removeHiddenFromClass("#my_dock_manager");
    this.removeHiddenFromClass(".dialog-floating");
  }
  private removeHiddenFromClass(selector: string): void {
    const element = document.querySelector(selector);
    if (element) {
      element.classList.remove("hidden");
    }
  }
  // フローティングウィンドウの位置
  public dragPosition = { x: 0, y: 0 };
  public changePosition() {
    this.dragPosition = {
      x: this.dragPosition.x,
      y: this.dragPosition.y,
    };
  }

  // アクティブになっているボタンを全て非アクティブにする
  deactiveButtons() {
    for (let i = 0; i <= 13; i++) {
      const data = document.getElementById(i + "");
      if (data != null) {
        if (data.classList.contains("active")) {
          data.classList.remove("active");
        }
      }
    }
  }

  // contents-dialogの高さをウィンドウサイズに合わせる
  setDialogHeight() {
    setTimeout(function () {
      const dialog = document.getElementById("contents-dialog-id");
      // ヘッダ領域を取得
      const header = document.getElementsByClassName("header");
      const container = document.getElementsByClassName("container");
      const headerSize =
        container[0].clientHeight + header[0].clientHeight + 50;
      dialog.style.height = window.innerHeight - headerSize + "px";
      console.log("dialog height:" + dialog.style.height);
    }, 100);
  }

  public getDialogHeight(): number {
    const dialog = document.getElementById("contents-dialog-id");
    let dialogHeight = parseFloat(dialog.style.height); // ヘッダー高さを引く
    if (isNaN(dialogHeight)) {
      dialogHeight = window.innerHeight - 84; // メニューとヘッダー高さを引く
    } else {
      dialogHeight -= 80;
    }
    return dialogHeight;
  }

  public getPanelElementContentContainerHeight(): number {
    let dialog = (document.getElementsByClassName("panel-element-content-container"))[0];
    if (dialog instanceof HTMLElement) {
      let dialogHeight = parseFloat(dialog.style.height); // ヘッダー高さを引く
      if (isNaN(dialogHeight)) {
        dialogHeight = window.innerHeight - 84; // メニューとヘッダー高さを引く
      } else {
        dialogHeight -= 80;
      }
      return dialogHeight;
    }
    return 0;
  }

  toggle(): void {
    this.isToggled = !this.isToggled;
  }
  // 計算
  public async calcrate(): Promise<void> {
    // const user = await this.auth.currentUser;
    const user = this.user.userProfile;
    if (!user) {
      this.helper.alert(this.translate.instant("menu.P_login"));
      return;
    }
    
    const jsonData: {} = this.InputData.getInputJson(0);

    if ("error" in jsonData) {
      this.helper.alert(jsonData["error"] as string);
      return;
    }

    const isConfirm = await this.helper.confirm(this.translate.instant("menu.calc_start"));
    if (!isConfirm) {
      return;
    }
    const modalRef = this.modalService.open(WaitDialogComponent, {
      backdrop: 'static'
    });
    jsonData["uid"] = user.uid;
    jsonData["production"] = environment.production;

    this.ResultData.clear(); // 解析結果情報をクリア

    this.post_compress(jsonData, modalRef);
  }

  private post_compress(jsonData: {}, modalRef: NgbModalRef) {
    const url = environment.calcURL;

    // json string にする
    const json = JSON.stringify(jsonData, null, 0);
    console.log(json);
    // pako を使ってgzip圧縮する
    const compressed = pako.gzip(json);
    //btoa() を使ってBase64エンコードする
    const base64Encoded = btoa(compressed);

    this.http
      .post(url, base64Encoded, {
        headers: new HttpHeaders({
          "Content-Type": "application/json",
          "Content-Encoding": "gzip,base64",
        }),
        responseType: "text",
      })
      .subscribe(
        (response) => {
          // 通信成功時の処理（成功コールバック）
          console.log(this.translate.instant("menu.success"));
          let check = true;
          try {
            if (response.includes("error") || response.includes("exceeded")) {
              throw response;
            }
            // Decode base64 (convert ascii to binary)
            const strData = atob(response);
            // Convert binary string to character-number array
            const charData = strData.split("").map(function (x) {
              return x.charCodeAt(0);
            });
            // Turn number array into byte-array
            const binData = new Uint8Array(charData);
            // Pako magic
            const json = pako.ungzip(binData, { to: "string" });

            const jsonData = JSON.parse(json);
            // サーバーのレスポンスを集計する
            console.log(jsonData);
            if ("error" in jsonData) {
              throw jsonData.error;
            }

            // ポイントの処理
            const _jsonData = {};
            for (const key of Object.keys(jsonData)) {
              if ((typeof jsonData[key]).toLowerCase() === "number") {
                this.user[key] = jsonData[key];
              } else {
                _jsonData[key] = jsonData[key];
              }
            }

            this.InputData.getResult(jsonData);

            // 解析結果を集計する
            this.ResultData.loadResultData(_jsonData);
            this.ResultData.isCalculated = true;

          } catch (e) {
            // "error" または "exceeded"を含む場合のエラー
            check = false;  // 計算異常終了フラグ
            if (e.includes("error") && this.language.browserLang == "ja") {
              // "error" を含み言語設定が日本語の場合のみエラー内容を出力
              const errJson = JSON.parse(e)
              this.helper.alert(errJson.error)
            } else {
              // 上記以外は一律に計算エラーメッセージを出力
              this.helper.alert(this.translate.instant("message.calc"));
            }

          } finally {          
            // 終了処理
            if (check) {
              // 計算正常終了時のメッセージ出力
              this.helper.alert(
                this.translate.instant("menu.calc_complete")
              // ポイント消費量は現在使用していないためコメントアウト
              /*this.user.deduct_points
              + this.translate.instant("menu.deduct_points")
              + this.user.new_points
              + this.translate.instant("menu.new_points")*/
              );
            }
            modalRef.close();  // モーダルダイアログを消す
          }
        },
        (error) => {
          // 通信失敗時のエラー
          this.helper.alert(this.translate.instant("message.transmission-error"));  // エラーメッセージの出力
          console.error(error);  // コンソールへのログ出力
          modalRef.close();  // モーダルダイアログを消す
        }
      );
  }

  onPagerEvent(eventData: number) {
    this.pagerEvent.emit(eventData);
  }
  onReceiveEventFromChild(event: number) {
    this.eventFromChild = event;
  }
  getDisgLink(): string {
    if (!this.disg.isCalculated) {
      return this._router.url;
    }
    let link: string;
    switch (this.ResultData.case) {
      case "basic":
        link = "./result-disg";
        break;
      case "comb":
        link = "./result-comb_disg";
        break;
      case "pic":
        link = "./result-pic_disg";
        break;
      default:
        link = "";
    }
    return link;
  }
  getReacLink(): string {
    if (!this.disg.isCalculated) {
      return this._router.url;
    }
    let link: string;
    switch (this.ResultData.case) {
      case "basic":
        link = "./result-reac";
        break;
      case "comb":
        link = "./result-comb_reac";
        break;
      case "pic":
        link = "./result-pic_reac";
        break;
      default:
        link = "";
    }
    return link;
  }
  getFsecLink(): string {
    if (!this.disg.isCalculated) {
      return this._router.url;
    }
    let link: string;
    switch (this.ResultData.case) {
      case "basic":
        link = "./result-fsec";
        break;
      case "comb":
        link = "./result-comb_fsec";
        break;
      case "pic":
        link = "./result-pic_fsec";
        break;
      default:
        link = "";
    }
    return link;
  }

  // public onPrintInvoice() {
  //   const invoiceIds = ["101", "102"];
  //   this.printService.printDocument("invoice", invoiceIds);
  // }
}

window.onload = function () {
  //HTML内に画像を表示
  html2canvas(document.getElementById("target")).then(function (canvas) {
    //imgタグのsrcの中に、html2canvasがレンダリングした画像を指定する。
    var imgData = canvas.toDataURL();
    var pic = document.getElementById("result");
    pic.setAttribute("src", "imgData");
  });
};
