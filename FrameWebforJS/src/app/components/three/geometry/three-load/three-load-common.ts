import * as THREE from "three";

import { ThreeLoadDimension } from "./three-load-dimension";

/** 部材座標系 */
export class LocalAxis {
  readonly x: THREE.Vector3;
  readonly y: THREE.Vector3;
  readonly z: THREE.Vector3;

  /**
   * @param data 部材座標系
   */
  constructor(data: { x: THREE.Vector3; y: THREE.Vector3; z: THREE.Vector3 }) {
    this.x = data.x;
    this.y = data.y;
    this.z = data.z;
  }

  /** 自身を複製する(ディープクローン) */
  clone(): LocalAxis {
    return new LocalAxis({
      x: this.x.clone(),
      y: this.y.clone(),
      z: this.z.clone(),
    });
  }
}

/** 他の部材の荷重と競合する可能性のある区間を示す情報 */
export class ConflictSection {
  private readonly list: { start: number; end: number }[] = [];

  /**
   * @param list startとendで表された競合区間データのリスト。startとendはいずれも部材のi端からの距離。「0 <= start <= end」の関係を満たす必要がある。
   * 個々のリスト要素が示す区間に重複があってもよい。リスト要素の並び順は任意でよい
   */
  constructor(...list: { start: number; end: number }[]) {
    list.forEach((elm) => {
      this.list.push({ start: elm.start, end: elm.end });
    });
  }

  /**
   * 競合の判定
   * @param target 他の部材の荷重の競合区間情報
   * @returns true=競合する、false=競合しない
   */
  conflictsTo(target: ConflictSection): boolean {
    for (const a of this.list) {
      for (const b of target.list) {
        if (a.start < b.end && a.end > b.start) {
          return true;
        }
      }
    }
    return false;
  }

  /**
   * 指定された競合区間情報を設定する
   * @param target 設定対象の競合区間情報
   */
  copy(target: ConflictSection): void {
    this.list.splice(0);
    this.list.push(...target.list);
  }

  /**
   * 指定された競合区間情報を追加する
   * @param target 追加対象の競合区間情報
   */
  append(target: ConflictSection): void {
    this.list.push(...target.list);
    // (必須ではないが)重複する区間を一つにまとめる
    this.list.sort((a, b) => a.start - b.start);
    for (let i = this.list.length - 1; i > 0; i--) {
      const ii = this.list[i];
      for (let j = i - 1; j >= 0; j--) {
        const jj = this.list[j];
        if (ii.start <= jj.end && ii.end >= jj.start) {
          const start = Math.min(ii.start, jj.start);
          const end = Math.max(ii.end, jj.end);
          this.list.splice(j, 1, { start: start, end: end });
          this.list.splice(i, 1);
        }
      }
    }
  }

  /** 部材全体が競合区間であることを示す情報 */
  static readonly EndToEnd = new ConflictSection({
    start: 0,
    end: Number.MAX_VALUE,
  });
}

const offsetDirectionList = [
  "rlx", // 11x
  "rly", // 11y
  "rlz", // 11z
  "rgx", // rx, 11gx
  "rgy", // ry, 11gy
  "rgz", // rz, 11gz
  "ly+", // 1y, 2y, 1x
  "ly-", // 1y, 2y, 9
  "lz+", // 1z, 2z
  "lz-", // 1z, 2z
  "gx+", // tx, 1gx, 2gx
  "gx-", // tx, 1gx, 2gx
  "gy+", // ty, 1gy, 2gy
  "gy-", // ty, 1gy, 2gy
  "gz+", // tz, 1gz, 2gz
  "gz-", // tz, 1gz, 2gz
  "R", // 2r
] as const;
/** オフセットの向き */
export type OffsetDirection = (typeof offsetDirectionList)[number];

/** 節点または部材のオフセットの向き別オフセット情報 */
export class OffsetDict {
  /** 部材座標系(部材用) */
  private localAxis: LocalAxis | undefined;
  /** オブセットの向き別オフセット情報 */
  private dict: {
    [
      /** オフセットの向き */
      key: string
    ]: {
      /** 上に描画される部材の荷重と競合する可能性のある部材の区間情報 */
      conflictSection: ConflictSection;
      /** 上に描画される部材の荷重が現在の荷重と競合*する*場合に使用されるオフセット値 */
      currOffset: number;
      /** 上に描画される部材の荷重が現在の荷重と競合*しない*場合に使用されるオフセット値 */
      lastOffset: number;
    };
  } = {};

