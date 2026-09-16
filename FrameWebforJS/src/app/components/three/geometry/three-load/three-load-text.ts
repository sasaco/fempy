import * as THREE from "three";

class TextFontLoader {
  private loaded: THREE.Font;

  constructor() {
    const loader = new THREE.FontLoader();
    loader.load(
      "./assets/fonts/optimer_regular.typeface.json",
      (font) => (this.loaded = font)
    );
  }

  get font(): THREE.Font {
    return this.loaded;
  }
}

abstract class ThreeLoadTextBase extends THREE.Mesh<
  THREE.TextGeometry,
  THREE.MeshBasicMaterial[]
> {
  protected static readonly fontLoader = new TextFontLoader();

  protected static readonly textMaterial = [
    new THREE.MeshBasicMaterial({ color: 0x000000 }),
    new THREE.MeshBasicMaterial({ color: 0x000000 }),
  ];

  protected constructor(
    textGeo: THREE.TextGeometry,
    textMat: THREE.MeshBasicMaterial[]
  ) {
    super(textGeo, textMat);
  }
}

/** 水平方向の配置指定 */
export type HAlign = "left" | "center" | "right";
/** 垂直方向の配置指定 */
export type VAlign = "top" | "center" | "bottom";

/** 描画方向などを指定するパラメータ(2つのベクトルを指定するタイプ) */
type Text3DParamsTypeA = {
  /** テキストの左から右に向かう向きを指すベクトル */
  vx: THREE.Vector3;
  /** テキストの上向きを指すベクトル */
  vy: THREE.Vector3;
  /** 水平方向のテキスト配置指定(デフォルトは"center") */
  hAlign: HAlign | undefined;
  /** 垂直方向のテキスト配置指定(デフォルトは"center") */
  vAlign: VAlign | undefined;
};
/** 描画方向などを指定するパラメータ(オイラー角を指定するタイプ) */
type Text3DParamsTypeB = {
  /** vxとvyから計算したオイラー角 */
  euler: THREE.Euler;
  /** 水平方向のテキスト配置指定(デフォルトは"center") */
  hAlign: HAlign | undefined;
  /** 垂直方向のテキスト配置指定(デフォルトは"center") */
  vAlign: VAlign | undefined;
};

/** テキストデータ(3D用) */
export class ThreeLoadText3D extends ThreeLoadTextBase {
  /**
   *
   * @param textString 描画対象文字列
   * @param position 描画位置
   * @param size フォントサイズ
   * @param params 描画方向などを指定するパラメータ。
   * 指定がない場合は、x軸正方向がテキストの左から右に向かう向き、y軸正方向がテキストの上向き、z軸正方向がテキストの表の向き、指定した描画位置がテキストの中央となる。
   */
  constructor(
    textString: string,
    position: THREE.Vector3,
    size: number,
    params: Text3DParamsTypeA | Text3DParamsTypeB | undefined = undefined
  ) {
    const textGeometry = new THREE.TextGeometry(textString, {
      font: ThreeLoadTextBase.fontLoader.font,
      size: size,
      height: 0.001,
      curveSegments: 4,
      bevelEnabled: false,
    });

    if (params) {
      // super()呼び出し前はthisが使えないのでstaticメソッドにしてある
      ThreeLoadText3D.adjustPosition(
        textGeometry,
        params.hAlign,
        params.vAlign
      );
    }

    super(textGeometry, ThreeLoadTextBase.textMaterial);

    // https://zenn.dev/tktcorporation/articles/8757400a6aa0b40e64bd
    const isTypeA = (arg: unknown): arg is Text3DParamsTypeA =>
      typeof arg === "object" &&
      arg !== null &&
      typeof (arg as Text3DParamsTypeA).vx === "object" &&
      typeof (arg as Text3DParamsTypeA).vy === "object" &&
      typeof (arg as Text3DParamsTypeB).euler === "undefined";
    const isTypeB = (arg: unknown): arg is Text3DParamsTypeB =>
      typeof arg === "object" &&
      arg !== null &&
      typeof (arg as Text3DParamsTypeA).vx === "undefined" &&
      typeof (arg as Text3DParamsTypeA).vy === "undefined" &&
      typeof (arg as Text3DParamsTypeB).euler === "object";

    if (params) {
      let euler: THREE.Euler;
      if (isTypeA(params)) {
        euler = ThreeLoadText3D.getEuler(params.vx, params.vy);
      } else if (isTypeB(params)) {
        euler = params.euler;
      } else {
        throw new Error();
      }
      this.setRotationFromEuler(euler);
    }

    this.position.copy(position);
  }

