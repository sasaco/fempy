import { Component, OnInit } from '@angular/core';
import { Subscription } from 'rxjs';
import { SceneService } from '../scene.service';
import { MaxMinService } from './max-min.service';

@Component({
  selector: 'app-max-min',
  templateUrl: './max-min.component.html',
  styleUrls: ['./max-min.component.scss']
})
export class MaxMinComponent implements OnInit {

  public max_Three: string = '';
  public min_Three: string = '';

  constructor(public max_min: MaxMinService,) {
      this.max_min.maxMinClear();
      this.max_min.setParent(this);
    }

  public ngOnInit(): void {
  }

  //　pager.component からの通知を受け取る
  setValue(max_Three: string, min_Three: string) {
    this.max_Three = max_Three;
    this.min_Three = min_Three;
  }

}
