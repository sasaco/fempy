import { Component, OnInit, OnDestroy } from "@angular/core";
import { Router, NavigationEnd } from "@angular/router";
import { filter } from "rxjs/operators";
import { DataHelperModule } from "src/app/providers/data-helper.module";
import { SceneService } from "../three/scene.service";
import { ThreeLoadService } from "../three/geometry/three-load/three-load.service";
import { Subscription } from "rxjs";

const URL_INPUTS = [
  "/input-fix_nodes",
  "/input-elements",
  "/input-joints",
  "/input-loads",
  "/input-load-name",
  "/input-fix_members"  
];
const URL_RESULTS = [
  "/result-disg",
  "/result-reac",
  "/result-fsec",
  "/result-comb_disg",
  "/result-comb_reac",
  "/result-comb_fsec",
  "/result-pic_disg",
  "/result-pic_reac",
  "/result-pic_fsec",
];
const URL_COMB_PIC = [
  "/result-comb_disg",
  "/result-comb_reac",
  "/result-comb_fsec",
  "/result-pic_reac",
  "/result-pic_fsec",
  "/result-pic_disg",
];
const URL_LOAD = ["/input-loads", "/input-load-name"];
const URL_DEFINE = ["/input-define", "/input-combine", "/input-pickup"];
const URL_RESULT_PIC = [
  "/result-pic_reac",
  "/result-pic_fsec",
  "/result-pic_disg",
];
const URL_MEM = ["/input-members", "/input-rigid_zone"];
const URL_RESULT_BASIC = ["/result-fsec", "/result-disg", "/result-reac"];
@Component({
  selector: "app-optional-header",
  templateUrl: "./optional-header.component.html",
  styleUrls: ["./optional-header.component.scss"],
})
export class OptionalHeaderComponent implements OnInit, OnDestroy {
  showPagerComponent: boolean;
  showLoadComponent: boolean;
  showDefineComponent: boolean;
  showResultComponent: boolean;
  showNotBasicComponent: boolean;
  showPicComponent: boolean;
  showDisgComponent: boolean;
  showReacComponent: boolean;
  showFsecComponent: boolean;
  showMemberComponent: boolean;

  selectedLink: string = "/input-load-name";
  selectedMemberLink: string ='/input-members'
  selectedDefineLink: string = "/input-define";
  selectedDisgLink: string = "/result-disg";
  selectedReacLink: string = "/result-reac";
  selectedFsecLink: string = "/result-fsec";
  isControlOpen: boolean = false;
  selectedLoadPage: number = 1;
  selectedDefinePage: number = 1;
  selectedResultPage: number = 1;
  selectedMemberPage: number = 1;
  loadPages: number[] = [1, 2];
  definePages: number[] = [1, 2, 3];
  resultPages: number[] = [1, 2, 3];
  memberPages: number[] = [1, 2];
  resultDisgURL: string[] = [
    "/result-disg",
    "/result-comb_disg",
    "/result-pic_disg",
  ];
  resultReacURL: string[] = [
    "/result-reac",
    "/result-comb_reac",
    "/result-pic_reac",
  ];
  resultFsecURL: string[] = [
    "/result-fsec",
    "/result-comb_fsec",
    "/result-pic_fsec",
  ];

  activeLoadName: boolean = true;
  activeLoadStrength: boolean = false;
  url: string = "";
  
  private LL_pageSubscription: Subscription;

  constructor(
    private router: Router,
    private scene: SceneService,
    private load: ThreeLoadService,
    public helper: DataHelperModule
  ) {
    this.router.events
      .pipe(filter((event) => event instanceof NavigationEnd))
      .subscribe((event: NavigationEnd) => {
        console.log("event", event.url)
        this.url= event.url
        if(event.url==="/input-load-name"){
         this.activeLoadName=true,
         this.activeLoadStrength= false;
        }
        if(event.url==="/input-loads"){
          this.activeLoadName=false,
          this.activeLoadStrength= true;
        }
        // 基本結果ページ以外に移動した場合はLL_pageをリセット
        if (!URL_RESULT_BASIC.includes(event.url)) {
          this.helper.LL_page = false;
        }

        this.showPagerComponent = URL_INPUTS.concat(URL_RESULTS).includes(
          event.url 
        );
        this.showMemberComponent = URL_MEM.includes(event.url);
        this.showLoadComponent = URL_LOAD.includes(event.url);
        this.showDefineComponent = URL_DEFINE.includes(event.url);
        this.showResultComponent = URL_RESULTS.includes(event.url);
        this.updateShowNotBasicComponent();
        this.showPicComponent = URL_RESULT_PIC.includes(event.url);

        this.showDisgComponent = this.resultDisgURL.includes(event.url);
        this.showReacComponent = this.resultReacURL.includes(event.url);
        this.showFsecComponent = this.resultFsecURL.includes(event.url);
        this.initSelectedValue(event.url);
      });

    // LL_pageの変更を購読
    this.LL_pageSubscription = this.helper.LL_pageChange$.subscribe((LL_page: boolean) => {
      this.updateShowNotBasicComponent();
    });
  }

