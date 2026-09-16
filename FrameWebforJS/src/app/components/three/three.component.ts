import {
  AfterViewInit,
  Component,
  ElementRef,
  ViewChild,
  HostListener,
  NgZone,
  OnDestroy,
} from "@angular/core";
import * as THREE from "three";

import { SceneService } from "./scene.service";
import { ThreeService } from "./three.service";
import html2canvas from "html2canvas";
import { TranslateService } from "@ngx-translate/core";
import { MenuComponent } from "../menu/menu.component";
import { MaxMinService } from "./max-min/max-min.service";

@Component({
  selector: "app-three",
  templateUrl: "./three.component.html",
  styleUrls: ["./three.component.scss"],
})
export class ThreeComponent implements AfterViewInit, OnDestroy {
  @ViewChild("myCanvas", { static: true }) private canvasRef: ElementRef;
  @ViewChild("img") img: ElementRef;
  @ViewChild("screen", { static: true }) private screen: ElementRef;
  @ViewChild("downloadLink") downloadLink: ElementRef;

  private get canvas(): HTMLCanvasElement {
    return this.canvasRef.nativeElement;
  }

  public contentsDialog = {
    nodes: this.translate.instant("app.node") + "図",
    fix_nodes: this.translate.instant("app.fixNode") + "図",
    members: "部材図",
    elements: "材料図",
    panel: "パネル図",
    joints: "結合図",
    notice_points: "着目点図",
    fix_member: "バネ図",
    load_names: "荷重図",
    load_values: "荷重図",
    disg: "変位量図",
    comb_disg: "Combine変位量図",
    pik_disg: "PickUp変位量図",
    fsec: "断面力図",
    comb_fsec: "Combine断面力図",
    pick_fsec: "PickUp断面力図",
    reac: "反力図",
    comb_reac: "Combine反力図",
    pik_reac: "PickUp反力図",
  };

  public direction = {
    axialForce: "軸方向力",
    shearForceY: "y軸方向のせん断力",
    shearForceZ: "z軸方向のせん断力",
    torsionalMoment: "ねじりモーメント",
    momentY: "y軸回りの曲げモーメント",
    momentZ: "z軸回りの曲げモーメント",
  };

  constructor(
    private ngZone: NgZone,
    public scene: SceneService,
    private max_min: MaxMinService,
    private three: ThreeService,
    private translate: TranslateService
  ) {
    THREE.Object3D.DefaultUp.set(0, 0, 1);
  }

  ngAfterViewInit() {
    this.scene.OnInit(
      this.getAspectRatio(),
      this.canvas,
      devicePixelRatio,
      window.innerWidth,
      window.innerHeight - 120
    );
    this.three.canvasWidth = String(window.innerWidth) + "px";
    this.three.canvasHeight = String(window.innerHeight - 120) + "px";
    this.three.OnInit();

    // ラベルを表示する用のレンダラーを HTML に配置する
    const element = this.scene.labelRendererDomElement();
    const div = document.getElementById("myCanvas"); // ボタンを置きたい場所の手前の要素を取得
    div.parentNode.insertBefore(element, div.nextSibling); // ボタンを置きたい場所にaタグを追加
    // レンダリングする
    this.animate();
    //
    this.three.canvasElement = this.canvas;
  }

  ngOnDestroy() {}

  animate(): void {
    // We have to run this outside angular zones,
    // because it could trigger heavy changeDetection cycles.
    this.ngZone.runOutsideAngular(() => {
      window.addEventListener("DOMContentLoaded", () => {
        this.scene.render();
      });
    });
  }

  // マウスクリック時のイベント
  @HostListener("pointerdown", ["$event"])
  public onMouseDown(event: MouseEvent) {
    const mouse: THREE.Vector2 = this.getMousePosition(event);
    this.three.detectObject(mouse, "click");
  }

  // マウスクリック時のイベント
  // @HostListener("pointerup", ["$event"])
  public onMouseUp(event: MouseEvent) {
    const mouse: THREE.Vector2 = this.getMousePosition(event);
    this.three.detectObject(mouse, "select");
  }

  // マウス移動時のイベント
  // @HostListener("mousemove", ["$event"])
  public onMouseMove(event: MouseEvent) {
    return; // クリックイベントが発生しないバグが解決するまで全てのマウスイベントを無効にする
    const mouse: THREE.Vector2 = this.getMousePosition(event);
    this.three.detectObject(mouse, "hover");
  }

  // マウス位置とぶつかったオブジェクトを検出する
  private getMousePosition(event: MouseEvent): THREE.Vector2 {
    event.preventDefault();
    const rect = this.scene.getBoundingClientRect();
    const mouse = new THREE.Vector2();
    mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
    return mouse;
  }

  // ウインドウがリサイズした時のイベント処理
  @HostListener("window:resize", ["$event"])
  public onResize(event: Event) {
    this.scene.onResize(
      this.getAspectRatio(),
      window.innerWidth,
      window.innerHeight - 120
    );
  }

  private getAspectRatio(): number {
    if (this.canvas.clientHeight === 0) {
      return 0;
    }
    return this.canvas.clientWidth / this.canvas.clientHeight;
  }

  public downloadImage() {
    const screenArea = document.getElementById("screenArea");
    screenArea.style.width = String(window.innerWidth) + "px";
    screenArea.style.height = String(window.innerHeight - 120) + "px";
    html2canvas(screenArea).then((canvas) => {
      this.img.nativeElement.src = canvas.toDataURL();
      this.downloadLink.nativeElement.href = canvas.toDataURL("image/png");
      let filename =
        this.three.fileName == void 0 ? "FrameWebForJS" : this.three.fileName;
      filename = filename.substring(0, filename.lastIndexOf("."));
      const mode = this.three.mode === void 0 ? "" : this.three.mode;

      const figureType = "_" + this.contentsDialog[this.three.mode];
      let index = "";
      if (!(mode == "nodes" || "members" || "panel" || "notice_points")) {
        index = "_" + "Case" + this.max_min.index;
      }

      let radio = "";
      if (mode !== "" && mode.includes("fsec")) {
        radio = "_" + this.direction[this.max_min.radio];
      }

      this.downloadLink.nativeElement.download =
        filename + figureType + index + radio + ".png";
      this.downloadLink.nativeElement.click();
    });
  }
  onToggleHome() {
    this.scene.resetCamera()
    this.scene.render();
  }
}
