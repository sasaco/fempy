import { Injectable } from "@angular/core";
import { SceneService } from "../scene.service";
import { InputNodesService } from "../../../components/input/input-nodes/input-nodes.service";
import { InputNoticePointsService } from "../../../components/input/input-notice-points/input-notice-points.service";
import { InputMembersService } from "../../../components/input/input-members/input-members.service";
import { ThreeNodesService } from "./three-nodes.service";
import * as THREE from "three";
import { CSS2DObject } from "../libs/CSS2DRenderer.js";
import { Vector3 } from "three";
import { ThreeMembersService } from "./three-members.service";
import { Subject } from "rxjs";
import { LanguagesService } from "src/app/providers/languages.service";

@Injectable({
  providedIn: "root",
})
export class ThreeNoticePointsService {

  private geometry: THREE.SphereBufferGeometry;

  public maxDistance: number;
  public minDistance: number;

  private noticePointList: THREE.Object3D;
  private nPointList: any[];
  private axisList: THREE.Group[]; // 軸は、メンバーのスケールと関係ないので、分けて管理する
  private selectionItem: THREE.Object3D; // 選択中のアイテム
  private currentIndex: string;

  // 大きさを調整するためのスケール
  private scale: number;
  private params: any; // GUIの表示制御
  private gui: any;

  private objVisible: boolean;
  private txtVisible: boolean;

  constructor( private scene: SceneService,
    private languagesService: LanguagesService,
              private nodeThree: ThreeNodesService,
              private node: InputNodesService,
              private noticePoint: InputNoticePointsService,
              private member: InputMembersService,
              private memberThree: ThreeMembersService) {

    this.geometry = new THREE.SphereBufferGeometry(1);
    this.noticePointList = new THREE.Object3D();
    this.nPointList = new Array();
    // this.axisList = new Array();
    this.ClearData();
    this.scene.add(this.noticePointList);
    // this.currentIndex = null;

    this.objVisible = true;
    this.txtVisible = false;

    // gui
    this.scale = 300;
    this.params = {
      nodeNo: this.txtVisible,
      nodeScale: this.scale
    };
    this.gui = null;
  }

  // 初期化
  public OnInit(): void {
    // 部材番号の表示を制御する gui を登録する
    this.scene.gui.add(this.params, "pointNo").onChange((value) => {
      for (const mesh of this.noticePointList.children) {
        mesh.getObjectByName("font").visible = value;
      }
      this.txtVisible = value;
      this.scene.render();
    });
    this.languagesService.tranText();
  }

  // 要素の太さを決定する基準値
  public baseScale(): number {
    // const scale = this.nodeThree.baseScale;
    return 0.018895766721676047;// scale * 0.3;
  }

  // データが変更された時の処理
  public changeData(): void {
    this.ClearData();
    // 格点データを入手
    const nodeData = this.node.getNodeJson(0);
    if (Object.keys(nodeData).length <= 0) {
      return;
    }
    // メンバーデータを入手
    const memberData = this.member.getMemberJson(0);
    if (Object.keys(memberData).length <= 0) {
      return;
    }

    // 着目点情報を入手
    const jsonData = this.noticePoint.getNoticePointsJson();
    if (Object.keys(jsonData).length <= 0) {     
      return;
    }

    // let jsonData = this.data.getNoticePointsJson()
    for (let target of jsonData) {
      let point:[] = target['Points'];
      let m: string = target['m'];
      let row: string = target['row'];
      const l: number = this.member.getMemberLength(m);
      for(let i = 0 ; i < point.length ; i++) {
        const item = this.noticePointList.children.find((target) => {
          return (target.name === 'points' + row + 'L' + (i + 1));
        });
        let length: number = point[i];
        let axis = this.member.getAxis(m, l, length);
        // console.log(axis)
        if (item !== undefined) {
          // すでに同じ名前の要素が存在している場合座標の更新
          item.position.x = axis.x;
          item.position.y = axis.y;
          item.position.z = axis.z;
        } else {
          const mesh = new THREE.Mesh(this.geometry,
            new THREE.MeshBasicMaterial({ color: 0X00A5FF }));
          mesh.name = 'points' + row + 'L' + (i + 1);
          mesh.position.x = axis.x;
          mesh.position.y = axis.y;
          mesh.position.z = axis.z;
          mesh.material.color.setHex(0X00A5FF);
  
          let sc = this.scale / 100; // this.scale は 100 が基準値なので、100 のとき 1 となるように変換する
          sc = Math.max(sc, 0.001); // ゼロは許容しない
      
          let scale: number = this.baseScale() * sc;
  
          mesh.scale.set(scale, scale, scale)
          this.scene.add(mesh);
          this.noticePointList.children.push(mesh);
        }
      }
    }

    this.scene.render();

    // 新しい入力を適用する
    // for (const key of jsonKeys) {

    // }
  }

