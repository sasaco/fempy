import * as THREE from "three";

import {
  ConflictSection,
  LoadData,
  LocalAxis,
  MaxLoadDict,
  OffsetDict,
  OffsetDirection,
} from "./three-load-common";
import { ThreeLoadMoment } from "./three-load-moment";
import { ThreeLoadText2D } from "./three-load-text";

type ColorMaterial = {
  meshColor: THREE.MeshBasicMaterial;
  lineColor: THREE.LineBasicMaterial;
};

/** 荷重描画色データ */
type SetColorParams = {
  /** L1点側の円筒(または円錐)の描画色 */
  mesh1: THREE.MeshBasicMaterial;
  /** L2点側の円筒(または円錐)の描画色 */
  mesh2: THREE.MeshBasicMaterial;
};

/** 荷重値描画用データ */
type SetTextParams = {
  /** 描画スケール */
  scale: number;
};

/** ねじりモーメント荷重データ */
export class ThreeLoadTorsion extends LoadData {
  /** 荷重の種別 */
  readonly loadType = "TorsionLoad";

  /** 荷重図形の拡大倍率 */
  readonly magnifier = 0.5;

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
  /** L1点の荷重値(kNm/m) */
  readonly P1: number;
  /** L2点の荷重値(kNm/m) */
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
  readonly rMax: number;
  /** 荷重値の最大値(部材軸方向分布荷重) */
  readonly qMax: number = 0;
  /** L1点側の荷重描画方向を示す単位ベクトル */
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
  readonly rank = 0;

  /** ハイライト表示状態を示すフラグ */
  private isSelected: boolean = false;

  // this.children["group"].children["child"].children["mesh1"] - L1点側の荷重円錐(?)
  // this.children["group"].children["child"].children["mesh2"] - L2点側の荷重円錐(?)
  // this.children["group"].children["child"].children["arrow1"].children["child"].children["arrow"] - L1点側の荷重矢印の矢尻
  // this.children["group"].children["child"].children["arrow1"].children["child"].children["line"] - L1点側の荷重矢印の矢柄
  // this.children["group"].children["child"].children["arrow1"].children["child"].children["P1"] - 荷重値テキスト
  // this.children["group"].children["child"].children["arrow2"].children["child"].children["arrow"] - L2点側の荷重矢印の矢尻
  // this.children["group"].children["child"].children["arrow2"].children["child"].children["line"] - L2点側の荷重矢印の矢柄
  // this.children["group"].children["child"].children["arrow1"].children["child"].children["P2"] - 荷重値テキスト
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

  private static readonly colorMaterials: {
    red: ColorMaterial;
    blue: ColorMaterial;
  } = {
    red: {
      meshColor: new THREE.MeshBasicMaterial({
        transparent: true,
        side: THREE.DoubleSide,
        color: 0xff0000,
        opacity: 0.3,
      }),
      lineColor: new THREE.LineBasicMaterial({ color: 0xff0000 }),
    },
    blue: {
      meshColor: new THREE.MeshBasicMaterial({
        transparent: true,
        side: THREE.DoubleSide,
        color: 0x0000ff,
        opacity: 0.3,
      }),
      lineColor: new THREE.LineBasicMaterial({ color: 0x0000ff }),
    },
  };
  private static readonly meshColorSelected = new THREE.MeshBasicMaterial({
    transparent: true,
    side: THREE.DoubleSide,
    color: 0xff00ff,
    opacity: 0.3,
  });

