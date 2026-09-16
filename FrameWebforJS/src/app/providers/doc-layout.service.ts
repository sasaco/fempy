import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';

@Injectable({
  providedIn: 'root',
})
export class DocLayoutService {
  public handleMove = new BehaviorSubject<any>(0);
  constructor() {}
}