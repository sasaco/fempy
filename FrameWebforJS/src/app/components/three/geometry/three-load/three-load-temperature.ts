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
  /** 荷重線の始点座標 */
  pia: THREE.Vector3;
  /** 荷重線の終点座標 */
  pja: THREE.Vector3;
  /** オイラー角 */
  euler: THREE.Euler;
  /** 描画スケール */
  scale: number;
};

/** 温度荷重データ */
export class ThreeLoadTemperature extends LoadData {
  /** 荷重の種別 */
  readonly loadType = "TemperatureLoad";

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
  /** i端節点とj端節点の間の距離(m) */
  readonly L: number;
  /** 荷重値(°C) */
  readonly P1: number;
  /** 部材荷重系 */
  readonly localAxis: LocalAxis;
  /** 荷重値の最大値(節点荷重と部材集中荷重) */
  readonly pMax: number = 0;
  /** 荷重値の最大値(節点モーメントと部材集中モーメント) */
  readonly mMax: number = 0;
  /** 荷重値の最大値(部材分布荷重) */
  readonly wMax: number = 0;
  /** 荷重値の最大値(部材ねじりモーメント) */
  readonly rMax: number = 0;
  /** 荷重値の最大値(部材軸方向分布荷重) */
  readonly qMax: number = 0;
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

  // this.children["group"].children["child"].children["line"] - 荷重線
  // this.children["P"] - 荷重値テキスト
  // this.children["Dimention"]
  // this.children["Dimention"].children["Dimension1"]
  // this.children["Dimention"].children["Dimension1"].children["line"] - i端の寸法補助線
  // this.children["Dimention"].children["Dimension1"].children["line"] - i端とj端の間の寸法線
  // this.children["Dimention"].children["Dimension1"].children["line"] - j端の寸法補助線
  // this.children["Dimention"].children["Dimension1"].children["text"] - i端とj端の間の寸法テキスト

  private static readonly lineMaterial = new THREE.LineBasicMaterial({
    color: 0xff0000,
    linewidth: 0.001,
  });
  private static readonly lineMaterialSelected = new THREE.LineBasicMaterial({
    color: 0x00ffff,
    linewidth: 0.001,
  });

  /**
   * 温度荷重データインスタンスの生成
   * @param mNo 部材番号
   * @param niNo i端節点の節点番号
   * @param njNo j端節点の節点番号
   * @param nodei i端節点の座標
   * @param nodej j端節点の座標
   * @param mark マーク
   * @param P1 L1点の荷重値(kN/m)
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
    P1: number,
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
    this.L = len;
    this.P1 = P1;
    this.localAxis = localAxis.clone();

    const uLoad = localAxis.y.clone().negate(); // 荷重描画方向は部材座標系y軸のマイナス側
    this.uLoad = uLoad;

    this.offsetDirection = "ly-";

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

    const uOffset = this.uLoad.clone().multiplyScalar(offsetData.offset + gap);
    const pia = this.nodei.clone().add(uOffset); // 荷重線の始点
    const pja = this.nodej.clone().add(uOffset); // 荷重線の終点

    const geometry = new THREE.BufferGeometry().setFromPoints([pia, pja]);
    const lineColor = isSelected
      ? ThreeLoadTemperature.lineMaterialSelected
      : ThreeLoadTemperature.lineMaterial;
    const line = new THREE.Line(geometry, lineColor);
    line.name = "line";

    const child = new THREE.Group();
    child.name = "child";
    child.add(line);

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
    const vx = pja.clone().sub(pia); // i端からj端に向かう向き
    // 荷重線の描画方向(上)
    const vy = this.uLoad; // 荷重を描画する向き
    // オイラー角
    const euler = ThreeLoadText3D.getEuler(vx, vy);

    // this.setColor(isSelected); // 呼び出し不要
    this.setText(isSelected, {
      pia: pia,
      pja: pja,
      euler: euler,
      scale: scale,
    });
    this.setDim(isSelected, {
      scale: scale,
      L1: 0,
      L: this.L,
      L2: 0,
      pi: undefined,
      pL1: this.nodei,
      pL2: this.nodej,
      pj: undefined,
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
   * 選択時と非選択時の寸法面の色の切り替え
   * @param isSelected true=選択状態、false=非選択状態
   */
  private setColor(isSelected: boolean): void {
    const line = this.getObjectByName("line") as THREE.Line<
      THREE.BufferGeometry,
      THREE.LineBasicMaterial
    >;
    if (!line) {
      throw new Error();
    }

    line.material = isSelected
      ? ThreeLoadTemperature.lineMaterialSelected
      : ThreeLoadTemperature.lineMaterial;
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
    const old = this.getObjectByName("P");
    if (old) {
      this.remove(old);
    }

    if (params) {
      this.setTextParams = params;
    }

    if (!isSelected) {
      return;
    }

    params ??= this.setTextParams;

    const value = Math.round(this.P1 * 100) / 100;
    const textString = value.toFixed(2) + " °C";

    const pos = params.pia.clone().lerp(params.pja, 0.5); // i端とj端の中央
    const scale = this.adjustTextScale(params.scale);

    const text = new ThreeLoadText3D(textString, pos, 0.1, {
      euler: params.euler,
      hAlign: "center",
      vAlign: "bottom",
    });
    text.name = "P";

    text.scale.set(scale, scale, scale);

    this.add(text);
  }

  /**
   * 温度荷重の描画インスタンス生成
   * @param mNo 部材番号
   * @param niNo i端節点の節点番号
   * @param njNo j端節点の節点番号
   * @param nodei i端節点の座標
   * @param nodej j端節点の座標
   * @param mark マーク
   * @param P1 荷重値(℃)
   * @param localAxis 部材座標系
   * @param row 部材荷重データテーブルの行インデックス
   * @returns 温度荷重の描画インスタンス。対象外の荷重の場合はundefined
   */
  static create(
    mNo: string,
    niNo: string,
    njNo: string,
    nodei: THREE.Vector3,
    nodej: THREE.Vector3,
    mark: number,
    P1: number | undefined,
    localAxis: LocalAxis,
    row: number
  ): ThreeLoadTemperature | undefined {
    switch (mark) {
      case 9:
        break;
      default:
        return undefined;
    }

    const L = nodei.distanceTo(nodej);
    if (L === 0) {
      return undefined;
    }

    const xP1 = P1 ?? 0;
    if (xP1 === 0) {
      return undefined;
    }

    return new ThreeLoadTemperature(
      mNo,
      niNo,
      njNo,
      nodei,
      nodej,
      mark,
      xP1,
      localAxis,
      row
    );
  }
}
