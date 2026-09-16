import * as THREE from "three";

import {
  LoadData,
  LocalAxis,
  OffsetDirection,
  OffsetDict,
  MaxLoadDict,
  ConflictSection,
} from "./three-load-common";
import { ThreeLoadText3D, VAlign } from "./three-load-text";

/** 荷重描画色データ */
type SetColorParams = {
  /** 面の描画色 */
  faceColor: THREE.MeshBasicMaterial;
  /** 線の描画色 */
  lineColor: THREE.LineBasicMaterial;
};

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

/** 部材分布荷重データ(軸方向、モーメント、温度分布を除く) */
export class ThreeLoadDistribute extends LoadData {
  /** 荷重の種別 */
  readonly loadType = "DistributeLoad";

  /** 荷重図形の拡大倍率 */
  readonly magnifier = 1;

  /** 部材番号 */
  readonly memberNo: string;

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
  readonly wMax: number;
  /** 荷重値の最大値(部材ねじりモーメント) */
  readonly rMax: number = 0;
  /** 荷重値の最大値(部材軸方向分布荷重) */
  readonly qMax: number = 0;
  /** 部材軸を基点とした場合の正値の荷重の描画方向を示す単位ベクトル */
  readonly uLoad: THREE.Vector3;
  /** 部材軸を起点とした場合の正値の荷重の荷重描画方向を示す文字列 */
  readonly pOffsetDirection: OffsetDirection;
  /** 部材軸を起点とした場合の負値の荷重の荷重描画方向を示す文字列 */
  readonly nOffsetDirection: OffsetDirection;
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

  // this.children["group"].children["child"].children["face"] - 荷重面(?)
  // this.children["group"].children["child"].children["line"] - 荷重面を囲む線(部材軸を除く)
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

  static readonly faceMaterialRed = new THREE.MeshBasicMaterial({
    transparent: true,
    side: THREE.DoubleSide,
    color: 0xff0000,
    opacity: 0.3,
  });
  static readonly faceMaterialGreen = new THREE.MeshBasicMaterial({
    transparent: true,
    side: THREE.DoubleSide,
    color: 0x00ff00,
    opacity: 0.3,
  });
  static readonly faceMaterialBlue = new THREE.MeshBasicMaterial({
    transparent: true,
    side: THREE.DoubleSide,
    color: 0x0000ff,
    opacity: 0.3,
  });
  static readonly faceMaterialSelected = new THREE.MeshBasicMaterial({
    transparent: true,
    side: THREE.DoubleSide,
    color: 0xafeeee,
    opacity: 0.3,
  });

  static readonly lineMaterialRed = new THREE.LineBasicMaterial({
    color: 0xff0000,
  });
  static readonly lineMaterialGreen = new THREE.LineBasicMaterial({
    color: 0x00ff00,
  });
  static readonly lineMaterialBlue = new THREE.LineBasicMaterial({
    color: 0x0000ff,
  });
  static readonly lineMaterialSelected = new THREE.LineBasicMaterial({
    color: 0xffeeee,
  });

