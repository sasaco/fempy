import { Injectable, NgZone } from '@angular/core';
// import { Subject } from 'rxjs/Subject';

@Injectable({
  providedIn: 'root'
})
export class ColorPaletteService {
  public isControlShow: boolean = false;

  constructor() { }

  public setControlShowHide(status: boolean) {
    this.isControlShow = status;
  }
}