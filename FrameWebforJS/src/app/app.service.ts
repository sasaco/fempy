import { Injectable } from "@angular/core";
import { DataHelperModule } from "./providers/data-helper.module";
import { PrintService } from "./components/print/print.service";

@Injectable({
  providedIn: "root",
})
export class AppService {
  public dataPreset: any[] = [];
  public fileSelected : any;
  public presetLink = "./assets/preset/";
  constructor(
    public helper: DataHelperModule,
    public printService: PrintService,
  ) {
    
  }
  public dialogClose(): void {
    this.helper.isContentsDailogShow = false;
    this.addHiddenFromElements();

    // 印刷ウィンドウの変数をリセット
    this.resetPrintdialog();
  }

  // 印刷ウィンドウの変数をリセット
  public resetPrintdialog(): void {
    for (let i = 0; i < this.printService.printTargetValues.length; i++) {
      this.printService.printTargetValues[i].value = false;
    }

    this.printService.resetPrintOption(); // いずれ消したい
    //this.printService.selectPrintCase('');
    this.printService.clearPrintCase(); // いずれ消したい

  }

  public addHiddenFromElements(): void {
    this.addHiddenFromClass(".panel-element-content-container");
    this.addHiddenFromClass("#my_dock_manager");
    this.addHiddenFromClass(".dialog-floating");
  }

  private addHiddenFromClass(selector: string): void {
    const element = document.querySelector(selector);
    if (element) {
      element.classList.add("hidden");
    }
  }
}
