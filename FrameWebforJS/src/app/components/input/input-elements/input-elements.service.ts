import { Injectable } from "@angular/core";
import { DataHelperModule } from "../../../providers/data-helper.module";

@Injectable({
  providedIn: "root",
})
export class InputElementsService {
  public element: object;

  constructor(private helper: DataHelperModule) {
    this.clear();
  }

  public clear(): void {
    this.element = {};
  }

  public getElementColumns(typNo: number, index: number): any {
    let target: any = null;
    let result: any = null;

    // タイプ番号を探す
    if (!this.element[typNo]) {
      target = new Array();
    } else {
      target = this.element[typNo];
    }

    // 行を探す
    for (let i = 0; i < target.length; i++) {
      const tmp = target[i];
      if (tmp["id"].toString() === index.toString()) {
        result = tmp;
        break;
      }
    }

    // 対象行が無かった時に処理
    if (result == null) {
      result = {
        id: index.toString(),
        E: "",
        G: "",
        Xp: "",
        A: "",
        J: "",
        Iy: "",
        Iz: "",
        n: "",
      };
      target.push(result);
      this.element[typNo] = target;
    }
    return result;
  }

  public setElementJson(jsonData: {}): void {
    if (!("element" in jsonData)) {
      return;
    }
    const json: {} = jsonData["element"];

    for (const typNo of Object.keys(json)) {
      const js = json[typNo];
      const target = new Array();

      for (const index of Object.keys(js)) {
        const item = js[index];
        const E = this.helper.toNumber(item["E"]);
        const G = this.helper.toNumber(item["G"]);
        const Xp = this.helper.toNumber(item["Xp"]);
        const A = this.helper.toNumber(item["A"]);
        const J = this.helper.toNumber(item["J"]);
        const Iy = this.helper.toNumber(item["Iy"]);
        const Iz = this.helper.toNumber(item["Iz"]);
        const n = item["n"];

        const result = {
          id: index,
          E: E == null ? "" : E.toExponential(2),
          G: G == null ? "" : G.toExponential(2),
          Xp: Xp == null ? "" : Xp.toExponential(2),
          A: A == null ? "" : A.toFixed(4),
          J: J == null ? "" : J.toFixed(6),
          Iy: Iy == null ? "" : Iy.toFixed(6),
          Iz: Iz == null ? "" : Iz.toFixed(6),
          n: n == null ? "" : n,
        };

        target.push(result);
      }

      this.element[typNo] = target;
    }
  }

  public getElementJson(empty: number = null, targetCase: string = "") {
    const result = {};

    for (const typNo of Object.keys(this.element)) {
      // ケースの指定がある場合、カレントケース以外は無視する
      if (targetCase.length > 0 && typNo !== targetCase) {
        continue;
      }

      const element = this.element[typNo];
      const jsonData: object = {};

      for (let i = 0; i < element.length; i++) {
        const row: {} = element[i];
        const E = this.helper.toNumber(row["E"]);
        const G = this.helper.toNumber(row["G"]);
        const Xp = this.helper.toNumber(row["Xp"]);
        const A = this.helper.toNumber(row["A"]);
        const J = this.helper.toNumber(row["J"]);
        const Iy = this.helper.toNumber(row["Iy"]);
        const Iz = this.helper.toNumber(row["Iz"]);
        const n = row["n"];

        if (
          E == null &&
          G == null &&
          Xp == null &&
          A == null &&
          J == null &&
          Iy == null &&
          Iz == null
        ) {
          continue;
        }

        const key: string = row["id"];
        jsonData[key] = {
          E: E == null ? empty : E,
          G: G == null ? empty : G,
          Xp: Xp == null ? empty : Xp,
          A: A == null ? empty : A,
          J: J == null ? empty : J,
          Iy: Iy == null ? empty : Iy,
          Iz: Iz == null ? empty : Iz,
          n: n == void 0 ? "" : n,
        };
        if (empty === null) {
          // 計算以外では、名前を保存
          jsonData[key]["n"] = n;
        }
      }
      if (Object.keys(jsonData).length > 0) {
        result[typNo] = jsonData;
      }
    }
    return result;
  }

  public getElementName(e: any): string {
    if (e === "" || e === undefined) {
      return "";
    }
    if (Object.keys(this.element).length === 0) {
      return "";
    }
    const key = Object.keys(this.element)[0];
    const row = this.element[key];

    const target = row.find((columns) => {
      return columns.id.toString() === e.toString();
    });
    let name: string = "";
    if (target !== undefined) {
      name = target.n !== undefined ? target.n : "";
    }

    return name;
  }

  public getAlignName(typeNo, row): string {
    const before_dataset = this.element[typeNo];
    const a = before_dataset.find(
      (dataset) => dataset.id == (row + 1).toString()
    );
    if (a === undefined) {
      return null;
    }
    const before_target_row = a;
    const target_name = before_target_row.n;

    //const keys = Object.keys(this.element);
    for (const key of Object.keys(this.element)) {
      const target_element = this.element[key].find(
        (element) => element.id === before_target_row.id
      );
      if (target_element === undefined) {
        continue;
      }
      target_element.n = target_name;
    }

    return target_name;
  }

  public matchName(data: any) {
    for (const key of Object.keys(this.element)) {
      const target = this.element[key].find((row) => {
        return row.id === data.id || row.id === data.id.toString();
      });
      if (target !== undefined) {
        target.n = data.n;
      }
    }
  }
}
