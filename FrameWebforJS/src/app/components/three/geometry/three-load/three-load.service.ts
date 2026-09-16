import { Injectable } from "@angular/core";
import * as THREE from "three";

import { InputLoadService } from "../../../input/input-load/input-load.service";
import { InputMembersService } from "../../../input/input-members/input-members.service";
import { InputNodesService } from "../../../input/input-nodes/input-nodes.service";
import { LanguagesService } from "src/app/providers/languages.service";
import { SceneService } from "../../scene.service";

import { ThreeMembersService } from "../three-members.service";
import { ThreeNodesService } from "../three-nodes.service";

import {
  LoadData,
  LocalAxis,
  MaxLoadDict,
  OffsetDict,
} from "./three-load-common";
import { ThreeLoadAxial } from "./three-load-axial";
import { ThreeLoadDistribute } from "./three-load-distribute";
import { ThreeLoadMemberMoment } from "./three-load-member-moment";
import { ThreeLoadMemberPoint } from "./three-load-member-point";
import { ThreeLoadMoment } from "./three-load-moment";
import { ThreeLoadPoint } from "./three-load-point";
import { ThreeLoadTemperature } from "./three-load-temperature";
import { ThreeLoadTorsion } from "./three-load-torsion";
import { DataHelperModule } from "src/app/providers/data-helper.module";

/** 荷重ケースデータ */
type CaseData = {
  /** threejsオブジェクト */
  ThreeObject: THREE.Object3D;
  /** 荷重データの一覧 */
  loadList: LoadData[];
  /** 荷重種別ごとの荷重最大値 */
  maxLoadDict: MaxLoadDict;
  /** 各部材の部材座標系。キーは部材番号。不要になったデータを削除する処理がないので、Object.{keys|values|entries}でループを回す場合は注意 */
  localAxisDict: {
    [
      /** 部材番号 */
      key: string
    ]: LocalAxis;
  };
};

@Injectable({
  providedIn: "root",
})
export class ThreeLoadService {
  private isVisible = { object: false, gui: false };

  /** 全ケースの荷重を保存。キーはケース番号 */
  private AllCaseDataDict: {
    [
      /** ケース番号 */
      key: string
    ]: CaseData;
  };
  /** 現在 表示中のケース番号 */
  private currentCaseId: string;

  /** 荷重図作成時の 節点データ */
  private nodeData: object;
  /** 荷重図作成時の 要素データ */
  private memberData: object;

  /** 変更された 節点データ */
  private newNodeData: object | undefined;
  /** 変更された 要素データ */
  private newMemberData: object | undefined;

  /** 選択中のオブジェクト */
  private selectedObject: LoadData | undefined;

  /** アニメーションハンドラ */
  private animationHandle: number | undefined;

  /** アニメーション用のクロック */
  private animationClock: THREE.Clock;
  /** アニメーション用の設定 */
  private animationConfig = {
    duration: 0.5, // 各ケース表示時間（秒）
    isActive: false,
    currentKeys: [] as string[],
    currentLL_list: {} as any,
    previousIndex: -1 // 前回のインデックスを記録
  };

  // 初期化
  constructor(
    private languagesService: LanguagesService,
    private scene: SceneService,
    private nodeThree: ThreeNodesService,
    private node: InputNodesService,
    private member: InputMembersService,
    private load: InputLoadService,
    private three_member: ThreeMembersService,
    private helper: DataHelperModule
  ) {
    // 全てのケースの荷重情報
    this.AllCaseDataDict = {};
    this.currentCaseId = null;

    // 節点、部材データ
    this.nodeData = {};
    this.memberData = {};
    this.newNodeData = undefined;
    this.newMemberData = undefined;

    // 選択中のオブジェクト
    this.selectedObject = undefined;

    // アニメーションハンドラ
    this.animationHandle = undefined;

    // アニメーション用のクロック
    this.animationClock = new THREE.Clock();
  }

  /** 荷重を再設定する */
  public ClearData(): void {
    // 荷重を全部削除する
    for (const id of Object.keys(this.AllCaseDataDict)) {
      this.removeCase(id);
    }

    this.AllCaseDataDict = {};
    this.currentCaseId = null;

    // 節点、部材データ
    this.nodeData = {};
    this.memberData = {};
    this.newNodeData = undefined;
    this.newMemberData = undefined;

    // 選択中のオブジェクト
    this.selectedObject = undefined;

    // アニメーションのオブジェクトを解放(アニメーションハンドラの初期化)
    this.cancelAnimation();
  }

