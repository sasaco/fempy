import { Injectable } from '@angular/core';

import * as THREE from 'three';
import { SceneService } from '../scene.service';

import { DataHelperModule } from '../../../providers/data-helper.module';

import { InputNodesService } from '../../../components/input/input-nodes/input-nodes.service';
import { InputMembersService } from '../../../components/input/input-members/input-members.service';
import { InputPanelService } from '../../input/input-panel/input-panel.service';

import { ResultCombineDisgService } from '../../result/result-combine-disg/result-combine-disg.service';
import { ResultPickupDisgService } from '../../result/result-pickup-disg/result-pickup-disg.service';

import { ThreeNodesService } from './three-nodes.service';
import { ThreeMembersService } from './three-members.service';
import { ThreePanelService } from './three-panel.service';
import { InputLoadService } from '../../input/input-load/input-load.service';
import { Object3D } from 'three';
import { LanguagesService } from 'src/app/providers/languages.service';

@Injectable({
  providedIn: 'root'
})
export class ThreeDisplacementService {
  private lineList: THREE.Object3D;
  private targetData: any;

  private scale: number;
  private params: any;          // GUIの表示制御
  private gui: any;
  private gui_max_scale: number;

  private nodeData: any
  private membData: any
  private panelData: any
  private allDisgData: any;
  private max_values: any;
  public value_range = { disg: null, comb_disg: null, pik_disg: null };

  // アニメーションのオブジェクト
  private animationObject: any;


  constructor(private scene: SceneService,
    private languagesService: LanguagesService,
    private node: InputNodesService,
    private member: InputMembersService,
    private panel: InputPanelService,
    private load: InputLoadService,
    private three_node: ThreeNodesService,
    private three_member: ThreeMembersService) {

    this.lineList = new THREE.Object3D();
    this.lineList.visible = false;
    this.scene.add(this.lineList);

    this.targetData = new Array();

    this.ClearData();

    // gui
    this.params = {
      dispScale: this.scale,
    };
    this.gui = null;
    this.gui_max_scale = 2;

    // アニメーションのオブジェクト
    this.animationObject = null;
  }

  public visibleChange(flag: boolean): void {
    if (this.lineList.visible === flag) {
      return;
    }
    this.lineList.visible = flag;

    if (flag === true) {
      this.guiEnable();

    } else {
      // アニメーションのオブジェクトを解放
      if (this.animationObject !== null) {
        cancelAnimationFrame(this.animationObject);
        this.animationObject = null;
      }
      this.guiDisable();
    }
  }

  // データをクリアする
  public ClearData(): void {

    // 線を削除する
    while (this.lineList.children.length > 0) {
      const object = this.lineList.children[0];
      object.parent.remove(object);
    }

    this.scale = 0.5;

    this.nodeData = {};
    this.membData = {};
    this.panelData = {};
    this.allDisgData = {};
    this.max_values = {};

    // アニメーションのオブジェクト
    if (this.animationObject !== null) {
      cancelAnimationFrame(this.animationObject);
      this.animationObject = null;
    }
  }

  private guiEnable(): void {
    if (this.gui !== null) {
      return;
    }

    const gui_step: number = this.gui_max_scale * 0.01;
    this.gui = this.scene.gui.add(this.params, 'dispScale', 0, this.gui_max_scale).step(gui_step).onChange((value) => {
      this.scale = value;
      this.onResize();
      this.scene.render();
    });
    this.languagesService.tranText();
  }

  private guiDisable(): void {
    if (this.gui === null) {
      return;
    }
    this.scene.gui.remove(this.gui);
    this.gui = null;
    this.scale = 0.5;
    this.params.dispScale = 0.5
  }

  // 解析結果をセットする
  public setResultData(getDisgJson: any, max_values: any, value_range: any, mode: string): void {

    this.nodeData = this.node.getNodeJson(0);
    this.membData = this.member.getMemberJson(0);

    /////// パネルの1辺を仮想の部材として登録
    const panelData = this.panel.getPanelJson(0);
    for (const pk of Object.keys(panelData)) {
      const p = panelData[pk];
      for (let i = 1; i < p.nodes.length; i++) {
        const temp = {
          'ni': p.nodes[i - 1],
          'nj': p.nodes[i],
          'e': p.e,
          'cg': 0
        };
        if (!this.member.sameNodeMember(temp, this.membData)) {
          this.membData['p' + pk + '-' + i.toString()] = temp;
        }
      }
      const temp = {  // 最初と最後の負始点
        'ni': p.nodes[0],
        'nj': p.nodes[p.nodes.length - 1],
        'e': p.e,
        'cg': 0
      };
      if (!this.member.sameNodeMember(temp, this.membData)) {
        this.membData['p' + pk + '-0'] = temp;
      }
    }
    this.panelData = this.panel.getPanelJson(0);
    this.allDisgData = getDisgJson;
    this.max_values = max_values;
    this.value_range[mode] = value_range;
    // this.changeData(1);
  }
  // combineとpickupの解析結果をセットする
  public setCombPickResultData(value_range: any, mode: string): void {
    this.value_range[mode] = value_range;
  }

