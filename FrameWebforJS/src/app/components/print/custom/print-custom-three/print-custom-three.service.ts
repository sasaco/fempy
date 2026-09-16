import { Injectable } from "@angular/core";
import { ResultDataService } from "src/app/providers/result-data.service";

@Injectable({
  providedIn: "root",
})
export class PrintCustomThreeService {
  public threeEditable: boolean[];
  public print2DThreeLayout: string; // 2次元で印刷する場合のレイアウト

  constructor(private ResultData: ResultDataService) {
    this.threeEditable = [
      true, // 軸力図
      true, // y方向のせん断力図
      true, // z方向のせん断力図
      true, // ねじりモーメント図
      true, // y軸周りのモーメント図
      true,  // z軸周りのモーメント図
      true  // 変位図
    ];
    // 2次元で印刷する場合のレイアウト
    /// splitHorizontal: 上, 下,
    /// splitVertical: 左, 右,
    /// single: シングル
    this.print2DThreeLayout = "single";
  }

}