  /**
   * @param localAxis 部材座標系(部材用)
   */
  constructor(localAxis: LocalAxis | undefined = undefined) {
    this.localAxis = localAxis;

    this.reset();
  }

  /** オフセット情報の初期化 */
  reset(): void {
    offsetDirectionList.forEach(
      (key) =>
        (this.dict[key] = {
          conflictSection: new ConflictSection(), // @TODO: もしくは ConflictSection.EndToEnd
          currOffset: 0,
          lastOffset: 0,
        })
    );
  }

  private getParam_rlx(): Map<string, number> {
    if (!this.localAxis) {
      return new Map<string, number>();
    }
    const result = new Map<string, number>([
      ["ly+", 1],
      ["ly-", 1],
      ["lz+", 1],
      ["lz-", 1],
    ]);
    [
      { gvec: new THREE.Vector3(1, 0, 0), gdir: "gx" },
      { gvec: new THREE.Vector3(0, 1, 0), gdir: "gy" },
      { gvec: new THREE.Vector3(0, 0, 1), gdir: "gz" },
    ].forEach(({ gvec, gdir }) => {
      const angle = this.localAxis.x.angleTo(gvec);
      const csc = 1 / Math.abs(Math.sin(angle));
      if (isFinite(csc)) {
        result.set(`${gdir}+`, csc);
        result.set(`${gdir}-`, csc);
      }
    });
    return result;
  }
  private getParam_rly(
    params:
      | { ldirs: OffsetDirection[]; lvec: THREE.Vector3 }
      | undefined = undefined
  ): Map<string, number> {
    if (!this.localAxis) {
      return new Map<string, number>();
    }
    params ??= { ldirs: ["lz+", "lz-"], lvec: this.localAxis.y };
    const result = new Map<string, number>();
    params.ldirs.forEach((ldir) => result.set(ldir, 1));
    [
      {
        gvec: new THREE.Vector3(1, 0, 0),
        gdir: "gx",
      },
      {
        gvec: new THREE.Vector3(0, 1, 0),
        gdir: "gy",
      },
      {
        gvec: new THREE.Vector3(0, 0, 1),
        gdir: "gz",
      },
    ].forEach(({ gvec, gdir }) => {
      if (params.lvec.dot(gvec) === 0) {
        const angle = this.localAxis.x.angleTo(gvec);
        const csc = 1 / Math.abs(Math.sin(angle));
        if (isFinite(csc)) {
          result.set(`${gdir}+`, csc);
          result.set(`${gdir}-`, csc);
        }
      }
    });
    return result;
  }
  private getParam_rlz(): Map<string, number> {
    if (!this.localAxis) {
      return new Map<string, number>();
    }
    return this.getParam_rly({ ldirs: ["ly+", "ly-"], lvec: this.localAxis.z });
  }
  private getParam_rgx(
    params:
      | { gdirs: OffsetDirection[]; gvec: THREE.Vector3 }
      | undefined = undefined
  ): Map<string, number> {
    params ??= {
      gdirs: ["gy+", "gy-", "gz+", "gz-"],
      gvec: new THREE.Vector3(1, 0, 0),
    };
    const result = new Map<string, number>();
    params.gdirs.forEach((gdir) => result.set(gdir, 1));
    if (this.localAxis) {
      [
        { lvec: this.localAxis.y, ldir: "ly", lxlvecNormal: this.localAxis.z },
        { lvec: this.localAxis.z, ldir: "lz", lxlvecNormal: this.localAxis.y },
      ].forEach(({ lvec, ldir, lxlvecNormal }) => {
        const cross = lxlvecNormal.clone().cross(params.gvec);
        const angle = cross.angleTo(lvec);
        const cos = Math.abs(Math.cos(angle));
        if (cos !== 0) {
          result.set(`${ldir}+`, cos);
          result.set(`${ldir}-`, cos);
        }
      });
    }
    return result;
  }
  private getParam_rgy(): Map<string, number> {
    return this.getParam_rgx({
      gdirs: ["gx+", "gx-", "gz+", "gz-"],
      gvec: new THREE.Vector3(0, 1, 0),
    });
  }
  private getParam_rgz(): Map<string, number> {
    return this.getParam_rgx({
      gdirs: ["gx+", "gx-", "gy+", "gy-"],
      gvec: new THREE.Vector3(0, 0, 1),
    });
  }
  private getParam_ly(
    pm: "+" | "-",
    params:
      | { lvec: THREE.Vector3; ldir: string; lxlvecNormal: THREE.Vector3 }
      | undefined = undefined
  ): Map<string, number> {
    if (!this.localAxis) {
      return new Map<string, number>();
    }
    params ??= {
      lvec: this.localAxis.y,
      ldir: "ly",
      lxlvecNormal: this.localAxis.z,
    };
    const result = new Map<string, number>([[`${params.ldir}${pm}`, 1]]);
    [
      {
        gvec: new THREE.Vector3(1, 0, 0),
        gdir: "gx",
      },
      {
        gvec: new THREE.Vector3(0, 1, 0),
        gdir: "gy",
      },
      {
        gvec: new THREE.Vector3(0, 0, 1),
        gdir: "gz",
      },
    ].forEach(({ gvec, gdir }) => {
      const lxgvecNormal = this.localAxis.x.clone().cross(gvec); // lx-gvec面の法線ベクトル
      if (params.lxlvecNormal.clone().cross(lxgvecNormal).length() === 0) {
        // lx-lvec面とlx-gvec面が平行な場合
        const angle = params.lvec.angleTo(gvec);
        const sec = 1 / Math.abs(Math.cos(angle));
        if (isFinite(sec)) {
          if (angle < Math.PI / 2) {
            result.set(`${gdir}${pm}`, sec);
          } else {
            result.set(`${gdir}${pm === "+" ? "-" : "+"}`, sec);
          }
        }
        return;
      }
    });
    return result;
  }
  private getParam_lz(pm: "+" | "-"): Map<string, number> {
    if (!this.localAxis) {
      return new Map<string, number>();
    }
    return this.getParam_ly(pm, {
      lvec: this.localAxis.z,
      ldir: "lz",
      lxlvecNormal: this.localAxis.y,
    });
  }
  private getParam_gx(
    pm: "+" | "-",
    { gvec, gdir } = { gvec: new THREE.Vector3(1, 0, 0), gdir: "gx" }
  ): Map<string, number> {
    const result = new Map<string, number>([[`${gdir}${pm}`, 1]]);
    if (this.localAxis) {
      const lxgvecNormal = this.localAxis.x.clone().cross(gvec);
      [
        {
          lvec: this.localAxis.y,
          ldir: "ly",
        },
        {
          lvec: this.localAxis.z,
          ldir: "lz",
        },
      ].forEach(({ lvec, ldir }) => {
        const lxlvecNormal = this.localAxis.x.clone().cross(lvec); // lx-lvec面の法線ベクトル
        if (lxlvecNormal.clone().cross(lxgvecNormal).length() === 0) {
          // lx-lvec面とlx-gvec面が平行な場合
          const angle = lvec.angleTo(gvec);
          const cos = Math.abs(Math.cos(angle));
          if (cos !== 0) {
            if (angle < Math.PI / 2) {
              result.set(`${ldir}${pm}`, cos);
            } else {
              result.set(`${ldir}${pm === "+" ? "-" : "+"}`, cos);
            }
          }
          return;
        }
      });
    }
    return result;
  }
  private getParam_gy(pm: "+" | "-"): Map<string, number> {
    return this.getParam_gx(pm, {
      gvec: new THREE.Vector3(0, 1, 0),
      gdir: "gy",
    });
  }
  private getParam_gz(pm: "+" | "-"): Map<string, number> {
    return this.getParam_gx(pm, {
      gvec: new THREE.Vector3(0, 0, 1),
      gdir: "gz",
    });
  }
  private getParam_R(): Map<string, number> {
    const result = new Map<string, number>();
    if (this.localAxis) {
      ["ly+", "ly-", "lz+", "lz-"].forEach((ldir) => result.set(ldir, 1));
      [
        { gvec: new THREE.Vector3(1, 0, 0), gdir: "gx" },
        { gvec: new THREE.Vector3(0, 1, 0), gdir: "gy" },
        { gvec: new THREE.Vector3(0, 0, 1), gdir: "gz" },
      ].forEach(({ gvec, gdir }) => {
        const angle = this.localAxis.x.angleTo(gvec);
        const csc = 1 / Math.abs(Math.sin(angle));
        if (isFinite(csc)) {
          result.set(`${gdir}+`, csc);
          result.set(`${gdir}-`, csc);
        }
      });
    }
    return result;
  }