  /**
   * 部材分布荷重データ(軸方向、モーメント、温度分布を除く)インスタンスの生成
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

    this.memberNo = mNo;

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
    this.wMax = Math.max(Math.abs(P1), Math.abs(P2));

    const uLoad = new THREE.Vector3();
    let offsetdir: string;
    switch (direction) {
      case "x":
      default:
        throw new Error();
      case "y":
        uLoad.copy(localAxis.y);
        offsetdir = "ly";
        break;
      case "z":
        uLoad.copy(localAxis.z);
        offsetdir = "lz";
        break;
      case "gx":
        if (localAxis.x.y === 0 && localAxis.x.z === 0) {
          throw new Error();
        }
        uLoad.set(1, 0, 0);
        offsetdir = "gx";
        break;
      case "gy":
        if (localAxis.x.x === 0 && localAxis.x.z === 0) {
          throw new Error();
        }
        uLoad.set(0, 1, 0);
        offsetdir = "gy";
        break;
      case "gz":
        if (localAxis.x.x === 0 && localAxis.x.y === 0) {
          throw new Error();
        }
        uLoad.set(0, 0, 1);
        offsetdir = "gz";
        break;
    }
    this.uLoad = uLoad.negate();

    this.pOffsetDirection = `${offsetdir}-` as OffsetDirection;
    this.nOffsetDirection = `${offsetdir}+` as OffsetDirection;

    const vij = nodej.clone().sub(nodei); // i端節点からj端節点に向かうベクトル
    const vOrth = vij.clone().cross(uLoad); // 部材軸と荷重描画方向とに直交するベクトル
    // const uDim = vij.clone().cross(vOrth).normalize(); // 部材軸と直交し、かつ部材描画方向と逆向きの単位ベクトル
    this.uDimension = vOrth.normalize(); // uDim; // @TODO: 荷重描画面と寸法線関連が重なって見苦しいので直交させておく・・・

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

    const { pL1a, pL1b, pL2b, pL2a } = this.getLoadPoints(
      nodeOffsetDictMap,
      memberOffsetDictMap,
      maxLoadDict.wMax,
      scale
    );
    const { faceColor, lineColor } = this.getLoadColor(false); // 非選択時の色
    const face = this.getLoadFace(pL1a, pL1b, pL2b, pL2a, faceColor);
    const line = this.getLoadLine(pL1a, pL1b, pL2b, pL2a, lineColor);

    const child = new THREE.Group();
    child.add(face, line);
    child.name = "child";

    const group = new THREE.Group();
    group.add(child);
    group.name = "group";

    this.add(group);

    // 荷重値の描画方向(右)
    const vx = this.uLoad; // 荷重を描画する向き
    // 荷重値の描画方向(上)
    const vy = this.nodei.clone().sub(this.nodej).normalize(); // j端からi端に向かう向き
    // オイラー角
    const euler = ThreeLoadText3D.getEuler(vx, vy);

    this.setColor(isSelected, {
      faceColor: faceColor,
      lineColor: lineColor,
    });
    this.setText(isSelected, {
      pP1: pL1b,
      pP2: pL2b,
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

  private getLoadPoints(
    nodeOffsetDictMap: Map<string, OffsetDict>,
    memberOffsetDictMap: Map<string, OffsetDict>,
    wMax: number,
    scale: number
  ): {
    pL1a: THREE.Vector3;
    pL1b: THREE.Vector3;
    pL2b: THREE.Vector3;
    pL2a: THREE.Vector3;
  } {
    // この荷重の競合区間情報
    const conflictSection = new ConflictSection({
      start: this.L1,
      end: this.L1 + this.L,
    });

    // この荷重に関連するOffsetDictの抽出
    const correspondingOffsetDictList: {
      offsetDict: OffsetDict;
      conflictSection: ConflictSection;
      pConflicted: boolean | undefined; // 正値の荷重の場合の干渉有無
      nConflicted: boolean | undefined; // 負値の荷重の場合の干渉有無
    }[] = [];
    this.correspondingMemberNoList.forEach((no) => {
      if (no === this.memberNo) {
        correspondingOffsetDictList.push({
          offsetDict: memberOffsetDictMap.get(no),
          conflictSection: conflictSection,
          pConflicted: undefined, // 直後の処理で再設定する
          nConflicted: undefined, // 直後の処理で再設定する
        });
      } else {
        correspondingOffsetDictList.push({
          offsetDict: memberOffsetDictMap.get(no),
          conflictSection: ConflictSection.EndToEnd,
          pConflicted: undefined, // 直後の処理で設定する
          nConflicted: undefined, // 直後の処理で設定する
        });
      }
    });

    // 正値の荷重の描画方向のoffset
    const pOffsetData = correspondingOffsetDictList
      .map((dict) => {
        const offsetData = dict.offsetDict.get(
          this.pOffsetDirection,
          dict.conflictSection
        );
        dict.pConflicted = offsetData.conflicted; // 干渉の有無
        return offsetData;
      })
      .reduce((a, b) => (a.offset > b.offset ? a : b));
    // 負値の荷重の描画方向のoffset
    const nOffsetData = correspondingOffsetDictList
      .map((dict) => {
        const offsetData = dict.offsetDict.get(
          this.nOffsetDirection,
          dict.conflictSection
        );
        dict.nConflicted = offsetData.conflicted; // 干渉の有無
        return offsetData;
      })
      .reduce((a, b) => (a.offset > b.offset ? a : b));

    // 荷重の大きさの正規化
    const coef1 = (this.P1 / wMax) * this.magnifier * scale;
    const coef2 = (this.P2 / wMax) * this.magnifier * scale;

    // この荷重に適用するoffsetの決定
    let offset: number;
    const { aMax, aMin } =
      Math.abs(coef1) < Math.abs(coef2)
        ? { aMax: coef2, aMin: coef1 }
        : { aMax: coef1, aMin: coef2 };
    if (aMax < 0) {
      if (pOffsetData.offset === 0 && nOffsetData.offset === 0) {
        offset = 0;
      } else {
        offset = -nOffsetData.offset - Math.max(0, aMin);
      }
    } else {
      if (pOffsetData.offset === 0 && nOffsetData.offset === 0) {
        offset = 0;
      } else {
        offset = pOffsetData.offset - Math.min(0, aMin);
      }
    }

    // L1点の荷重描画開始点の座標
    const pL1a = this.pL1
      .clone()
      .add(this.uLoad.clone().multiplyScalar(offset));
    // L2点の荷重描画開始点の座標
    const pL2a = this.pL2
      .clone()
      .add(this.uLoad.clone().multiplyScalar(offset));

    // L1点の荷重頂点の座標
    const pL1b = pL1a.clone().add(this.uLoad.clone().multiplyScalar(coef1));
    // L2点の荷重頂点の座標
    const pL2b = pL2a.clone().add(this.uLoad.clone().multiplyScalar(coef2));

    // この荷重に関連するOffsetDictの更新(節点の情報も併せて更新する)
    this.correspondingNodeNoList.forEach((no) =>
      correspondingOffsetDictList.push({
        offsetDict: nodeOffsetDictMap.get(no),
        conflictSection: ConflictSection.EndToEnd,
        pConflicted: true,
        nConflicted: true,
      })
    );
    [offset + coef1, offset + coef2].forEach((nextOffset) => {
      if (nextOffset < 0) {
        correspondingOffsetDictList.forEach((dict) =>
          dict.offsetDict.update(
            this.nOffsetDirection,
            -nextOffset,
            dict.nConflicted,
            dict.conflictSection
          )
        );
      } else {
        correspondingOffsetDictList.forEach((dict) =>
          dict.offsetDict.update(
            this.pOffsetDirection,
            nextOffset,
            dict.pConflicted,
            dict.conflictSection
          )
        );
      }
    });

    return { pL1a, pL1b, pL2b, pL2a };
  }

  private getLoadColor(isSelected: boolean): {
    faceColor: THREE.MeshBasicMaterial;
    lineColor: THREE.LineBasicMaterial;
  } {
    const faceColor = new THREE.MeshBasicMaterial();
    const lineColor = new THREE.LineBasicMaterial();
    if (isSelected) {
      faceColor.copy(ThreeLoadDistribute.faceMaterialSelected);
      lineColor.copy(ThreeLoadDistribute.lineMaterialSelected);
    } else {
      switch (this.direction) {
        case "gx":
          faceColor.copy(ThreeLoadDistribute.faceMaterialRed);
          lineColor.copy(ThreeLoadDistribute.lineMaterialRed);
          break;
        case "y":
        case "gy":
          faceColor.copy(ThreeLoadDistribute.faceMaterialGreen);
          lineColor.copy(ThreeLoadDistribute.lineMaterialGreen);
          break;
        case "z":
        case "gz":
          faceColor.copy(ThreeLoadDistribute.faceMaterialBlue);
          lineColor.copy(ThreeLoadDistribute.lineMaterialBlue);
          break;
        default:
          throw new Error();
      }
    }
    return { faceColor, lineColor };
  }

  private getLoadFace(
    pL1a: THREE.Vector3,
    pL1b: THREE.Vector3,
    pL2b: THREE.Vector3,
    pL2a: THREE.Vector3,
    faceColor: THREE.MeshBasicMaterial
  ): THREE.Mesh {
    const points: THREE.Vector3[] = [];
    if (this.P1 * this.P2 < 0) {
      const p = this.getIntersection(pL1a, pL2a, pL1b, pL2b);
      points.push(pL1a, pL1b, p, pL2a, pL2b, p);
    } else if (this.P1 === 0) {
      points.push(pL1a, pL2b, pL2a);
    } else if (this.P2 === 0) {
      points.push(pL2a, pL1b, pL1a);
    } else {
      points.push(pL1a, pL1b, pL2b, pL2a, pL2b, pL1a);
    }
    const geometry = new THREE.BufferGeometry().setFromPoints(points);
    const mesh = new THREE.Mesh(geometry, faceColor);
    mesh.name = "face";
    return mesh;
  }

  /**
   * pA点とpB点を通る直線とpC点とpD点を通る直線の交点の座標を求める。交差していない場合は null を返す
   * @param pA pA点の座標
   * @param pB pB点の座標
   * @param pC pC点の座標
   * @param pD pD点の座標
   * @param threshold 交差しているとみなす最近点間の距離の最大値。デフォルト値は 0.00001
   * @returns pA点とpB点を通る直線とpC点とpD点を通る直線の交点の座標。交差していない場合は null
   */
  private getIntersection(
    pA: THREE.Vector3,
    pB: THREE.Vector3,
    pC: THREE.Vector3,
    pD: THREE.Vector3,
    threshold = 0.00001
  ): THREE.Vector3 | null {
    // 処理内容は下記URLの丸写し
    // http://www.sousakuba.com/Programming/gs_two_lines_intersect.html

    const vAB = pB.clone().sub(pA),
      n1 = vAB.clone().normalize();
    const vCD = pD.clone().sub(pC),
      n2 = vCD.clone().normalize();

    const wk1 = n1.dot(n2);
    const wk2 = 1 - wk1 * wk1;

    if (wk2 === 0) {
      return null;
    }

    const vAC = pC.clone().sub(pA);

    const dot1 = vAC.dot(n1);
    const dot2 = vAC.dot(n2);
    const d1 = (dot1 - wk1 * dot2) / wk2;
    const d2 = (wk1 * dot1 - dot2) / wk2;

    const pCross1 = pA.clone().add(n1.clone().multiplyScalar(d1));
    const pCross2 = pC.clone().add(n2.clone().multiplyScalar(d2));

    if (pCross1.distanceTo(pCross2) > threshold) {
      return null;
    }

    return pCross1;
  }

