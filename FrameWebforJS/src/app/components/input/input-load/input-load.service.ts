import { Injectable } from "@angular/core";
import { RouterLink } from "@angular/router";
import { DataHelperModule } from "../../../providers/data-helper.module";
import { InputMembersService } from "../input-members/input-members.service";

export type LoadColumns = {
  row: number,
  m1: string,
  m2: string,
  direction: string,
  mark: string,
  L1: string,
  L2: string,
  P1: string,
  P2: string,
  n: string,
  tx: string,
  ty: string,
  tz: string,
  rx: string,
  ry: string,
  rz: string,
};

@Injectable({
  providedIn: "root",
})
export class InputLoadService {
  public load_name: any[];
  public load: {
    [key: string]: LoadColumns[]
  };

  constructor(
    private member: InputMembersService,
    private helper: DataHelperModule
  ) {
    this.clear();
  }

  public clear(): void {
    this.load_name = new Array();
    this.load = {};
  }

  public partClear(row): void {
    this.load[row] = new Array();
  }

  public getLoadNameColumns(index: number): any {
    const caseNo: string = index.toString();

    let result = this.load_name.find((tmp) => {
      return tmp.id === caseNo;
    });

    if (result === undefined) {
      result = {
        id: caseNo,
        rate: "",
        symbol: "",
        name: "",
        fix_node: "",
        fix_member: "",
        element: "",
        joint: "",
        LL_pitch: 0.1
      };
      this.load_name.push(result);
    } else{
      // バージョンアップなどにより 足りない項目を追加
      if(!("LL_pitch" in result)){
        result["LL_pitch"] = 0.1;
      }
    }

    return result;
  }

  public getLoadColumns( index: number, row: number): LoadColumns {
    const typNo: string = index.toString();

    let target: LoadColumns[];

    // タイプ番号を探す
    if (!this.load[typNo]) {
      target = new Array();
    } else {
      target = this.load[typNo];
    }

    // 行を探す
    let result = target.find((tmp) => tmp.row === row);

    // 対象行が無かった時に処理
    if (result === undefined) {
      result = {
        row: row,
        m1: "",
        m2: "",
        direction: "",
        mark: "",
        L1: "",
        L2: "",
        P1: "",
        P2: "",
        n: "",
        tx: "",
        ty: "",
        tz: "",
        rx: "",
        ry: "",
        rz: "",
      };
      target.push(result);
      this.load[typNo] = target;
    }
    return result;
  }

  public setLoadJson(jsonData: {}): void {
    if (!("load" in jsonData)) {
      return;
    }

    this.clear();

    const json: {} = jsonData["load"];

    for (const index of Object.keys(json)) {
      const tmp_load1 = {};
      const tmp_load2 = {};

      const item1: {} = json[index];

      const _rate: string = ("rate" in item1 ? item1["rate"] : "") as string;
      const _symbol: string = ("symbol" in item1 ? item1["symbol"] : "") as string;
      const _name: string = ("name" in item1 ? item1["name"] : "") as string;
      const _fix_node: string = ("fix_node" in item1 ? item1["fix_node"] : "") as string;
      const _fix_member: string =
        ("fix_member" in item1 ? item1["fix_member"] : "") as string;
      const _element: string = ("element" in item1 ? item1["element"] : "") as string;
      const _joint: string = ("joint" in item1 ? item1["joint"] : "") as string;
      const _LL_pitch: number =  "LL_pitch" in item1 ? item1["LL_pitch"] as number : 0.1;

      this.load_name.push({
        id: index,
        rate: _rate,
        symbol: _symbol,
        name: _name,
        fix_node: _fix_node,
        fix_member: _fix_member,
        element: _element,
        joint: _joint,
        LL_pitch: _LL_pitch
      });

      if ("load_node" in item1) {
        const load_node_list: any = item1["load_node"];
        if(load_node_list != null){
          for (let i = 0; i < load_node_list.length; i++) {
            const item2 = load_node_list[i];

            const _row: string = "row" in item2 ? item2["row"] : i + 1;

            const _n: string = "n" in item2 ? item2.n : "";
            let _tx: string = "";
            if ("tx" in item2) _tx = item2.tx;
            if ("dx" in item2) _tx = item2.dx;
            let _ty: string = "";
            if ("ty" in item2) _ty = item2.ty;
            if ("dy" in item2) _ty = item2.dy;
            let _tz: string = "";
            if ("tz" in item2) _tz = item2.tz;
            if ("dz" in item2) _tz = item2.dz;
            let _rx: string = "";
            if ("rx" in item2) _rx = item2.rx;
            if ("ax" in item2) _rx = item2.ax;
            let _ry: string = "";
            if ("ry" in item2) _ry = item2.ry;
            if ("ay" in item2) _ry = item2.ay;
            let _rz: string = "";
            if ("rz" in item2) _rz = item2.rz;
            if ("az" in item2) _rz = item2.az;

            tmp_load1[_row] = {
              row: _row,
              n: _n,
              tx: _tx,
              ty: _ty,
              tz: _tz,
              rx: _rx,
              ry: _ry,
              rz: _rz,
            };
          }
        }
      }
      if ("load_member" in item1) {
        const load_member_list: any = item1["load_member"];
        if(load_member_list != null){
          for (let i = 0; i < load_member_list.length; i++) {
            const item3 = load_member_list[i];
            const _row: string = "row" in item3 ? item3.row : (i + 1).toString();
            const _m1: string = "m1" in item3 ? item3.m1 : "";
            const _m2: string = "m2" in item3 ? item3.m2 : "";
            const _L1: string = "L1" in item3 ? item3.L1 : "";

            let _direction: string = "direction" in item3 ? item3.direction : "";
            if (_direction !== null) {
              _direction = _direction.trim().toLowerCase();
            }

            const _mark: string = "mark" in item3 ? item3.mark : "";

            const _L2: string = "L2" in item3 ? item3.L2 : "";
            const _P1: string = "P1" in item3 ? item3.P1 : "";
            const _P2: string = "P2" in item3 ? item3.P2 : "";

            tmp_load2[_row] = {
              row: _row,
              m1: _m1,
              m2: _m2,
              direction: _direction,
              mark: _mark,
              L1: _L1,
              L2: _L2,
              P1: _P1,
              P2: _P2,
            };
          }
        }
      }

      // 同じ行に load_node があったら合成する
      const tmp_load = new Array();
      for (const row1 of Object.keys(tmp_load1)) {
        const result2 = tmp_load1[row1];
        if (row1 in tmp_load2) {
          const result3 = tmp_load2[row1];
          result2["m1"] = result3["m1"];
          result2["m2"] = result3["m2"];
          result2["direction"] = result3["direction"];
          result2["mark"] = result3["mark"];
          result2["L1"] = result3["L1"];
          result2["L2"] = result3["L2"];
          result2["P1"] = result3["P1"];
          result2["P2"] = result3["P2"];
          delete tmp_load2[row1];
        }
        tmp_load.push(result2);
      }
      for (const row2 of Object.keys(tmp_load2)) {
        const result3 = tmp_load2[row2];
        tmp_load.push(result3);
      }

      this.load[index] = tmp_load;
    }
  }

