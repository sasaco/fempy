import { Component, OnInit } from '@angular/core';
import { Subscription } from 'rxjs';
import { SceneService } from '../scene.service';
import { ColorPaletteService } from './color-palette.service';
import { ResultFsecBehaviorSubject } from './color-palette-behaviorsubject.service';

@Component({
  selector: 'app-color-palette',
  templateUrl: './color-palette.component.html',
  styleUrls: ['./color-palette.component.scss']
})
export class ColorPaletteComponent implements OnInit {
  public colorRulerList : any[] = [];
  public isControlOpen: boolean = true;
  
  constructor(
    private resultFsecBehaviorSubject: ResultFsecBehaviorSubject,
    public colorPaletteService: ColorPaletteService) {
    
    }

  public ngOnInit(): void {
    this.colorRulerList = [];
  }

  ngAfterViewInit() {
      this.resultFsecBehaviorSubject.drawColor$.subscribe((data) => {
        console.log(data);
        this.colorRulerList = data;
      });
  }

  getBackground(item: any){
    return "background: rgb(" + item._color.r + "," + item._color.g + "," + item._color.b + ")";
  }

  onToggleControl(){
    this.isControlOpen = !this.isControlOpen;
  }
}