  //シートの選択行が指すオブジェクトをハイライトする
  public selectChange(index,index_sub): void{

    // if (this.currentIndex === index){
    //   //選択行の変更がないとき，何もしない
    //   return
    // }
    const jsonData = this.noticePoint.getNoticePointsJson();
    if (Object.keys(jsonData).length <= 0) {
      this.memberThree.selectChange_clear_points();
      this.scene.render();
      return;
    }  
    const target = jsonData.find( (e) => e.row === index );
    const m_no = (target !== undefined) ? target.m: '0';
    this.memberThree.selectChange_points(m_no);

    for (let item of this.noticePointList.children){
      item['material']['color'].setHex(0X00A5FF);
      if (item.name === 'points' + index.toString() + index_sub.toString()){
        this.memberThree.selectChange_clear_points();
        item['material']['color'].setHex(0XFF0000);
      }
    }

    // const axisKey_list = [];

    //全てのハイライトを元に戻し，選択行のオブジェクトのみハイライトを適応する
    // for (let item of this.memberList.children){
    //   item['material']['color'].setHex(0X000000);
    //   if( mode === "elements"){
    //     if (item['element'] === 'element' + index.toString()){
    //       item['material']['color'].setHex(0XFF0000);
    //       // ハイライトした部材の名称を保存しておく
    //       axisKey_list.push(item.name + 'axis'); 
    //     }
    //   } else {
    //     if (item.name === 'member' + index.toString()){
    //       item['material']['color'].setHex(0XFF0000);
    //     }
    //   }
    // }

    // this.scene.render();

    this.scene.render();
  }

  // データをクリアする
  public ClearData(): void {

      // データをクリアする

      console.log("noticePointList clear")
      for (const mesh of this.noticePointList.children) {
        // 文字を削除する
        while (mesh.children.length > 0) {
          const object = mesh.children[0];
          object.parent.remove(object);
        }
        // オブジェクトを削除する
        this.scene.remove(mesh);
      }
      this.noticePointList.children = new Array();
    // 線を削除する
    // for (const mesh of this.memberList.children) {
    //   // 文字を削除する
    //   while (mesh.children.length > 0) {
    //     const object = mesh.children[0];
    //     object.parent.remove(object);
    //   }
    //   this.scene.remove(mesh);
    // }
    // this.memberList.children = new Array();

    // ローカル座標を示す線を削除する
    // for (const group of this.axisList) {
    //   this.scene.remove(group);
    // }
    // this.axisList = new Array();

    // this.minDistance = Number.MAX_VALUE;
    // this.maxDistance = 0;
  }

  // スケールを反映する
  private onResize(): void {

    let sc = this.scale / 100; // this.scale は 100 が基準値なので、100 のとき 1 となるように変換する
    sc = Math.max(sc, 0.001); // ゼロは許容しない

    let scale: number = this.baseScale() * sc;
  
    for (const item of this.noticePointList.children) {
      item.scale.set(scale, 1, scale);
    }
    // scale *= 50 ;
    // for (const arrows of this.noticePointList) {
    //   for (const item of arrows.children) {
    //     item.scale.set(scale, scale, scale);
    //   }
    // }
  }