  public getLoadJson(empty: number = null, isPrint = false) {
    const result = {};

    // 荷重基本設定
    const load_name = this.getLoadNameJson(empty, "", isPrint);

    // 節点荷重データ
    const load_node = this.getNodeLoadJson(empty);

    // 要素荷重データ
    const load_member = this.getMemberLoadJson(empty);

    // 合成する
    if (empty === null) {
      for (const load_id of Object.keys(load_name)) {
        const jsonData = load_name[load_id];
        if (load_id in load_node) {
          jsonData["load_node"] = load_node[load_id];
          delete load_node[load_id];
        }
        if (load_id in load_member) {
          jsonData["load_member"] = load_member[load_id];
          delete load_member[load_id];
        }
        result[load_id] = jsonData;
        delete load_name[load_id];
      }
    }

    //
    for (const load_id of Object.keys(load_node)) {
      let jsonData = {};
      if (load_id in load_name) {
        jsonData = load_name[load_id];
      } else {
        jsonData = { fix_node: 1, fix_member: 1, element: 1, joint: 1 };
      }
      jsonData["load_node"] = load_node[load_id];
      if (load_id in load_member) {
        jsonData["load_member"] = load_member[load_id];
        delete load_member[load_id];
      }
      result[load_id] = jsonData;
      delete load_name[load_id];
    }

    //
    for (let load_id_1 of Object.keys(load_member)) {
      let jsonData = {};
      const load_id = parseInt(load_id_1, 10);
      if (load_id in load_name) {
        jsonData = JSON.parse(JSON.stringify(load_name[load_id]));
      } else {
        jsonData["rate"] = 1;
        jsonData["fix_node"] = 1;
        jsonData["fix_member"] = 1;
        jsonData["element"] = 1;
        jsonData["joint"] = 1;
      }
      jsonData["load_member"] = JSON.parse(
        JSON.stringify(load_member[load_id_1])
      );
      result[load_id_1] = jsonData;
      // jsonData["load_member"] = load_member[load_id_1];
      // delete load_member[load_id_1];
    }

    // 無効なデータを削除する
    const deleteKey: string[] = Array();
    for (const load_id of Object.keys(result)) {
      let jsonData = result[load_id];
      let flg: boolean = false;
      if (empty === 0) {
        flg = true;
        for (const key of ["fix_node", "fix_member", "element", "joint", "rate"]) {
          if (jsonData[key] === empty) {
            jsonData[key] = 1;
          }
        }
      } else {
        for (const key of Object.keys(jsonData)) {
          if(key === 'LL_pitch'){
            continue;
          }
          if(typeof(jsonData[key]) === 'string'){
            if(jsonData[key].trim().length > 0){
              flg = true;
              break;
            }
          } else if (jsonData[key] !== empty) {
            flg = true;
            break;
          }
        }
      }
      if (flg === false) {
        deleteKey.push(load_id);
      } else if (empty === 0) {
        const ln = "load_node" in jsonData ? jsonData["load_node"] : [];
        const lm = "load_member" in jsonData ? jsonData["load_member"] : [];
        if (ln.length + lm.length === 0) {
          deleteKey.push(load_id);
        }
      }
    }
    for (const load_id of deleteKey) {
      delete result[load_id];
    }

    return result;
  }

