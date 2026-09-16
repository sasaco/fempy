import { Component, OnInit } from "@angular/core";
import {
  DockManager,
  PanelContainer,
  DockConfig,
} from "dock-spawn-ts/lib/js/Exports";
import { PanelType } from "dock-spawn-ts/lib/js/enums/PanelType";
import { DataHelperModule } from "./../../providers/data-helper.module";
import { DocLayoutService } from "src/app/providers/doc-layout.service";
import { AppComponent } from "src/app/app.component";


@Component({
  selector: "app-doc-layout",
  templateUrl: "./doc-layout.component.html",
  styleUrls: ["./doc-layout.component.scss"],
})
export class DocLayoutComponent implements OnInit {


  constructor(public helper: DataHelperModule,  public docLayOut: DocLayoutService,private app: AppComponent,) {}

  ngOnInit() {
    const divDockContainer = document.getElementById("dock_div");
    const divDockManager = document.getElementById("my_dock_manager");
    const config: DockConfig = new DockConfig();
    config.escClosesDialog = false;
    config.escClosesWindow = false;

    const dockManager = new DockManager(divDockManager, config);
    dockManager.initialize();

    dockManager.layoutEventListeners;

    dockManager.addLayoutListener({
      onClosePanel: (dockManager, panel) => {
        localStorage.setItem("framewebfor", dockManager.saveState());
      },
      onResumeLayout:  (dockManager, dockContainer) => {
        this.docLayOut.handleMove.next(dockContainer.height);
      },
      onChangeDialogPosition: (dockManager, dialog , x , y) => {
        if(x > 0 && y > 0){
          this.docLayOut.handleMove.next(this.app.getPanelElementContentContainerHeight() + 100);
        }
        else{
          this.docLayOut.handleMove.next(this.app.getPanelElementContentContainerHeight() + 30);
         
        }
      }
    });

    window.onresize = () =>{

    
      dockManager.resize(
        divDockContainer?.clientWidth!,
        divDockContainer?.clientHeight!
      );
    }
    window.onresize(null);

    const panelType = PanelType.panel;
    const editor1 = new PanelContainer(
      document.getElementById("edt1")!,
      dockManager,
      "",
      panelType,
      true
    );
    editor1.hideCloseButton(false);

    const documentNode = dockManager.context.model.documentManagerNode;

    dockManager.dockRight(documentNode, editor1, 0.4);

    dockManager.addLayoutListener({
      onCreateDialog: (dockManager, dialog) => {
        dialog.resize(
          divDockContainer?.clientWidth! * 0.6,
          divDockContainer?.clientHeight! * 0.8
        );
      },
      
    });
    this.docLayOut.handleMove.next(this.app.getPanelElementContentContainerHeight() + 30);
  }

  ngAfterViewInit() {
    window.onresize(null);
    this.hideElement(".panel-element-content-container");

    const closeButton = document.querySelector(".panel-titlebar-button-close");
    if (closeButton) {
      closeButton.addEventListener("click", () =>
        this.handleCloseButtonClick()
      );
    }
  }
  private handleCloseButtonClick(): void {
    this.helper.isContentsDailogShow = false;
    this.hideElement("#my_dock_manager");
    this.hideElement(".panel-element-content-container");
    this.hideElement(".dialog-floating");
  }
  private hideElement(selector: string): void {
    const element = document.querySelector(selector);
    if (element) {
      element.classList.add("hidden");
    }
  }
}
