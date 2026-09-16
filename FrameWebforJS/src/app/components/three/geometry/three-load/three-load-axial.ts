import * as THREE from "three";

import {
  ConflictSection,
  LoadData,
  LocalAxis,
  MaxLoadDict,
  OffsetDict,
  OffsetDirection,
} from "./three-load-common";
import { ThreeLoadText3D } from "./three-load-text";

/** 荷重値描画用データ */
type SetTextParams = {
  /** L1点の荷重値を描画する座標 */
  pP1: THREE.Vector3;
  /** L2点の荷重値を描画する座標 */
  pP2: THREE.Vector3;
  /** 荷重値の描画方向(上) */
  vy: THREE.Vector3;
  /** オイラー角 */
  euler: THREE.Euler;
  /** 描画スケール */
  scale: number;
};

/** 部材軸方向分布荷重データ */
export class ThreeLoadAxial extends LoadData {
  /** 荷重の種別 */
  readonly loadType = "AxialLoad";

  /** 荷重図形の拡大倍率 */
  readonly magnifier = 1;

  /** この荷重と関連を持つ節点の節点番号一覧 */
  readonly correspondingNodeNoList: string[];
  /** この荷重と関連を持つ部材の部材番号一覧 */
  readonly correspondingMemberNoList: string[];

  /** i端節点の座標(基準点はthis.position) */
  readonly nodei: THREE.Vector3;
  /** j端節点の座標(基準点はthis.position) */
  readonly nodej: THREE.Vector3;
  /** マーク */
  readonly mark: number;
  /** 方向 */
  readonly direction: string;
  /** i端節点とL1点の間の距離(m) */
  readonly L1: number;
  /** L1点とL2点の間の距離(m) */
  readonly L: number;
  /** j端節点とL2点の間の距離(m) */
  readonly L2: number;
  /** L1点の荷重値(kN/m) */
  readonly P1: number;
  /** L2点の荷重値(kN/m) */
  readonly P2: number;
  /** 部材荷重系 */
  readonly localAxis: LocalAxis;
  /** L1点の座標 */
  readonly pL1: THREE.Vector3;
  /** L2点の座標 */
  readonly pL2: THREE.Vector3;
  /** 荷重値の最大値(節点荷重と部材集中荷重) */
  readonly pMax: number = 0;
  /** 荷重値の最大値(節点モーメントと部材集中モーメント) */
  readonly mMax: number = 0;
  /** 荷重値の最大値(部材分布荷重) */
  readonly wMax: number = 0;
  /** 荷重値の最大値(部材ねじりモーメント) */
  readonly rMax: number = 0;
  /** 荷重値の最大値(部材軸方向分布荷重) */
  readonly qMax: number;
  /** 荷重描画方向を示す単位ベクトル */
  readonly uLoad: THREE.Vector3;
  /** 部材軸を起点とした場合の荷重描画方向を示す文字列 */
  readonly offsetDirection: OffsetDirection;
  /** 寸法線の描画方向を示す単位ベクトル */
  readonly uDimension: THREE.Vector3;
  /** 荷重テーブルの列情報(m=部材荷重、p=節点荷重) */
  readonly col: "m" | "p" = "m";
  /** 荷重テーブルの行番号 */
  readonly row: number;
  /** 荷重の積み上げ順を決める数値 */
  readonly rank = 10;

  /** ハイライト表示状態を示すフラグ */
  private isSelected: boolean = false;

  // this.children["group"].children["child"].children["arrow"] - 荷重矢印
  // this.children["P1"] - 荷重値テキスト
  // this.children["P2"] - 荷重値テキスト
  // this.children["Dimention"]
  // this.children["Dimention"].children["Dimension1"]
  // this.children["Dimention"].children["Dimension1"].children["line"] - L1点の寸法補助線
  // this.children["Dimention"].children["Dimension1"].children["line"] - L1点とL2点の間の寸法線
  // this.children["Dimention"].children["Dimension1"].children["line"] - L2点の寸法補助線
  // this.children["Dimention"].children["Dimension1"].children["text"] - L1点とL2点の間の寸法テキスト
  // this.children["Dimention"].children["Dimension2"]
  // this.children["Dimention"].children["Dimension2"].children["line"] - i端節点の寸法補助線
  // this.children["Dimention"].children["Dimension2"].children["line"] - i端節点とL1点の間の寸法線
  // this.children["Dimention"].children["Dimension2"].children["text"] - i端節点とL1点お間の寸法テキスト
  // this.children["Dimention"].children["Dimension3"]
  // this.children["Dimention"].children["Dimension3"].children["line"] - j端節点の寸法補助線
  // this.children["Dimention"].children["Dimension3"].children["line"] - j端節点とL2点の間の寸法線
  // this.children["Dimention"].children["Dimension3"].children["text"] - j端節点とL2点の間の寸法テキスト

