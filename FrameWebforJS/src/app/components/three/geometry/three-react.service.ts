import { Injectable } from '@angular/core';

import * as THREE from 'three';

import { SceneService } from '../scene.service';

import { DataHelperModule } from '../../../providers/data-helper.module';

import { InputNodesService } from '../../../components/input/input-nodes/input-nodes.service';

import { ResultCombineReacService } from '../../result/result-combine-reac/result-combine-reac.service';
import { ResultPickupReacService } from '../../result/result-pickup-reac/result-pickup-reac.service';
import { ThreeNodesService } from './three-nodes.service';
import { LanguagesService } from 'src/app/providers/languages.service';

@Injectable({
  providedIn: 'root'
})
export class ThreeReactService {

  private isVisible: boolean;
  private pointLoadList: any[];
  private selectionItem: THREE.Mesh;     // 選択中のアイテム
  private reacData: any;
  private nodeData: any;
  private max_values: any;
  public value_range = { reac: null, comb_reac: null, pik_reac: null };
  // GUI
  private scale: number;
  private params: any; // GUIの表示制御
  private gui: any;
  private gui_max_scale: number;

  constructor(private scene: SceneService,
    private languagesService: LanguagesService,
    private helper: DataHelperModule,
    private nodeThree: ThreeNodesService,
    private node: InputNodesService,) {

    this.isVisible = null;
    this.pointLoadList = new Array();
    this.selectionItem = null;
    this.reacData = {};

    // gui
    this.scale = 1.0;
    this.params = { 
      reactScale: this.scale 
    };
    this.gui = null;
    this.gui_max_scale = 10;
    
    this.ClearData();
  }

  public visibleChange(flag: boolean): void {
    if (this.isVisible === flag) {
      return;
    }
    for (const mesh of this.pointLoadList) {
      mesh.visible = flag;
    }

    this.isVisible = flag;
    
    if (flag === true) {
      this.guiEnable();
    } else {
      this.guiDisable();
    }
  }
  
  // guiを表示する
  private guiEnable(): void {
    if (this.gui !== null) {
      return;
    }

    this.gui = this.scene.gui
      .add(this.params, "reactScale", 0, this.gui_max_scale)
      .step(0.1)
      .onChange((value) => {
        this.scale = value;
        this.onResize();
        this.scene.render();
      });
    this.languagesService.tranText();
  }

  // guiを非表示にする
  private guiDisable(): void {
    if (this.gui === null) {
      return;
    }
    this.scene.gui.remove(this.gui);
    this.gui = null;
  }


  // データをクリアする
  public ClearData(): void {
    
    for (const mesh of this.pointLoadList) {
      // 文字を削除する
      while (mesh.children.length > 0) {
        const object = mesh.children[0];
        object.parent.remove(object);
      }
      // オブジェクトを削除する
      this.scene.remove(mesh);
    }
    this.pointLoadList = new Array();

    this.scale = 1.0;

    // this.max_values = {};
    // this.value_range = {};

  }

  public maxLength(): number {
    // 最も距離の近い2つの節点距離
    return this.nodeThree.baseScale * 80;
  }

  // 解析結果をセットする
  public setResultData(getReacJson: any, max_values: any, value_range: any, mode: string): void {
    this.nodeData = this.node.getNodeJson(0);
    this.reacData = getReacJson;
    this.max_values = max_values;
    this.value_range[mode] = value_range[mode];
  }
  // combineとpickupの解析結果をセットする
  public setCombPickResultData(value_range: any, mode: string): void {
    this.value_range[mode] = value_range;
  }

  public changeData(index: number): void {

    // 一旦全排除
    this.ClearData();

    // 反力データを入手
    //const reacData = this.reac.getReacJson();
    if (Object.keys(this.reacData).length <= 0) {
      return;
    }
    const targetCase: string = index.toString();
    if (!(targetCase in this.reacData)) {
      return;
    }

    // 格点データを入手
    const nodeKeys = Object.keys(this.nodeData);
    if (nodeKeys.length <= 0) {
      return;
    }

    // サイズを調整しオブジェクトを登録する
    this.createReact(this.reacData[targetCase], this.nodeData);

    this.onResize();
  }