  ngOnInit() {
    this.handleShow(2)
  }

  ngOnDestroy() {
    if (this.LL_pageSubscription) {
      this.LL_pageSubscription.unsubscribe();
    }
  }

  private updateShowNotBasicComponent() {
    this.showNotBasicComponent = URL_COMB_PIC.includes(this.url) || (URL_RESULT_BASIC.includes(this.url) && this.helper.LL_page);
  }
  initSelectedValue(url: string) {
    if (url === "/input-load-name") {
      this.selectedLink = url;
      this.selectedLoadPage = 1;
      this.onLinkChange(this.selectedLink);
    } else if (url === "/input-define") {
      this.selectedDefineLink = url;
      this.selectedDefinePage = 1;
      this.onLinkChange(this.selectedDefineLink);
    } else if(url === "/input-members"){
      this.selectedMemberLink = url;
      this.selectedMemberPage = 1;
      this.onLinkChange(this.selectedMemberLink);
    }else {
      this.onLinkChange(url);
    }
  }

  onToggleControl() {
    this.isControlOpen ? this.scene.close() : this.scene.open();
    this.isControlOpen = !this.isControlOpen;
  }
  previousMemberPage(){
    this.selectedMemberPage--;
    this.selectedMemberLink = "/input-members";
    this.onLinkChange(this.selectedMemberLink);
  }
  nextMemberPage(){
    this.selectedMemberPage++;
    this.selectedMemberLink = "/input-rigid_zone";
    this.onLinkChange(this.selectedMemberLink);
  }
  previousLoadPage() {
    this.selectedLoadPage--;
    this.selectedLink = "/input-load-name";
    this.activeLoadName= true;
    this.activeLoadStrength= false;
    this.onLinkChange(this.selectedLink);
  }

  nextLoadPage() {
    this.selectedLoadPage++;
    this.selectedLink = "/input-loads";
    this.activeLoadName= false;
    this.activeLoadStrength= true;
    this.onLinkChange(this.selectedLink);
  }

  previousDefinePage() {
    this.selectedDefinePage--;
    if (this.selectedDefinePage === 2) {
      this.selectedDefineLink = "/input-combine";
    } else if (this.selectedDefinePage === 1) {
      this.selectedDefineLink = "/input-define";
    }
    this.onLinkChange(this.selectedDefineLink);
  }

  nextDefinePage() {
    this.selectedDefinePage++;
    if (this.selectedDefinePage === 2) {
      this.selectedDefineLink = "/input-combine";
    } else if (this.selectedDefinePage === 3) {
      this.selectedDefineLink = "/input-pickup";
    }
    this.onLinkChange(this.selectedDefineLink);
  }

  handleDefinePage(select: any) {
    this.selectedDefineLink = select;
    this.onLinkChange(this.selectedDefineLink);
  }

  previousResultPage() {
    this.selectedResultPage--;
    const resultURL = this.selectedUrlChange();
    this.router.navigate([resultURL[this.selectedResultPage - 1]]);
  }

  nextResultPage() {
    this.selectedResultPage++;
    const resultURL = this.selectedUrlChange();
    this.router.navigate([resultURL[this.selectedResultPage - 1]]);
  }

  handleResultPage(select:any){
    this.selectedResultPage= select
    const resultURL = this.selectedUrlChange();
    this.router.navigate([resultURL[this.selectedResultPage - 1]]);
  }

  onLinkChange(link: string) {
    this.router.navigate([link]);
  }
  onResultLinkChange(page: number) {
    const resultURL = this.selectedUrlChange();
    this.router.navigate([resultURL[page - 1]]);
  }

  selectedUrlChange(): string[] {
    if (this.showDisgComponent) return this.resultDisgURL;
    if (this.showReacComponent) return this.resultReacURL;
    if (this.showFsecComponent) return this.resultFsecURL;
  }
  handleShow(dimension:number){
    if (dimension !== 2) {
      dimension = 3;
    }
    this.helper.dimension = dimension
    this.load.redraw();
    this.scene.changeGui(dimension);
  }
}