  // 荷重基本データ
  public getLoadNameJson(
    empty: number = null,
    targetCase: string = "",
    isPrint = false
  ): any {
    const load_name = {};

    for (let i = 0; i < this.load_name.length; i++) {
      const tmp = this.load_name[i];
      const key: string = tmp["id"];

      // ケースの指定がある場合、カレントケース以外は無視する
      if (targetCase.length > 0 && key !== targetCase) {
        continue;
      }

      const id = this.helper.toNumber(key);
      if (id == null) {
        continue;
      }

      const rate = this.helper.toNumber(tmp["rate"]);
      const symbol: string = tmp["symbol"];
      const name: string = tmp["name"];

      let fix_node = this.helper.toNumber(tmp["fix_node"]);
      let fix_member = this.helper.toNumber(tmp["fix_member"]);
      let element = this.helper.toNumber(tmp["element"]);
      let joint = this.helper.toNumber(tmp["joint"]);
      let LL_pitch: number = ("LL_pitch" in tmp) ? this.helper.toNumber(tmp["LL_pitch"]) : 0.1;

      if ( rate == null && symbol === "" &&  name === "" &&
          fix_node == null && fix_member == null && 
          element == null && joint == null ) {
        continue;
      }

      const load_id = (i + 1).toString();

      const temp = {
        rate: rate == null ? empty : rate,
        fix_node: fix_node == null ? empty : fix_node,
        fix_member: fix_member == null ? empty : fix_member,
        element: element == null ? empty : element,
        joint: joint == null ? empty : joint,
        symbol: symbol == null ? empty : symbol,
        LL_pitch: LL_pitch == null ? 0.1 : LL_pitch
      };

      if (empty !== 0 || isPrint === true) {
        temp["rate"] = rate == null ? empty : rate;
        temp["symbol"] = symbol;
        temp["name"] = name;
      }

      load_name[load_id] = temp;
    }
    return load_name;
  }

  public getLoadName(currentPage: number, target: string = "name"): any {
    if (currentPage < 1) {
      return "";
    }
    if (currentPage > this.load_name.length) {
      return "";
    }

    const i = currentPage - 1;
    const tmp = this.load_name[i];

    if(target===null){
      return tmp; // ターゲットの指定が無い時は、全部入り
    }

    let result = "";
    if (target in tmp) {
      result = tmp[target];
    }
    if(result === undefined){
      result = "";
    }
    if(result === null){
      result = "";
    }

    return result;
  }

  // 節点荷重データ
  public getNodeLoadJson(empty: number = null, targetCase: string = ""): any {
    const load_node = {};

    for (const load_id of Object.keys(this.load)) {
      // ケースの指定がある場合、カレントケース以外は無視する
      if (targetCase.length > 0 && load_id !== targetCase) {
        continue;
      }

      const tmp_node = new Array();

      for (const row of this.load[load_id]) {
        let n = this.helper.toNumber(row["n"]);
        let tx = this.helper.toNumber(row["tx"]);
        let ty = this.helper.toNumber(row["ty"]);
        let tz = this.helper.toNumber(row["tz"]);
        let rx = this.helper.toNumber(row["rx"]);
        let ry = this.helper.toNumber(row["ry"]);
        let rz = this.helper.toNumber(row["rz"]);

        if (
          n != null &&
          (tx != null ||
            ty != null ||
            tz != null ||
            rx != null ||
            ry != null ||
            rz != null)
        ) {
          let tmp = {};
          if (n > 0) {
            tmp = {
              n: row.n,
              tx: tx == null ? empty : tx,
              ty: ty == null ? empty : ty,
              tz: tz == null ? empty : tz,
              rx: rx == null ? empty : rx,
              ry: ry == null ? empty : ry,
              rz: rz == null ? empty : rz,
            };
          } else {
            const coef = empty === null ? 1 : 1000;
            const No = empty === null ? row["n"] : Math.abs(n).toString();
            tmp = {
              n: No,
              dx: tx == null ? empty : tx / coef,
              dy: ty == null ? empty : ty / coef,
              dz: tz == null ? empty : tz / coef,
              ax: rx == null ? empty : rx / coef,
              ay: ry == null ? empty : ry / coef,
              az: rz == null ? empty : rz / coef,
            };
          }

          tmp["row"] = row.row;

          tmp_node.push(tmp);
        }
      }

      if (tmp_node.length > 0) {
        load_node[load_id] = tmp_node;
      }
    }
    return load_node;
  }

