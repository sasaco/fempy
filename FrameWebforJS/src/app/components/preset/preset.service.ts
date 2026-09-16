import { Injectable } from "@angular/core";

@Injectable({
  providedIn: "root",
})
export class PresetService {
  public dataPreset: any[] = [];
  public fileSelected : any;
  public presetLink = "./assets/preset/";
  constructor(
  ) {
    this.fileSelected = {};
  }

  public isDisabled : boolean = true;
  bindData(){
    this.dataPreset = [];
    this.dataPreset.push({
      title: "preset.ramen-viaduct",
      fileThumb: this.presetLink + "thumbnail-image/Ramen.png",
      fileName: "サンプル（ラーメン高架橋）.json",
      order: 1,
    });
    this.dataPreset.push({
      title: "preset.concrete-t-beam-bridge",
      fileThumb: this.presetLink + "thumbnail-image/ConcreteT-beam.png",
      fileName: "サンプル（Ct桁）.json",
      order: 2,
    });
    this.dataPreset.push({
      title: "preset.u-shaped-retaining-wall",
      fileThumb: this.presetLink + "thumbnail-image/U-shapedWall.png",
      fileName: "サンプル（U型擁壁）.json",
      order: 3,
    });
    this.dataPreset.push({
      title: "preset.portal-bracing",
      fileThumb: this.presetLink + "thumbnail-image/PortalBracing.png",
      fileName: "サンプル（門型橋脚）.json",
      order: 4,
    });
  }

  sortBy(prop: string) {
    return this.dataPreset.sort((a, b) =>
      a[prop] > b[prop] ? 1 : a[prop] === b[prop] ? 0 : -1
    );
  }
  selectRadio(item: any){
    this.fileSelected = {};
    this.fileSelected = {...item};
    this.isDisabled = false;
  }
}
