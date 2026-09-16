import { Component, OnInit, OnDestroy, ViewChild } from "@angular/core";
import { InputLoadService, LoadColumns } from "./input-load.service";
import { DataHelperModule } from "../../../providers/data-helper.module";
import { ThreeService } from "../../three/three.service";
import { SheetComponent } from "../sheet/sheet.component";
import pq from "pqgrid";
import { AppComponent } from "src/app/app.component";
import { FormControl, FormGroup } from "@angular/forms";
import { ThreeLoadService } from "../../three/geometry/three-load/three-load.service";
import { SceneService } from "../../three/scene.service";
import { TranslateService } from "@ngx-translate/core";
import { NgbModal } from "@ng-bootstrap/ng-bootstrap";
import { WaitDialogComponent } from "../../wait-dialog/wait-dialog.component";
import { Subscription } from "rxjs";
import { PagerService } from "../pager/pager.service";
import { DocLayoutService } from "src/app/providers/doc-layout.service";
import { LanguagesService } from "src/app/providers/languages.service";

@Component({
  selector: "app-input-load",
  templateUrl: "./input-load.component.html",
  styleUrls: ["./input-load.component.scss", "../../../app.component.scss"],
})
export class InputLoadComponent implements OnInit, OnDestroy {
  @ViewChild("grid") grid: SheetComponent;

  private subscription: Subscription;
  public load_name: string;
  public LL_flg: boolean = false;

  public LL_pitch: number;

  public options: pq.gridT.options;
  public width: number;

