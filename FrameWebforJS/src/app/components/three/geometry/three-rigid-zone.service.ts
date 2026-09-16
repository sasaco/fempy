import { Injectable } from "@angular/core";
import { SceneService } from "../scene.service";
import { ThreeNodesService } from "./three-nodes.service";
import { InputNodesService } from "../../input/input-nodes/input-nodes.service";
import { InputMembersService } from "../../input/input-members/input-members.service";
import { ThreeMembersService } from "./three-members.service";
import { InputRigidZoneService } from "../../input/input-rigid-zone/input-rigid-zone.service";
import * as THREE from "three";
import { Subject } from "rxjs";

@Injectable({
    providedIn: "root"
})
export class ThreeRigidZoneService {
    private geometry: THREE.SphereBufferGeometry;
    private rigidZoneList: THREE.Object3D;
    private rigidZoneList1: THREE.Object3D;
    private scale: number;
    private params: any;
    private gui: any;
    private nPointList: any[]
    private objVisible: boolean;
    private txtVisible: boolean;
    constructor(private scene: SceneService,
        private nodeThree: ThreeNodesService,
        private node: InputNodesService,
        private rigidZone: InputRigidZoneService,
        private member: InputMembersService,
        private memberThree: ThreeMembersService) {

        this.geometry = new THREE.SphereBufferGeometry(1);
        this.rigidZoneList = new THREE.Object3D();
        this.rigidZoneList1 = new THREE.Object3D();
        this.nPointList = new Array()
        this.ClearData();
        this.scene.add(this.rigidZoneList);
        this.scene.add(this.rigidZoneList1);
        this.objVisible = true;
        this.txtVisible = false;

        this.scale = 300;
        this.params = {
            nodeNo: this.txtVisible,
            nodeScale: this.scale
        };
        this.gui = null;
    }

