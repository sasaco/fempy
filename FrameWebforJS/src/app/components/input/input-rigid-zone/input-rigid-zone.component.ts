import { Component, OnInit, ViewChild } from "@angular/core";
import { InputMembersService } from "../input-members/input-members.service";
import { InputElementsService } from "../input-elements/input-elements.service";
import { DataHelperModule } from "../../../providers/data-helper.module";
import { ThreeService } from "../../three/three.service";
import { SheetComponent } from "../sheet/sheet.component";
import pq from "pqgrid";
import { AppComponent } from "src/app/app.component";
import { TranslateService } from "@ngx-translate/core";
import { DocLayoutService } from "src/app/providers/doc-layout.service";
import { InputRigidZoneService } from "./input-rigid-zone.service";
import { InputNodesService } from "../input-nodes/input-nodes.service";
import { ThreeRigidZoneService } from "../../three/geometry/three-rigid-zone.service";
import { forEach } from "jszip";

@Component({
  selector: 'app-input-rigid-zone',
  templateUrl: './input-rigid-zone.component.html',
  styleUrls: ["./input-rigid-zone.component.scss", "../../../app.component.scss"]
})
export class InputRigidZoneComponent implements OnInit {
  @ViewChild("grid") grid: SheetComponent;
  private dataset = [];
  private columnKeys = ['m', 'L','e','n','Ilength','Jlength', 'e1', ];
  private columnHeaders3D = [
    {
      title: this.translate.instant("input.input-rigid.member"),
      colModel: [
        {
          title: this.translate.instant("input.input-rigid.no"),
          align: "left",
          dataType: "string",
          dataIndx: this.columnKeys[0],
          sortable: false,
          width: 70,
          editable: false,
          style: { background: "#33363c !important" },
        },
      ],
    },
    {
      title: this.translate.instant("input.input-rigid.distance"),
      align: "center",
      colModel: [
        {
          title: "(m)",
          dataType: "float",
          format: "#.000",
          dataIndx: this.columnKeys[1],
          sortable: false,
          width: 100,
          editable: false,
          style: { background: "#33363c !important" },
        },
      ],
    },
    {
      title: this.translate.instant("input.input-rigid.material"),
      align: "center",
      colModel: [
        {
          title: this.translate.instant("input.input-rigid.no"),
          dataType: "integer",
          dataIndx: this.columnKeys[2],
          sortable: false,
          minwidth: 100,
          align: "left",
          width: 100,
          editable: false,
          style: { background: "#33363c !important" },
        },
      ],
    }, 
    {
      title: this.translate.instant("input.input-rigid.material_name"),
      align: "center",
      dataType: "string",
      dataIndx: this.columnKeys[3],
      sortable: false,
      width: 100,
      editable: false,
      style: { background: "#33363c !important" },
    },
    {
      title: this.translate.instant("input.input-rigid.rigid"),
      align: "center",
      colModel: [
        {
          title: this.translate.instant("input.input-rigid.node_i"),
          dataType: "float",
          dataIndx: this.columnKeys[4],
          sortable: false,
          minwidth: 100,
          format: "#.00",
          width: 100,
        },
        {
          title: this.translate.instant("input.input-rigid.node_j"),
          dataType: "float",
          dataIndx: this.columnKeys[5],
          sortable: false,
          minwidth: 100,
          format: "#.00",
          width: 100,
        },
        {
          title: this.translate.instant("input.input-rigid.materialNo"),
          dataType: "integer",
          dataIndx: this.columnKeys[6],
          sortable: false,
          minwidth: 100,
          width: 100,
        },
      ],
    },
  ];
  private columnHeaders2D = [
    {
      title: this.translate.instant("input.input-rigid.member"),
      colModel: [
        {
          title: this.translate.instant("input.input-rigid.no"),
          align: "left",
          dataType: "string",
          dataIndx: this.columnKeys[0],
          sortable: false,
          width: 100,
          editable: false,
          style: { background: "#33363c !important" },
        },
      ],
    },
    {
      title: this.translate.instant("input.input-rigid.distance"),
      align: "center",
      colModel: [
        {
          title: "(m)",
          dataType: "float",
          format: "#.000",
          dataIndx: this.columnKeys[1],
          sortable: false,
          width: 100,
          editable: false,
          style: { background: "#33363c !important" },
        },
      ],
    },
    {
      title: this.translate.instant("input.input-rigid.material"),
      align: "center",
      colModel: [
        {
          title: this.translate.instant("input.input-rigid.no"),
          dataType: "integer",
          dataIndx: this.columnKeys[2],
          sortable: false,
          align: "left",
          minwidth: 100,
          width: 100,
          editable: false,
          style: { background: "#33363c !important" }
        },
      ],
    },   
    {
      title: this.translate.instant("input.input-rigid.material_name"),
      align: "center",
      dataType: "string",
      dataIndx: this.columnKeys[3],
      sortable: false,
      width: 100,
      editable: false,
      style: { background: "#33363c !important" },
    },
    {
      title: this.translate.instant("input.input-rigid.rigid"),
      align: "center",
      colModel: [
        {
          title: this.translate.instant("input.input-rigid.node_i"),
          dataType: "float",
          dataIndx: this.columnKeys[4],
          sortable: false,
          format: "#.00",
          minwidth: 100,
          width: 100,
        },
        {
          title: this.translate.instant("input.input-rigid.node_j"),
          dataType: "float",
          dataIndx: this.columnKeys[5],
          sortable: false,
          minwidth: 100,
          format: "#.00",
          width: 100,
        },
        {
          title: this.translate.instant("input.input-rigid.materialNo"),
          dataType: "integer", 
          dataIndx: this.columnKeys[6],
          sortable: false,
          minwidth: 100,
          width: 100        
        },
      ],
    },
  ];
  private ROWS_COUNT = 15;
  private currentIndex: string;
  constructor(
    public dataRigid: InputRigidZoneService,
    private member: InputMembersService,
    private node: InputNodesService,
    private element: InputElementsService,
    private helper: DataHelperModule,
    private app: AppComponent,
    private three: ThreeService,
    private translate: TranslateService,
    private threeRigidZoneService: ThreeRigidZoneService,
    public docLayout: DocLayoutService) {
      this.currentIndex = null;     
  }
  