  private static readonly arrowColor = 0xff0000;
  private static readonly arrowColorPick = 0x00ffff;

  /**
   * 部材軸方向分布荷重データインスタンスの生成
   * @param mNo 部材番号
   * @param niNo i端節点の節点番号
   * @param njNo j端節点の節点番号
   * @param nodei i端節点の座標
   * @param nodej j端節点の座標
   * @param mark マーク
   * @param direction 方向
   * @param L1 i端節点とL1点の間の距離(m)
   * @param L2 j端節点とL2点の間の距離(m)
   * @param P1 L1点の荷重値(kN/m)
   * @param P2 L2点の荷重値(kN/m)
   * @param localAxis 部材座標系
   * @param row 部材荷重データテーブルの行インデックス
   */
  constructor(
    mNo: string,
    niNo: string,
    njNo: string,
    nodei: THREE.Vector3,
    nodej: THREE.Vector3,
    mark: number,
    direction: string,
    L1: number,
    L2: number,
    P1: number,
    P2: number,
    localAxis: LocalAxis,
    row: number
  ) {
    super();

    this.correspondingNodeNoList = [niNo, njNo];
    this.correspondingMemberNoList = [mNo];

    // 部材長
    const len = nodei.distanceTo(nodej);

    // 部材の基準点
    const pBase = nodei.clone().lerp(nodej, 0.5); // nodeiとnodejの中点
    // i端節点とj端節点の座標の基準点を原点からpBaseに移動
    nodei = nodei.clone().sub(pBase);
    nodej = nodej.clone().sub(pBase);

    this.nodei = nodei;
    this.nodej = nodej;
    this.mark = mark;
    this.direction = direction;
    this.L1 = L1;
    this.L = len - L1 - L2;
    this.L2 = L2;
    this.P1 = P1;
    this.P2 = P2;
    this.localAxis = localAxis.clone();
    this.pL1 = nodei.clone().lerp(nodej, L1 / len);
    this.pL2 = nodej.clone().lerp(nodei, L2 / len);
    this.qMax = Math.max(Math.abs(P1), Math.abs(P2));

    // 部材座標系のy軸プラス側に矢印を描画する
    const uLoad = new THREE.Vector3();
    switch (direction) {
      case "x":
      case "gx":
      case "gy":
      case "gz":
        uLoad.copy(localAxis.y);
        break;
      default:
        throw new Error();
    }
    this.uLoad = uLoad;
    this.offsetDirection = "ly+" as OffsetDirection;

    // 寸法関連は部材軸および矢印を描画する向きと直交する向きに描画
    this.uDimension = this.localAxis.x.clone().cross(uLoad);

    this.row = row;

    this.name = `${this.loadType}-${row}-m`;
    this.position.copy(pBase);
  }