  // 表示設定を変更する
  public visibleChange(flag: boolean): void {

    // this.selectChange(-1, -1)

    if( this.objVisible === flag){
      return;
    }
    for (const mesh of this.noticePointList.children) {
      mesh.visible = flag;
    }
    this.objVisible = flag;
    // this.selectChange(-1);

    // 表示設定
    // if (this.objVisible !== flag) {
    //   this.memberList.visible = flag;
    //   this.objVisible = flag;
    // }

    // 部材軸の表示設定
    // if (text === false) {
    //   // テキストが非表示なら部材軸の表示も消す
    //   for (const group of this.axisList) {
    //     group.visible = false;
    //   }
    // }

    // guiの表示設定
    // if (gui === true) {
    //   this.guiEnable();
    // } else {
    //   // 黒に戻す
    //   this.selectionItem = null;
    //   this.memberList.children.map((item) => {
    //     // 元の色にする
    //     const material = item['material'];
    //     material["color"].setHex(0x000000);
    //     material["opacity"] = 1.0;
    //   });
    //   this.axisList.map((item) => {
    //     item.visible = false;
    //   });
    //   this.guiDisable();
    // }
  }

  // guiを表示する
  private guiEnable(): void {
    // if (this.gui !== null) {
    //   return;
    // }

    // this.gui = this.scene.gui
    //   .add(this.params, "memberScale", 0, 1000)
    //   .step(1)
    //   .onChange((value) => {
    //     this.scale = value;
    //     this.onResize();
    //     this.scene.render();
    //   });
  }

  // guiを非表示にする
  private guiDisable(): void {
    // if (this.gui === null) {
    //   return;
    // }
    // this.scene.gui.remove(this.gui);
    // this.gui = null;
  }

  // マウス位置とぶつかったオブジェクトを検出する
  public detectObject(raycaster: THREE.Raycaster, action: string): void {
    
    if (this.noticePointList.children.length === 0) {
      return; // 対象がなければ何もしない
    }

    //交差しているオブジェクトを取得
    const intersects = raycaster.intersectObjects(this.noticePointList.children, true);
    if ( intersects.length <= 0 ){
      return;
    }

    switch (action) {
      case "click":
        this.noticePointList.children.map((item) => {
          if (intersects.length > 0 && item === intersects[0].object) {
            // 色を赤くする
            this.sendNoticePointNodeSubject(item);
            const material = item['material'];
            material["color"].setHex(0xff0000);
            material["opacity"] = 1.0;
          }
          else{
            const material = item['material'];
            material['color'].setHex(0X00A5FF);
            material["opacity"] = 1.0;
          }
        });
        break;

      // case "select":
      //   if (intersects.length > 0) {
      //     this.selectionItem = null;
      //     this.noticePointList.children.map((item) => {
      //       const material = item['material'];
      //       if (item === intersects[0].object) {
      //         // 色を赤くする
      //         material["color"].setHex(0xff0000);
      //         material["opacity"] = 1.0;
      //         this.selectionItem = item;
      //       } else {
      //         // それ以外は元の色にする
      //         material["color"].setHex(0x000000);
      //         material["opacity"] = 1.0;
      //       }
      //     });
      //     // 選択されたアイテムの軸を表示する
      //     if (this.selectionItem !== null) {
      //       this.axisList.map((item) => {
      //         const key: string = this.selectionItem.name + "axis";
      //         if (item.name === key) {
      //           item.visible = true;
      //         } else {
      //           item.visible = false;
      //         }
      //       });
      //     }
      //   }
      //   break;

      // case "hover":
      //   this.noticePointList.children.map((item) => {
      //     const material = item['material'];
      //     if (intersects.length > 0 && item === intersects[0].object) {
      //       // 色を赤くする
      //       material["color"].setHex(0xff0000);
      //       material["opacity"] = 0.25;
      //     } else {
      //       if (item === this.selectionItem) {
      //         material["color"].setHex(0xff0000);
      //         material["opacity"] = 1.0;
      //       } else {
      //         // それ以外は元の色にする
      //         material["color"].setHex(0x000000);
      //         material["opacity"] = 1.0;
      //       }
      //     }
      //   });
      //   break;

      default:
        return;
    }
    this.scene.render();
  }

  private noticePointSelectedInThreeSubject = new Subject<any>();
  noticePointSelected$ = this.noticePointSelectedInThreeSubject.asObservable();

  sendNoticePointNodeSubject(item: any) {
    this.noticePointSelectedInThreeSubject.next(item);
  }
}