  private dataset: LoadColumns[] = [];
  private columnKeys3D = ['m1', 'm2', 'direction', 'mark', 'L1', 'L2', 'P1', 'P2', 'n', 'tx', 'ty', 'tz', 'rx', 'ry', 'rz'];
  private columnKeys2D = ['m1', 'm2', 'direction', 'mark', 'L1', 'L2', 'P1', 'P2', 'n', 'tx', 'ty', 'rz'];
  private columnHeaders3D = [
    {
      title: this.translate.instant("input.input-load.memberLoad"),
      align: "center",
      colModel: [
        {
          title: this.translate.instant("input.input-load.memberNo"),
          align: "center",
          colModel: [
            {
              title: "1",
              align: "center",
              dataType: "string",
              dataIndx: this.columnKeys3D[0],
              sortable: false,
              width: 30,
            },
            {
              title: "2",
              align: "center",
              dataType: "string",
              dataIndx: this.columnKeys3D[1],
              sortable: false,
              width: 30,
            },
          ],
        },
        {
          title: this.translate.instant("input.input-load.direction"),
          align: "center",
          colModel: [
            {
              title: "(x,y,z)",
              dataType: "string",
              align: "center",
              dataIndx: this.columnKeys3D[2],
              sortable: false,
              width: 30,
            },
          ],
        },
        {
          title: this.translate.instant("input.input-load.mark"),
          align: "center",
          colModel: [
            {
              title: `(1,2,9,11)<div id="load-strength-info" style="cursor:pointer"><svg width="17" height="17" viewBox="0 0 17 17" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M2.49023 14.5098C4.1504 16.1699 6.15363 17 8.5 17C10.8464 17 12.8496 16.1699 14.5098 14.5098C16.1699 12.8496 17 10.8464 17 8.5C17 6.15363 16.1699 4.1504 14.5098 2.49023C12.8496 0.83007 10.8464 0 8.5 0C6.15363 0 4.1504 0.83007 2.49023 2.49023C0.83007 4.1504 0 6.15363 0 8.5C0 10.8464 0.83007 12.8496 2.49023 14.5098ZM8.5 2.125C9.67318 2.125 10.6748 2.46256 11.5049 3.1377C12.335 3.81283 12.75 4.78124 12.75 6.04297C12.75 7.63673 11.9753 8.85416 10.4258 9.69531C10.2044 9.80599 10.0052 9.96094 9.82812 10.1602C9.65104 10.3594 9.5625 10.5143 9.5625 10.625C9.5625 10.9128 9.45736 11.1618 9.24707 11.3721C9.03678 11.5824 8.78776 11.6875 8.5 11.6875C8.21224 11.6875 7.96322 11.5824 7.75293 11.3721C7.54264 11.1618 7.4375 10.9128 7.4375 10.625C7.4375 10.0273 7.64778 9.4795 8.06836 8.98145C8.48893 8.4834 8.93164 8.10156 9.39648 7.83594C10.2155 7.39323 10.625 6.79558 10.625 6.04297C10.625 4.84765 9.91667 4.25 8.5 4.25C7.92448 4.25 7.42643 4.43815 7.00586 4.81445C6.58528 5.19076 6.375 5.722 6.375 6.4082C6.375 6.69597 6.26986 6.94499 6.05957 7.15527C5.84928 7.36556 5.60026 7.4707 5.3125 7.4707C5.02474 7.4707 4.77572 7.36556 4.56543 7.15527C4.35514 6.94499 4.25 6.69597 4.25 6.4082C4.25 5.08007 4.68164 4.03418 5.54492 3.27051C6.40821 2.50683 7.39322 2.125 8.5 2.125ZM9.26367 14.6094C9.06445 14.8086 8.8099 14.9082 8.5 14.9082C8.1901 14.9082 7.93001 14.8031 7.71973 14.5928C7.50944 14.3825 7.4043 14.1279 7.4043 13.8291C7.4043 13.5303 7.50944 13.2702 7.71973 13.0488C7.93001 12.8275 8.1901 12.7168 8.5 12.7168C8.8099 12.7168 9.06999 12.8275 9.28027 13.0488C9.49056 13.2702 9.5957 13.5303 9.5957 13.8291C9.5957 14.1279 9.48503 14.388 9.26367 14.6094Z" fill="#00C95F"/>
              </svg></div>`,
              dataType: "string",
              dataIndx: this.columnKeys3D[3],
              sortable: false,
              width: 60,
              align: 'center'
            },
          ],
        },
        {
          title: "L1",
          align: "center",
          colModel: [
            {
              title: "(m)",
              dataType: "string",
              align: "right",
              dataIndx: this.columnKeys3D[4],
              sortable: false,
              width: 70,
            },
          ],
        },
        {
          title: "L2",
          align: "center",
          colModel: [
            {
              title: "(m)",
              dataType: "string",
              align: "right",
              dataIndx: this.columnKeys3D[5],
              sortable: false,
              width: 70,
            },
          ],
        },
        {
          title: "P1",
          align: "center",
          colModel: [
            {
              title: "(kN/m)",
              dataType: "string",
              align: "right",
              dataIndx: this.columnKeys3D[6],
              sortable: false,
              width: 70,
            },
          ],
        },
        {
          title: "P2",
          align: "center",
          colModel: [
            {
              title: "(kN/m)",
              dataType: "string",
              align: "right",
              dataIndx: this.columnKeys3D[7],
              sortable: false,
              width: 70,
            },
          ],
        },
      ],
    },
    {
      title: this.translate.instant("input.input-load.nodeLoad"),
      align: "center",
      colModel: [
        {
          title: this.translate.instant("input.input-load.node"),
          align: "center",
          colModel: [
            {
              title: this.translate.instant("input.input-load.No"),
              align: "center",
              dataType: "string",
              dataIndx: this.columnKeys3D[8],
              sortable: false,
              width: 30,
            },
          ],
        },
        {
          title: "X",
          align: "center",
          colModel: [
            {
              title: "(kN)",
              dataType: "string",
              align: "right",
              dataIndx: this.columnKeys3D[9],
              sortable: false,
              width: 70,
            },
          ],
        },
        {
          title: "Y",
          align: "center",
          colModel: [
            {
              title: "(kN)",
              dataType: "string",
              align: "right",
              dataIndx: this.columnKeys3D[10],
              sortable: false,
              width: 70,
            },
          ],
        },
        {
          title: "Z",
          align: "center",
          colModel: [
            {
              title: "(kN)",
              dataType: "string",
              align: "right",
              dataIndx: this.columnKeys3D[11],
              sortable: false,
              width: 70,
            },
          ],
        },
        {
          title: "RX",
          align: "center",
          colModel: [
            {
              title: "(kN m)",
              dataType: "string",
              align: "right",
              dataIndx: this.columnKeys3D[12],
              sortable: false,
              width: 70,
            },
          ],
        },
        {
          title: "RY",
          align: "center",
          colModel: [
            {
              title: "(kN m)",
              dataType: "string",
              align: "right",
              dataIndx: this.columnKeys3D[13],
              sortable: false,
              width: 70,
            },
          ],
        },
        {
          title: "RZ",
          align: "center",
          colModel: [
            {
              title: "(kN m)",
              dataType: "string",
              align: "right",
              dataIndx: this.columnKeys3D[14],
              sortable: false,
              width: 70,
            },
          ],
        },
      ],
    },
  ];
  private columnHeaders2D = [
    {
      title: this.translate.instant("input.input-load.memberLoad"),
      align: "center",
      colModel: [
        {
          title: this.translate.instant("input.input-load.memberNo"),
          colModel: [
            {
              title: "1",
              align: "center",
              dataType: "string",
              dataIndx: this.columnKeys2D[0],
              sortable: false,
              width: 30,
            },
            {
              title: "2",
              align: "center",
              dataType: "string",
              dataIndx: this.columnKeys2D[1],
              sortable: false,
              width: 30,
            },
          ],
        },
        {
          title: this.translate.instant("input.input-load.direction"),
          align: "center",
          colModel: [
            {
              title: "(x,y,z)",
              dataType: "string",
              align: "center",
              dataIndx: this.columnKeys2D[2],
              sortable: false,
              width: 30,
            },
          ],
        },
        {
          title: this.translate.instant("input.input-load.mark"),
          align: "center",
          colModel: [
            {
              title: `(1,2,9,11)<div id="load-strength-info" style="cursor:pointer"><svg width="17" height="17" viewBox="0 0 17 17" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M2.49023 14.5098C4.1504 16.1699 6.15363 17 8.5 17C10.8464 17 12.8496 16.1699 14.5098 14.5098C16.1699 12.8496 17 10.8464 17 8.5C17 6.15363 16.1699 4.1504 14.5098 2.49023C12.8496 0.83007 10.8464 0 8.5 0C6.15363 0 4.1504 0.83007 2.49023 2.49023C0.83007 4.1504 0 6.15363 0 8.5C0 10.8464 0.83007 12.8496 2.49023 14.5098ZM8.5 2.125C9.67318 2.125 10.6748 2.46256 11.5049 3.1377C12.335 3.81283 12.75 4.78124 12.75 6.04297C12.75 7.63673 11.9753 8.85416 10.4258 9.69531C10.2044 9.80599 10.0052 9.96094 9.82812 10.1602C9.65104 10.3594 9.5625 10.5143 9.5625 10.625C9.5625 10.9128 9.45736 11.1618 9.24707 11.3721C9.03678 11.5824 8.78776 11.6875 8.5 11.6875C8.21224 11.6875 7.96322 11.5824 7.75293 11.3721C7.54264 11.1618 7.4375 10.9128 7.4375 10.625C7.4375 10.0273 7.64778 9.4795 8.06836 8.98145C8.48893 8.4834 8.93164 8.10156 9.39648 7.83594C10.2155 7.39323 10.625 6.79558 10.625 6.04297C10.625 4.84765 9.91667 4.25 8.5 4.25C7.92448 4.25 7.42643 4.43815 7.00586 4.81445C6.58528 5.19076 6.375 5.722 6.375 6.4082C6.375 6.69597 6.26986 6.94499 6.05957 7.15527C5.84928 7.36556 5.60026 7.4707 5.3125 7.4707C5.02474 7.4707 4.77572 7.36556 4.56543 7.15527C4.35514 6.94499 4.25 6.69597 4.25 6.4082C4.25 5.08007 4.68164 4.03418 5.54492 3.27051C6.40821 2.50683 7.39322 2.125 8.5 2.125ZM9.26367 14.6094C9.06445 14.8086 8.8099 14.9082 8.5 14.9082C8.1901 14.9082 7.93001 14.8031 7.71973 14.5928C7.50944 14.3825 7.4043 14.1279 7.4043 13.8291C7.4043 13.5303 7.50944 13.2702 7.71973 13.0488C7.93001 12.8275 8.1901 12.7168 8.5 12.7168C8.8099 12.7168 9.06999 12.8275 9.28027 13.0488C9.49056 13.2702 9.5957 13.5303 9.5957 13.8291C9.5957 14.1279 9.48503 14.388 9.26367 14.6094Z" fill="#00C95F"/>
              </svg></div>`,
              dataType: "string",
              dataIndx: this.columnKeys2D[3],
              sortable: false,
              width: 60,
              align: 'center'
            },
          ],
        },
        {
          title: "L1",
          align: "center",
          colModel: [
            {
              title: "(m)",
              dataType: "string",
              align: "right",
              dataIndx: this.columnKeys2D[4],
              sortable: false,
              width: 70,
            },
          ],
        },
        {
          title: "L2",
          align: "center",
          colModel: [
            {
              title: "(m)",
              dataType: "string",
              align: "right",
              dataIndx: this.columnKeys2D[5],
              sortable: false,
              width: 70,
            },
          ],
        },
        {
          title: "P1",
          align: "center",
          colModel: [
            {
              title: "(kN/m)",
              dataType: "string",
              align: "right",
              dataIndx: this.columnKeys2D[6],
              sortable: false,
              width: 70,
            },
          ],
        },
        {
          title: "P2",
          align: "center",
          colModel: [
            {
              title: "(kN/m)",
              dataType: "string",
              align: "right",
              dataIndx: this.columnKeys2D[7],
              sortable: false,
              width: 70,
            },
          ],
        },
      ],
    },
    {
      title: this.translate.instant("input.input-load.nodeLoad"),
      align: "center",
      dataIndx: "ddd",
      colModel: [
        {
          title: this.translate.instant("input.input-load.node"),
          align: "center",
          colModel: [
            {
              title: this.translate.instant("input.input-load.No"),
              align: "center",
              dataType: "string",
              dataIndx: this.columnKeys2D[8],
              sortable: false,
              width: 30,
            },
          ],
        },
        {
          title: "X",
          align: "center",
          colModel: [
            {
              title: "(kN)",
              dataType: "string",
              align: "right",
              dataIndx: this.columnKeys2D[9],
              sortable: false,
              width: 70,
            },
          ],
        },
        {
          title: "Y",
          align: "center",
          colModel: [
            {
              title: "(kN)",
              dataType: "string",
              align: "right",
              dataIndx: this.columnKeys2D[10],
              sortable: false,
              width: 70,
            },
          ],
        },
        {
          title: "M",
          align: "center",
          colModel: [
            {
              title: "(kN m)",
              dataType: "string",
              align: "right",
              dataIndx: this.columnKeys2D[11],
              sortable: false,
              width: 70,
            },
          ],
        },
      ],
    },
  ];
  // 3次元の連行荷重
  private columnHeaders3D_LL = [
    {
      title: this.translate.instant("input.input-load.memberLoad"),
      align: "center",
      colModel: [
        {
          title: this.translate.instant("input.input-load.memberNo"),
          align: "center",
          colModel: [
            {
              title: "1",
              align: "center",
              dataType: "string",
              dataIndx: this.columnKeys3D[0],
              sortable: false,
              width: 30,
            },
            {
              title: "2",
              align: "center",
              dataType: "string",
              dataIndx: this.columnKeys3D[1],
              sortable: false,
              width: 30,
            },
          ],
        },
        {
          title: this.translate.instant("input.input-load.direction"),
          align: "center",
          colModel: [
            {
              title: "(x,y,z)",
              dataType: "string",
              align: "center",
              dataIndx: this.columnKeys3D[2],
              sortable: false,
              width: 30,
            },
          ],
        },
        {
          title: this.translate.instant("input.input-load.mark"),
          align: "center",
          colModel: [
            {
              title: `(1,2,9,11)<div id="load-strength-info" style="cursor:pointer"><svg width="17" height="17" viewBox="0 0 17 17" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M2.49023 14.5098C4.1504 16.1699 6.15363 17 8.5 17C10.8464 17 12.8496 16.1699 14.5098 14.5098C16.1699 12.8496 17 10.8464 17 8.5C17 6.15363 16.1699 4.1504 14.5098 2.49023C12.8496 0.83007 10.8464 0 8.5 0C6.15363 0 4.1504 0.83007 2.49023 2.49023C0.83007 4.1504 0 6.15363 0 8.5C0 10.8464 0.83007 12.8496 2.49023 14.5098ZM8.5 2.125C9.67318 2.125 10.6748 2.46256 11.5049 3.1377C12.335 3.81283 12.75 4.78124 12.75 6.04297C12.75 7.63673 11.9753 8.85416 10.4258 9.69531C10.2044 9.80599 10.0052 9.96094 9.82812 10.1602C9.65104 10.3594 9.5625 10.5143 9.5625 10.625C9.5625 10.9128 9.45736 11.1618 9.24707 11.3721C9.03678 11.5824 8.78776 11.6875 8.5 11.6875C8.21224 11.6875 7.96322 11.5824 7.75293 11.3721C7.54264 11.1618 7.4375 10.9128 7.4375 10.625C7.4375 10.0273 7.64778 9.4795 8.06836 8.98145C8.48893 8.4834 8.93164 8.10156 9.39648 7.83594C10.2155 7.39323 10.625 6.79558 10.625 6.04297C10.625 4.84765 9.91667 4.25 8.5 4.25C7.92448 4.25 7.42643 4.43815 7.00586 4.81445C6.58528 5.19076 6.375 5.722 6.375 6.4082C6.375 6.69597 6.26986 6.94499 6.05957 7.15527C5.84928 7.36556 5.60026 7.4707 5.3125 7.4707C5.02474 7.4707 4.77572 7.36556 4.56543 7.15527C4.35514 6.94499 4.25 6.69597 4.25 6.4082C4.25 5.08007 4.68164 4.03418 5.54492 3.27051C6.40821 2.50683 7.39322 2.125 8.5 2.125ZM9.26367 14.6094C9.06445 14.8086 8.8099 14.9082 8.5 14.9082C8.1901 14.9082 7.93001 14.8031 7.71973 14.5928C7.50944 14.3825 7.4043 14.1279 7.4043 13.8291C7.4043 13.5303 7.50944 13.2702 7.71973 13.0488C7.93001 12.8275 8.1901 12.7168 8.5 12.7168C8.8099 12.7168 9.06999 12.8275 9.28027 13.0488C9.49056 13.2702 9.5957 13.5303 9.5957 13.8291C9.5957 14.1279 9.48503 14.388 9.26367 14.6094Z" fill="#00C95F"/>
              </svg></div>`,
              dataType: "string",
              dataIndx: this.columnKeys3D[3],
              sortable: false,
              width: 60,
              align: 'center'
            },
          ],
        },
        {
          title: "L1",
          align: "center",
          colModel: [
            {
              title: "(m)",
              dataType: "string",
              align: "right",
              dataIndx: this.columnKeys3D[4],
              sortable: false,
              width: 70,
            },
          ],
        },
        {
          title: "L2",
          align: "center",
          colModel: [
            {
              title: "(m)",
              dataType: "string",
              align: "right",
              dataIndx: this.columnKeys3D[5],
              sortable: false,
              width: 70,
            },
          ],
        },
        {
          title: "P1",
          align: "center",
          colModel: [
            {
              title: "(kN/m)",
              dataType: "string",
              align: "right",
              dataIndx: this.columnKeys3D[6],
              sortable: false,
              width: 70,
            },
          ],
        },
        {
          title: "P2",
          align: "center",
          colModel: [
            {
              title: "(kN/m)",
              dataType: "string",
              align: "right",
              dataIndx: this.columnKeys3D[7],
              sortable: false,
              width: 70,
            },
          ],
        },
      ],
    },
  ];
  // 2次元の連行荷重
  private columnHeaders2D_LL = [
    {
      title: this.translate.instant("input.input-load.memberLoad"),
      align: "center",
      colModel: [
        {
          title: this.translate.instant("input.input-load.memberNo"),
          colModel: [
            {
              title: "1",
              align: "center",
              dataType: "string",
              dataIndx: this.columnKeys2D[0],
              sortable: false,
              width: 30,
            },
            {
              title: "2",
              align: "center",
              dataType: "string",
              dataIndx: this.columnKeys2D[1],
              sortable: false,
              width: 30,
            },
          ],
        },
        {
          title: this.translate.instant("input.input-load.direction"),
          align: "center",
          colModel: [
            {
              title: "(x,y,z)",
              dataType: "string",
              align: "center",
              dataIndx: this.columnKeys2D[2],
              sortable: false,
              width: 30,
            },
          ],
        },
        {
          title: this.translate.instant("input.input-load.mark"),
          align: "center",
          colModel: [
            {
              title: `(1,2,9,11)<div id="load-strength-info" style="cursor:pointer"><svg width="17" height="17" viewBox="0 0 17 17" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M2.49023 14.5098C4.1504 16.1699 6.15363 17 8.5 17C10.8464 17 12.8496 16.1699 14.5098 14.5098C16.1699 12.8496 17 10.8464 17 8.5C17 6.15363 16.1699 4.1504 14.5098 2.49023C12.8496 0.83007 10.8464 0 8.5 0C6.15363 0 4.1504 0.83007 2.49023 2.49023C0.83007 4.1504 0 6.15363 0 8.5C0 10.8464 0.83007 12.8496 2.49023 14.5098ZM8.5 2.125C9.67318 2.125 10.6748 2.46256 11.5049 3.1377C12.335 3.81283 12.75 4.78124 12.75 6.04297C12.75 7.63673 11.9753 8.85416 10.4258 9.69531C10.2044 9.80599 10.0052 9.96094 9.82812 10.1602C9.65104 10.3594 9.5625 10.5143 9.5625 10.625C9.5625 10.9128 9.45736 11.1618 9.24707 11.3721C9.03678 11.5824 8.78776 11.6875 8.5 11.6875C8.21224 11.6875 7.96322 11.5824 7.75293 11.3721C7.54264 11.1618 7.4375 10.9128 7.4375 10.625C7.4375 10.0273 7.64778 9.4795 8.06836 8.98145C8.48893 8.4834 8.93164 8.10156 9.39648 7.83594C10.2155 7.39323 10.625 6.79558 10.625 6.04297C10.625 4.84765 9.91667 4.25 8.5 4.25C7.92448 4.25 7.42643 4.43815 7.00586 4.81445C6.58528 5.19076 6.375 5.722 6.375 6.4082C6.375 6.69597 6.26986 6.94499 6.05957 7.15527C5.84928 7.36556 5.60026 7.4707 5.3125 7.4707C5.02474 7.4707 4.77572 7.36556 4.56543 7.15527C4.35514 6.94499 4.25 6.69597 4.25 6.4082C4.25 5.08007 4.68164 4.03418 5.54492 3.27051C6.40821 2.50683 7.39322 2.125 8.5 2.125ZM9.26367 14.6094C9.06445 14.8086 8.8099 14.9082 8.5 14.9082C8.1901 14.9082 7.93001 14.8031 7.71973 14.5928C7.50944 14.3825 7.4043 14.1279 7.4043 13.8291C7.4043 13.5303 7.50944 13.2702 7.71973 13.0488C7.93001 12.8275 8.1901 12.7168 8.5 12.7168C8.8099 12.7168 9.06999 12.8275 9.28027 13.0488C9.49056 13.2702 9.5957 13.5303 9.5957 13.8291C9.5957 14.1279 9.48503 14.388 9.26367 14.6094Z" fill="#00C95F"/>
              </svg></div>`,
              dataType: "string",
              dataIndx: this.columnKeys2D[3],
              sortable: false,
              width: 60,
              align: 'center'
            },
          ],
        },
        {
          title: "L1",
          align: "center",
          colModel: [
            {
              title: "(m)",
              dataType: "string",
              align: "right",
              dataIndx: this.columnKeys2D[4],
              sortable: false,
              width: 70,
            },
          ],
        },
        {
          title: "L2",
          align: "center",
          colModel: [
            {
              title: "(m)",
              dataType: "string",
              align: "right",
              dataIndx: this.columnKeys2D[5],
              sortable: false,
              width: 70,
            },
          ],
        },
        {
          title: "P1",
          align: "center",
          colModel: [
            {
              title: "(kN/m)",
              dataType: "string",
              align: "right",
              dataIndx: this.columnKeys2D[6],
              sortable: false,
              width: 70,
            },
          ],
        },
        {
          title: "P2",
          align: "center",
          colModel: [
            {
              title: "(kN/m)",
              dataType: "string",
              align: "right",
              dataIndx: this.columnKeys2D[7],
              sortable: false,
              width: 70,
            },
          ],
        },
      ],
    },
  ];