  /**
   * ねじりモーメント荷重データインスタンスの生成
   * @param mNo 部材番号
   * @param niNo i端節点の節点番号
   * @param njNo j端節点の節点番号
   * @param nodei i端節点の座標
   * @param nodej j端節点の座標
   * @param mark マーク
   * @param direction 方向
   * @param L1 i端節点とL1点の間の距離(m)
   * @param L2 j端節点とL2点の間の距離(m)
   * @param P1 L1点の荷重値(kNm/m)
   * @param P2 L2点の荷重値(kNm/m)
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
    this.rMax = Math.max(Math.abs(P1), Math.abs(P2));

    switch (direction) {
      case "r":
        this.uLoad = localAxis.y.clone().negate();
        break;
      default:
        throw new Error();
    }

    this.offsetDirection = "R";

    this.uDimension = this.uLoad.clone().negate();

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
    this.correspondingNodeNoList.forEach((no) => {
      correspondingOffsetDictList.push(nodeOffsetDictMap.get(no));
    });
    this.correspondingMemberNoList.forEach((no) => {
      correspondingOffsetDictList.push(memberOffsetDictMap.get(no));
    });

    // モーメント系は積み上げ対象外

    // 線の色を決める
    const my_color = this.getColors();

    const child = new THREE.Group();
    child.name = "child";

    // 長さを決める
    const { radius1, radius2, points } = this.getPoints(
      maxLoadDict.rMax,
      scale
    );

    // 面
    for (const mesh of this.getFace(my_color, points)) {
      child.add(mesh);
    }

    // 線
    for (const arrow of this.getArrow([this.P1, this.P2], my_color, [
      points[0],
      points[2],
    ])) {
      child.add(arrow);
    }

    // 全体
    const group = new THREE.Group();
    group.name = "group";
    group.add(child);

    // この荷重に関連するOffsetDictの更新(節点の情報も併せて更新する)
    const largerRadius = Math.max(radius1, radius2);
    this.correspondingNodeNoList.forEach((no) =>
      correspondingOffsetDictList.push(nodeOffsetDictMap.get(no))
    );
    correspondingOffsetDictList.forEach((dict) =>
      dict.update(
        this.offsetDirection,
        largerRadius,
        true,
        ConflictSection.EndToEnd
      )
    );

    this.add(group);

    // 全体の向きを修正する
    this.setRotate(this.direction, group, this.localAxis);

    // 全体の位置を修正する
    group.position.copy(this.nodei);

    this.setColor(isSelected, {
      mesh1: my_color[0].meshColor,
      mesh2: my_color[1].meshColor,
    });
    this.setText(isSelected, {
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

  private getColors(): ColorMaterial[] {
    const P1 = this.P1;
    const P2 = this.P2;
    if (P1 > 0) {
      if (P2 >= 0) {
        return [
          ThreeLoadTorsion.colorMaterials.red,
          ThreeLoadTorsion.colorMaterials.red,
        ];
      } else {
        return [
          ThreeLoadTorsion.colorMaterials.red,
          ThreeLoadTorsion.colorMaterials.blue,
        ];
      }
    } else if (P1 === 0) {
      if (P2 > 0) {
        return [
          ThreeLoadTorsion.colorMaterials.red,
          ThreeLoadTorsion.colorMaterials.red,
        ];
      } else if (P2 === 0) {
        throw new Error();
      } else {
        return [
          ThreeLoadTorsion.colorMaterials.blue,
          ThreeLoadTorsion.colorMaterials.blue,
        ];
      }
    } else {
      if (P2 > 0) {
        return [
          ThreeLoadTorsion.colorMaterials.blue,
          ThreeLoadTorsion.colorMaterials.red,
        ];
      } else {
        return [
          ThreeLoadTorsion.colorMaterials.blue,
          ThreeLoadTorsion.colorMaterials.blue,
        ];
      }
    }
  }

  // 座標
  private getPoints(
    rMax: number,
    scale: number
  ): {
    radius1: number;
    radius2: number;
    points: THREE.Vector3[];
  } {
    const P1 = this.P1;
    const P2 = this.P2;
    const L1 = this.L1;
    const L = this.L;
    // const L2 = this.L2;

    const x1 = L1;
    const x3 = L1 + L;
    let x2 = (x1 + x3) / 2;

    const y1 = (P1 / rMax) * this.magnifier * scale;
    const y3 = (P2 / rMax) * this.magnifier * scale;
    let y2 = (y1 + y3) / 2;

    if (P1 * P2 < 0) {
      const pp1 = Math.abs(P1);
      const pp2 = Math.abs(P2);
      x2 = (L * pp1) / (pp1 + pp2) + x1;
      y2 = 0;
    }

    return {
      radius1: Math.abs(y1),
      radius2: Math.abs(y3),
      points: [
        new THREE.Vector3(x1, y1, 0),
        new THREE.Vector3(x2, y2, 0),
        new THREE.Vector3(x3, y3, 0),
      ],
    };
  }

  // 面
  private getFace(
    my_color: ColorMaterial[],
    points: THREE.Vector3[]
  ): THREE.Mesh[] {
    const result: THREE.Mesh[] = new Array();

    for (let i = 0; i < my_color.length; i++) {
      const cylinder_mat = my_color[i].meshColor;

      const height = points[i + 1].x - points[i].x;
      const cylinder_geo = new THREE.CylinderBufferGeometry(
        Math.abs(points[i].y),
        Math.abs(points[i + 1].y),
        height, // radiusTop, radiusBottom, height
        12,
        1,
        true, // radialSegments, heightSegments, openEnded
        (-Math.PI * 2) / 6,
        (Math.PI * 10) / 6 // thetaStart, thetaLength
      );
      const mesh = new THREE.Mesh(cylinder_geo, cylinder_mat);
      mesh.rotation.z = Math.PI / 2;
      mesh.position.x = height / 2 + points[i].x;

      mesh.name = "mesh" + (i + 1).toString();

      result.push(mesh);
    }

    return result;
  }

  // 矢印
  private getArrow(
    values: number[],
    my_color: ColorMaterial[],
    points: THREE.Vector3[]
  ): THREE.Group[] {
    const result: THREE.Group[] = new Array();

    for (let i = 0; i < values.length; i++) {
      const arrowColor = my_color[i].meshColor;
      const lineColor = my_color[i].lineColor;
      const arrow: THREE.Group = ThreeLoadMoment.getArrow(
        "rx",
        values[i],
        Math.abs(points[i].y),
        arrowColor,
        lineColor
      );

      if (values[i] < 0) {
        arrow.rotateX(Math.PI);
      }

      arrow.position.set(points[i].x, 0, 0);

      // nameを group から arrow1 または arrow2 に変更する
      arrow.name = "arrow" + (i + 1).toString();

      result.push(arrow);
    }
    return result;
  }

  private setRotate(direction: string, group: any, localAxis: LocalAxis) {
    // 全体の向きを修正する
    if (!direction.includes("g")) {
      const XY = new THREE.Vector2(localAxis.x.x, localAxis.x.y).normalize();
      let A = Math.asin(XY.y);

      if (XY.x < 0) {
        A = Math.PI - A;
      }
      group.rotateZ(A);

      const lenXY = Math.sqrt(
        Math.pow(localAxis.x.x, 2) + Math.pow(localAxis.x.y, 2)
      );
      const XZ = new THREE.Vector2(lenXY, localAxis.x.z).normalize();
      group.rotateY(-Math.asin(XZ.y));

      if (localAxis.x.x === 0 && localAxis.x.y === 0) {
        // 鉛直の部材
        if (direction === "z") {
          group.rotateX(-Math.PI);
        } else if (direction === "y") {
          group.rotateX(Math.PI / 2);
        }
      } else {
        if (direction === "z") {
          group.rotateX(-Math.PI / 2);
        } else if (direction === "y") {
          group.rotateX(Math.PI);
        }
      }
    } else if (direction === "gx") {
      group.rotation.z = Math.asin(-Math.PI / 2);
    } else if (direction === "gz") {
      group.rotation.x = Math.asin(-Math.PI / 2);
    }
  }

  /** 荷重描画色データの退避先 */
  private setColorParams: SetColorParams;

