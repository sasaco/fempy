import { Component, OnInit, Input } from '@angular/core';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { Auth, getAuth, signInWithEmailAndPassword } from '@angular/fire/auth';

import { UntypedFormGroup, UntypedFormBuilder, Validators } from '@angular/forms';
import { environment } from 'src/environments/environment';
import { TranslateService } from "@ngx-translate/core";
import { DataHelperModule } from 'src/app/providers/data-helper.module';
import { KeycloakService } from 'keycloak-angular';
import axios from 'axios';
import qs from 'qs';
import { UserInfoService } from 'src/app/providers/user-info.service';

@Component({
  selector: 'app-login-dialog',
  templateUrl: './login-dialog.component.html',
  styleUrls: ['./login-dialog.component.scss']
})


export class LoginDialogComponent implements OnInit {
  loginForm: UntypedFormGroup;
  loginUserName: string;
  loginPassword: string;

  loginError: boolean;
  errorMessage: string;
  connecting: boolean;

  constructor(public activeModal: NgbActiveModal,
    public auth: Auth,
    private fb: UntypedFormBuilder,
    private translate: TranslateService,
    private helper: DataHelperModule,
    public user: UserInfoService,
    // private readonly keycloak: KeycloakService
    ) {
    this.loginError = false;
    this.connecting = false;
    this.auth = getAuth();
  }

  ngOnInit() {
    this.loginForm = this.fb.group({
      'email': ['', [Validators.required, Validators.email]],
      'password': ['', [Validators.required]]
    });
  }

  public login() {
    this.connecting = true;
    const email = this.loginForm.get('email').value;
    const password = this.loginForm.get('password').value;
    let data = qs.stringify({
      'client_id': 'malme-mypage',
      'grant_type': 'password',
      'username': email,
      'password': password,
      'scope': 'openid profile email',
    });
    
    let config = {
      method: 'post',
      url: 'https://auth.malme.app/realms/structural-engine/protocol/openid-connect/token',
      headers: { 
        'Content-Type': 'application/x-www-form-urlencoded'
      },
      data : data
    };
    
    axios.request(config)
      .then((response) => {
        // const keycloakInstance = this.keycloak.getKeycloakInstance();
        const keycloakInstance = null;

        keycloakInstance.token = response.data.access_token;
        keycloakInstance.refreshToken = response.data.refresh_token;
        keycloakInstance.idToken = response.data.id_token;
        axios.get('https://auth.malme.app/realms/structural-engine/protocol/openid-connect/userinfo', {
          headers: {
            Authorization: `Bearer ${keycloakInstance.token}`
          }
        })
          .then(response => {
            const profile = response.data;
            this.user.setUserProfile({
              uid: profile.sub,
              email: profile.email,
              firstName: profile.given_name,
              lastName: profile.family_name,
              extension_Role: profile.extension_Role || 'none',
              malme_roles: profile.extension_Role || profile.malme_roles || 'none',
            });
          })
          .catch(error => {
            console.error("Error:", error);
          });
        return this.activeModal.close('Submit');
      })
      .catch((error) => {
        this.connecting = false;
        this.loginError = true;
        this.errorMessage = error;
        this.helper.alert(this.translate.instant("login-dialog.fail") + error);
      });
    
    // signInWithEmailAndPassword(this.auth, email, password)
    //   .then(auth => {
    //     this.connecting = false;
    //     // メールアドレス確認が済んでいるかどうか
    //     if (!auth.user.emailVerified) {
    //       this.auth.signOut();
    //       return Promise.reject(this.translate.instant("login-dialog.mail_check"));
    //     }
    //     return this.activeModal.close('Submit');
    //   })
    //   .catch( err => {
    //     this.connecting = false;
    //     this.loginError = true;
    //     this.errorMessage = err;
    //     this.helper.alert(this.translate.instant("login-dialog.fail") + err);
    //   });
  }

  public goToLink(){
    window.open(environment.loginURL, "_blank");
  }
}