  // 要素荷重データ
  public getMemberLoadJson(empty: number = null, targetCase: string = ""): any {
    const load_member = {};

    for (const load_id of Object.keys(this.load)) {
      // ケースの指定がある場合、カレントケース以外は無視する
      if (targetCase.length > 0 && load_id !== targetCase) {
        continue;
      }

      const load1: any[] = JSON.parse(JSON.stringify(this.load[load_id]));
      if (load1.length === 0) {
        continue;
      }

      let tmp_member = new Array();
      if (empty === null) {
        // ファイルに保存する用のデータ作成

        for (let j = 0; j < load1.length; j++) {
          const row = load1[j];
          const m1 = this.helper.toNumber(row["m1"]);
          const m2 = this.helper.toNumber(row["m2"]);
          const direction: string = row["direction"];
          const mark = this.helper.toNumber(row["mark"]);
          const L1 = this.helper.toNumber(row["L1"]);
          const L2 = this.helper.toNumber(row["L2"]);
          const P1 = this.helper.toNumber(row["P1"]);
          const P2 = this.helper.toNumber(row["P2"]);

          if (
            m1 != null ||
            m2 != null ||
            mark != null ||
            direction != "" ||
            L1 != null ||
            L2 != null ||
            P1 != null ||
            P2 != null
          ) {
            const tmp = {
              m1: row.m1,
              m2: row.m2,
              direction: row.direction,
              mark: row.mark,
              L1: row.L1,
              L2: row.L2,
              P1: P1,
              P2: P2,
            };

            tmp["row"] = row.row;

            tmp_member.push(tmp);
          }
        }
        if (tmp_member.length > 0) {
          load_member[load_id] = tmp_member;
        }
      } else {
        // 計算用のデータ作成

        // load1のlistの順番をrow順に入れ替える
        load1.sort((a, b) => {
          if (a.row < b.row) {
            return -1;
          } else if (a.row > b.row) {
            return 1;
          } else {
            return 0;
          }
        });

        let numL1: number = 0;  // 連行荷重のスタート位置

        let _L11: number[] = [0]; // L1に加算する距離で、 連行荷重(symbol == "LL")の時に特別な処理をする

        const load_num = parseInt(load_id, 10);

        const load_name = this.getLoadName(load_num, null);
        const symbol: string = load_name["symbol"];
        if (symbol == "LL") {
          // 列車連行荷重の場合
          // _L11 = [0, 0.1, 0.2, 0.3, 0.4, 0.5]; // L1に加算したケースを複数作る
          const LL_pitch: number = load_name["LL_pitch"];
          load1[0].L1 = '0';
          // 連行荷重のスタート位置を決定する
          const LL_position = this.get_LL_position(load1);
          numL1 = LL_position['L1']; 
          load1[0].L1 = numL1.toFixed(3);
          let LL_length = LL_position['LL_length'];
          LL_length += Math.abs(numL1);

          // 連行荷重の荷重の数を決定する
          const count = Math.round(LL_length / LL_pitch *10)/10; 
          for (let i = 1; i <= count; i++) {
            const LL1: number = Math.round(i * LL_pitch * 1000) / 1000;
            _L11.push(LL1);
          }

        }
        const digit: number = Math.pow(10, (_L11.length-1).toString().length); // 桁数

        // 連行荷重がある場合はループになる
        for (let i = 0; i < _L11.length; i++) {

          if( i > 0 ){
            load1[0].L1 = (numL1 + _L11[i]).toFixed(3); // 連行荷重の場合1行目の L1を
          }

          let new_load_id: string = load_id;

          if( i > 0 ){
            // 連行荷重の場合の ケースid を決める（連行荷重がある場合 x.1, x.2 というケースid を生成する）
            new_load_id = (Math.round((load_num + i / digit)*digit)/digit).toString();
          }

          // 要素荷重を サーバーで扱える形式に変換する
          const load2: any[] = this.convertMemberLoads(load1);

          if (load2.length > 0) {

            load_member[new_load_id] = load2;

          }
        }
        if (load_member[targetCase] == null) {
          load_member[targetCase] = new Array();
        }
      }
    }

    // 不要なケースを削除する
    let keys: string[] = Object.keys(load_member);
    for (const key of keys) {
      const value: any[] = load_member[key];
      if(value.length == 0)
        delete load_member[key];
    }
    /// 基本となるケースが削除されている場合
    keys = Object.keys(load_member);
    if(keys.length > 0){
      const found = keys.find(key => !key.includes('.'));
      if( found == null ){
        const key = keys[0];
        const value: any[] = load_member[key];
        delete load_member[key];
        const key0 = key.substring(0, key.lastIndexOf("."));
        load_member[key0] = value;
      }
    }

    return load_member;
  }

  private get_LL_position(load1: any[]): object {

    // 荷重の長さ(L1)を調べる
    let L1: number = 0

    for(let i = 0; i < load1.length; i++){
      const targetLoad = load1[i];
      const _L1 : number = this.helper.toNumber(targetLoad.L1);
      if(_L1 <= 0){
        L1 -= _L1;
      }
      if(targetLoad.L2 <= 0){
        L1 -= targetLoad.L2;
      }
    }

    // 載荷する部材の長さ(L2)を調べる
    let L2 = 0

    const m1: number = Math.abs(load1[0].m1);
    let m2: number = Math.abs(load1[0].m2);
    if(m2===0) 
      m2 = m1;
    for (let j = m1; j <= m2; j++) {
      L2 += Math.round(this.member.getMemberLength(j.toString()) * 1000);
    }
    L2 = L2 / 1000;

    
    return {
      L1: -L1,      // スタート位置は -L1
      LL_length: L2 // 最大長さは L2
    };

  }