  /** 指定されたオフセットの向きごとのオフセットの向き別オフセット補正係数 */
  private coefDict: {
    [key: string]: Map<string, number> | undefined;
  } = {
    rlx: undefined,
    rly: undefined,
    rlz: undefined,
    rgx: undefined,
    rgy: undefined,
    rgz: undefined,
    "ly+": undefined,
    "ly-": undefined,
    "lz+": undefined,
    "lz-": undefined,
    "gx+": undefined,
    "gx-": undefined,
    "gy+": undefined,
    "gy-": undefined,
    "gz+": undefined,
    "gz-": undefined,
    R: undefined,
  };

  /**
   * 指定されたオフセットの向きに対するオフセット値でオフセット情報を更新する
   * @param offsetdir オフセットの向き
   * @param newOffset 更新用オフセット値
   * @param conflicted オフセット値の取得時に荷重の競合が検出されていたか
   * @param conflictSection 対象の荷重の干渉区間データ
   */
  update(
    offsetdir: OffsetDirection,
    newOffset: number,
    conflicted: boolean,
    conflictSection: ConflictSection
  ): void {
    if (!this.coefDict[offsetdir]) {
      let map: Map<string, number>;
      switch (offsetdir) {
        case "rlx":
          map = this.getParam_rlx();
          break;
        case "rly":
          map = this.getParam_rly();
          break;
        case "rlz":
          map = this.getParam_rlz();
          break;
        case "rgx":
          map = this.getParam_rgx();
          break;
        case "rgy":
          map = this.getParam_rgy();
          break;
        case "rgz":
          map = this.getParam_rgz();
          break;
        case "ly+":
          map = this.getParam_ly("+");
          break;
        case "ly-":
          map = this.getParam_ly("-");
          break;
        case "lz+":
          map = this.getParam_lz("+");
          break;
        case "lz-":
          map = this.getParam_lz("-");
          break;
        case "gx+":
          map = this.getParam_gx("+");
          break;
        case "gx-":
          map = this.getParam_gx("-");
          break;
        case "gy+":
          map = this.getParam_gy("+");
          break;
        case "gy-":
          map = this.getParam_gy("-");
          break;
        case "gz+":
          map = this.getParam_gz("+");
          break;
        case "gz-":
          map = this.getParam_gz("-");
          break;
        case "R":
          map = this.getParam_R();
          break;
        default:
          throw new Error();
      }
      this.coefDict[offsetdir] = map;
    }
    this.coefDict[offsetdir].forEach((coef, dir) => {
      const actualNewOffset = newOffset * coef;
      const offsetData = this.dict[dir];
      if (conflicted) {
        offsetData.conflictSection.copy(conflictSection);
        offsetData.lastOffset = offsetData.currOffset;
        offsetData.currOffset = Math.max(
          offsetData.currOffset,
          actualNewOffset
        );
      } else {
        offsetData.conflictSection.append(conflictSection);
        // lastOffsetはそのまま
        offsetData.currOffset = Math.max(
          offsetData.currOffset,
          actualNewOffset
        );
      }
    });
  }

