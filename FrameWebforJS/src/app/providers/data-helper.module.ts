import { NgModule } from "@angular/core";
import { ElectronService } from "src/app/providers/electron.service";
import { NgbModal } from "@ng-bootstrap/ng-bootstrap";
import { AlertDialogComponent } from "../components/alert-dialog/alert-dialog.component";
import { Subject } from "rxjs";

@NgModule({
  imports: [],
  exports: [],
})
export class DataHelperModule {

  constructor(
    public electronService: ElectronService,
    private modalService: NgbModal
  ) {}

  // ３次元解析=3, ２次元解析=2
  public dimension: number;

  //remember dimension when open file
  public dimensionInit: number;
  public isContentsDailogShow: boolean;

  // 連行荷重LL判定フラグ
  private _LL_page: boolean = false;
  public LL_pageChange$ = new Subject<boolean>();

  public get LL_page(): boolean {
    return this._LL_page;
  }

  public set LL_page(value: boolean) {
    if (this._LL_page !== value) {
      this._LL_page = value;
      this.LL_pageChange$.next(value);
    }
  }

  // アラートを表示する
  public alert(message: string): void{
    if(this.electronService.isElectron) {
      this.electronService.ipcRenderer.sendSync('alert', message);
    } else {
      const modalRef = this.modalService.open(AlertDialogComponent, {
        centered: true,
        backdrop: true,
        keyboard: true,
        size: "sm",
        windowClass: "alert-modal",
      });
      modalRef.componentInstance.message = message;
    }
  }

  // Yes/Noのダイアログを表示する
  public async confirm(message: string, title?: string | undefined): Promise<boolean> {
    if (this.electronService.isElectron) {
      return window.confirm(message);
    } else {
      const modalRef = this.modalService.open(AlertDialogComponent, {
        centered: true,
        backdrop: true,
        keyboard: true,
        size: "md",
        windowClass: "confirm-modal",
      });
      modalRef.componentInstance.title = title;
      modalRef.componentInstance.message = message;
      modalRef.componentInstance.dialogMode = "confirm";

      const result = await modalRef.result;
      return result === "yes";
    }
  }

  public async notify(message: string, title?: string | undefined): Promise<boolean> {
    if (this.electronService.isElectron) {
      window.alert(message);
      return true;
    } else {
      const modalRef = this.modalService.open(AlertDialogComponent, {
        centered: true,
        backdrop: true,
        keyboard: true,
        size: "md",
        windowClass: "alert-modal",
      });
      modalRef.componentInstance.title = title;
      modalRef.componentInstance.message = message;
      modalRef.componentInstance.dialogMode = "alert";

      const result = await modalRef.result;
      return result === 'ok';
    }
  }

  /**
   * 文字列として与えられた数値が自然数(0を含まない正の整数)であるかを調べる
   * @param str 文字列として与えられた数値
   * @returns 自然数ならtrue、それ以外はfalse
   */
  isNaturalNumber(str: string | null): boolean {
    if (str === null) {
      return false;
    }
    return /^[1-9][0-9]*$/.test(str);
  }

  /**
   * 文字列として与えられた数値が0を除く整数であるかを調べる
   * @param str 文字列として与えられた数値
   * @returns 0を除く整数ならtrue、それ以外はfalse
   */
  isNonZeroInteger(str: string | null): boolean {
    if (str === null) {
      return false;
    }
    return /^-?[1-9][0-9]*$/.test(str);
  }

  /**
   * 文字列として与えられた数値が正の整数であるかを調べる
   * @param str 文字列として与えられた数値
   * @returns 正の整数ならtrue、それ以外はfalse
   */
  isPositiveInteger(str: string | null): boolean {
    if (str === null) {
      return false;
    }
    return /^[1-9][0-9]*$/.test(str);
  }

  /**
   * 文字列として与えられた数値が負符号を持つ0.0であるかを調べる
   * @param str 文字列として与えられた数値
   * @returns 負符号を持つ0.0ならtrue、それ以外はfalse
   */
  isNegativeFloatZero(str: string | null): boolean {
    if (str === null) {
      return false;
    }
    return /^-0(\.0*)?$/.test(str);
  }

  /**
   * 文字列として与えられた数値が整数の1または0であるかを調べる
   * @param str 文字列として与えられた数値
   * @returns 整数の1または0ならtrue、それ以外はfalse
   */
  isZeroOrOne(str: string | null): boolean {
    if (str === null) {
      return false;
    }
    return /^(0|1)$/.test(str);
  }

  // 文字列string を数値にする
  public toNumber(num: string, digit: number = null): number {
    let result: number = null;
    try {
      const tmp: string = num.toString().trim();
      if (tmp.length > 0) {
        result = ((n: number) => (isNaN(n) ? null : n))(+tmp);
      }
    } catch {
      result = null;
    }
    if (digit != null) {
      const dig: number = 10 ** digit;
      result = Math.round(result * dig) / dig;
    }
    return result;
  }

  // 文字列の配列を数字の小さい順に並べ替える
  public numberSort(strList: string[]): string[]{
    // 数値型の配列に変換する
    const toNumbers = strList.map(Number);

    toNumbers.sort((a, b) => {
      return a - b
    })

    return toNumbers.map(String);
  }



  // ２つのオブジェクトが同じものかどうか判定する
  public objectEquals(a: any, b: any): boolean {
    if (a === b) {
      // 同一インスタンスならtrueを返す
      return true;
    }

    // 比較対象双方のキー配列を取得する（順番保証のためソートをかける）
    const aKeys = Object.keys(a).sort();
    const bKeys = Object.keys(b).sort();

    // 比較対象同士のキー配列を比較する
    if (aKeys.toString() !== bKeys.toString()) {
      // キーが違う場合はfalse
      return false;
    }

    // 値をすべて調べる。
    const wrongIndex = aKeys.findIndex(function (value) {
      // 注意！これは等価演算子で正常に比較できるもののみを対象としています。
      // つまり、ネストされたObjectやArrayなどには対応していないことに注意してください。
      return a[value] !== b[value];
    });

    // 合致しないvalueがなければ、trueを返す。
    return wrongIndex === -1;
  }

  public table_To_text(wTABLE) {
    var wRcString = "";
    var wTR = wTABLE.rows;
    for (var i = 0; i < wTR.length; i++) {
      var wTD = wTABLE.rows[i].cells;
      var wTR_Text = "";
      for (var j = 0; j < wTD.length; j++) {
        const a: string = wTD[j].innerText;
        const b = a.replace(" ", "");
        const c = b.replace("\n", "");
        wTR_Text += c;
        if (j === wTD.length - 1) {
          wTR_Text += "";
        } else {
          wTR_Text += "\t";
        }
      }
      wRcString += wTR_Text + "\r\n";
    }
    return wRcString;
  }

  // ファイル名から拡張子を取得する関数
  public getExt(filename: string): string {
    const pos = filename.lastIndexOf('.');
    if (pos === -1) {
      return '';
    }
    const ext = filename.slice(pos + 1);
    return ext.toLowerCase();
  }

  public getScale(data: number, max: number): number {

    const ratio = 1 * Math.abs(data) / max;
    return ratio;

  }

  public getCircleScale(data: number, max: number): number {

    const ratio = 1 * Math.abs(data) / max;

    // 円を用いた倍率を設定する
    const scale = ( 1 - ( ratio - 1)**2 )**0.5;
    return scale;
  }

}