  ngOnInit() {
    this.ROWS_COUNT = this.rowsCount();
    // three.js にモードの変更を通知する
    //this.three.ChangeMode("members");
    this.three.ChangeMode("rigid_zone");
    this.three.changeData('rigid_zone');
  }
  ngAfterViewInit() {
    this.docLayout.handleMove.subscribe(data => {
    this.options.height = data - 60;
    })
    this.threeRigidZoneService.rigidSelected$.subscribe((item: any) => {
      var name = item.name.replace('rigid', '');
      var nameColumn = name.substring(0, 7);
      var indexRow = +name.replace(nameColumn, '');
      var indexCol = this.columnKeys.findIndex((x) => x === nameColumn);
      if (indexRow >= 29) {
        let d = Math.ceil(indexRow / 29);
        this.grid.grid.scrollY(
          d * this.grid.div.nativeElement.clientHeight,
          () => {
            this.grid.grid.setSelection({
              rowIndx: indexRow - 1,
              rowIndxPage: 1,
              colIndx: indexCol,
              focus: true,
            });
          }
        );
      } else {
        this.grid.grid.setSelection({
          rowIndx: indexRow - 1,
          rowIndxPage: 1,
          colIndx: indexCol,
          focus: true,
        });
      }
    });
  }
  private loadData(row: number): void {
    for (let i = this.dataset.length + 1; i <= row; i++) {
      const regid = this.dataRigid.getRigidZoneColums(i);
      const m: string = regid["m"];
      const e = (regid.e !== null) ? regid.e : undefined;
      if (m !== "") {
        const l: any = this.member.getMemberLength(m);
        regid["L"] = l != null ? l.toFixed(3) : l;
        const name = this.element.getElementName(e);
        regid["n"] = name != null ? name : "";
      }
      this.dataset.push(regid);
    }    
  }
  private getMember(memberNo: string) {

    const member = this.member.member.find((columns) => {
      return columns.id === memberNo;
    })

    if (member === undefined) {
      return { ni: null, nj: null };
    }

    return member;

  }
  public getMemberLength(memberNo: string): number {

    const memb = this.getMember(memberNo.toString());
    if (memb.ni === undefined || memb.nj === undefined) {
      return null;
    }
    const ni: string = memb.ni;
    const nj: string = memb.nj;
    if (ni === null || nj === null) {
      return null;
    }

    const iPos = this.node.getNodePos(ni)
    const jPos = this.node.getNodePos(nj)
    if (iPos == null || jPos == null) {
      return null;
    }

    const xi: number = iPos['x'];
    const yi: number = iPos['y'];
    const zi: number = iPos['z'];
    const xj: number = jPos['x'];
    const yj: number = jPos['y'];
    const zj: number = jPos['z'];

    const result: number = Math.sqrt((xi - xj) ** 2 + (yi - yj) ** 2 + (zi - zj) ** 2);
    return result;

  }
  private tableHeight(): string {
    const containerHeight = this.app.getPanelElementContentContainerHeight() - 10;
    return containerHeight.toString();
  }
  // 表高さに合わせた行数を計算する
  private rowsCount(): number {
    const containerHeight = this.app.getDialogHeight();
    return Math.round(containerHeight / 30);
  }
  options: pq.gridT.options = {
    showTop: false,
    reactive: true,
    sortable: false,
    locale: "jp",
    height: this.tableHeight(),
    numberCell: {
      show: false, // 行番号
    },
    colModel:
      this.helper.dimension === 3 ? this.columnHeaders3D : this.columnHeaders2D,
    dataModel: {
      data: this.dataset,
    },
    beforeTableView: (evt, ui) => {
      const finalV = ui.finalV;
      const dataV = this.dataset.length;    
      if (ui.initV == null) {
        return;
      }
      if (finalV >= dataV - 1) {
        this.loadData(dataV + this.ROWS_COUNT);
        this.grid.refreshDataAndView();
      }
    },
    selectEnd: (evt, ui) => {
      const range = ui.selection.iCells.ranges;
      const row = range[0].r1 + 1;
      const column = this.columnKeys[range[0].c1];
       //if (this.currentIndex !== row){
        //選択行の変更があるとき，ハイライトを実行する
        this.three.selectChange("rigid_zone", row, column);
      //}
      this.currentIndex = row;
    },     
    change: (evt, ui) =>{
      const changes = ui.updateList;
      console.log(this.dataRigid.rigid_zone);
      for (const target of changes) {
        const row: number = target.rowIndx;     
        Object.keys(target.newRow).forEach(element => {
          this.dataRigid.rigid_zone[row][element] = target.newRow[element]
        });              
      }
      this.three.changeData('rigid_zone')

      const row = changes[0].rowIndx + 1;
      let column: string; // 複数の時は左上に合わせる
      for (const key of this.columnKeys) {
        if (key in ui.updateList[0].newRow && key !== 'm') {
          column = key;
          break;
        }
      }     
      this.three.selectChange("rigid_zone", row, column);
    },
  };

  width = this.helper.dimension === 3 ? 580 : 450;
}