  // 要素荷重を サーバーで扱える形式に変換する
  private convertMemberLoads(load1: any[]): any {

    // 有効な行を選別する
    let load2 = this.getEnableLoad(load1);
    if (load2.length === 0) {
      return new Array();
    }

    // 要素番号 m1, m2 に入力が無い場合 -------------------------------------
    for(const row of load2){
      if (row.m1 === 0) {
        row.m1 = row.m2;
      }
      if (row.m2 === 0) {
        row.m2 = row.m1;
      }
    }

    // m1>m2 の場合 入れ替え
    for(const row of load2){
      if (Math.abs(row.m1) > Math.abs(row.m2)) {
        if (row.m2 < 0){
          // m2にマイナスがあって m1>m2 の場合 入れ替え
          [row.m1, row.m2] = [-row.m2, -row.m1];
        } else{
          [row.m1, row.m2] = [row.m2, row.m1]; // 入れ替え
        }
      }
    }
    
    // 要素番号 m1 != m2 の場合の入力を分ける -----------------------
    let load3 = new Array();
    for(const row of load2){
      if (row.m1 < row.m2) {
        const res = this.getMemberRepeatLoad(row);
        load3 = load3.concat(res);
      } else {
        load3.push(row);
      }
    }

    // 入力行番号 row 順に並べ替える -----------------------
    load2 = load3.sort((a, b) => {
      return (a.row < b.row) ? -1 : 1;  //オブジェクトの昇順ソート
    });

    // 荷重スタート地点を決定する -----------------------
    load3 = this.checkIntoMemberL1(load2);

    // 要素番号 m2 にマイナスが付いた場合の入力を分ける ------------------------
    load2 = this.checkMember2(load3);

    load3 = load2;

    // 要素番号 m1 != m2 の場合の入力を分ける -----------------------
    load2 = new Array();
    for(const row of load3){
      if (row.m1 < row.m2) {
        const res = this.getMemberRepeatLoad(row);
        load2 = load2.concat(res);
      } else {
        load2.push(row);
      }
    }

    // memberの外側に出ている荷重を破棄する
    load3 = new Array();
    for(const row of load2){
      const checked = this.checkIntoMember(row);
      if(checked.P1 !==0 || checked.P2 !==0){
        load3.push(checked);
      }
    }

    // サーバーでは使わない記号を置き換える
    for (const row of load3) {
      row["m"] = row["m1"];
      delete row["sL1"];
      delete row["m1"];
      delete row["m2"];
      // delete row["row"]; // row は three.js の ID として使うので消さない
    }

    return load3;
  }