  /**
   * 選択時と非選択時の寸法面の色の切り替え
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

    const mesh1 = this.getObjectByName("mesh1") as THREE.Mesh<
      THREE.CylinderBufferGeometry,
      THREE.MeshBasicMaterial
    >;
    if (mesh1) {
      const meshColor = isSelected
        ? ThreeLoadTorsion.meshColorSelected
        : params.mesh1;
      mesh1.material = meshColor;
    }

    const mesh2 = this.getObjectByName("mesh2") as THREE.Mesh<
      THREE.CylinderBufferGeometry,
      THREE.MeshBasicMaterial
    >;
    if (mesh2) {
      const meshColor = isSelected
        ? ThreeLoadTorsion.meshColorSelected
        : params.mesh2;
      mesh2.material = meshColor;
    }
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
        old.parent.remove(old);
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
      { key: "P1", value: this.P1, name: "arrow1" },
      { key: "P2", value: this.P2, name: "arrow2" },
    ].forEach(({ key, value, name }) => {
      const arrow = this.getObjectByName(name);
      const child = arrow.getObjectByName("child");

      // child.scale.xで除算しているのは、荷重値の大小に伴う荷重線のスケール調整がテキストの描画サイズに影響するのを防ぐため
      const textSize = (0.1 / child.scale.x) * scale;
      if (!isFinite(textSize)) {
        return;
      }

      const textString: string = value.toFixed(2) + " kNm/m";

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
    });
  }

  /**
   * ねじりモーメント荷重の描画インスタンス生成
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
   * @returns ねじりモーメント荷重の描画インスタンス。対象外の荷重の場合はundefined
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
  ): ThreeLoadTorsion | undefined {
    switch (mark) {
      case 1:
      case 9:
      case 11:
      default:
        return undefined;
      case 2:
        break;
    }
    if (is3d) {
      switch (direction) {
        case "r":
          break;
        default:
          return undefined;
      }
    } else {
      return undefined;
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

    return new ThreeLoadTorsion(
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
