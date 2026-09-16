import { Inject, Injectable } from '@angular/core';
import * as THREE from "three";
import { Vector2 } from 'three';
import { ThreeLoadText } from './three-load-text';


@Injectable({
  providedIn: 'root'
})
export class ThreeSectionForceMeshService {

  private text: ThreeLoadText;

  private face_mat: THREE.MeshBasicMaterial;
  private line_mat: THREE.LineBasicMaterial;

  constructor(font: THREE.Font) {
    this.text = new ThreeLoadText(font);

    this.face_mat = new THREE.MeshBasicMaterial({
      transparent: true,
      side: THREE.DoubleSide,
      color: 0x0000ff,
      opacity: 0.1,
    });

    this.line_mat = new THREE.LineBasicMaterial({ color: 0x0000ff });

  }

  /// 断面力を編集する
  // target: 編集対象の荷重,
  // nodei: 部材始点,
  // nodej: 部材終点,
  // localAxis: 部材座標系
  // direction: 荷重の向き(wy, wz)
  // L1: 始点からの距離
  // L2: 終点からの距離
  // P1: 始点側の荷重値
  // P2: 終点側の荷重値
  // offset: 配置位置（その他の荷重とぶつからない位置）
  // scale: スケール
  public create(
    nodei: THREE.Vector3,
    nodej: THREE.Vector3,
    localAxis: any,
    direction: string,
    pL1: number,
    pL2: number,
    P1: number,
    P2: number,
    dummy1: boolean,
    dummy2: boolean,
    cg?: number
  ): THREE.Group {

    const child = new THREE.Group();

    // 長さを決める
    const p  = this.getPoints(
      nodei, nodej, direction, pL1, pL2, P1, P2, cg);

    const points: THREE.Vector3[] = p.points;

    // 面
    child.add(this.getFace(points));

    // 線
    child.add(this.getLine(points));

    // 全体
    child.name = "child";

    const group0 = new THREE.Group();
    group0.add(child);
    group0.name = "group";


     // 全体の位置を修正する
    const group = new THREE.Group();
    group.add(group0);

    group['value'] = p.Pmax; // 大きい方の値を保存　
    group.position.set(nodei.x, nodei.y, nodei.z);
    // // 全体の向きを修正する
    // group.rotation.set(0, 0, 0); 
    // this.setRotate(direction, group, localAxis);

    group.name = "SectionForce";

    group["points"] = p.points;
    group["L1"] = p.L1;
    group["L"] = p.L;
    group["L2"] = p.L2;
    group["P1"] = P1;
    group["P2"] = P2;
    group["nodei"] = nodei;
    group["nodej"] = nodej;
    group["direction"] = direction;
    group["localAxis"] = localAxis;
    group["dummy1"] = dummy1;
    group["dummy2"] = dummy2;

    return group;
  }


  // 座標
  private getPoints(
    nodei: THREE.Vector3, nodej: THREE.Vector3, direction: string,
    pL1: number, pL2: number, P1: number, P2: number, cg?: number ): any {

    const len = nodei.distanceTo(nodej);

    let LL: number = len;
    const L1 = pL1 * len / LL;
    const L2 = pL2 * len / LL;
    const L: number = len - L1 - L2;

     // 荷重の各座標
    let x1 = L1;
    let x3 = L1 + L;
    let x2 = (x1 + x3) / 2;

    // y座標 値の大きい方が１となる
    const Pmax = (Math.abs(P1) > Math.abs(P2)) ? P1 : P2;

    const y1 = P1;
    const y3 = P2;
    let y2 = (y1 + y3) / 2;

    const sg1 = Math.sign(P1);
    const sg2 = Math.sign(P2);
    if (sg1 !== sg2 && sg1 * sg2 !== 0) {
      const pp1 = Math.abs(P1);
      const pp2 = Math.abs(P2);
      x2 = L * pp1 / (pp1 + pp2) + x1;
      y2 = 0;
    }
    
    let testP = [
      new THREE.Vector3(x1, 0, 0),  // 0
      new THREE.Vector3(x1, -y1, 0), // 1
      new THREE.Vector3(x2, -y2, 0), // 2

      new THREE.Vector3(x2, -y2, 0), // 2
      new THREE.Vector3(x3, -y3, 0), // 3
      new THREE.Vector3(x3, 0, 0),  // 4

      new THREE.Vector3(x1, 0, 0),  // 0
      new THREE.Vector3(x2, -y2, 0), // 2
      new THREE.Vector3(x3, 0, 0),  // 4
    ];
    if(cg != 0 && cg != undefined){
      let angle = THREE.MathUtils.degToRad(cg);
      let rotationMatrixX = new THREE.Matrix4().makeRotationX(angle);
      testP.forEach(x => x.applyMatrix4(rotationMatrixX))
    }
 
   
    return {
      points: testP,
      L1,
      L,
      L2,
      Pmax
    };
  }

  // 面
  private getFace(points: THREE.Vector3[]): THREE.Mesh {

    const face_geo = new THREE.BufferGeometry().setFromPoints( points )

    // const face_geo = new THREE.Geometry();
    // face_geo.vertices = points;
    
    // face_geo.faces.push(new THREE.Face3(0, 1, 2));
    // face_geo.faces.push(new THREE.Face3(2, 3, 4));
    // face_geo.faces.push(new THREE.Face3(0, 2, 4));

    const mesh = new THREE.Mesh(face_geo, this.face_mat);
    mesh.name = "face";
    return mesh;

  }