    public OnInit(): void {
        // 部材番号の表示を制御する gui を登録する
        this.scene.gui.add(this.params, "pointNo").onChange((value) => {
            for (const mesh of this.rigidZoneList.children) {
                mesh.getObjectByName("font").visible = value;
            }
            this.txtVisible = value;
            this.scene.render();
        });

    }
    public baseScale(): number {
        // const scale = this.nodeThree.baseScale;
        return 0.018895766721676047;// scale * 0.3;
    }
    public selectChange(index, index_sub): void {
        const jsonData = this.rigidZone.getRigidJson();
        if (Object.keys(jsonData).length <= 0) {
            this.memberThree.selectChange_clear_points();
            this.scene.render();
            return;
        }
        const target = jsonData.find((e) => e.m === index.toString());
        const m_no = (target !== undefined) ? target.m : '0';
        this.memberThree.selectChange_points(m_no);

        for (let item of this.rigidZoneList.children) {
            item['material']['color'].setHex(0X00A5FF);
            if (item.name === 'rigid' + `${index_sub.toString() + index.toString()}`) {
                this.memberThree.selectChange_clear_points();
                item['material']['color'].setHex(0XFF0000);
            }
        }        
        this.scene.render();
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
        const jsonData = this.rigidZone.getRigidJson();
        if (Object.keys(jsonData).length <= 0) {
            this.scene.render();
            return;
        }

        // let jsonData = this.data.getNoticePointsJson()
        for (let target of jsonData) {
            let iLength = target['Ilength'];
            let jLength = target['Jlength'];
            let row: string;
            let point: any = []
            let m: string = target['m'];
            const l: number = this.member.getMemberLength(m);

            if (jLength !== null) {
                point.push(l - jLength)
                row = `Jlength${m}`
            }
            else {
                point.push(iLength)
                row = `Ilength${m}`
            }
            if (iLength !== null && jLength !== null) {
                point.push(iLength)
            }
            for (let i = 0; i < point.length; i++) {
                if (i > 0) {
                    row = `Ilength${m}`
                }
                const item = this.rigidZoneList.children.find((target) => {
                    return (target.name === 'rigid' + row);
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
                    mesh.name = 'rigid' + row;
                    mesh.position.x = axis.x;
                    mesh.position.y = axis.y;
                    mesh.position.z = axis.z;
                    mesh.material.color.setHex(0X00A5FF);

                    let sc = this.scale / 100; // this.scale は 100 が基準値なので、100 のとき 1 となるように変換する
                    sc = Math.max(sc, 0.001); // ゼロは許容しない

                    let scale: number = this.baseScale() * sc;

                    mesh.scale.set(scale, scale, scale)
                    this.scene.add(mesh);
                    this.rigidZoneList.children.push(mesh);

                    const memb = this.member.getMemberNo(m)
                    let i = null;
                    let v;
                    if(row.includes("I")){
                        i = nodeData[memb['ni']]
                        v = new THREE.Vector3(i.x - axis.x, i.y - axis.y, i.z - axis.z);
                    }                       
                    else{
                         i = nodeData[memb['nj']]
                         v = new THREE.Vector3(axis.x - i.x, axis.y - i.y, axis.z - i.z);
                    }
                    const len: number = v.length();
                    if (len < 0.001) {
                        continue;
                    }

                    const geometry1 = new THREE.CylinderBufferGeometry(0.04, 0.04, len-0.08, 12);
                    
                    const mesh1 = new THREE.Mesh(
                        geometry1,
                        new THREE.MeshBasicMaterial({ color: 0x000000 })
                    );
                    const x: number = (i.x + +axis.x) / 2;
                    const y: number = (i.y + +axis.y) / 2;
                    const z: number = (i.z + +axis.z) / 2; 
                    mesh1.rotation.z = Math.acos(v.y / len);
                    mesh1.rotation.y = 0.5 * Math.PI + Math.atan2(v.x, v.z);                 
                    mesh1.position.set(x, y, z);
                    
                    this.rigidZoneList1.children.push(mesh1);

                }
            }
        }

        this.scene.render();

        // 新しい入力を適用する
        // for (const key of jsonKeys) {

        // }
    }
    public visibleChange(flag: boolean): void {

        this.selectChange(-1, -1)

        if (this.objVisible === flag) {
            return;
        }
        for (const mesh of this.rigidZoneList.children) {
            mesh.visible = flag;
        }
        for (const mesh of this.rigidZoneList1.children) {
            mesh.visible = flag;
        }
        this.objVisible = flag;
    }
    public ClearData(): void {

        // データをクリアする
        for (const mesh of this.rigidZoneList.children) {
            // 文字を削除する
            while (mesh.children.length > 0) {
                const object = mesh.children[0];
                object.parent.remove(object);
            }
            // オブジェクトを削除する
            this.scene.remove(mesh);
        }
        for (const mesh of this.rigidZoneList1.children) {
            // 文字を削除する
            while (mesh.children.length > 0) {
                const object = mesh.children[0];
                object.parent.remove(object);
            }
            // オブジェクトを削除する
            this.scene.remove(mesh);
        }
        this.rigidZoneList.children = new Array();
        this.rigidZoneList1.children = new Array();
    }
    public detectObject(raycaster: THREE.Raycaster, action: string): void {
        if (this.rigidZoneList.children.length === 0) {
            return;
        }
        const intersects = raycaster.intersectObjects(this.rigidZoneList.children, true);
        if (intersects.length <= 0) {
            return;
        }
        switch (action) {
            case "click":
                this.rigidZoneList.children.map((item) => {
                    if (intersects.length > 0 && item === intersects[0].object) {
                        // 色を赤くする
                        this.sendRigidZoneNodeSubject(item);
                        const material = item['material'];
                        material["color"].setHex(0xff0000);
                        material["opacity"] = 1.0;
                    }
                    else {
                        const material = item['material'];
                        material['color'].setHex(0X00A5FF);
                        material["opacity"] = 1.0;
                    }
                });
                break;
            default:
                return;
        }
        this.scene.render();
    }
    private rigidZoneSelectedInThreeSubject = new Subject<any>();
    rigidSelected$ = this.rigidZoneSelectedInThreeSubject.asObservable();

    sendRigidZoneNodeSubject(item: any) {
        this.rigidZoneSelectedInThreeSubject.next(item);
    }

}
