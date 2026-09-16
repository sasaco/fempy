import { Injectable, NgZone } from '@angular/core';
import { Subject } from 'rxjs';

@Injectable({
  providedIn: 'root'
})

export class ResultFsecBehaviorSubject {

  private drawColorGradientSubject = new Subject<any>();

  drawColor$ = this.drawColorGradientSubject.asObservable();

  drawColor(data: any) {
    this.drawColorGradientSubject.next(data);
  }
}