  /**
   * 荷重図の再配置
   * @param nodeOffsetDictMap key=節点番号、value=各接点のOffsetDict
   * @param memberOffsetDictMap key=部材番号、value=各部材のOffsetDict
   * @param maxLoadDict
   * @param scale 描画スケール
   * @param isSelected true=ハイライト表示、false=ハイライト表示解除、undefined=状態継続
   */
  relocate(
    nodeOffsetDictMap: Map<string, OffsetDict>,
    memberOffsetDictMap: Map<string, OffsetDict>,
    maxLoadDict: MaxLoadDict,
    scale: number,
    isSelected: boolean | undefined
  ): void {
    const old = this.getObjectByName("group");
    if (old) {
      this.remove(old);
    }

    if (isSelected !== undefined) {
      this.isSelected = isSelected;
    } else {
      isSelected = this.isSelected;
    }

    // この荷重に関連するOffsetDictの抽出
    const correspondingOffsetDictList: OffsetDict[] = [];
    this.correspondingMemberNoList.forEach((no) => {
      correspondingOffsetDictList.push(memberOffsetDictMap.get(no));
    });

    // この荷重に適用するoffsetの決定
    const offsetData = correspondingOffsetDictList
      .map((dict) => dict.get(this.offsetDirection, ConflictSection.EndToEnd))
      .reduce((a, b) => (a.offset > b.offset ? a : b));

    const gap = 0.1 * scale; // 部材軸と荷重矢印間の間隙の大きさ

    const value = this.P1; // 矢印の向きを決める値

    const pL1 = this.pL1; // L1点の座標
    const pL2 = this.pL2; // L2点の座標
    const uLoad = this.uLoad; // 荷重を描画する向き

    const pL1a = pL1
      .clone()
      .add(uLoad.clone().multiplyScalar(offsetData.offset + gap)); // 荷重矢印の始点
    const pL2a = pL2
      .clone()
      .add(uLoad.clone().multiplyScalar(offsetData.offset + gap)); // 荷重矢印の始点

    // 矢印
    const origin = pL1a.clone();
    const dir = pL2a.clone().sub(pL1a).normalize();
    if (value < 0) {
      origin.copy(pL2a);
      dir.negate();
    }
    const length = pL1a.distanceTo(pL2a);
    const color = 0xff0000;
    const arrow = new THREE.ArrowHelper(dir, origin, length, color);
    arrow.name = "arrow";

    const child = new THREE.Group();
    child.name = "child";
    child.add(arrow);

    const group = new THREE.Group();
    group.name = "group";
    group.add(child);

    // 荷重の外側の間隙の大きさ
    const overGap = 0.1 * scale;

    // この荷重に関連するOffsetDictの更新(節点の情報も併せて更新する)
    this.correspondingNodeNoList.forEach((no) =>
      correspondingOffsetDictList.push(nodeOffsetDictMap.get(no))
    );
    correspondingOffsetDictList.forEach((dict) =>
      dict.update(
        this.offsetDirection,
        offsetData.offset + gap + overGap,
        true,
        ConflictSection.EndToEnd
      )
    );

    this.add(group);

    // 荷重値の描画方向(右)
    const vx = this.nodej.clone().sub(this.nodei).normalize(); // i端からj端に向かう向き
    // 荷重値の描画方向(上)
    const vy = this.uLoad;
    // オイラー角
    const euler = ThreeLoadText3D.getEuler(vx, vy);

    this.setColor(isSelected);
    this.setText(isSelected, {
      pP1: pL1a,
      pP2: pL2a,
      vy: vy,
      euler: euler,
      scale: scale,
    });
    this.setDim(isSelected, {
      scale: scale,
      L1: this.L1,
      L: this.L,
      L2: this.L2,
      pi: this.nodei,
      pL1: this.pL1,
      pL2: this.pL2,
      pj: this.nodej,
      uDimension: this.uDimension,
    });
  }

  /**
   * 選択状態と非選択状態の切り替え
   * @param isSelected true=選択状態、false=非選択状態
   */
  highlight(isSelected: boolean): void {
    if (this.isSelected === isSelected) {
      return;
    }
    this.isSelected = isSelected;

    this.setColor(isSelected);
    this.setText(isSelected);
    this.setDim(isSelected);
  }

  /**
   * 選択状態と非選択状態とで矢印の色を切り替える
   * @param isSelected true=選択状態、false=非選択状態
   */
  private setColor(isSelected: boolean): void {
    const arrowColor = isSelected
      ? ThreeLoadAxial.arrowColorPick
      : ThreeLoadAxial.arrowColor;
    const arrow = this.getObjectByName("arrow") as THREE.ArrowHelper;
    if (!arrow) {
      throw new Error();
    }
    arrow.setColor(arrowColor);
  }

  /** 荷重値描画用データの退避先 */
  private setTextParams: SetTextParams;