  // 要素番号 m2 にマイナスが付いた場合の入力を分ける
  private getMemberGroupLoad( targetLoad: any, curNo: number,  _curPos: number ): any {
    // ※この関数内では距離を 100倍した整数で処理する

    let curPos: number = Math.round(_curPos * 1000);

    const result = {};

    // もともとの入力データを保存  . . . . . . . . . . . . . . . . . .
    const org_m1: number = Math.abs(targetLoad.m1);
    const org_m2: number = Math.abs(targetLoad.m2);

     // L1の位置を確定する . . . . . . . . . . . . . . . . . . . . . .
    [curNo, curPos] = this.getL1Position(targetLoad, curNo, curPos);

    // L2の位置を確定する . . . . . . . . . . . . . . . . . . . . . .
    [curNo, curPos] = this.getL2Position(targetLoad, curNo, curPos, org_m1);

    // 部材を連続して入力データを作成する  . . . . . . . . . . . . . . . . . . . . . . . . . . .
    const loads = new Array();
    let m1: number = Math.abs(targetLoad.m1);
    let m2: number = Math.abs(targetLoad.m2);

    // 連続部材の全長さLLを計算する
    let ll: number = 0;
    for (let j = m1; j <= m2; j++) {
      ll = ll + Math.round(this.member.getMemberLength(j.toString()) * 1000);
    }
    let L1: number = Math.round(targetLoad.L1 * 1000);
    let L2: number = Math.round(targetLoad.L2 * 1000);

    switch (targetLoad.mark) {
      case 1:
      case 11:
        const LL1: number = targetLoad.L1;
        const LL2: number = targetLoad.L2;
        if (m1 === m2 && LL1 * LL2 >= 0) {
          const newLoads = {};
          newLoads["direction"] = targetLoad.direction;
          newLoads["mark"] = targetLoad.mark;
          newLoads["m1"] = m1;
          newLoads["m2"] = m2;
          newLoads["L1"] = targetLoad.L1;
          newLoads["L2"] = targetLoad.L2;
          newLoads["P1"] = targetLoad.P1;
          newLoads["P2"] = targetLoad.P2;
          loads.push(newLoads);
        } else {
          const newLoads1 = {};
          newLoads1["direction"] = targetLoad.direction;
          newLoads1["mark"] = targetLoad.mark;
          newLoads1["m1"] = m1;
          newLoads1["m2"] = m1;
          newLoads1["L1"] = targetLoad.L1;
          newLoads1["L2"] = 0;
          newLoads1["P1"] = targetLoad.P1;
          newLoads1["P2"] = 0;
          loads.push(newLoads1);

          const newLoads2 = {};
          newLoads2["direction"] = targetLoad.direction;
          newLoads2["mark"] = targetLoad.mark;
          newLoads2["m1"] = m2;
          newLoads2["m2"] = m2;
          newLoads2["L1"] = targetLoad.L2;
          newLoads2["L2"] = 0;
          newLoads2["P1"] = targetLoad.P2;
          newLoads2["P2"] = 0;
          loads.push(newLoads2);
        }
        break;
      case 9:
        for (let j = m1; j <= m2; j++) {
          const newLoads = {};
          newLoads["direction"] = targetLoad.direction;
          newLoads["mark"] = targetLoad.mark;
          newLoads["m1"] = j;
          newLoads["m2"] = j;
          newLoads["L1"] = 0;
          newLoads["L2"] = 0;
          newLoads["P1"] = targetLoad.P1;
          newLoads["P2"] = targetLoad.P1;
          loads.push(newLoads);
        }
        break;

      default:
        // 中間値を計算
        let lo: number = ll - L1 - L2; // 荷重載荷長
        let Po: number = (targetLoad.P2 - targetLoad.P1) / lo;
        let P2: number = targetLoad.P1;

        for (let j = m1; j <= m2; j++) {
          const newLoads = {};
          newLoads["direction"] = targetLoad.direction;
          newLoads["mark"] = targetLoad.mark;

          newLoads["m1"] = j;
          newLoads["m2"] = j;
          let L: number = Math.round(this.member.getMemberLength(newLoads["m1"]) * 1000); // 要素長
          switch (j) {
            case m1:
              L = L - L1;
              break;
            case m2:
              L = L - L2;
              break;
          }
          newLoads["L1"] = 0;
          newLoads["L2"] = 0;
          newLoads["P1"] = P2;
          P2 = P2 + Po * L;
          newLoads["P2"] = P2;

          if (j === m1) {
            newLoads["L1"] = targetLoad.L1;
            newLoads["P1"] = targetLoad.P1;
          }
          if (j === m2) {
            newLoads["L2"] = targetLoad.L2;
            newLoads["P2"] = targetLoad.P2;
          }
          loads.push(newLoads);
        }
    }

    // 行rowの入力情報があれば追加する  . . . . . . . . . . . . . . . . . . . .
    if ("row" in targetLoad) {
      for (const load of loads) {
        load["row"] = targetLoad["row"];
      }
    }

    // 戻り値を作成する  . . . . . . . . . . . . . . . . . . . . . . . . . . .
    _curPos = curPos / 1000;
    result["loads"] = loads;
    result["curNo"] = curNo;
    result["curPos"] = _curPos;

    return result;
  }


  // L1の位置を確定する . . . . . . . . . . . . . . . . . . . . . .
  private getL1Position(targetLoad: any,
    curNo: number, curPos: number): number[] {

    const sL1: string = targetLoad.sL1;

    let m1: number = Math.abs(targetLoad.m1);
    const m2: number = Math.abs(targetLoad.m2);
    let L1: number = Math.round(Math.abs(targetLoad.L1) * 1000);
    let L2: number = Math.round(Math.abs(targetLoad.L2) * 1000);

    if (sL1.includes("-")) {
      // 距離L1が加算モードで入力されている場合
      // ->最初のL1が負のときも加算モードが適用されるため、負の方向に移動できなくなる（エラー）
      if (m1 <= curNo && curNo <= m2) {
        m1 = curNo;
        L1 = curPos + L1;
        targetLoad.m1 = m1;
        targetLoad.L1 = L1 / 1000;
      }
      // 最初の加算モードならば、そのまま負の状態にする
      if (curNo < m1 && curNo < m2) {
        targetLoad.m1 = m1;
        L1 = Math.round(targetLoad.L1 * 1000);
      }
    }

    // 距離L1 から 部材m1 を推定する
    for (let j = m1; j <= m2; j++) {
      let L: number = Math.round(this.member.getMemberLength(j.toString()) * 1000);
      if (L1 > L) {
        if (j + 1 > m2) {
          // 全長より荷重の方が長い場合 なかったことにする
          targetLoad.m1 = m2;
          targetLoad.L1 = 0;
          targetLoad.P1 = 0;
          break;
        }
        L1 = L1 - L;
        if (targetLoad.mark === 1 || targetLoad.mark === 11) {
          L2 = L2 - L;
        }
        if (j + 1 <= m2) {
          targetLoad.m1 = j + 1;
          targetLoad.L1 = L1 / 1000;
        }
      } else {
        targetLoad.m1 = j;
        break;
      }
    }
    curNo = targetLoad.m1;
    curPos = Math.round(targetLoad.L1 * 1000);

    targetLoad.L1 = L1 / 1000;
    // L2が加算モードになっていた場合、L2の計算結果を更新しない
    if (targetLoad.L2 >= 0) {
      targetLoad.L2 = L2 / 1000;
    }

    return [curNo, curPos];
  }