  /** ファイルを読み込むなど、りセットする */
  public ResetData(): void {
    this.ClearData();

    // ファイルを開いたときの処理
    // 荷重を作成する
    const loadData = this.load.getLoadJson(0);
    for (const id of Object.keys(loadData)) {
      this.addCase(id);
    }

    // データを入手
    this.nodeData = this.node.getNodeJson(0);
    this.memberData = this.member.getMemberJson(0);

    // 格点データ
    this.newNodeData = undefined;
    if (Object.keys(this.nodeData).length <= 0) {
      return; // 格点がなければ 以降の処理は行わない
    }

    // 節点荷重データを入手
    // const nodeLoadData = this.load.getNodeLoadJson(0);
    const nodeLoadData = {};
    // 要素荷重データを入手
    const memberLoadData = {};
    for (const [id, tmp] of Object.entries<any>(loadData)) {
      if ("load_member" in tmp && tmp.load_member.length > 0) {
        memberLoadData[id] = tmp.load_member;
      }
      if ("load_node" in tmp && tmp.load_node.length > 0) {
        nodeLoadData[id] = tmp.load_node;
      }
    }

    // 荷重図を(非表示のまま)作成する
    for (const [id, LoadList] of Object.entries(this.AllCaseDataDict)) {
      // 節点荷重 --------------------------------------------
      if (id in nodeLoadData) {
        const targetNodeLoad = nodeLoadData[id];
        // 節点荷重を作成する
        this.createPointLoad(
          targetNodeLoad,
          this.nodeData,
          this.memberData,
          LoadList.ThreeObject,
          LoadList.loadList
        );
      }

      // 要素荷重 --------------------------------------------
      // 要素データを入手
      this.newMemberData = undefined;
      if (Object.keys(this.memberData).length > 0) {
        if (id in memberLoadData) {
          const targetMemberLoad = memberLoadData[id];
          // 要素荷重を作成する
          this.createMemberLoad(
            targetMemberLoad,
            this.nodeData,
            this.memberData,
            LoadList.ThreeObject,
            LoadList.loadList,
            LoadList.localAxisDict
          );
        }
      }

      // 荷重の最大値を調べる
      this.updateMaxLoad(id);
      // 荷重データの並び替え
      this.loadListSort(id);
    }
  }

  /**
   * 表示ケースを変更する
   * @param changeCase ケース番号
   * @param isLL_Load 連行移動荷重を描画するか
   */
  public changeCase(changeCase: number, isLL_Load: boolean = false): void {
    // 全てのオブジェクトをデフォルトのカラーの状態にする
    this.selectChange(-1, "");

    // 連行荷重が完成したら 以下のアニメーションを有効にする
    // 荷重名称を調べる
    if (isLL_Load === false) {
      const symbol: string = this.load.getLoadName(changeCase, "symbol");
      isLL_Load = symbol.includes("LL");
    }

    this.cancelAnimation();

    // this.currentCaseId = changeCase.toString();
    if (!isLL_Load) {
      this.visibleCaseChange(changeCase.toString());
      this.onResize();
      this.scene.render();
    } else {
      // 連行荷重の場合
      const LL_list = this.load.getMemberLoadJson(0, changeCase.toString());
      const LL_keys: string[] = Object.keys(LL_list);
      if (LL_keys.length > 0) {
        this.new_animation(LL_keys, LL_list); //ループのきっかけ
      }
    }

  }

  /**
   * 表示ケースの変更
   * @param id ケース番号
   * @param isLL_Load 連行移動荷重を描画するか
   */
  private visibleCaseChange(id: string): void {
    if (id === null) {
      // 非表示にして終わる
      for (const targetLoad of Object.values(this.AllCaseDataDict)) {
        const ThreeObject: THREE.Object3D = targetLoad.ThreeObject;
        ThreeObject.visible = false;
      }
      // アニメーションのオブジェクトを解放
      this.cancelAnimation();
      // this.scene.render();
      this.currentCaseId = id;
      return;
    }

    // 初めての荷重ケースが呼び出された場合
    if (!(id in this.AllCaseDataDict)) {
      this.addCase(id);
    }

    // 前のケースを非表示、新しいケースを表示（効率的な切り替え）
    if (this.currentCaseId && this.currentCaseId in this.AllCaseDataDict) {
      this.AllCaseDataDict[this.currentCaseId].ThreeObject.visible = false;
    }

    this.AllCaseDataDict[id].ThreeObject.visible = true;

    // // 荷重の表示非表示を切り替える
    // for (const [key, targetLoad] of Object.entries(this.AllCaseDataDict)) {
    //   console.log("targetLoad", targetLoad);
    //   const ThreeObject: THREE.Object3D = targetLoad.ThreeObject;
    //   ThreeObject.visible = key === id ? true : false;
    // }

    // 連行荷重アニメーション中は currentIndex は 親id のまま変えない
    this.currentCaseId = id;
  }

  /**
   * 連行移動荷重のアニメーションを開始する（Three.js標準手法版）
   * @param keys ケース番号のリスト
   * @param LL_list 連行荷重データ
   * @param duration 各ケース表示時間（秒）
   */
  public new_animation(
    keys: string[], 
    LL_list: any, 
    duration: number = 0.5
  ): void {
    // 既存のアニメーションを停止
    this.cancelAnimation();
    
    // アニメーション設定
    this.animationConfig.duration = duration;
    this.animationConfig.isActive = true;
    this.animationConfig.currentKeys = keys;
    this.animationConfig.currentLL_list = LL_list;
    this.animationConfig.previousIndex = -1; // 初期化
    
    // クロックをリセット
    this.animationClock.start();

    // アニメーションループを開始
    this.animationLoop();
  }

  /**
   * アニメーションループ（時間ベース制御）
   */
  private animationLoop(): void {
    if (!this.animationConfig.isActive) {
      return;
    }

    // 経過時間を取得
    const elapsedTime = this.animationClock.getElapsedTime();

    // 現在表示すべきケースのインデックスを計算
    const totalDuration = this.animationConfig.duration * this.animationConfig.currentKeys.length;
    const normalizedTime = (elapsedTime % totalDuration) / totalDuration;
    const currentIndex = Math.floor(normalizedTime * this.animationConfig.currentKeys.length);


    // インデックスが変更された時のみ処理を実行
    if (currentIndex !== this.animationConfig.previousIndex) {
      this.animationConfig.previousIndex = currentIndex;

      // 表示ケースを変更
      const currentKey = this.animationConfig.currentKeys[currentIndex];

      console.log(currentKey,
        this.animationConfig.currentLL_list[currentKey][0].L1,
        this.animationConfig.currentLL_list[currentKey][0].L2,
        this.animationConfig.currentLL_list[currentKey][0].P1,
        this.animationConfig.currentLL_list[currentKey][0].P2);

      this.visibleCaseChange(currentKey);//, true);

      // レンダリング
      this.onResize();
      this.scene.render();
    }

    // 次のフレームを要求
    this.animationHandle = requestAnimationFrame(() => {
      this.animationLoop();
    });
  }