  private getLoadLine(
    pL1a: THREE.Vector3,
    pL1b: THREE.Vector3,
    pL2b: THREE.Vector3,
    pL2a: THREE.Vector3,
    lineColor: THREE.LineBasicMaterial
  ): THREE.Line<THREE.BufferGeometry, THREE.LineBasicMaterial> {
    const points = [pL1a, pL1b, pL2b, pL2a];
    const line_geo = new THREE.BufferGeometry().setFromPoints(points);
    const line = new THREE.Line(line_geo, lineColor);
    line.name = "line";
    return line;
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
    const face = this.getObjectByName("face") as THREE.Mesh<
      THREE.BufferGeometry,
      THREE.MeshBasicMaterial
    >;
    if (!face) {
      throw new Error();
    }

    const line = this.getObjectByName("line") as THREE.Line<
      THREE.BufferGeometry,
      THREE.LineBasicMaterial
    >;
    if (!line) {
      throw new Error();
    }

    if (params) {
      this.setColorParams = params;
    } else {
      params = this.setColorParams;
    }

    if (isSelected) {
      face.material = ThreeLoadDistribute.faceMaterialSelected;
      line.material = ThreeLoadDistribute.lineMaterialSelected;
    } else {
      face.material = params.faceColor;
      line.material = params.lineColor;
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
      {
        key: "P1",
        value: this.P1,
        pos: params.pP1,
        yOffset: -0.01,
        vAlign: "top" as VAlign,
      },
      {
        key: "P2",
        value: this.P2,
        pos: params.pP2,
        yOffset: 0.01,
        vAlign: "bottom" as VAlign,
      },
    ].forEach(({ key, value, pos, yOffset, vAlign }) => {
      value = Math.round(value * 100) / 100;
      if (value === 0) {
        return;
      }
      const textString = value.toFixed(2) + " kN/m";
      pos = pos.clone().add(params.vy.clone().multiplyScalar(yOffset)); // 荷重線とテキストの間を少し空ける
      const text = new ThreeLoadText3D(textString, pos, 0.1, {
        euler: params.euler,
        hAlign: "center",
        vAlign: vAlign,
      });
      text.name = key;

      text.scale.set(scale, scale, scale);

      this.add(text);
    });
  }

  /**
   * 部材分布荷重の描画インスタンス生成
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
   * @returns 部材分布荷重の描画インスタンス。対象外の荷重の場合はundefined
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
  ): ThreeLoadDistribute | undefined {
    switch (mark) {
      case 2:
        break;
      default:
        return undefined;
    }
    if (is3d) {
      switch (direction) {
        case "y":
        case "z":
          break;
        case "gx":
          if (localAxis.x.y === 0 && localAxis.x.z === 0) {
            return undefined;
          }
          break;
        case "gy":
          if (localAxis.x.x === 0 && localAxis.x.z === 0) {
            return undefined;
          }
          break;
        case "gz":
          if (localAxis.x.x === 0 && localAxis.x.y === 0) {
            return undefined;
          }
          break;
        default:
          return undefined;
      }
    } else {
      switch (direction) {
        case "y":
          break;
        case "gx":
          if (localAxis.x.y === 0 && localAxis.x.z === 0) {
            return undefined;
          }
          break;
        case "gy":
          if (localAxis.x.x === 0 && localAxis.x.z === 0) {
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

    return new ThreeLoadDistribute(
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