  // L2の位置を確定する . . . . . . . . . . . . . . . . . . . . . .
  private getL2Position(targetLoad: any,
    curNo: number, curPos: number, org_m1: number): number[] {

  // L2の位置を確定する . . . . . . . . . . . . . . . . . . . . . .
    let m1: number = Math.abs(targetLoad.m1);
    let L2: number = Math.round(Math.abs(targetLoad.L2) * 1000);
    let m2: number = Math.abs(targetLoad.m2);
    let L1: number = targetLoad.L1 * 1000;

    switch (targetLoad.mark) {
      case 1:
      case 11:
        if (targetLoad.L2 < 0) {
          L2 = L1 + L2;
        }
        for (let j = m1; j <= m2; j++) {
          const L: number = Math.round(this.member.getMemberLength(j.toString()) * 1000);
          if (L2 > L) {
            if (j + 1 > m2) {
              // 全長より荷重の方が長い場合 なかったことにする
              targetLoad.m2 = m2;
              targetLoad.L2 = 0;
              targetLoad.P2 = 0;
              curNo = -1;
              break;
            }
            L2 = L2 - L;
            targetLoad.m2 = j + 1;
            targetLoad.L2 = L2 / 1000;
            curNo = Math.abs(targetLoad.m2);
          } else {
            targetLoad.m2 = j;
            targetLoad.L2 = L2 / 1000;
            curNo = Math.abs(targetLoad.m2);
            break;
          }
        }
        // curPos = Math.round(Math.abs(targetLoad.L2) * 1000);
        curPos = Math.round(targetLoad.L2 * 1000);
        break;

      default:
        if (targetLoad.L2 < 0) {
          // 連続部材の全長さLLを計算する
          let ll = 0;
          for (let j = m1; j <= m2; j++) {
            ll += Math.round(this.member.getMemberLength(j.toString()) * 1000);
          }
          L2 = ll - (curPos + L2);
          if (L2 < 0) {
            L2 = 0;
          }
          targetLoad.m2 = m2;
          targetLoad.L2 = L2 / 1000;
        }
        let L = 0;
        for (let j = m2; j >= org_m1; j--) {
          L = Math.round(this.member.getMemberLength(j.toString()) * 1000);
          if (L2 > L) {
            L2 = L2 - L;
            targetLoad.m2 = j - 1;
            targetLoad.L2 = L2 / 1000;
          } else {
            break;
          }
        }
        if (curNo <= targetLoad.m2) {
          curNo = Math.abs(targetLoad.m2);
          curPos = L - Math.round(Math.abs(targetLoad.L2) * 1000);
        } else {
          L = 0;
          curPos = L - Math.round(Math.abs(targetLoad.L2) * 1000);
        }
        break;
    }

    return [curNo, curPos];
  }

  // 要素番号 m1 != m2 の場合の入力を分ける
  private getMemberRepeatLoad(targetLoad: any): any {
    const result = new Array();

    const m1: number = Math.abs(targetLoad.m1);
    const m2: number = Math.abs(targetLoad.m2);

    for (let i = m1; i <= m2; i++) {
      const newLoads = {};
      newLoads["direction"] = targetLoad.direction;
      newLoads["m1"] = i;
      newLoads["m2"] = i;
      newLoads["L1"] = targetLoad.L1;
      newLoads["L2"] = targetLoad.L2;
      newLoads["mark"] = targetLoad.mark;
      newLoads["P1"] = targetLoad.P1;
      newLoads["P2"] = targetLoad.P2;
      newLoads["row"] = targetLoad.row;
      newLoads["sL1"] = targetLoad.sL1;
      result.push(newLoads);
    }
    return result;
  }

  // 有効な行を選別する
  private getEnableLoad(load1: any[]): any[] {

    let load2 = new Array();

    // 有効な行を選別する
    for (const row of load1) {

      // const r = row["row"];
      let m1 = this.helper.toNumber(row["m1"]);
      let m2 = this.helper.toNumber(row["m2"]);
      let direction: string = row["direction"];

      if (direction === null || direction === undefined) {
        direction = "";
      }
      direction = direction.trim().toLowerCase();

      const mark = this.helper.toNumber(row["mark"]);
      let L1 = this.helper.toNumber(row["L1"]);
      let L2 = this.helper.toNumber(row["L2"]);
      let P1 = this.helper.toNumber(row["P1"]);
      let P2 = this.helper.toNumber(row["P2"]);

      if (mark === 9) {
        direction = "x";
      }

      if (
        (m1 != null || m2 != null) &&
        direction !== "" &&
        mark != null &&
        (L1 != null || L2 != null || P1 != null || P2 != null)
      ) {
        m1 = m1 == null ? 0 : m1;
        m2 = m2 == null ? 0 : m2;

        direction = direction.trim();
        let sL1: string = L1 == null ? "0" : row["L1"].toString();
        L1 = L1 == null ? 0 : L1;
        L2 = L2 == null ? 0 : L2;
        P1 = P1 == null ? 0 : P1;
        P2 = P2 == null ? 0 : P2;

        load2.push({
          row: row["row"],
          m1, m2, direction, mark,
          sL1, L1, L2, P1, P2
        });
      }
    }

    return load2;
  }