  public changeData(index: number): void {
    // 格点データを入手
    if (Object.keys(this.nodeData).length <= 0) {
      this.visibleChange(false);
      return;
    }

    // メンバーデータを入手
    const membKeys = Object.keys(this.membData);
    // パネルデータを入手
    const panelKeys = Object.keys(this.panelData);
    if (membKeys.length <= 0 && panelKeys.length <= 0) {
      this.visibleChange(false);
      return;
    }

    // 変位データを入手
    const targetKey: string = index.toString();
    if (!(targetKey in this.allDisgData)) {
      this.visibleChange(false);
      return;
    }

    // スケールの決定に用いる変数を写す
    let minDistance: number;
    let maxDistance: number;
    [minDistance, maxDistance] = this.getDistance();

    // 非表示だったら 表示する
    if (this.lineList.visible === false) {
      this.visibleChange(true);
    }

    // 連行荷重の場合 アニメーションを走らせる
    const symbol: string = this.load.getLoadName(index, "symbol");
    if (symbol === "LL") {
      this.change_LL_Load(targetKey, membKeys, minDistance, maxDistance);
      return;
    }

    // アニメーションのオブジェクト
    if (this.animationObject !== null) {
      cancelAnimationFrame(this.animationObject);
      this.animationObject = null;
    }
    // 描く
    this.changeDisg(targetKey, membKeys, minDistance, maxDistance);

  }

  private changeDisg(targetKey: string, membKeys: string[],
    minDistance: number, maxDistance: number): void {

    const disgData = this.allDisgData[targetKey];

    this.targetData = new Array();

    // 新しい入力を適用する
    for (const key of membKeys) {

      // 節点データを集計する
      const m = this.membData[key];
      const i = this.nodeData[m.ni];
      const j = this.nodeData[m.nj];
      if (i === undefined || j === undefined) {
        continue;
      }

      const disgKeys = Object.keys(disgData);
      if (disgKeys.length <= 0) {
        return;
      }

      const di: any = disgData.find((tmp) => {
        return tmp.id === m.ni.toString();
      });

      const dj: any = disgData.find((tmp) => {
        return tmp.id === m.nj.toString();
      });

      if (di === undefined || dj === undefined) {
        continue;
      }
      let Division = 20;
      if (di.rx === dj.rx && di.ry === dj.ry && di.rz === dj.rz) {
        Division = 1;
      }

      this.targetData.push({
        name: key,
        xi: i.x,
        yi: i.y,
        zi: i.z,
        xj: j.x,
        yj: j.y,
        zj: j.z,
        theta: m.cg,
        dxi: di.dx,
        dyi: di.dy,
        dzi: di.dz,
        dxj: dj.dx,
        dyj: dj.dy,
        dzj: dj.dz,
        rxi: di.rx,
        ryi: di.ry,
        rzi: di.rz,
        rxj: dj.rx,
        ryj: dj.ry,
        rzj: dj.rz,
        Division,
      });
    }

    const i = targetKey.indexOf('.');
    let targetKey2 = targetKey;
    if (i > 0) {
      targetKey2 = targetKey.slice(0, i);
    }
    const maxValue: number = this.max_values[targetKey2];
    if (maxValue > 0) {
      let s = this.three_node.maxDistance * 0.1 / maxValue;
      if (s === Infinity) {
        s = 1;
      }
      this.targetData['scale'] = s;
    }
    else {
      this.targetData['scale'] = 1;
    }
    // this.gui_max_scale = maxDistance / minDistance;

    this.onResize();

  }

  // 連行荷重を変更する
  public change_LL_Load(id: string, membKeys: string[],
    minDistance: number, maxDistance: number): void {

    // 対象の連行荷重を全部削除する
    let LL_keys = Object.keys(this.allDisgData).filter(e => {
      return e.indexOf(id + ".") === 0;
    })
    if (LL_keys === undefined) {
      return;
    }

    LL_keys = [id].concat(LL_keys)

    // 一旦アニメーションを削除
    if (this.animationObject !== null) {
      cancelAnimationFrame(this.animationObject);
      this.animationObject = null;
    }

    // 連行荷重の場合
    this.animation(LL_keys, membKeys, minDistance, maxDistance); //ループのきっかけ

  }


  // 連行移動荷重のアニメーションを開始する
  public animation(keys: string[], membKeys: string[],
    minDistance: number, maxDistance: number,
    i: number = 0, old_j: number = 0) {

    let j: number = Math.floor(i / 10); // 10フレームに１回位置を更新する

    if (j < keys.length) {
      i = i + 1; // 次のフレーム
    } else {
      i = 0;
      j = 0;
    }

    // 次のフレームを要求
    this.animationObject = requestAnimationFrame(() => {
      this.animation(keys, membKeys, minDistance, maxDistance, i, j);
    });

    if (j === old_j) {
      return;
    }

    this.changeDisg(keys[j], membKeys, minDistance, maxDistance)

    this.scene.render();
  }