  /**
   * 指定された向きのオフセット値と荷重の競合の発生有無を返す
   * @param direction オフセットの向き
   * @param conflictSection 対象の荷重の競合区間情報
   * @returns offset=指定された向きのオフセット値、conflicted=荷重の競合の発生有無
   */
  get(
    direction: OffsetDirection,
    conflictSection: ConflictSection
  ): { offset: number; conflicted: boolean } {
    const offsetData = this.dict[direction];
    if (offsetData.conflictSection.conflictsTo(conflictSection)) {
      return { offset: offsetData.currOffset, conflicted: true };
    } else {
      return { offset: offsetData.lastOffset, conflicted: false };
    }
  }
}

/** 各種荷重値の最大値 */
export type MaxLoadDict = {
  /** 節点荷重(t)と部材集中荷重(1)の最大値 */
  pMax: number;
  /** 節点モーメント(r)と部材集中モーメント(11)の最大値 */
  mMax: number;
  /** 部材分布荷重(2xと2rを除く)の最大値 */
  wMax: number;
  /** 部材ねじりモーメント(2r)の最大値 */
  rMax: number;
  /** 部材軸方向分布荷重(2x) */
  qMax: number;
};

/** 寸法線描画用データ */
export type SetDimParams = {
  /** 描画スケール */
  scale: number;
  /** i端節点とL1点の距離 */
  L1: number;
  /** L1点とL2点の距離 */
  L: number;
  /** j端節点とL2点の距離 */
  L2: number;
  /** i端節点の座標 */
  pi: THREE.Vector3;
  /** L1点の座標 */
  pL1: THREE.Vector3;
  /** L2点の座標 */
  pL2: THREE.Vector3;
  /** j端節点の座標 */
  pj: THREE.Vector3;
  /** 寸法線関連を描画する向きを表す単位ベクトル */
  uDimension: THREE.Vector3;
};