  /**
   * 新しいアニメーションを停止する
   */
  public stopNewAnimation(): void {
    this.animationConfig.isActive = false;
    this.animationConfig.previousIndex = -1; // リセット
    this.animationClock.stop();
    this.cancelAnimation();
  }

  /**
   * アニメーション設定を変更する
   * @param duration 各ケース表示時間（秒）
   */
  public setAnimationDuration(duration: number): void {
    this.animationConfig.duration = duration;
  }

  /**
   * アニメーションの状態を取得する
   */
  public getAnimationStatus(): {
    isActive: boolean;
    elapsedTime: number;
    currentIndex: number;
    totalKeys: number;
  } {
    const elapsedTime = this.animationClock.getElapsedTime();
    const totalDuration = this.animationConfig.duration * this.animationConfig.currentKeys.length;
    const normalizedTime = (elapsedTime % totalDuration) / totalDuration;
    const currentIndex = Math.floor(normalizedTime * this.animationConfig.currentKeys.length);
    
    return {
      isActive: this.animationConfig.isActive,
      elapsedTime: elapsedTime,
      currentIndex: currentIndex,
      totalKeys: this.animationConfig.currentKeys.length
    };
  }

  /**
   * ケースを追加する
   * @param id ケース番号
   */
  private addCase(id: string): void {
    const ThreeObject = new THREE.Object3D();
    ThreeObject.name = id;
    ThreeObject.visible = false; // ファイルを読んだ時点では、全ケース非表示
    this.AllCaseDataDict[id] = {
      ThreeObject,
      loadList: [],
      maxLoadDict: {
        pMax: 0, // 最も大きい集中荷重値
        mMax: 0, // 最も大きいモーメント
        wMax: 0, // 最も大きい分布荷重
        rMax: 0, // 最も大きいねじり分布荷重
        qMax: 0, // 最も大きい軸方向分布荷重
      },
      localAxisDict: {},
    };

    this.scene.add(ThreeObject); // シーンに追加
  }

  /**
   * シートの選択行が指すオブジェクトをハイライトする
   * @param index_row 選択行の行番号
   * @param index_column 選択されたカラムの列見出し
   */
  public selectChange(index_row: number, index_column: string): void {
    const id: string = this.currentCaseId;
    if (!this.AllCaseDataDict[id]) {
      return;
    }
    const threeObject = this.AllCaseDataDict[id].ThreeObject;

    for (const child of threeObject.children) {
      const item = child as LoadData;

      let column = "";
      if (["n", "tx", "ty", "tz", "rx", "ry", "rz"].includes(index_column)) {
        // 節点荷重の場合
        column = index_column;
      } else {
        // 部材荷重の場合
        column = "m";
      }
      const key = item.loadType + "-" + index_row.toString() + "-" + column;

      if (item.name.startsWith(key)) {
        item.highlight(true);
        this.scene.getScreenPosition(item);
      } else {
        item.highlight(false);
      }
    }

    this.scene.render();
  }

  /**
   * ケースの荷重図を消去する
   * @param id ケース番号
   * @param option 消去後にレンダリングを実行するか
   */
  public removeCase(id: string, option: boolean = true): void {
    if (!(id in this.AllCaseDataDict)) {
      return;
    }

    const data = this.AllCaseDataDict[id];
    this.removeMemberLoadList(data);
    this.removePointLoadList(data);

    const ThreeObject = data.ThreeObject;
    this.scene.remove(ThreeObject);

    delete this.AllCaseDataDict[id];

    // アニメーションのオブジェクトを解放
    this.cancelAnimation();

    if (option) {
      this.scene.render();
    }
  }

  /**
   * 節点の入力が変更された場合 新しい入力データを保持しておく
   * @param jsonData 節点データ(JSON)
   */
  public changeNode(jsonData: object | null): void {
    if (!jsonData) {
      this.newNodeData = {};
    } else {
      this.newNodeData = jsonData;
    }
  }

  /**
   * 要素の入力が変更された場合 新しい入力データを保持しておく
   * @param jsonData 要素データ(JSON)
   */
  public changeMember(jsonData: object | null): void {
    if (!jsonData) {
      this.newMemberData = {};
    } else {
      this.newMemberData = jsonData;
    }
  }