  private onResize(): void {

    let scale: number = this.targetData['scale'] * this.scale * 0.2; // 例 : this.scaleが0.5の時, 最大変位量をモデルサイズの5%にして0.2をかける

    for (let i = 0; i < this.targetData.length; i++) {
      const target = this.targetData[i];

      const xi: number = target.xi + target.dxi * scale;
      const yi: number = target.yi + target.dyi * scale;
      const zi: number = target.zi + target.dzi * scale;

      const xj: number = target.xj + target.dxj * scale;
      const yj: number = target.yj + target.dyj * scale;
      const zj: number = target.zj + target.dzj * scale;

      // 要素座標系への変換
      const t = this.three_member.tMatrix(xi, yi, zi, xj, yj, zj, target.theta);
      const dge = [target.dxi, target.dyi, target.dzi, target.rxi, target.ryi, target.rzi,
      target.dxj, target.dyj, target.dzj, target.rxj, target.ryj, target.rzj];//.map(v => v * scale);;
      const de: number[] = new Array(dge.length);
      for (let ip = 0; ip < 4; ip++) {
        const ib = 3 * ip;
        for (let i = 0; i < 3; i++) {
          let s = 0;
          for (let j = 0; j < 3; j++) {
            s = s + t[i][j] * dge[ib + j];
          }
          de[ib + i] = s
        }
      }


      const Division: number = target.Division;

      const L = Math.sqrt(Math.pow(xi - xj, 2) + Math.pow(yi - yj, 2) + Math.pow(zi - zj, 2));

      const positions = [];
      const threeColor = new THREE.Color(0xFF0000);
      const colors = [];

      // 補間点の節点変位の計算
      for (let j = 0; j <= Division; j++) {
        const n = j / Division;
        const xhe = (1 - n) * de[0] + n * de[6];
        const yhe = (1 - 3 * Math.pow(n, 2) + 2 * Math.pow(n, 3)) * de[1] + L * (n - 2 * Math.pow(n, 2) + Math.pow(n, 3)) * de[5]
          + (3 * Math.pow(n, 2) - 2 * Math.pow(n, 3)) * de[7] + L * (0 - Math.pow(n, 2) + Math.pow(n, 3)) * de[11];
        const zhe = (1 - 3 * Math.pow(n, 2) + 2 * Math.pow(n, 3)) * de[2] - L * (n - 2 * Math.pow(n, 2) + Math.pow(n, 3)) * de[4]
          + (3 * Math.pow(n, 2) - 2 * Math.pow(n, 3)) * de[8] - L * (Math.pow(n, 3) - Math.pow(n, 2)) * de[10];


        // 全体座標系への変換
        const xhg = t[0][0] * xhe + t[1][0] * yhe + t[2][0] * zhe;
        const yhg = t[0][1] * xhe + t[1][1] * yhe + t[2][1] * zhe;
        const zhg = t[0][2] * xhe + t[1][2] * yhe + t[2][2] * zhe;

        // 補間点の変位を座標値に付加
        const xk = (1 - n) * xi + n * xj + xhg * scale;
        const yk = (1 - n) * yi + n * yj + yhg * scale;
        const zk = (1 - n) * zi + n * zj + zhg * scale;

        positions.push(new THREE.Vector3(xk, yk, zk));
        colors.push(threeColor.r, threeColor.g, threeColor.b);
      }

      if (this.lineList.children.length > i) {
        const line: Object3D = this.lineList.children[i];
        // line を修正するコード
        const geometry: THREE.BufferGeometry = line['geometry'];
        geometry.setFromPoints(positions);

      } else {
        const geometry = new THREE.BufferGeometry().setFromPoints(positions);
        const matLine = new THREE.LineBasicMaterial({
          color: 0xFF0000,
          linewidth: 0.001,
        });
        const line = new THREE.Line(geometry, matLine);
        line.computeLineDistances();

        line.scale.set(1, 1, 1);
        line.name = target.name;
        this.lineList.add(line);
      }
    }
  }

  private getDistance(): number[] {
    let minDistance: number = Number.MAX_VALUE;
    let maxDistance: number = 0;

    const member: object = this.membData;
    for (const memberNo of Object.keys(member)) {
      let l: number;
      if (!memberNo.includes('p')) {
        l = this.member.getMemberLength(memberNo);
      } else {
        l = this.panel.getPanelLength(member[memberNo]);
      }
      minDistance = Math.min(l, minDistance);
      maxDistance = Math.max(l, maxDistance);
    }

    return [minDistance, maxDistance];
  }

}