/** 荷重データの基底クラス */
export abstract class LoadData extends THREE.Group {
  /** 荷重の種別 */
  abstract get loadType(): string;

  /**
   * 荷重図の再配置
   * @param nodeOffsetDictMap key=節点番号、value=各接点のOffsetDict
   * @param memberOffsetDictMap key=部材番号、value=各部材のOffsetDict
   * @param maxLoadDict
   * @param scale 描画スケール
   * @param isSelected true=ハイライト表示、false=ハイライト表示解除、undefined=状態継続
   */
  abstract relocate(
    nodeOffsetDictMap: Map<string, OffsetDict>,
    memberOffsetDictMap: Map<string, OffsetDict>,
    maxLoadDict: MaxLoadDict,
    scale: number,
    isSelected: boolean | undefined
  ): void;

  /**
   * 選択状態と非選択状態の切り替え
   * @param isSelected true=選択状態、false=非選択状態
   */
  abstract highlight(isSelected: boolean): void;

  /** この荷重と関連を持つ節点の節点番号一覧 */
  abstract get correspondingNodeNoList(): string[];
  /** この荷重と関連を持つ部材の部材番号一覧 */
  abstract get correspondingMemberNoList(): string[];

  /** 荷重値の最大値(節点荷重と部材集中荷重) */
  abstract get pMax(): number;
  /** 荷重値の最大値(節点モーメントと部材集中モーメント) */
  abstract get mMax(): number;
  /** 荷重値の最大値(部材分布荷重) */
  abstract get wMax(): number;
  /** 荷重値の最大値(部材ねじりモーメント) */
  abstract get rMax(): number;
  /** 荷重値の最大値(部材軸方向分布荷重) */
  abstract get qMax(): number;

  /** 荷重テーブルの列情報(m=部材荷重、p=節点荷重) */
  abstract get col(): "m" | "p";

  /** 荷重テーブルの行番号 */
  abstract get row(): number;

  /**
   * 荷重の積み上げ順を決める数値(値が小さいほど節点または部材に近く表示する。モーメント系は積み上げ対象外)
   * 0=節点モーメント(r)、部材集中モーメント(11)、部材分布モーメント(2r)
   * 10=部材集中荷重(1)、部材分布荷重(2)、温度(9)
   * 20=節点荷重(t)
   */
  abstract get rank(): number;

  // this.name: string - 例：DistributeLoad-3-y
  // this.position: Vector3 - 部材基準点(i端節点とj端節点の中点)の座標

  /**
   * 荷重値や寸法値のテキスト描画スケール調整
   * @param scale 描画スケール
   * @returns 調整後の描画スケール
   */
  protected adjustTextScale(scale: number): number {
    // 「0.2」が基準となる描画スケール
    // テキストがあまり大きくならないように「2」を上限としておく
    const adjusted = Math.min(scale / 0.2, 2);
    return adjusted;
  }

  /** 寸法線描画用データの退避先 */
  private setDimParams: SetDimParams;