  /**
   * 選択時は荷重値を描画し、非選択時は荷重値の描画をクリアする
   * @param isSelected true=選択状態、false=非選択状態
   * @param params relocate()から呼び出された時は荷重値描画用データ、highlight()から呼び出された時はundefined
   */
  private setText(
    isSelected: boolean,
    params: SetTextParams | undefined = undefined
  ): void {
    // 一旦削除
    ["P1", "P2"].forEach((key) => {
      const old = this.getObjectByName(key);
      if (old) {
        this.remove(old);
      }
    });

    if (params) {
      this.setTextParams = params;
    }

    if (!isSelected) {
      return;
    }

    params ??= this.setTextParams;

    const scale = this.adjustTextScale(params.scale);

    [
      { key: "P1", value: this.P1, pos: params.pP1 },
      { key: "P2", value: this.P2, pos: params.pP2 },
    ].forEach(({ key, value, pos }) => {
      value = Math.round(value * 100) / 100;
      if (value === 0) {
        return;
      }
      const textString = value.toFixed(2) + " kN/m";
      pos = pos.clone().add(params.vy.clone().multiplyScalar(0.03)); // テキストと荷重線の間を少し空ける
      const text = new ThreeLoadText3D(textString, pos, 0.1, {
        euler: params.euler,
        hAlign: "center",
        vAlign: "bottom",
      });
      text.name = key;

      text.scale.set(scale, scale, scale);

      this.add(text);
    });
  }

  /**
   * 部材軸方向分布荷重の描画インスタンス生成
   * @param mNo 部材番号
   * @param niNo i端節点の節点番号
   * @param njNo j端節点の節点番号
   * @param nodei i端節点の座標
   * @param nodej j端節点の座標
   * @param mark マーク
   * @param direction 方向
   * @param L1 i端節点からL1点までの距離(m)
   * @param L2 j端節点からL2点までの距離(m)
   * @param P1 L1点の荷重値(kN/m)
   * @param P2 L2点の荷重値(kN/m)
   * @param localAxis 部材座標系
   * @param row 部材荷重データテーブルの行インデックス
   * @param is3d 3D描画モードかどうか
   * @returns 部材軸方向分布荷重の描画インスタンス。対象外の荷重の場合はundefined
   */
  static create(
    mNo: string,
    niNo: string,
    njNo: string,
    nodei: THREE.Vector3,
    nodej: THREE.Vector3,
    mark: number,
    direction: string,
    L1: number | undefined,
    L2: number | undefined,
    P1: number | undefined,
    P2: number | undefined,
    localAxis: LocalAxis,
    row: number,
    is3d: boolean
  ): ThreeLoadAxial | undefined {
    switch (mark) {
      case 2:
        break;
      default:
        return undefined;
    }
    if (is3d) {
      switch (direction) {
        case "x":
          break;
        case "gx":
          if (!(localAxis.x.y === 0 && localAxis.x.z === 0)) {
            return undefined;
          }
          break;
        case "gy":
          if (!(localAxis.x.x === 0 && localAxis.x.z === 0)) {
            return undefined;
          }
          break;
        case "gz":
          if (!(localAxis.x.x === 0 && localAxis.x.y === 0)) {
            return undefined;
          }
          break;
        default:
          return undefined;
      }
    } else {
      switch (direction) {
        case "x":
          break;
        case "gx":
          if (!(localAxis.x.y === 0 && localAxis.x.z === 0)) {
            return undefined;
          }
          break;
        case "gy":
          if (!(localAxis.x.x === 0 && localAxis.x.z === 0)) {
            return undefined;
          }
          break;
        default:
          return undefined;
      }
    }

    const L = nodei.distanceTo(nodej);
    if (L === 0) {
      return undefined;
    }

    const xL1 = L1 ?? 0;
    const xL2 = L2 ?? 0;
    if (xL1 < 0 || xL2 < 0 || L - xL1 - xL2 < 0) {
      return undefined;
    }

    const xP1 = P1 ?? 0;
    const xP2 = P2 ?? 0;
    if (xP1 === 0 && xP2 === 0) {
      return undefined;
    }

    return new ThreeLoadAxial(
      mNo,
      niNo,
      njNo,
      nodei,
      nodej,
      mark,
      direction,
      xL1,
      xL2,
      xP1,
      xP2,
      localAxis,
      row
    );
  }
}