  // 枠線
  private getLine(points: THREE.Vector3[]): THREE.Line {

    const line_point = [
      points[0],
      points[1],
      points[2],
      points[3],
      points[4],
      points[5]
    ]

    const line_geo = new THREE.BufferGeometry().setFromPoints(line_point);
    const line = new THREE.Line(line_geo, this.line_mat);
    line.name = "line";

    return line;
  }

  // 文字
  public setText(group: any, Enable1: boolean, Enable2: boolean): any {

    const child = group.getObjectByName('group');

    // 一旦削除
    const names = [];
    for(const text of child.children){
      if(text.name.includes('P')){ 
        // name に P が付いた children 
        names.push(text.name)
      }
    }
    for(const key  of names){
        const text = group.getObjectByName(key);
        child.remove(text); // を消す
        // text.dispose();
    }
    for(let i=0; i<2; i++){
      const key = 'P' + (i+1);
      const text = group.getObjectByName(key);
      if(text !== undefined){
        child.remove(text);
        // text.dispose();
      }
    }
    // この時点で child に P1, P2 というオブジェクトは削除されている

    // Enable = true の 文字を追加
    const points = group.points;
    const pos = [];
    const vartical = [];
    const keys = []
    if(Enable1===true){
      pos.push(points[1]);
      vartical.push('bottom');
      keys.push('P1');
    }
    if(Enable2===true){
      pos.push(points[4]);
      vartical.push('top');
      keys.push('P2');
    }
    for(let i=0; i<pos.length; i++){
      const key = keys[i];
      const value = Math.round(group[key]*100) / 100;
      if(value === 0){
        continue;
      }
      const textString: string = value.toFixed(2);
      const text = this.text.create(textString, pos[i], 0.1);
      // const text = new Text();
      // text.text = textString;
      // text.fontSize = 0.2;
      // text.position.set(pos[i].x, pos[i].y, 0);
      // text.color = 0x000000;
      // text.rotateX(Math.PI);
      text.rotateZ(Math.PI/2);
      text.name = key;
      child.add(text);
      // text.sync();
    }
    return;
  }


  private setRotate( direction: string, group: any, 
    localAxis: { x: THREE.Vector3, y: THREE.Vector3, z: THREE.Vector3 }) {

    // 全体の向きを修正する
    const XY = new Vector2(localAxis.x.x, localAxis.x.y).normalize();
    let A = Math.asin(XY.y)

    if (XY.x < 0) {
      A = Math.PI - A;
    }
    group.rotateZ(A);

    const lenXY = Math.sqrt(
      Math.pow(localAxis.x.x, 2) + Math.pow(localAxis.x.y, 2)
    );
    const XZ = new Vector2(lenXY, localAxis.x.z).normalize();
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

  }


  // 大きさを反映する
  public setScale(target: any, scale: number): void {

    const group: THREE.Group = target.getObjectByName("group");
    if(group === undefined){
      return
    }

    group.scale.set(1, scale, scale);

    // 文字は scale を
    for(let i=0; i<2; i++){
      const key = 'P' + (i+1);
      const text = group.getObjectByName(key);
      if(text !== undefined){
        text.scale.set(1/scale, 1, 1);
      }
    }

  }

  public change(
    target: any, nodei: THREE.Vector3, nodej: THREE.Vector3, localAxis: any,
    direction: string, L1: number, L2: number, P1: number, P2: number, dummy1: boolean, dummy2: boolean, cg?: number ): THREE.Group {

    // 長さを決める
    const p  = this.getPoints(
      nodei, nodej, direction, L1, L2, P1, P2, cg);
    const points: THREE.Vector3[] = p.points;

    const group: THREE.Group = target.getObjectByName("group");
    if(group === undefined){
      return target
    }

    const child = group.getObjectByName("child");

    // 面
    const mesh = child.getObjectByName("face");
    const geo: THREE.BufferGeometry = mesh["geometry"];
    for(let i= 0; i < geo.attributes.position.count; i++){
      geo.attributes.position.setXYZ(i, points[i].x, points[i].y, points[i].z);
    }
    geo.attributes.position.needsUpdate = true;

     // 線
    const line: any = child.getObjectByName("line");
    const positions = line.geometry.attributes.position.array;
    let index = 0;
    for(const pos of points) {
        positions[ index ++ ] = pos.x;
        positions[ index ++ ] = pos.y;
        positions[ index ++ ] = pos.z;
    }
    line.geometry.attributes.position.needsUpdate = true;

    target.position.set(nodei.x, nodei.y, nodei.z);

    // 全体の向きを修正する
    target.rotation.set(0, 0, 0); 
    this.setRotate(direction, target, localAxis);
    
    target["points"] = p.points;
    target["L1"] = p.L1;
    target["L"] = p.L;
    target["L2"] = p.L2;
    target["P1"] = P1;
    target["P2"] = P2;
    target["nodei"] = nodei;
    target["nodej"] = nodej;
    target["direction"] = direction;
    target["localAxis"] = localAxis;
    target["dummy1"] = dummy1;
    target["dummy2"] = dummy2;

    return target;
  }

}