  /**
   * 選択時は寸法線関連を描画し、非選択時はクリアする
   * @param isSelected true=選択状態、false=非選択状態
   * @param params relocate()から呼び出された時は寸法線描画用データ、highlight()から呼び出された時はundefined
   */
  protected setDim(
    isSelected: boolean,
    params: SetDimParams | undefined = undefined
  ): void {
    // 一旦削除
    const old = this.getObjectByName("Dimension");
    if (old) {
      this.remove(old);
    }

    if (params) {
      this.setDimParams = params;
    }

    if (!isSelected) {
      return;
    }

    params ??= this.setDimParams;

    // 部材L1点またはL2点から寸法補助線の始点までの距離
    const offset = 0;

    // i端節点とL1点の距離
    const L1 = params.L1;
    // L1点とL2点の距離
    const L = params.L;
    // j端節点とL2点の距離
    const L2 = params.L2;

    // 寸法補助線の長さ(でっぱりを除く)
    const size = 1 * params.scale;
    // 寸法補助線のでっぱりの長さ
    const protrude = 0.03 * params.scale;

    // テキストの描画スケール
    const textScale = this.adjustTextScale(params.scale);

    const dim = new THREE.Group();

    // L1点の座標
    const pL1 = params.pL1;
    // L2点の座標
    const pL2 = params.pL2;
    // 部材軸に直交しており、かつ荷重面に平行な単位ベクトル(寸法補助線の向き)
    const uDimension = params.uDimension;

    // 寸法補助線の始点(L1点)
    const pL1a = pL1.clone().add(uDimension.clone().multiplyScalar(offset));
    // 寸法線の始点(L1点)
    const pL1b = pL1a.clone().add(uDimension.clone().multiplyScalar(size));
    // 寸法補助線の終点(L1点)
    const pL1c = pL1b.clone().add(uDimension.clone().multiplyScalar(protrude));

    // 寸法補助線の始点(L2点)
    const pL2a = pL2.clone().add(uDimension.clone().multiplyScalar(offset));
    // 寸法線の始点(L2点)
    const pL2b = pL2a.clone().add(uDimension.clone().multiplyScalar(size));
    // 寸法補助線の終点(L2点)
    const pL2c = pL2b.clone().add(uDimension.clone().multiplyScalar(protrude));

    const pp: THREE.Vector3[][] = [
      [pL1a, pL1c], // 寸法補助線(L1点)
      [pL2a, pL2c], // 寸法補助線(L2点)
      [pL1b, pL2b], // 寸法線
    ];
    const dim1 = new ThreeLoadDimension(pp, L.toFixed(3), textScale);
    dim1.visible = true;
    dim1.name = "Dimentsion1";
    dim.add(dim1);

    if (L1 > 0) {
      // 部材i端の座標
      const pi = params.pi;

      // 寸法補助線の始点(i端)
      const pia = pi.clone().add(uDimension.clone().multiplyScalar(offset));
      // 寸法線の始点(i端)
      const pib = pia.clone().add(uDimension.clone().multiplyScalar(size));
      // 寸法補助線の終点(i端)
      const pic = pib.clone().add(uDimension.clone().multiplyScalar(protrude));

      const pp: THREE.Vector3[][] = [
        [pia, pic], // 寸法補助線(i端)
        [pib, pL1b], // 寸法線
      ];
      const dim2 = new ThreeLoadDimension(pp, L1.toFixed(3), textScale);
      dim2.visible = true;
      dim2.name = "Dimentsion2";
      dim.add(dim2);
    }

    if (L2 > 0) {
      // 部材j端の座標
      const pj: THREE.Vector3 = params.pj;

      // 寸法補助線の始点(j端)
      const pja = pj.clone().add(uDimension.clone().multiplyScalar(offset));
      // 寸法線の始点(j端)
      const pjb = pja.clone().add(uDimension.clone().multiplyScalar(size));
      // 寸法補助線の終点(j端)
      const pjc = pjb.clone().add(uDimension.clone().multiplyScalar(protrude));

      const pp: THREE.Vector3[][] = [
        [pja, pjc], // 寸法補助線(j端)
        [pL2b, pjb], // 寸法線
      ];
      const dim3 = new ThreeLoadDimension(pp, L2.toFixed(3), textScale);
      dim3.visible = true;
      dim3.name = "Dimentsion3";
      dim.add(dim3);
    }

    // 登録
    dim.name = "Dimension";

    this.add(dim);
  }
}
