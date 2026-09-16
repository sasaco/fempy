import { Component, OnInit, ViewChild } from "@angular/core";
import { DocLayoutService } from "src/app/providers/doc-layout.service";
import { ThreeMembersService } from "../../../three/geometry/three-members.service";
import { InputMemberDetailService } from "./input-member-detail.service";
import { InputMembersService } from "../input-members.service";
import { InputElementsService } from "../../input-elements/input-elements.service";
import { ThreeNodesService } from "src/app/components/three/geometry/three-nodes.service";

@Component({
  selector: "app-input-member-detail",
  templateUrl: "./input-member-detail.component.html",
  styleUrls: ["./input-member-detail.component.scss"],
})
export class InputMemberDetailComponent implements OnInit {


  constructor(
    private data: InputMembersService,
    private element: InputElementsService,
    private threeMembersService: ThreeMembersService,
    private threeNodesService : ThreeNodesService,
    public docLayout:DocLayoutService,
    public inputMemberDetailService: InputMemberDetailService
  ) {
  }

  ngOnInit() {

  }

  ngAfterViewInit() {
   
  }
  updateDetail(){
    this.inputMemberDetailService.updateDetail(this.data, this.element);
    // load data node in model
    this.threeNodesService.changeData();
    // load data member in model
    this.threeMembersService.updateDetail();
  }
  onChangeNodeI(event:any, type: any){
    if(event != null){   
      if (/^[0-9]*$/.test(event)) {
        let valueInput = event;    
        this.inputMemberDetailService.getValueNodeI(this.data, valueInput, type);        
      } else {       
        this.inputMemberDetailService.entity.ni = '';
      } 
    }      
  }
  onChangeMaterial(event:any){
    if(event != null){      
      let valueInput = event;    
      this.inputMemberDetailService.getValueMaterial(this.element, new String(valueInput)); 
    }      
  }
}