  // 要素番号 m2 にマイナスが付いた場合の入力を分ける
  private checkMember2(load2){
    let curNo = -1;
    let curPos1 = 0;
    let load3 = new Array();
  
    let old_row: number = -1;
    for(const row of load2){

      if(old_row + 1 < row['row']){
        // 空白行があった場合 グループ化を解除
        curNo = -1;
        curPos1 = 0;
      }
      old_row = row['row']; //現在の行数を記録しておく

      const res = this.getMemberGroupLoad(row, curNo, curPos1); // ※ここで L1 のマイナスは消える
      if(res !== null) {
        load3 = load3.concat(res["loads"]);
        curNo = res["curNo"];
        curPos1 = res["curPos"];
      }

    }

    return load3;
  }


  // 荷重スタート地点を決定する
  private checkIntoMemberL1(load) {

    let load2 = new Array();

    let curPos1 = 0;
    let curPos2 = 0;
    let old_row: number = -1;

    for(const row of load){

      // loadの数値と型を保存しておく
      let L1 = Math.round(row.L1 *1000);
      let L2 = Math.round(row.L2 *1000);

      // P1 の位置を調べる
      if(old_row + 1 < row['row']) {
        // リセット
        curPos1 = L1;
      } else if(L1 < 0) {
        // 加算モード
        curPos1 = curPos2 + Math.abs(L1);
      } else {
        curPos1 = L1;
      }
      old_row = row['row']; //現在の行数を記録しておく

      // P2 の位置を調べる
      if(L2 < 0) {
        // 加算モード
        curPos2 = curPos1 + Math.abs(L2);
      } else {
        curPos2 = L2;
      }

      // P1 が ゼロより外側にある場合か判定
      const mark = row.mark;
      if (mark === 1 || mark === 11) {
        if(curPos1 <= 0 && curPos2 <= 0){
          continue; // その荷重は、無視する

        } else {
          if(curPos1 < 0){
            row.L1 = 0;
            row.sL1 = row.L1.toString();
            row.P1 = 0;
          } else {
            row.L1 = curPos1 /1000;
            row.sL1 = row.L1.toString();
          }
          row.L2 = curPos2 /1000;
        }

      } else if (mark === 2 ) {
        if(curPos1 < 0){
          const P1: number = row.P1;
          const P2: number = row.P2;
          const L1: number = Math.abs(curPos1);
          const L2: number = Math.abs(curPos2);
          const P3 = P1 + L1 * (P2 - P1) / (L1 + L2);
          row.L1 = 0;
          row.sL1 = row.L1.toString();
          row.L2 = -curPos2 /1000;
          row.P1 = P3;
        }
      }

      load2.push(row);

    }

    return load2;
  }

  // memberの範囲外に出ていないか確認する
  private checkIntoMember(load) {
    // loadの数値と型を保存しておく
    const mark = load.mark;
    const m1 = load.m1.toString();
    let L1 = Math.round(load.L1 *1000);
    let L2 = Math.round(load.L2 *1000);
    let P1 = Math.round(load.P1 *100)/100;
    let P2 = Math.round(load.P2 *100)/100;

    // memberの範囲外に出ていないか確認
    const len = Math.round(this.member.getMemberLength(m1) * 1000);

    if (mark === 1 || mark === 11) {
      if (L1 < 0 || L1 > len) {
        L1 = 0;
        P1 = 0;
      }
      if (L2 < 0 || L2 > len) {
        L2 = 0;
        P2 = 0;
      };
    } else if (mark === 2 || mark === 9) {
      if ( L1 + L2 > len) {
        L1 = 0;
        L2 = 0;
        P1 = 0;
        P2 = 0;
      } else {
        if (L2 < 0 ) {
          L2 = 0;
        }
        if (L1 < 0) {
          L1 = 0;
        }    
      }
    }
    // 数値をそのままに、元の型に戻す
    load.L1 = L1 / 1000;
    load.L2 = L2 / 1000;
    load.P1 = P1;
    load.P2 = P2;
    return load;
  }

  // 有効な 荷重ケース数を調べる
  public getLoadCaseCount(): number {
    const list = new Array();
    list.push(this.getLoadJson());
    list.push(this.getNodeLoadJson());
    list.push(this.getMemberLoadJson());
    let maxCase = 0;
    for (let i = 0; i < list.length; i++) {
      const dict = list[i];
      for (const load_id of Object.keys(dict)) {
        const load_no: number = this.helper.toNumber(load_id);
        if (maxCase < load_no) {
          maxCase = load_no;
        }
      }
    }
    return maxCase;
  }

}