  private ROWS_COUNT = 15;
  private page = 1;

  private currentRow: number; // 現在 選択中の行番号
  private currentCol: string; // 現在 選択中の列記号

  constructor(
    private data: InputLoadService,
    private helper: DataHelperModule,
    private app: AppComponent,
    private three: ThreeService,
    private threeLoad: ThreeLoadService,
    private modalService: NgbModal,
    private translate: TranslateService,
    private language: LanguagesService,
    private pagerService: PagerService, public docLayout:DocLayoutService
  ) {

    this.currentRow = null;
    this.currentCol = null;

    // グリッドの設定
    this.options = {
      showTop: false,
      reactive: true,
      sortable: false,
      locale: "jp",
      height: this.tableHeight(),
      numberCell: {
        show: false, // 行番号
      },
      colModel: this.columnHeaders3D,
      dataModel: {
        data: this.dataset,
      },
      contextMenu: {
        on: true,
        items: [
          {
            name: this.translate.instant("action_key.copy"),
            shortcut: 'Ctrl + C',
            action: function (evt, ui, item) {
              this.copy();
            }
          },
          {
            name: this.translate.instant("action_key.paste"),
            shortcut: 'Ctrl + V',
            action: function (evt, ui, item) {
              this.paste();
            }
          },
          {
            name: this.translate.instant("action_key.cut"),
            shortcut: 'Ctrl + X',
            action: function (evt, ui, item) {
              this.cut();
            }
          },
          {
            name: this.translate.instant("action_key.undo"),
            shortcut: 'Ctrl + Z',
            action: function (evt, ui, item) {
              this.History().undo();
            }
          }
        ]
      },
      beforeTableView: (evt, ui) => {
        const finalV = ui.finalV;
        const dataV = this.dataset.length;
        if (ui.initV == null) {
          return;
        }
        if (finalV >= dataV - 1) {
          this.loadPage(this.page, dataV + this.ROWS_COUNT);
          this.grid.refreshDataAndView();
        }
      },
      selectEnd: (evt, ui) => {
        const range = ui.selection.iCells.ranges;
        const row = range[0].r1 + 1;
        const columnList = this.getColumnList(this.helper.dimension);
        const column = columnList[range[0].c1];
        if (this.currentRow !== row || this.currentCol !== column) {
          //選択行の変更があるとき，ハイライトを実行する
          this.three.selectChange("load_values", row, column);
        }
        this.currentRow = row;
        this.currentCol = column;
        console.log("click ", column)
      },
      change: (evt, ui) => {
        console.log("UI: ", ui)

        // 入力制限
        for (const range of [...ui.addList, ...ui.updateList]) {
          range.rowData = this.fixRowData(range.rowData, this.helper.dimension);
        }

        const updatedRows = ui.updateList.map(range => range.rowData.row);
        this.three.changeDataList("load_values", { updatedRows });

        // ハイライトの処理を再度実行する
        const row = ui.updateList[0].rowIndx + 1;
        let column: string;
        const columnList = this.getColumnList(this.helper.dimension);
        for (const key of columnList) {
          if (key in ui.updateList[0].newRow) {
            column = key;
            break;
          }
        }
        this.three.selectChange("load_values", row, column);
      },
    };
    this.subscription = this.pagerService.pageSelected$.subscribe((text) => {
      this.onReceiveEventFromChild(text);
    });
  }