  /** 節点や要素が変更された部分を描きなおす */
  public reDrawNodeMember(): void {
    if (!this.newNodeData && !this.newMemberData) {
      return;
    }

    // 格点の変わった部分を探す
    const changeNodeList = {};
    if (this.newNodeData) {
      for (const key of Object.keys(this.nodeData)) {
        if (!(key in this.newNodeData)) {
          // 古い情報にあって新しい情報にない節点
          changeNodeList[key] = "delete";
        }
      }
      for (const key of Object.keys(this.newNodeData)) {
        if (!(key in this.nodeData)) {
          // 新しい情報にあって古い情報にない節点
          changeNodeList[key] = "add";
          continue;
        }
        const oldNode = this.nodeData[key];
        const newNode = this.newNodeData[key];
        if (
          oldNode.x !== newNode.x ||
          oldNode.y !== newNode.y ||
          oldNode.z !== newNode.z
        ) {
          changeNodeList[key] = "change";
        }
      }
    }

    const changeMemberList = {};
    // 部材の変わった部分を探す
    if (this.newMemberData) {
      for (const key of Object.keys(this.memberData)) {
        if (!(key in this.newMemberData)) {
          // 古い情報にあって新しい情報にない節点
          changeMemberList[key] = "delete";
        }
      }
      for (const key of Object.keys(this.newMemberData)) {
        if (!(key in this.memberData)) {
          // 新しい情報にあって古い情報にない節点
          changeMemberList[key] = "add";
          continue;
        }
        const oldMember = this.memberData[key];
        const newMember = this.newMemberData[key];
        if (
          oldMember.ni !== newMember.ni ||
          oldMember.nj !== newMember.nj ||
          oldMember.cg !== newMember.cg
        ) {
          changeMemberList[key] = "change";
        }
      }
    }
    // 格点の変更によって影響のある部材を特定する
    const targetMemberData = this.newMemberData ?? this.memberData;
    for (const [key, newMember] of Object.entries(targetMemberData)) {
      if (newMember.ni in changeNodeList || newMember.nj in changeNodeList) {
        changeMemberList[key] = "node change";
      }
    }

    // 荷重を変更する
    const oldIndex = this.currentCaseId;
    this.nodeData = this.newNodeData ?? this.nodeData;
    this.memberData = this.newMemberData ?? this.memberData;
    // 荷重データを入手
    const nodeLoadData = this.load.getNodeLoadJson(0);
    const memberLoadData = this.load.getMemberLoadJson(0);
    // 荷重を修正
    for (const id of Object.keys(this.AllCaseDataDict)) {
      this.currentCaseId = id;
      let editFlg = false;
      if (this.currentCaseId in nodeLoadData) {
        for (const load of nodeLoadData[this.currentCaseId]) {
          if (load.n.toString() in changeNodeList)
            this.changeNodeLoad(load.row, nodeLoadData);
          editFlg = true;
        }
      }
      if (this.currentCaseId in memberLoadData) {
        for (const load of memberLoadData[this.currentCaseId]) {
          if (load.m.toString() in changeMemberList) {
            this.changeMemberLoad(load.row, memberLoadData);
            editFlg = true;
          }
        }
      }
      if (editFlg === true) {
        this.onResize();
      }
    }

    this.newNodeData = undefined;
    this.newMemberData = undefined;
    this.currentCaseId = oldIndex;
  }

  /**
   * 連行荷重を変更する
   * @param id ケース番号
   */
  public change_LL_Load(id: string): void {
    const memberLoadData = this.load.getMemberLoadJson(0, id); //計算に使う版
    const LL_keys = Object.keys(memberLoadData);

    // 対象の連行荷重を全部削除する
    let keys = Object.keys(this.AllCaseDataDict).filter((e) => {
      return e.indexOf(id + ".") === 0;
    });
    if (keys !== undefined) {
      keys = [id].concat(keys);
      for (const key of keys) {
        this.removeCase(key);
      }
    } else {
      keys = [id];
    }

    if (Object.keys(this.memberData).length > 0) {
      for (const key of LL_keys) {
        // 一旦削除したので追加する
        this.addCase(key);
        const LoadList = this.AllCaseDataDict[key];
        //
        const targetMemberLoad = memberLoadData[key];
        // 要素荷重を作成する
        this.createMemberLoad(
          targetMemberLoad,
          this.nodeData,
          this.memberData,
          LoadList.ThreeObject,
          LoadList.loadList,
          LoadList.localAxisDict
        );
      }
      // 荷重の最大値を調べる
      Object.keys(memberLoadData).forEach((caseStr) => {
        this.updateMaxLoad(caseStr);
      });
      // 荷重データの並び替え
      this.loadListSort();
      // サイズを調整する
      this.onResize();
    }

    // 連行荷重の場合
    this.new_animation(LL_keys, memberLoadData); //ループのきっかけ
  }

