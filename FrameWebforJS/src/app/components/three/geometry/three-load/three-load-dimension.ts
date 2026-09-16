import * as THREE from "three";

import { ThreeLoadText3D } from "./three-load-text";

/** 寸法線関連データ */
export class ThreeLoadDimension extends THREE.Group {
  /** 寸法線と寸法補助線の色 */
  private static readonly lineMaterial = new THREE.LineBasicMaterial({
    color: 0x000000,
  });

  /**
   * @param points 寸法線と寸法補助線の描画用データ(直線の始点と終点の座標の組のリスト。リストの最後の要素が寸法線用、それ以外は寸法補助線用)
   * @param textStr 寸法値テキスト
   * @param textScale 寸法値テキストの描画スケール
   */
  constructor(points: THREE.Vector3[][], textStr: string, textScale: number) {
    super();

    // 寸法線と寸法補助線の描画
    points.forEach((p) => {
      this.add(this.getLine(p));
    });

    // 寸法線の両端の座標
    const pxS = points[points.length - 1][0];
    const pxE = points[points.length - 1][1];
    // 寸法補助線(の内の1本)の両端の座標
    const pyS = points[0][0];
    const pyE = points[0][1];

    // 寸法線の中間点の座標
    const position = pxS.clone().lerp(pxE, 0.5);
    // 寸法値テキストの左から右への向きを指すベクトル
    const vx = pxE.clone().sub(pxS);
    // 寸法値テキストの上向きを指すベクトル
    const vy = pyE.clone().sub(pyS);
    // 寸法値テキストの描画
    const text = new ThreeLoadText3D(textStr, position, 0.08, {
      vx: vx,
      vy: vy,
      hAlign: "center",
      vAlign: "bottom",
    });
    text.name = "text";

    text.scale.set(1 * textScale, 2 * textScale, 0); // 縦長

    this.add(text);
  }

  private getLine(positions: THREE.Vector3[]): THREE.Line {
    const line_geo = new THREE.BufferGeometry().setFromPoints(positions);
    const line = new THREE.Line(line_geo, ThreeLoadDimension.lineMaterial);
    line.name = "line";

    return line;
  }
}