  ngOnInit() {
    this.ROWS_COUNT = this.rowsCount();
    const load_name = this.data.getLoadNameColumns(1);
    this.load_name = load_name.name;
    this.checkLL(load_name.symbol);

    this.loadPage(1, this.ROWS_COUNT);
    this.sheetChange(this.helper.dimension, this.LL_flg);

    this.three.ChangeMode("load_values");
    this.three.ChangePage(1);
  }

 
  ngAfterViewInit() {
    this.docLayout.handleMove.subscribe(data => {
    this.options.height = data - 60;
    })
    this.language.tranText();
  }
  ngOnDestroy() {
    this.subscription.unsubscribe();
  }

  //　pager.component からの通知を受け取る
  onReceiveEventFromChild(eventData: number) {
    this.dataset.splice(0);
    const load_name = this.data.getLoadNameColumns(eventData);
    this.load_name = load_name.name;
    this.checkLL(load_name.symbol);

    this.page = eventData;
    this.loadPage(eventData, this.ROWS_COUNT);
    this.sheetChange(this.helper.dimension, this.LL_flg);
    this.grid.refreshDataAndView();
    this.three.ChangePage(eventData);
  }

  /**
   * 指定されたデータ行に対して入力制限を適用した結果を返す。現状は入力行のデータを編集しているので入力データも更新されることに注意
   * @param load 処理対象のデータ行
   * @param dimension 2=2Dデータ、3=3Dデータ
   * @returns 入力制限を適用した結果のデータ行
   */
  private fixRowData(load: LoadColumns, dimension: number): LoadColumns {
    // const result = structuredClone(load);
    const result = load;

    // m1 自然数
    if (!this.helper.isNaturalNumber(result.m1)) {
      result.m1 = '';
    }
    // m2 0を除く整数
    if (!this.helper.isNonZeroInteger(result.m2)) {
      result.m2 = '';
    }
    // mark [1, 2, 9, 11]
    if (result.mark === '') {
      // do nothing
    } else if (result.mark === null) {
      result.mark = '';
    } else {
      // 元のresult.markがstring型でないケースも考慮
      const mark = String(result.mark);
      result.mark = ['1', '2', '9', '11'].some((s) => s === mark) ? mark : '';
    }
    // direction mark==1 or mark==11 ? [x, y, z, gx, gy, gz] : mark==9 ? null : [x, y, z, gx, gy, gz, r]
    let allowed_directions: string[];
    switch (result.mark) {
      case '1':
      case '11':
        allowed_directions = ['x', 'y', 'z', 'gx', 'gy', 'gz']
        break;
      case '2':
      default:
        allowed_directions = ['x', 'y', 'z', 'gx', 'gy', 'gz', 'r'];
        break;
      case '9':
        allowed_directions = []
        break;
    }
    if (allowed_directions.every((s) => s !== result.direction)) {
      result.direction = '';
    }
    // L1 実数 mark=9では入力不可 -0なら-0.000を表示
    if (result.L1 === '') {
      // do nothing
    } else if (result.L1 === null) {
      result.L1 = '';
    } else if (result.mark === '9') {
      result.L1 = '';
    } else if (this.helper.isNegativeFloatZero(result.L1)) {
      result.L1 = '-0.000';
    } else {
      const value = Number(result.L1);
      result.L1 = isNaN(value) ? '' : value.toFixed(3);
    }
    // L2 実数 mark=9では入力不可
    if (result.L2 === '') {
      // do nothing
    } else if (result.L2 === null) {
      result.L2 = '';
    } else if (result.mark === '9') {
      result.L2 = '';
    } else {
      const value = Number(result.L2);
      result.L2 = isNaN(value) ? '' : value.toFixed(3);
    }
    // P1 実数
    if (result.P1 === '') {
      // do nothing
    } else if (result.P1 === null) {
      result.P1 = '';
    } else {
      const value = Number(result.P1);
      result.P1 = isNaN(value) ? '' : value.toFixed(2);
    }
    // P2 実数 mark=9では入力不可
    if (result.P2 === '') {
      // do nothing
    } else if (result.P2 === null) {
      result.P2 = '';
    } else if (result.mark === '9') {
      result.P2 = '';
    } else {
      const value = Number(result.P2);
      result.P2 = isNaN(value) ? '' : value.toFixed(2);
    }

    // n 正の整数
    if (!this.helper.isPositiveInteger(result.n)) {
      result.n = '';
    }
    // tx 実数
    if (result.tx === '') {
      // do nothing
    } else if (result.tx === null) {
      result.tx = '';
    } else {
      const value = Number(result.tx);
      result.tx = isNaN(value) ? '' : value.toFixed(2);
    }
    // ty 実数
    if (result.ty === '') {
      // do nothing
    } else if (result.ty === null) {
      result.ty = '';
    } else {
      const value = Number(result.ty);
      result.ty = isNaN(value) ? '' : value.toFixed(2);
    }
    // tz 実数
    if (result.tz === '') {
      // do nothing
    } else if (result.tz === null) {
      result.tz = '';
    } else {
      const value = Number(result.tz);
      result.tz = isNaN(value) ? '' : value.toFixed(2);
    }
    // rx 実数
    if (result.rx === '') {
      // do nothing
    } else if (result.rx === null) {
      result.rx = '';
    } else {
      const value = Number(result.rx);
      result.rx = isNaN(value) ? '' : value.toFixed(2);
    }
    // ry 実数
    if (result.ry === '') {
      // do nothing
    } else if (result.ry === null) {
      result.ry = '';
    } else {
      const value = Number(result.ry);
      result.ry = isNaN(value) ? '' : value.toFixed(2);
    }
    // rz 実数
    if (result.rz === '') {
      // do nothing
    } else if (result.rz === null) {
      result.rz = '';
    } else {
      const value = Number(result.rz);
      result.rz = isNaN(value) ? '' : value.toFixed(2);
    }

    return result;
  }