  /**
   * 荷重の入力が変更された場合の処理
   * @param row 入力が変更された荷重強度表の行番号
   */
  public changeData(row: number): void {
    let row1 = row; // 変更・調整されるrow1を定義

    const index = parseInt(this.currentCaseId, 10);
    const symbol: string = this.load.getLoadName(index, "symbol");
    if (symbol === "LL") {
      this.change_LL_Load(this.currentCaseId);
      return;
    }

    // データになカレントデータがなければ
    if (!(this.currentCaseId in this.load.load)) {
      this.removeCase(this.currentCaseId);
      return;
    }

    // 格点データを入手
    if (Object.keys(this.nodeData).length <= 0) {
      return; // 格点がなければ 以降の処理は行わない
    }

    // 節点荷重データを入手
    const nodeLoadData = this.load.getNodeLoadJson(0, this.currentCaseId);
    // 節点荷重を変更
    this.changeNodeLoad(row, nodeLoadData);

    // 要素データを入手
    if (Object.keys(this.memberData).length <= 0) {
      return; //要素がなければ 以降の処理は行わない
    }

    const tempMemberLoad = this.load.getMemberLoadJson(
      null,
      this.currentCaseId
    ); //計算に使う版
    const memberLoadData = this.load.getMemberLoadJson(0, this.currentCaseId); //計算に使う版

    if (this.currentCaseId in memberLoadData) {
      // 要素荷重を変更
      this.changeMemberLoad(row1, memberLoadData); //実際に荷重として使っているのは　memberLoadData こっち

      // 対象行以下の行について
      row1++;
      const tmLoad = tempMemberLoad[this.currentCaseId];
      let i = tmLoad.findIndex((e) => e.row === row1);
      while (i >= 0) {
        if (tmLoad[i].L1 == null) {
          break;
        }
        if (!tmLoad[i].L1.includes("-")) {
          break;
        }
        // 要素荷重を変更
        this.changeMemberLoad(tmLoad[i].row, memberLoadData); //実際に荷重として使っているのは　memberLoadData こっち
        row1++;
        i = tmLoad.findIndex((e) => e.row === row1);
      }
    }

    // 荷重の最大値を調べる
    new Set(
      ...Object.keys(nodeLoadData),
      ...Object.keys(memberLoadData)
    ).forEach((caseStr) => {
      this.updateMaxLoad(caseStr);
    });

    // 荷重データの並び替え
    this.loadListSort();

    // サイズを調整する
    this.onResize();
    // レンダリング
    this.scene.render();
    // 表示フラグを ON にする
    this.isVisible.object = true;
  }

  /**
   * 荷重の入力が変更された場合の処理（複数行）
   * @param param updatedRows=入力が変更された荷重強度表の行番号のリスト
   */
  public changeDataList(param): void {
    const { updatedRows } = param;

    const index = parseInt(this.currentCaseId, 10);
    const symbol: string = this.load.getLoadName(index, "symbol");
    if (symbol === "LL") {
      const integerPart = this.currentCaseId.split('.')[0];
      this.change_LL_Load(integerPart);
      return;
    }

    // データになカレントデータがなければ
    if (!(this.currentCaseId in this.load.load)) {
      this.removeCase(this.currentCaseId);
      return;
    }

    // 格点データを入手
    if (Object.keys(this.nodeData).length <= 0) {
      return; // 格点がなければ 以降の処理は行わない
    }

    // 要素データを入手
    if (Object.keys(this.memberData).length <= 0) {
      return; //要素がなければ 以降の処理は行わない
    }

    const tempMemberLoad = this.load.getMemberLoadJson(
      null,
      this.currentCaseId
    ); //計算に使う版
    let memberLoadData = this.load.getMemberLoadJson(0, this.currentCaseId); //計算に使う版

    // TODO: 「確認要」荷重強度をすべて削除するとレンダリングにならないトラブルを解消
    if (!(this.currentCaseId in memberLoadData)) {
      memberLoadData[this.currentCaseId] = [];
    }

    // 節点荷重データを入手
    const nodeLoadData = this.load.getNodeLoadJson(0, this.currentCaseId);

    updatedRows.forEach((row: number) => {
      let row1 = row; // 変更・調整されるrow1を定義

      // 節点荷重を変更
      this.changeNodeLoad(row, nodeLoadData);

      if (this.currentCaseId in memberLoadData) {
        // 要素荷重を変更
        this.changeMemberLoad(row1, memberLoadData); //実際に荷重として使っているのは　memberLoadData こっち

        // 対象行以下の行について
        row1++;
        const tmLoad = tempMemberLoad[this.currentCaseId];
        if (!tmLoad) {
          return;
        }
        let i = tmLoad.findIndex((e) => e.row === row1);
        while (i >= 0) {
          if (tmLoad[i].L1 == null) {
            break;
          }
          if (!tmLoad[i].L1.includes("-")) {
            break;
          }
          // 要素荷重を変更
          this.changeMemberLoad(tmLoad[i].row, memberLoadData); //実際に荷重として使っているのは　memberLoadData こっち
          row1++;
          i = tmLoad.findIndex((e) => e.row === row1);
        }
      }
    });

    // 荷重の最大値を調べる
    new Set(
      Object.keys(nodeLoadData).concat(Object.keys(memberLoadData))
    ).forEach((caseStr) => {
      this.updateMaxLoad(caseStr);
    });

    // 荷重データの並び替え
    this.loadListSort();

    // サイズを調整する
    this.onResize();
    // レンダリング
    this.scene.render();
    // 表示フラグを ON にする
    this.isVisible.object = true;
  }

  /**
   * 節点荷重を変更
   * @param row 入力が変更された荷重強度表の行番号
   * @param nodeLoadData 節点荷重データ(JSON)
   */
  private changeNodeLoad(row: number, nodeLoadData: any): void {
    const LoadList = this.AllCaseDataDict[this.currentCaseId];

    if (this.currentCaseId in nodeLoadData) {
      // 節点荷重の最大値を調べる
      const tempNodeLoad = nodeLoadData[this.currentCaseId];

      // 対象行(row) に入力されている部材番号を調べる
      const targetNodeLoad = tempNodeLoad.filter((load) => load.row === row);

      this.removePointLoadList(LoadList, row);

      this.createPointLoad(
        targetNodeLoad,
        this.nodeData,
        this.memberData,
        LoadList.ThreeObject,
        LoadList.loadList
      );
    } else {
      // ケースが存在しなかった
      this.removePointLoadList(LoadList);
    }
  }

