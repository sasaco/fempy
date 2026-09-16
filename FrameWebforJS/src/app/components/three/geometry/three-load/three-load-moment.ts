import * as THREE from "three";

import {
  ConflictSection,
  LoadData,
  LocalAxis,
  MaxLoadDict,
  OffsetDict,
  OffsetDirection,
} from "./three-load-common";
import { ThreeLoadText2D } from "./three-load-text";

/** 荷重描画色データ */
type SetColorParams = {
  /** 矢尻の描画色 */
  arrowColor: THREE.MeshBasicMaterial;
  /** 矢柄の描画色 */
  lineColor: THREE.LineBasicMaterial;
};

/** 荷重値描画用データ */
type SetTextParams = {
  /** 描画スケール */
  scale: number;
};

/** 節点モーメント荷重データ */
export class ThreeLoadMoment extends LoadData {
  /** 荷重の種別 */
  readonly loadType = "MomentLoad";

  /** 荷重図形の拡大倍率 */
  readonly magnifier = 1;

  /** この荷重と関連を持つ節点の節点番号一覧 */
  readonly correspondingNodeNoList: string[];
  /** この荷重と関連を持つ部材の部材番号一覧 */
  readonly correspondingMemberNoList: string[];

  /** 節点座標 */
  readonly position: THREE.Vector3;
  /** 方向 */
  readonly direction: string;
  /** 荷重値(kN) */
  readonly value: number;
  /** 荷重値の最大値(節点荷重と部材集中荷重) */
  readonly pMax: number = 0;
  /** 荷重値の最大値(節点モーメントと部材集中モーメント) */
  readonly mMax: number;
  /** 荷重値の最大値(部材分布荷重) */
  readonly wMax: number = 0;
  /** 荷重値の最大値(部材ねじりモーメント) */
  readonly rMax: number = 0;
  /** 荷重値の最大値(部材軸方向分布荷重) */
  readonly qMax: number = 0;
  /** 節点を起点とした場合の荷重描画方向を示す文字列 */
  readonly offsetDirection: OffsetDirection;
  /** 荷重テーブルの列情報(m=部材荷重、p=節点荷重) */
  readonly col: "m" | "p" = "p";
  /** 荷重テーブルの行番号 */
  readonly row: number;
  /** 荷重の積み上げ順を決める数値 */
  readonly rank = 0;

  /** ハイライト表示状態を示すフラグ */
  private isSelected: boolean = false;

  // this.children["group"].children["child"].children["arrow"] - 矢印
  // this.children["group"].children["child"].children["ellipse"] - 本体
  // this.children["group"].children["child"].children["value"] - 荷重値

  private static readonly arrowMaterialRed = new THREE.MeshBasicMaterial({
    color: 0xff0000,
  });
  private static readonly arrowMaterialGreen = new THREE.MeshBasicMaterial({
    color: 0x00ff00,
  });
  private static readonly arrowMaterialBlue = new THREE.MeshBasicMaterial({
    color: 0x0000ff,
  });
  private static readonly arrowMaterialPick = new THREE.MeshBasicMaterial({
    color: 0xafeeee,
  });
  private static readonly lineMaterialRed = new THREE.LineBasicMaterial({
    color: 0xff0000,
  });
  private static readonly lineMaterialGreen = new THREE.LineBasicMaterial({
    color: 0x00ff00,
  });
  private static readonly lineMaterialBlue = new THREE.LineBasicMaterial({
    color: 0x0000ff,
  });
  private static readonly lineMaterialPick = new THREE.LineBasicMaterial({
    color: 0xafeeee,
  });