  //
  private loadPage(currentPage: number, row: number): void {
    for (let i = this.dataset.length + 1; i <= row; i++) {
      const load = this.data.getLoadColumns(currentPage, i);
      const fixed = this.fixRowData(load, this.helper.dimension);
      this.dataset.push(fixed);
    }

    const load_name = this.data.getLoadNameColumns(currentPage);
    this.checkLL(load_name.symbol);
    if (this.LL_flg) {
      this.LL_pitch = load_name.LL_pitch;
    }
    this.page = currentPage;
  }

  // 表の高さを計算する
  private tableHeight(): string {
    const containerHeight = this.app.getDialogHeight() - 70; // pagerの分減じる
    return containerHeight.toString();
  }
  // 表高さに合わせた行数を計算する
  private rowsCount(): number {
    const containerHeight = this.app.getDialogHeight();
    return Math.round(containerHeight / 30);
  }

  // 連行荷重のピッチを変えた場合
  public change_pich() {

    if (this.LL_pitch < 0.1) {
      this.LL_pitch = 0.1;
      return;
    }
    // 入力情報をデータに反映する
    const load_name = this.data.getLoadNameColumns(this.page);
    load_name.LL_pitch = this.LL_pitch;

    this.threeLoad.change_LL_Load(this.page.toString());
  }

  private checkLL(symbol: string): void {
    if (symbol === undefined) {
      this.LL_flg = false;
      return
    }
    if (symbol.includes("LL")) {
      this.LL_flg = true;
    } else {
      this.LL_flg = false;
    }
  }

  private sheetChange(dim: number, LL: boolean) {
    if (dim === 3) {
      if (LL) {
        this.options.colModel = this.columnHeaders3D_LL;
        this.width = 550;
      } else {
        this.options.colModel = this.columnHeaders3D;
        this.width = 1020;
      }
    } else {
      if (LL) {
        this.options.colModel = this.columnHeaders2D_LL;
        this.width = 550;
      } else {
        this.options.colModel = this.columnHeaders2D;
        this.width = 810;
      }
    }

  }

  private getColumnList(dimension): string[] {
    if (dimension === 3) {
      return this.columnKeys3D;
    } else {
      return this.columnKeys2D;
    }
  }

}