  /**
   * 節点荷重を削除する
   * @param LoadList 描画ケースデータ
   * @param row 削除対象の荷重強度表の行番号。undefinedの場合は全ての節点荷重を削除
   */
  private removePointLoadList(
    LoadList: CaseData,
    row: number | undefined = undefined
  ): void {
    const list = LoadList.loadList;
    for (let i = list.length - 1; i >= 0; i--) {
      const item = list[i];
      if (item.col !== "p") {
        // 節点荷重ではない荷重は処理対象外
        continue;
      }
      if (row !== undefined && item.row !== row) {
        continue;
      }
      LoadList.ThreeObject.remove(item);
      list.splice(i, 1);
    }
  }

  /**
   * 要素荷重を変更
   * @param row 入力が変更された荷重強度表の行番号
   * @param memberLoadData 要素荷重データ(JSON)
   */
  private changeMemberLoad(row: number, memberLoadData: any): void {
    for (const [key, tempMemberLoad] of Object.entries<any>(memberLoadData)) {
      // "LL"（連行荷重）の時に発生する
      if (this.AllCaseDataDict[key] === undefined) {
        this.addCase(key); // 来ないと思う
      }
      const LoadList = this.AllCaseDataDict[key];

      // 対象行(row) に入力されている部材番号を調べる
      const targetMemberLoad = tempMemberLoad.filter(
        (load) => load.row === row
      );
      // 同じ行にあった荷重を一旦削除
      this.removeMemberLoadList(LoadList, row);

      this.createMemberLoad(
        targetMemberLoad,
        this.nodeData,
        this.memberData,
        LoadList.ThreeObject,
        LoadList.loadList,
        LoadList.localAxisDict
      );
    }
  }

  /**
   * 要素荷重を削除する
   * @param LoadList 描画ケースデータ
   * @param row 削除対象の荷重強度表の行番号。undefinedの場合は全ての要素荷重を削除
   */
  private removeMemberLoadList(
    LoadList: CaseData,
    row: number | undefined = undefined
  ): void {
    const list = LoadList.loadList;
    for (let i = list.length - 1; i >= 0; i--) {
      const item = list[i];
      if (item.col !== "m") {
        // 部材荷重ではない荷重は処理対象外
        continue;
      }
      if (row !== undefined && item.row !== row) {
        continue;
      }
      LoadList.ThreeObject.remove(item);
      list.splice(i, 1);
    }
  }

  /**
   * 節点荷重と節点モーメント荷重の荷重データとthreejsオブジェクトを生成する
   * @param targetNodeLoad 生成対象の荷重データ(JSON)
   * @param nodeData 節点データ(JSON)
   * @param memberData 部材データ(JSON)
   * @param threeObject 生成したthreejsオブジェクトの格納先
   * @param pointLoadList 生成した荷重データの格納先
   */
  private createPointLoad(
    targetNodeLoad: any[],
    nodeData: object,
    memberData: object,
    threeObject: THREE.Object3D,
    pointLoadList: LoadData[]
  ): void {
    if (!targetNodeLoad) {
      return;
    }

    // 集中荷重の矢印をシーンに追加する
    for (const load of targetNodeLoad) {
      const n = load.n.toString(); // 節点番号

      if (!(n in nodeData)) {
        continue;
      }
      const node = nodeData[n];
      const position = new THREE.Vector3(node.x, node.y, node.z);

      const mNoList = new Array<string>();
      for (const mNo in memberData) {
        const member = memberData[mNo];
        if (member.ni == n || member.nj == n) {
          mNoList.push(mNo.toString());
        }
      }

      // 集中荷重 ---------------------------------
      for (let key of ["tx", "ty", "tz"]) {
        if (!(key in load)) continue;

        const value = load[key];
        if (value === 0) continue;

        const arrow = ThreeLoadPoint.create(
          n,
          mNoList,
          position,
          key,
          value,
          load.row
        );
        if (!arrow) {
          continue;
        }

        pointLoadList.push(arrow);
        threeObject.add(arrow);
      }

      // 節点モーメント荷重 -------------------------
      for (const key of ["rx", "ry", "rz"]) {
        if (!(key in load)) continue;

        const value = load[key];
        if (load[key] === 0) continue;

        const arrow = ThreeLoadMoment.create(n, position, key, value, load.row);
        if (!arrow) {
          continue;
        }

        pointLoadList.push(arrow);
        threeObject.add(arrow);
      }
    }
  }