  // 節点荷重の矢印を描く
  private createReact(reacData: any, nodeData: object): void {

    // 新しい入力を適用する
    const targetReact = JSON.parse(
      JSON.stringify({
        temp: reacData
      })
    ).temp;
    // 文字列を数値型に変換する  
    for (const reac of targetReact) {
      reac.tx = this.helper.toNumber(reac.tx);
      reac.ty = this.helper.toNumber(reac.ty);
      reac.tz = this.helper.toNumber(reac.tz);
      reac.mx = this.helper.toNumber(reac.mx);
      reac.my = this.helper.toNumber(reac.my);
      reac.mz = this.helper.toNumber(reac.mz);
    }
    // スケールを決定する 最大の荷重を 1とする
    let pMax = 0;
    let mMax = 0;
    for (const reac of targetReact) {
      pMax = Math.max(pMax, Math.abs(reac.tx));
      pMax = Math.max(pMax, Math.abs(reac.ty));
      pMax = Math.max(pMax, Math.abs(reac.tz));
      mMax = Math.max(mMax, Math.abs(reac.mx));
      mMax = Math.max(mMax, Math.abs(reac.my));
      mMax = Math.max(mMax, Math.abs(reac.mz));
    }

    // 集中荷重の矢印をシーンに追加する
    for (const reac of targetReact) {
      // 節点座標 を 取得する
      const node = nodeData[reac.id];
      if (node === undefined) {
        continue;
      }
      // x方向の集中荷重
      const xArrow: THREE.Group = this.setPointReact(reac.tx, pMax, node, 'px');
      if (xArrow !== null) {
        this.pointLoadList.push(xArrow);
        this.scene.add(xArrow);
      }
      // y方向の集中荷重
      const yArrow: THREE.Group = this.setPointReact(reac.ty, pMax, node, 'py');
      if (yArrow !== null) {
        this.pointLoadList.push(yArrow);
        this.scene.add(yArrow);
      }
      // z方向の集中荷重
      const zArrow: THREE.Group = this.setPointReact(reac.tz, pMax, node, 'pz');
      if (zArrow !== null) {
        this.pointLoadList.push(zArrow);
        this.scene.add(zArrow);
      }

      // x軸周りのモーメント
      const xMoment = this.setMomentReact(reac.mx, mMax, node, 0xFF0000, 'mx');
      if (xMoment !== null) {
        this.pointLoadList.push(xMoment);
        this.scene.add(xMoment);
      }

      // y軸周りのモーメント
      const yMoment = this.setMomentReact(reac.my, mMax, node, 0x00FF00, 'my');
      if (yMoment !== null) {
        this.pointLoadList.push(yMoment);
        this.scene.add(yMoment);
      }

      // z軸周りのモーメント
      const zMoment = this.setMomentReact(reac.mz, mMax, node, 0x0000FF, 'mz');
      if (zMoment !== null) {
        this.pointLoadList.push(zMoment);
        this.scene.add(zMoment);
      }

    }
  }

