import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Component, OnDestroy, OnInit } from "@angular/core";
import { ResultDataService } from "src/app/providers/result-data.service";
import { ThreeService } from "../three/three.service";
import { DataHelperModule } from "src/app/providers/data-helper.module";
import { TranslateService } from "@ngx-translate/core";
import { AppComponent } from "../../app.component";
import { Router } from "@angular/router";
import { LanguagesService } from "src/app/providers/languages.service";
import { PrintCustomFsecService } from "../print/custom/print-custom-fsec/print-custom-fsec.service";
import { NgbModal } from "@ng-bootstrap/ng-bootstrap";
import { SceneService } from "../three/scene.service";
import { InputDataService } from "src/app/providers/input-data.service";
import { WaitDialogComponent } from "../wait-dialog/wait-dialog.component";
import { MenuService } from "../menu/menu.service";
import { AppService } from "src/app/app.service";

@Component({
  selector: "app-start-menu",
  templateUrl: "./start-menu.component.html",
  styleUrls: ["./start-menu.component.scss", "../../app.component.scss"],
})
export class StartMenuComponent implements OnInit, OnDestroy {
  constructor(
    private router: Router,
    public menuService: MenuService,
    public ResultData: ResultDataService,
    private http: HttpClient,
    public helper: DataHelperModule,
    private translate: TranslateService,
    private app: AppComponent,
    private language: LanguagesService,
    private three: ThreeService,
    private CustomFsecData: PrintCustomFsecService,
    private modalService: NgbModal,
    private scene: SceneService,
    private appService: AppService,
    public InputData: InputDataService,
    public printCustomFsecService: PrintCustomFsecService,
  ) {}

  ngOnInit() {}

  ngOnDestroy(): void {}

  public onPageBack(): void {
    this.helper.isContentsDailogShow = false;
    this.appService.addHiddenFromElements();
  }

  clickSelectPreset() {
    this.helper.isContentsDailogShow = false;
    this.router.navigateByUrl('/', { skipLocationChange: true }).then(() =>{
      this.router.navigate([{ outlets: { presetOutlet: ['preset'] } }]);
    })
  }
  clickOpenFile(event) {
    this.helper.isContentsDailogShow = false;
    this.menuService.open(event)
    this.router.navigate(['/']);
  }
  async clickStartNew(){
    const isConfirm = await this.helper.confirm(
      this.translate.instant("window.confirm")
    );
    if (isConfirm) {
      this.appService.dialogClose(); // 現在表示中の画面を閉じる
      this.menuService.renew();
      this.router.navigate(['/']);
    }
  }
}
