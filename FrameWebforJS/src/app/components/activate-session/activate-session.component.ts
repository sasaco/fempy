import { Component } from '@angular/core';
import { UserInfoService } from 'src/app/providers/user-info.service';


@Component({
  selector: 'app-activate-session',
  templateUrl: './activate-session.component.html',
  styleUrls: ['./activate-session.component.scss']
})
export class ActivateSessionComponent {
  constructor(
    public userInfoService: UserInfoService,
  ) { }
}