  /**
   * 節点モーメント荷重の描画インスタンス生成
   * @param no 節点番号
   * @param position 節点座標
   * @param direction 方向
   * @param value 荷重値(kN)
   * @param row 節点荷重データテーブルの行インデックス
   * @returns 節点荷重の描画インスタンス
   */
  constructor(
    no: string,
    position: THREE.Vector3,
    direction: string,
    value: number,
    row: number
  ) {
    super();

    this.correspondingNodeNoList = [no];
    this.correspondingMemberNoList = [];

    this.position.copy(position);
    this.direction = direction;
    this.value = value;
    this.mMax = Math.abs(value);

    switch (direction) {
      case "rx":
        this.offsetDirection = "rgx";
        break;
      case "ry":
        this.offsetDirection = "rgy";
        break;
      case "rz":
        this.offsetDirection = "rgz";
        break;
      default:
        throw new Error();
    }

    this.row = row;

    this.name = `${this.loadType}-${row}-${direction}`;
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
    this.correspondingNodeNoList.forEach((no) => {
      correspondingOffsetDictList.push(nodeOffsetDictMap.get(no));
    });
    this.correspondingMemberNoList.forEach((no) => {
      correspondingOffsetDictList.push(memberOffsetDictMap.get(no));
    });

    // モーメント系は積み上げ対象外

    const { arrowColor, lineColor } = this.getColor();

    const radius =
      (Math.abs(this.value) / maxLoadDict.mMax) * this.magnifier * scale;

    const group = ThreeLoadMoment.getArrow(
      this.direction,
      this.value,
      radius,
      arrowColor,
      lineColor
    );

    // 関連するOffsetDictの更新
    correspondingOffsetDictList.forEach((dict) =>
      dict.update(this.offsetDirection, radius, true, ConflictSection.EndToEnd)
    );

    this.add(group);

    this.setText(isSelected, {
      scale: scale,
    });
    this.setColor(isSelected, {
      arrowColor: arrowColor,
      lineColor: lineColor,
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

    this.setText(isSelected);
    this.setColor(isSelected);
  }

  /**
   * モーメント荷重矢印のグループを生成する
   * @param direction 向き(x, y, z, gx, gy, gz, rx, ry, rz)
   * @param value 荷重値(負号を参照して矢印の向きを決定する)
   * @param radius 半径
   * @param arrowColor 矢尻の色
   * @param lineColor 矢柄の色
   * @param localAxis 部材座標系(向きとしてx, y, zを指定した場合に必須)  @default undefined
   * @returns モーメント荷重矢印のグループ
   */
  static getArrow(
    direction: string,
    value: number,
    radius: number,
    arrowColor: THREE.MeshBasicMaterial,
    lineColor: THREE.LineBasicMaterial,
    localAxis: LocalAxis | undefined = undefined
  ): THREE.Group {
    // 矢尻
    const arrow_geo = new THREE.ConeBufferGeometry(0.05, 0.25, 3, 1, false);
    const arrow = new THREE.Mesh(arrow_geo, arrowColor);
    arrow.rotation.x = Math.PI;
    arrow.rotateZ(-Math.PI / 6);
    arrow.position.set(Math.cos(Math.PI / 6), Math.sin(Math.PI / 6), 0);
    arrow.name = "arrow";

    // 矢柄
    const curve = new THREE.EllipseCurve(
      0,
      0, // ax, aY
      1,
      1, // xRadius, yRadius
      (1 / 6) * Math.PI,
      (11 / 6) * Math.PI, // aStartAngle, aEndAngle
      false, // aClockwise
      0 // aRotation
    );

    const points = curve.getPoints(12);
    const lineGerometry = new THREE.BufferGeometry().setFromPoints(points);
    const ellipse = new THREE.Line(lineGerometry, lineColor);
    ellipse.name = "line";

    const child = new THREE.Group();
    child.name = "child";
    child.add(arrow, ellipse);

    // 符号に応じて矢印の向きを反転させる
    switch (direction) {
      case "x":
      case "rx":
      case "gx":
        if (value < 0) {
          child.rotation.set(0, Math.PI / 2, 0, "YXZ");
        } else {
          child.rotation.set(Math.PI, Math.PI / 2, 0, "YXZ");
        }
        child.rotation.z += Math.PI / 2; // ねじりモーメント荷重(2r)のmeshと開口部を一致させるために必要
        // この時点では、x軸が法線、y軸の負方向が開口部(荷重値が正の場合)
        break;
      case "y":
      case "ry":
      case "gy":
        if (value < 0) {
          child.rotation.set(Math.PI, 0, Math.PI); // z軸方向にも回転させているので開口部が反対側に移動する
        }
        child.rotation.x += Math.PI / 2;
        // この時点では、y軸が法線、x軸の正方向が開口部(荷重値が正の場合)
        break;
      case "z":
      case "rz":
      case "gz":
        if (value > 0) {
          child.rotation.set(Math.PI, 0, Math.PI); // z軸方向にも回転させているので開口部が反対側に移動する
        }
        // この時点では、z軸が法線、x軸の負方向が開口部(荷重値が正の場合)
        break;
      default:
        throw new Error();
    }

    child.scale.set(radius, radius, radius);

    const group = new THREE.Group();
    group.add(child);
    group.name = "group";

    // 部材座標系の場合は全体の向きを修正する
    switch (direction) {
      case "x":
      case "y":
      case "z":
        if (!localAxis) {
          throw new Error();
        }
        const XY = new THREE.Vector2(localAxis.x.x, localAxis.x.y);
        let aXY = XY.angle();
        if (XY.x < 0) {
          aXY = Math.PI - aXY;
        }
        const XZ = new THREE.Vector2(XY.length(), localAxis.x.z).normalize();
        const aXZ = XZ.angle();

        group.rotation.set(aXZ, -aXZ, aXY, "ZYX"); // @TODO: コードアングルには未対応
        break;
      default:
        break;
    }

    return group;
  }

  private getColor(): {
    arrowColor: THREE.MeshBasicMaterial;
    lineColor: THREE.LineBasicMaterial;
  } {
    const arrowColor = new THREE.MeshBasicMaterial();
    const lineColor = new THREE.LineBasicMaterial();
    switch (this.direction) {
      case "rx":
        arrowColor.copy(ThreeLoadMoment.arrowMaterialRed);
        lineColor.copy(ThreeLoadMoment.lineMaterialRed);
        break;
      case "ry":
        arrowColor.copy(ThreeLoadMoment.arrowMaterialGreen);
        lineColor.copy(ThreeLoadMoment.lineMaterialGreen);
        break;
      case "rz":
        arrowColor.copy(ThreeLoadMoment.arrowMaterialBlue);
        lineColor.copy(ThreeLoadMoment.lineMaterialBlue);
        break;
      default:
        throw new Error();
    }
    return { arrowColor, lineColor };
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
    const key = "value";

    // 一旦削除
    const child = this.getObjectByName("child");
    const old = child.getObjectByName(key);
    if (old) {
      child.remove(old);
    }

    if (params) {
      this.setTextParams = params;
    }

    if (!isSelected) {
      return;
    }

    params ??= this.setTextParams;

    const scale = this.adjustTextScale(params.scale);

    // child.scale.xで除算しているのは、荷重値の大小に伴う荷重線のスケール調整がテキストの描画サイズに影響するのを防ぐため
    const textSize = (0.1 / child.scale.x) * scale;
    if (!isFinite(textSize)) {
      return;
    }

    const value = this[key] as number;

    const textString: string = value.toFixed(2) + " kN m";

    // テキストの描画位置は矢筈(ノック)の位置
    const group_scale = 1;
    const pi6 = -Math.PI / 6;
    const sin30 = Math.sin(pi6);
    const cos30 = Math.cos(pi6);
    const position = new THREE.Vector2(
      group_scale * cos30,
      group_scale * sin30
    );
    const text = new ThreeLoadText2D(textString, position, textSize);
    //   text.rotateX(Math.PI);
    text.name = key;

    child.add(text);
  }

  /** 荷重描画色データの退避先 */
  private setColorParams: SetColorParams;

  /**
   * 選択状態と非選択状態とで矢印の色を切り替える
   * @param isSelected true=選択状態、false=非選択状態
   * @param params relocate()から呼び出された時は荷重描画色データ、highlight()から呼び出された時はundefined
   */
  private setColor(
    isSelected: boolean,
    params: SetColorParams | undefined = undefined
  ): void {
    if (params) {
      this.setColorParams = params;
    } else {
      params = this.setColorParams;
    }

    const arrowColor = isSelected
      ? ThreeLoadMoment.arrowMaterialPick
      : params.arrowColor;
    const arrow = this.getObjectByName("arrow") as THREE.Mesh;
    if (!arrow) {
      throw new Error();
    }
    arrow.material = arrowColor;

    const lineColor = isSelected
      ? ThreeLoadMoment.lineMaterialPick
      : params.lineColor;
    const line = this.getObjectByName("line") as THREE.Line<
      THREE.BufferGeometry,
      THREE.LineBasicMaterial
    >;
    if (!line) {
      throw new Error();
    }
    line.material = lineColor;
  }

  /**
   * 節点モーメント荷重の描画インスタンス生成
   * @param no 節点番号
   * @param position 節点座標
   * @param direction 方向
   * @param value 荷重値(kN)
   * @param row 節点荷重データテーブルの行インデックス
   * @returns 節点モーメント荷重の描画インスタンス
   */
  static create(
    no: string,
    position: THREE.Vector3,
    direction: string,
    value: number,
    row: number
  ): ThreeLoadMoment | undefined {
    switch (direction) {
      case "rx":
      case "ry":
      case "rz":
        break;
      default:
        return undefined;
    }
    if (value === 0) {
      return undefined;
    }

    return new ThreeLoadMoment(no, position, direction, value, row);
  }
}