  /**
   * 要素荷重の荷重データとthreejsオブジェクトを生成する
   * @param memberLoadData 生成対象の要素荷重データ(JSON)
   * @param nodeData 節点データ(JSON)
   * @param memberData 部材データ(JSON)
   * @param threeObject 生成したthreejsオブジェクトの格納先
   * @param memberLoadList 生成した荷重データの格納先
   * @param localAxisDict 各部材の部材座標系
   */
  private createMemberLoad(
    memberLoadData: any[],
    nodeData: object,
    memberData: object,
    threeObject: THREE.Object3D,
    memberLoadList: LoadData[],
    localAxisDict: {
      [key: string]: LocalAxis;
    }
  ): void {
    if (!memberLoadData) {
      return;
    }

    const is3d = this.helper.dimension === 3;

    // 分布荷重の矢印をシーンに追加する
    for (const load of memberLoadData) {
      // 部材データを集計する
      if (!(load.m in memberData)) {
        continue;
      }
      const mNo: string = load.m.toString(); // 部材番号
      const m = memberData[mNo];
      // 節点データを集計する
      if (!(m.ni in nodeData && m.nj in nodeData)) {
        continue;
      }

      if (load.P1 === 0 && load.P2 === 0) {
        continue;
      }

      // 部材の座標軸を取得
      const i = nodeData[m.ni];
      const j = nodeData[m.nj];
      const nodei = new THREE.Vector3(i.x, i.y, i.z);
      const nodej = new THREE.Vector3(j.x, j.y, j.z);
      const localAxis = new LocalAxis(
        this.three_member.localAxis(i.x, i.y, i.z, j.x, j.y, j.z, m.cg)
      );
      localAxisDict[mNo] = localAxis;

      // 荷重値と向き -----------------------------------
      let P1: number = load.P1;
      let P2: number = load.P2;
      let direction: string | undefined = load.direction;

      direction = direction?.trim().toLowerCase() ?? "";

      const niNo = m.ni.toString();
      const njNo = m.nj.toString();

      let arrow: LoadData = undefined;
      switch (load.mark) {
        case 1:
          // 集中荷重
          arrow = ThreeLoadMemberPoint.create(
            mNo,
            niNo,
            njNo,
            nodei,
            nodej,
            load.mark,
            direction,
            load.L1,
            load.L2,
            P1,
            P2,
            localAxis,
            load.row,
            is3d
          );
          break;
        case 2:
          switch (direction) {
            case "x":
            case "y":
            case "z":
            case "gx":
            case "gy":
            case "gz":
              // 軸方向の分布荷重
              arrow = ThreeLoadAxial.create(
                mNo,
                niNo,
                njNo,
                nodei,
                nodej,
                load.mark,
                direction,
                load.L1,
                load.L2,
                P1,
                P2,
                localAxis,
                load.row,
                is3d
              );
              if (arrow !== undefined) {
                break;
              }
              // 軸方向ではない分布荷重
              arrow = ThreeLoadDistribute.create(
                mNo,
                niNo,
                njNo,
                nodei,
                nodej,
                load.mark,
                direction,
                load.L1,
                load.L2,
                P1,
                P2,
                localAxis,
                load.row,
                is3d
              );
              break;
            case "r":
              // ねじりモーメント荷重
              arrow = ThreeLoadTorsion.create(
                mNo,
                niNo,
                njNo,
                nodei,
                nodej,
                load.mark,
                direction,
                load.L1,
                load.L2,
                P1,
                P2,
                localAxis,
                load.row,
                is3d
              );
              break;
            default:
              // 無視
              break;
          }
          break;
        case 9:
          // 温度荷重
          arrow = ThreeLoadTemperature.create(
            mNo,
            niNo,
            njNo,
            nodei,
            nodej,
            load.mark,
            P1,
            localAxis,
            load.row
          );
          break;
        case 11:
          // 集中モーメント荷重
          arrow = ThreeLoadMemberMoment.create(
            mNo,
            niNo,
            njNo,
            nodei,
            nodej,
            load.mark,
            direction,
            load.L1,
            load.L2,
            P1,
            P2,
            localAxis,
            load.row,
            is3d
          );
          break;
        default:
          // 無視
          break;
      }
      if (!arrow) {
        continue;
      }

      memberLoadList.push(arrow);
      threeObject.add(arrow);
    }
  }

  /**
   * 荷重の種類ごとの最大値を調べる
   * @param targetCase 対象データのケース番号
   */
  private updateMaxLoad(targetCase: string) {
    const caseData = this.AllCaseDataDict[targetCase];

    ["pMax", "mMax", "wMax", "rMax", "qMax"].forEach((id) => {
      caseData.maxLoadDict[id] = caseData.loadList
        .map((load) => load[id] as number)
        .reduce((a, b) => Math.max(a, b), 0);
    });
  }

  /**
   * 荷重データの並び替え
   * @param targetCase 対象データのケース番号。undefinedの場合は全てのケースが対象 @default this.currentCaseId
   */
  private loadListSort(targetCase: string = this.currentCaseId) {
    for (const [caseStr, caseData] of Object.entries(this.AllCaseDataDict)) {
      if (targetCase !== undefined && targetCase !== caseStr) {
        continue;
      }
      caseData.loadList.sort((a, b) => {
        if (a.rank < b.rank) {
          return -1;
        } else if (a.rank === b.rank) {
          return a.row - b.row;
        } else {
          return 1;
        }
      });
    }
  }

  /**
   * three.service から呼ばれる 表示・非表示の制御
   * @param flag true=荷重図を表示(guiの表示・非表示はguiの値に依存)、false=荷重図とguiを非表示
   * @param gui true=guiを表示(flag===trueの場合)、false=guiを非表示
   */
  public visibleChange(flag: boolean, gui: boolean): void {
    // 非表示にする
    if (flag === false) {
      this.guiDisable();
      this.changeCase(-1);
      this.isVisible.object = false;
      return;
    }

    // gui の表示を切り替える
    if (gui === true) {
      this.guiEnable();
      //console.log('荷重強度の入力です。')
    } else {
      // 黒に戻す
      this.guiDisable();
      // setColor を初期化する
      //console.log('荷重名称の入力です。')
      this.selectChange(-1, "");
    }
    this.isVisible.gui = gui;

    // すでに表示されていたら変わらない
    if (this.isVisible.object === true) {
      return;
    }

    // 表示する
    this.changeCase(1);
    this.isVisible.object = true;
  }

  // #region gui制御と描画スケール

