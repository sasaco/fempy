import { Component, Input } from "@angular/core";
import { NgbActiveModal } from "@ng-bootstrap/ng-bootstrap";

@Component({
  selector: "app-alert-dialog",
  templateUrl: "./alert-dialog.component.html",
  styleUrls: ["./alert-dialog.component.scss"],
})
export class AlertDialogComponent {
  @Input() message: string;
  @Input() title: string | undefined;
  @Input() dialogMode: "confirm" | "alert" = "alert";

  constructor(public modal: NgbActiveModal) {}
}