  // 反力モーメントの矢印オブジェクトの作成
  private setMomentReact(value: number, mMax: number, node: any, color: number, name: string): THREE.Group {

    if (value === 0) {
      return null;
    }

    const group = new THREE.Group();

    let scale: number = Math.sign(value) * this.helper.getCircleScale(value, mMax);
    scale /= 5;

    const curve = new THREE.EllipseCurve(
      0, 0,              // ax, aY
      4, 4,               // xRadius, yRadius
      0, 1.5 * Math.PI,  // aStartAngle, aEndAngle
      false,              // aClockwise
      0                   // aRotation
    );

    const points = curve.getPoints(20);
    const lineGeometry = new THREE.BufferGeometry().setFromPoints(points);
    const lineMaterial = new THREE.LineBasicMaterial({ color, linewidth: 5 });
    const ellipse = new THREE.Line(lineGeometry, lineMaterial);
    group.add(ellipse);

    const arrowGeometry = new THREE.ConeGeometry(0.3, 3, 3, 1, true);
    const arrowMaterial = new THREE.MeshBasicMaterial({ color });
    const cone = new THREE.Mesh(arrowGeometry, arrowMaterial);
    cone.rotateX(Math.PI);
    cone.position.set(4, 0.5, 0);
    group.add(cone);

    group.position.set(node.x, node.y, node.z);
    group.scale.set(scale, scale, scale)
    // この時点で全体Z軸に対して左ねじ回りの矢印が作成される

    // 全体座標系に対して右ねじ方向が正となるよう矢印を回転させる
    switch (name) {
      case 'mx':
        // ローカルy軸に対して右ねじ方向に90°回転（これで全体X軸に対して左ねじ回りの矢印となる）  
        group.rotateY(Math.PI / 2);
        if(value > 0){
          // 反力モーメントの値が正なら反転
          group.rotateX(Math.PI);
        }
        break;
      case 'my':
        // ローカルx軸に対して右ねじ方向に90°回転（これで全体Y軸に対して右ねじ回りの矢印となる）
        group.rotateX(Math.PI / 2);
        if(value < 0){
          // 反力モーメントの値が負なら反転
          group.rotateY(Math.PI);
        }
        break;
      case 'mz':
        if(value > 0){
          // 反力モーメントの値が正なら反転
          group.rotateX(Math.PI);
        }
        break;
    }

    group['base_scale'] = scale

    return group;

  }

  // 支点反力の矢印オブジェクトを作成する
  private setPointReact(value: number, pMax: number,
    node: any, name: string): THREE.Group {

    if (value === 0) {
      return null;
    }

    const group = new THREE.Group();

    const maxLength: number = this.maxLength() * 0.2;
    const ratio = this.helper.getCircleScale(value, pMax);
    const length = Math.sign(value) * maxLength * ratio;

    const linewidth: number = Math.abs(length) / 5000;

    let color: number;
    const positions: THREE.Vector3[] = [];


    positions.push(new THREE.Vector3(0, 0, 0));
    switch (name) {
      case 'px':
        positions.push(new THREE.Vector3( -length, 0, 0 ));
        color = 0xFF0000;
        break;
      case 'py':
        positions.push(new THREE.Vector3( 0, -length, 0 ));
        color = 0x00FF00;
        break;
      case 'pz':
        positions.push(new THREE.Vector3( 0, 0, -length ));
        color = 0x0000FF;
        break;
    }
    const cone_scale: number = length * 0.3;
    const cone_radius: number = 0.1 * cone_scale;
    const cone_height: number = 1 * cone_scale;
    const arrowGeometry: THREE.ConeGeometry = new THREE.ConeGeometry(cone_radius, cone_height, 3, 1, true);
    const arrowMaterial = new THREE.MeshBasicMaterial({ color });
    const cone: THREE.Mesh = new THREE.Mesh(arrowGeometry, arrowMaterial);
    switch (name) {
      case 'px':
        cone.position.set( -cone_height / 2, 0, 0 );
        cone.rotation.z = Math.PI / 2 * 3;
        break;
      case 'py':
        cone.position.set( 0, -cone_height / 2, 0 );
        break;
      case 'pz':
        cone.position.set( 0, 0, -cone_height / 2 );
        cone.rotation.x = Math.PI / 2;
        break;
    }
    cone.name = "cone";
    group.add(cone);

    const threeColor = new THREE.Color(color);
    const colors = [];
    colors.push(threeColor.r, threeColor.g, threeColor.b);
    colors.push(threeColor.r, threeColor.g, threeColor.b);

    const geometry = new THREE.BufferGeometry().setFromPoints( positions );
    const matLine = new THREE.LineBasicMaterial({
      color,
      linewidth,
    });
    const line = new THREE.Line(geometry, matLine);
    line.name = "line";
    line.computeLineDistances();
    group.add(line)

    group.position.set(node.x, node.y, node.z);
    group.name = name;
    group["base_scale"] = 1;

    return group;

  }

  private onResize(): void{
    const pointLoadList = this.pointLoadList;
    const scale = this.params.reactScale;
    for (const key of Object.keys(pointLoadList)) {
      const target = pointLoadList[key];
      const scale2 = scale * target.base_scale;
      target.scale.set(scale2, scale2, scale2);
    }
  }



}