  /** guiが無効な間の描画スケール値の退避先 */
  private _loadScale = 100;
  /** dat.gui.NumberControllerSlider */
  private gui: any = {};

  /** 描画スケール値 */
  private get LoadScale(): number {
    if ("LoadScale" in this.gui) {
      // guiが有効な間はguiの値を参照
      return this.gui.LoadScale.getValue();
    } else {
      // guiが無効な間は退避されている値を参照
      return this._loadScale;
    }
  }

  /** guiの有効化 */
  private guiEnable(): void {
    if (!("LoadScale" in this.gui)) {
      const gui_step: number = 1;
      this.gui["LoadScale"] = this.scene.gui
        .add({ LoadScale: this._loadScale }, "LoadScale", 0, 400)
        .step(gui_step)
        .onChange((value) => {
          this.onResize();
          this.scene.render();
        });
      this.languagesService.tranText();
    }
  }

  /** guiの無効化 */
  private guiDisable(): void {
    for (const [key, target] of Object.entries(this.gui)) {
      if (key === "LoadScale") {
        // 描画スケール値を退避
        this._loadScale = this.gui.LoadScale.getValue();
      }
      this.scene.gui.remove(target);
    }
    this.gui = {};
  }

  // #endregion gui制御と描画スケール

  private baseScale(): number {
    return this.nodeThree.baseScale * 10;
  }

  /**
   * 描画スケールの変更に伴う全荷重の再描画
   * @param id 処理対象のケース番号
   */
  private onResize(id: string = this.currentCaseId): void {
    if (!(id in this.AllCaseDataDict)) {
      return;
    }

    const caseData = this.AllCaseDataDict[id];
    if (caseData === undefined) {
      return;
    }

    const scale1: number = this.LoadScale / 100;
    const scale2: number = this.baseScale();
    let scale: number = scale1 * scale2;

    // 荷重データの再ソートは不要

    // 節点番号と部材番号の抽出
    const totalNodeNoList = Array.from(
      new Set(
        Array.prototype.concat(
          ...caseData.loadList.map((load) => load.correspondingNodeNoList)
        )
      )
    );
    const totalMemberNoList = Array.from(
      new Set(
        Array.prototype.concat(
          ...caseData.loadList.map((load) => load.correspondingMemberNoList)
        )
      )
    );

    // 全ての節点と部材のoffsetDict初期化
    const nodeOffsetDictMap = new Map(
      totalNodeNoList.map((no) => [no, new OffsetDict()])
    );
    const memberOffsetDictMap = new Map(
      totalMemberNoList.map((no) => {
        const localAxis = caseData.localAxisDict[no];
        return [no, new OffsetDict(localAxis)];
      })
    );

    // 全荷重の再描画
    for (const load of caseData.loadList) {
      load.relocate(
        nodeOffsetDictMap,
        memberOffsetDictMap,
        caseData.maxLoadDict,
        scale,
        undefined
      );
    }

    //this.scene.render(); //コメントアウト：レンダリング不要の場合があるため、レンダリングはこの関数の外側で行う
  }

  /**
   * 2D/3D切り替えに伴う荷重再描画
   */
  public redraw(): void {
    if (this.currentCaseId === null) {
      return;
    }
    const currentCaseId = this.currentCaseId;
    this.changeCase(-1);
    this.ResetData();
    this.changeCase(Number(currentCaseId))
  }

  /**
   * マウス位置とぶつかったオブジェクトを検出する
   * @param raycaster THREE.Raycaster
   * @param action "click", "select", or "hover"
   */
  public detectObject(raycaster: THREE.Raycaster, action: string): void {
    return; // マウスの位置と 当たり判定の位置が ずれてる・・・使いにくいので
    if (!(this.currentCaseId in this.AllCaseDataDict)) {
      this.selectedObject = undefined;
      return; // 対象がなければ何もしない
    }

    const targetLoad = this.AllCaseDataDict[this.currentCaseId];
    const ThreeObject: THREE.Object3D = targetLoad.ThreeObject;

    // 交差しているオブジェクトを取得
    const intersects = raycaster.intersectObjects(ThreeObject.children, true);
    if (intersects.length <= 0) {
      return;
    }

    // マウス位置とぶつかったオブジェクトの親を取得
    const item = this.getParent(intersects[0].object);
    if (!item) {
      return;
    }

    if (action === "hover") {
      if (this.selectedObject === item) {
        return;
      }
    }

    // 全てのハイライトを元に戻す
    this.selectChange(-1, "");

    this.selectedObject = item;

    item.highlight(true);

    this.scene.render();
  }

  /**
   * マウス位置とぶつかったオブジェクトの親を取得
   * @param item マウス位置のthreejsオブジェクト
   * @returns マウス位置にあるオブジェクトに対応する荷重データ
   */
  private getParent(item: THREE.Object3D): LoadData | undefined {
    if (item instanceof LoadData) {
      return item;
    }

    if (!item.parent) {
      return undefined;
    }

    return this.getParent(item.parent);
  }

  /** 連行移動荷重のアニメーションを終了する（新旧両対応） */
  private cancelAnimation(): void {
    // 従来のアニメーションを停止
    if (this.animationHandle !== undefined) {
      cancelAnimationFrame(this.animationHandle);
      this.animationHandle = undefined;
    }
    
    // 新しいアニメーションを停止
    if (this.animationConfig.isActive) {
      this.animationConfig.isActive = false;
      this.animationConfig.previousIndex = -1;
      this.animationClock.stop();
    }
  }


}
