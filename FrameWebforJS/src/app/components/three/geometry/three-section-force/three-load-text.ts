import { Injectable } from '@angular/core';
import * as THREE from "three";

@Injectable({
  providedIn: 'root'
})
export class ThreeLoadText { 

  private font: THREE.Font;
  private text_mat: THREE.MeshBasicMaterial[];
  constructor(font: THREE.Font) {
  const loader = new THREE.FontLoader();
   let self = this;
    loader.load( './assets/fonts/optimer_regular.typeface.json', function ( font ) {
      self.font = font;
    } );
    this.text_mat = [
      new THREE.MeshBasicMaterial({ color: 0x000000 }),
      new THREE.MeshBasicMaterial({ color: 0x000000 }),
    ];
  }

  // 文字を描く
  public create(
    textString: string,
    position: THREE.Vector2,
    size: number,
    offset: number = 0): THREE.Mesh {

    const text_geo = new THREE.TextGeometry(textString, {
      font: this.font,
      size: size,
      height: 0.001,
      curveSegments: 4,
      bevelEnabled: false,
    });

    text_geo.center();

    const text = new THREE.Mesh(text_geo, this.text_mat);

    text.position.set(position.x, position.y + offset, 0);

    return text;
  }

  create3d(
    textString: string,
    position: THREE.Vector3,
    size: number,
    offset: number = 0
  ): THREE.Mesh {

    const text_geo = new THREE.TextGeometry(textString, {
      font: this.font,
      size: size,
      height: 0.001,
      curveSegments: 4,
      bevelEnabled: false,
    });

    text_geo.center();

    const text = new THREE.Mesh(text_geo, this.text_mat);

    text.position.set(position.x, position.y, position.z);

    return text;
  }
}
