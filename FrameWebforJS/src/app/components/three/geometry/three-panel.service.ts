import { SceneService } from '../scene.service';
import { InputNodesService } from '../../input/input-nodes/input-nodes.service';
import { InputPanelService } from '../../input/input-panel/input-panel.service';
import { Injectable } from '@angular/core';

import * as THREE from 'three';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Subject } from 'rxjs';
import { LanguagesService } from 'src/app/providers/languages.service';

@Injectable({
  providedIn: 'root'
})
export class ThreePanelService {

  private panelList: any[];
  private isVisible: boolean;
  private opacity: number;
  private currentIndex: string;
  private selectcolors: any[];

  private selectionItem: THREE.Object3D;     // 選択中のアイテム

  // 大きさを調整するためのスケール
  private scale: number;
  private params: any;          // GUIの表示制御
  private gui: any;

  constructor(
    private languagesService: LanguagesService,
    private scene: SceneService,
    private node: InputNodesService,
    private panel: InputPanelService,
    private http: HttpClient) {

    this.panelList = new Array();
    this.isVisible = null;
    this.opacity = 0.7;
    this.currentIndex = null;
    
    // gui
    this.scale = 1.0;
    this.params = {
      meshScale: this.scale
    };
    this.gui = null;
  }

  public visibleChange(flag: boolean, opacity: number = 0.7): void {

    this.selectChange(-1);

    if (this.isVisible === flag && this.opacity === opacity) {
      return;
    }
    for (const mesh of this.panelList) {
      mesh.visible = flag;
      mesh.material.color.set( 0x7F8F9F );
      mesh.material.opacity = opacity;
    }
    this.isVisible = flag;
    this.opacity = opacity;

    // guiの表示設定
    if (flag === true && opacity === 0.7) {
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
    .add(this.params, 'meshScale', 0, 10)
    .step(1)
    .onFinishChange((value) => {
      this.scale = value;
      this.post_gmsh();
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

  public changeData(index: number = 0): void {

    this.ClearData();

    // 格点データを入手
    const nodeData = this.node.getNodeJson(0);
    if (Object.keys(nodeData).length <= 0) {
      return;
    }
    
    // パネルデータを入手
    const panelData = this.panel.getPanelJson(0);
    if (Object.keys(panelData).length <= 0) {
      return;
    }

    for (const key of Object.keys(panelData)) {
      const target = panelData[key];
      if (target.nodes.length <= 2) {
        continue
      }

      //対象のnodeDataを入手
      const vertexlist = [];
      for (const check of target.nodes) {
        if (check - 1 in Object.keys(nodeData)) {   //nodeData.key=>0~7, nodeData=>1~8のため（-1）で調整
          const n = nodeData[check];
          const x = n.x;
          const y = n.y;
          const z = n.z;
          vertexlist.push([x, y, z]);
        } else if (!(check - 1 in Object.keys(nodeData))) {
          continue;
        }
      }
      console.log("vertexlist",vertexlist)
      this.createPanel(vertexlist, key);
    }
  }

  //シートの選択行が指すオブジェクトをハイライトする
  public selectChange(index_row): void {

    //[0]はハイライト用のカラー、[1]はデフォルトカラー
    this.selectcolors = [0x00AFAF, 0x7F8F9F];

    //全てのハイライトを元に戻し，選択行のオブジェクトのみハイライトを適応する
    for (let item of this.panelList) {

      item['material']['color'].setHex(this.selectcolors[1]); //処理の変更あり

      if (item.name === 'panel-' + index_row.toString()) {

        item['material']['color'].setHex(this.selectcolors[0]); //処理の変更あり
      }
    }

    this.scene.render();
  }

  // 通常のgeometry(buffergeometryではない)
  private createPanel(vertexlist, row): void {

    const points = []
    const geometrys: THREE.BufferGeometry[] = [];
    for(const p of vertexlist){
      points.push(new THREE.Vector3(p[0], p[1], p[2]))
    }
    // 三角形を作る
    geometrys.push(new THREE.BufferGeometry().setFromPoints([points[0],points[1],points[2]]));
    // 四角形が作れる場合
    if(points.length > 3)
      geometrys.push(new THREE.BufferGeometry().setFromPoints([points[3],points[0],points[2]]));

    const material = new THREE.MeshBasicMaterial({
      transparent: true,
      side: THREE.DoubleSide,
      color: 0x7f8F9F,
      opacity: 0.7,
    });

    for(const g of geometrys) {
      const mesh = new THREE.Mesh(g, material);
      mesh.name = 'panel-' + row.toString();
      this.panelList.push(mesh);
      this.scene.add(mesh);
    }
  }

  // データをクリアする
  public ClearData(): void {

    for (const mesh of this.panelList) {
      // 文字を削除する
      while (mesh.children.length > 0) {
        const object = mesh.children[0];
        object.parent.remove(object);
      }
      // オブジェクトを削除する
      this.scene.remove(mesh);
    }
    this.panelList = new Array();
  }
  private prevSelect:any = {};
  // マウス位置とぶつかったオブジェクトを検出する
  public detectObject(raycaster: THREE.Raycaster, action: string): void {

    if (this.panelList.length === 0) {
      return; // 対象がなければ何もしない
    }
    //[0]はハイライト用のカラー、[1]はデフォルトカラー
    this.selectcolors = [0x00AFAF, 0x7F8F9F];

    // 交差しているオブジェクトを取得
    const intersects = raycaster.intersectObjects(this.panelList);
    if ( intersects.length <= 0 ){
      return;
    }
    switch (action) {
      case "click":
        this.panelList.map((item) => {
          if (intersects.length > 0 && item === intersects[0].object) {
            // 色を赤くする
            // const material = intersects[0].object['material'];
            
            const material = item['material'];
            // material['vertexColors'] = true;
            material['color'].setHex(0xff0000);
            material["opacity"] = 0.7;
            this.sendPanelSubject(item);
          }
          else{
            const material = item['material'];
            material['color'].setHex(this.selectcolors[1]);
            material["opacity"] = 0.7;
          }
        });
        break;

      // case "select":
      //   if (intersects.length > 0) {
      //     this.selectionItem = null;
      //     this.panelList.map((item) => {
      //       const material = item['material'];
      //       material['vertexColors'] = true;
      //       if (item === intersects[0].object) {
      //         // 色を赤くする
      //         material['color'].setHex(this.selectcolors[0]);
      //         material["opacity"] = 0.7;
      //         this.selectionItem = item;
      //       } else {
      //         // それ以外は元の色にする
      //         material['color'].setHex(this.selectcolors[1]);
      //         material["opacity"] = 0.7;
      //       }
      //     });
      //   }
      //   break;

      // case "hover":
      //   this.panelList.map((item) => {
      //     const material = item['material'];
      //     material['vertexColors'] = true;
      //     if (intersects.length > 0 && item === intersects[0].object) {
      //       // 色を赤くする
      //       material['color'].setHex(this.selectcolors[0]);
      //       material["opacity"] = 0.7;
      //     } else {
      //       if (item === this.selectionItem) {
      //         material['vertexColors'] = true;
      //         material['color'].setHex(this.selectcolors[0]);
      //         material["opacity"] = 0.7;
      //       } else {
      //         // それ以外は元の色にする
      //         material['vertexColors'] = true;
      //         material['color'].setHex(this.selectcolors[1]);
      //         material["opacity"] = 0.7;
      //       }
      //     }
      //   });
      //   break;

      default:
        return;
    }
    this.scene.render();
  }

  //
  private post_gmsh(): void {

    const geo: string = this.panel.getGeoString();

    const inputJson: string = JSON.stringify({
      dim: 3, geo
    })
    const options = {
      headers: new HttpHeaders({
        'Accept': 'application/json'
      })
    };
    const URL = 'https://uij0y12e2l.execute-api.ap-northeast-1.amazonaws.com/default/gmsh';

    this.http.post(URL, inputJson, options).subscribe(
      (response) => {
        console.log('通信成功', response)
        this.scene.render();
      },
      (error) => {
        console.log('エラー',error)
      }
    );

  }

  private panelSelectedInThreeSubject = new Subject<any>();
  panelSelected$ = this.panelSelectedInThreeSubject.asObservable();

  sendPanelSubject(item: any) {
    this.panelSelectedInThreeSubject.next(item);
  }
}