  /**
   * テキストの水平方向と垂直方向の位置調整
   * @param textGeometry テキストの形状情報(THREE.TextGeometry)
   * @param hAlign 水平方向の配置指定 @default "center"
   * @param vAlign 垂直方向の配置指定 @default "center"
   */
  private static adjustPosition(
    textGeometry: THREE.TextGeometry,
    hAlign: HAlign,
    vAlign: VAlign
  ): void {
    hAlign ??= "center";
    vAlign ??= "center";

    textGeometry.computeBoundingBox();
    let xOffset: number;
    switch (hAlign) {
      case "left":
        xOffset = 0;
        break;
      case "center":
        xOffset = -textGeometry.boundingBox.max.x / 2;
        break;
      case "right":
        xOffset = -textGeometry.boundingBox.max.x;
        break;
      default:
        throw new Error();
    }
    let yOffset: number;
    switch (vAlign) {
      case "top":
        yOffset = -textGeometry.boundingBox.max.y;
        break;
      case "center":
        yOffset = -textGeometry.boundingBox.max.y / 2;
        break;
      case "bottom":
        yOffset = 0;
        break;
      default:
        throw new Error();
    }
    textGeometry.translate(xOffset, yOffset, 0);
  }

  /**
   * オイラー角の計算
   * @param vx テキストの左から右に向かう向きを指すベクトル
   * @param vy テキストの上向きを指すベクトル
   * @param threshold ジンバルロック検出用の閾値
   * @returns オイラー角
   */
  static getEuler(
    vx: THREE.Vector3,
    vy: THREE.Vector3,
    threshold = 1e-6
  ): THREE.Euler {
    // 下記URLのorder=="xyz"のケースを参考に作成
    // https://qiita.com/kit2cuz/items/55be3f432783fc979b16
    const X = vx.clone().normalize();
    const Y = vy
      .clone()
      .sub(X.clone().multiplyScalar(X.dot(vy)))
      .normalize();
    const Z = X.clone().cross(Y);
    let rx = Math.atan2(Y.z, Z.z);
    const ry = Math.asin(-X.z);
    let rz = Math.atan2(X.y, X.x);
    const judge = Math.abs(Math.cos(ry));
    if (judge < threshold) {
      rx = 0;
      rz = Math.atan2(-Y.x, Y.y);
    }
    const euler = new THREE.Euler(rx, ry, rz, "ZYX");
    return euler;
  }
}

/** テキストデータ(2D用) */
export class ThreeLoadText2D extends ThreeLoadTextBase {
  /**
   * テキスト描画インスタンスの生成(2D用)
   * @param textString 描画対象文字列
   * @param position 描画位置(描画される文字列の中央に該当)
   * @param size フォントサイズ
   */
  constructor(textString: string, position: THREE.Vector2, size: number) {
    const textGerometry = new THREE.TextGeometry(textString, {
      font: ThreeLoadTextBase.fontLoader.font,
      size: size,
      height: 0.001,
      curveSegments: 4,
      bevelEnabled: false,
    });
    textGerometry.center();

    super(textGerometry, ThreeLoadTextBase.textMaterial);

    this.position.set(position.x, position.y, 0);
  }
}
