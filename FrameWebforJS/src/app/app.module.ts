import { BrowserModule } from "@angular/platform-browser";
import { APP_INITIALIZER, NgModule } from "@angular/core";
import { FormsModule, ReactiveFormsModule } from "@angular/forms";
import { HttpClientModule, HttpClient, HTTP_INTERCEPTORS } from "@angular/common/http";
import { DragDropModule } from "@angular/cdk/drag-drop";
import { ScrollingModule } from '@angular/cdk/scrolling';
import { BrowserAnimationsModule } from "@angular/platform-browser/animations";
import { MatRadioModule } from "@angular/material/radio";
import { NgbModule } from "@ng-bootstrap/ng-bootstrap";

import { AppRoutingModule } from "./app-routing.module";
import { AppComponent } from "./app.component";
import { MatInputModule } from "@angular/material/input";
import { MatExpansionModule } from '@angular/material/expansion';

import { initializeApp, provideFirebaseApp } from "@angular/fire/app";
import { getAuth, provideAuth } from '@angular/fire/auth';
import { getFirestore, LogLevel, provideFirestore } from '@angular/fire/firestore';
import { environment } from "../environments/environment";

import { KeycloakAngularModule, KeycloakService } from 'keycloak-angular';

import { InputDataService } from "./providers/input-data.service";
import { DataHelperModule } from "./providers/data-helper.module";
import { ResultDataService } from "./providers/result-data.service";
import { UserInfoService } from "./providers/user-info.service";

import { MenuComponent } from "./components/menu/menu.component";
import { LoginDialogComponent } from "./components/login-dialog/login-dialog.component";
import { WaitDialogComponent } from "./components/wait-dialog/wait-dialog.component";
import { AlertDialogComponent } from "./components/alert-dialog/alert-dialog.component";

import { InputNodesComponent } from "./components/input/input-nodes/input-nodes.component";
import { InputNodesService } from "./components/input/input-nodes/input-nodes.service";
import { InputMembersComponent } from "./components/input/input-members/input-members.component";
import { InputMembersService } from "./components/input/input-members/input-members.service";

import { InputMemberDetailComponent } from "./components/input/input-members/input-member-detail/input-member-detail.component";
import { InputMemberDetailService } from "./components/input/input-members/input-member-detail/input-member-detail.service";

import { InputFixNodeComponent } from "./components/input/input-fix-node/input-fix-node.component";
import { InputFixNodeService } from "./components/input/input-fix-node/input-fix-node.service";
import { InputElementsComponent } from "./components/input/input-elements/input-elements.component";
import { InputElementsService } from "./components/input/input-elements/input-elements.service";
import { InputJointComponent } from "./components/input/input-joint/input-joint.component";
import { InputJointService } from "./components/input/input-joint/input-joint.service";
import { InputNoticePointsComponent } from "./components/input/input-notice-points/input-notice-points.component";
import { InputNoticePointsService } from "./components/input/input-notice-points/input-notice-points.service";
import { InputFixMemberComponent } from "./components/input/input-fix-member/input-fix-member.component";
import { InputFixMemberService } from "./components/input/input-fix-member/input-fix-member.service";
import { InputLoadNameComponent } from "./components/input/input-load/input-load-name.component";
import { InputLoadComponent } from "./components/input/input-load/input-load.component";
import { InputLoadService } from "./components/input/input-load/input-load.service";
import { InputDefineComponent } from "./components/input/input-define/input-define.component";
import { InputDefineService } from "./components/input/input-define/input-define.service";
import { InputCombineComponent } from "./components/input/input-combine/input-combine.component";
import { InputCombineService } from "./components/input/input-combine/input-combine.service";
import { InputPickupComponent } from "./components/input/input-pickup/input-pickup.component";
import { InputPickupService } from "./components/input/input-pickup/input-pickup.service";

import { ResultDisgComponent } from "./components/result/result-disg/result-disg.component";
import { ResultDisgService } from "./components/result/result-disg/result-disg.service";
import { ResultReacComponent } from "./components/result/result-reac/result-reac.component";
import { ResultReacService } from "./components/result/result-reac/result-reac.service";
import { ResultFsecComponent } from "./components/result/result-fsec/result-fsec.component";
import { ResultFsecService } from "./components/result/result-fsec/result-fsec.service";
import { ResultCombineDisgComponent } from "./components/result/result-combine-disg/result-combine-disg.component";
import { ResultCombineDisgService } from "./components/result/result-combine-disg/result-combine-disg.service";
import { ResultPickupDisgComponent } from "./components/result/result-pickup-disg/result-pickup-disg.component";
import { ResultPickupDisgService } from "./components/result/result-pickup-disg/result-pickup-disg.service";
import { ResultCombineReacComponent } from "./components/result/result-combine-reac/result-combine-reac.component";
import { ResultCombineReacService } from "./components/result/result-combine-reac/result-combine-reac.service";
import { ResultPickupReacComponent } from "./components/result/result-pickup-reac/result-pickup-reac.component";
import { ResultPickupReacService } from "./components/result/result-pickup-reac/result-pickup-reac.service";
import { ResultPickupFsecComponent } from "./components/result/result-pickup-fsec/result-pickup-fsec.component";
import { ResultPickupFsecService } from "./components/result/result-pickup-fsec/result-pickup-fsec.service";
import { ResultCombineFsecComponent } from "./components/result/result-combine-fsec/result-combine-fsec.component";
import { ResultCombineFsecService } from "./components/result/result-combine-fsec/result-combine-fsec.service";

import { ThreeComponent } from "./components/three/three.component";
import { SceneService } from "./components/three/scene.service";
import { InputPanelComponent } from "./components/input/input-panel/input-panel.component";

import { StartMenuComponent } from "./components/start-menu/start-menu.component";
import { PrintComponent } from "./components/print/print.component";
import { PrintService } from "./components/print/print.service";
import { PresetComponent } from "./components/preset/preset.component";
import { PresetService } from "./components/preset/preset.service";
import { PrintLayoutComponent } from "./components/print/print-layout/print-layout.component";
import { InvoiceComponent } from "./components/print/invoice/invoice.component";

import { PrintThreeComponent } from "./components/print/invoice/print-three/print-three.component";

import { PagerComponent } from "./components/input/pager/pager.component";
import { PagerDirectionComponent } from "./components/input/pager-direction/pager-direction.component";
import { SheetComponent } from "./components/input/sheet/sheet.component";
import { PrintCustomFsecComponent } from "./components/print/custom/print-custom-fsec/print-custom-fsec.component";
import { PrintCustomThreeComponent } from "./components/print/custom/print-custom-three/print-custom-three.component";
import { PrintCustomComponent } from "./components/print/custom/print-custom.component";
import { PrintCustomReacComponent } from "./components/print/custom/print-custom-reac/print-custom-reac.component";
import { PrintCustomDisgComponent } from "./components/print/custom/print-custom-disg/print-custom-disg.component";
import { TranslateLoader, TranslateModule } from "@ngx-translate/core";
import { TranslateHttpLoader } from "@ngx-translate/http-loader";
import { MaxMinComponent } from './components/three/max-min/max-min.component';
import { ColorPaletteComponent } from './components/three/color-palette/color-palette.component';
import { ChatComponent } from './components/chat/chat.component';
import { ElectronService } from "./providers/electron.service";
import { DocLayoutComponent } from "./components/doc-layout/doc-layout.component";
import { OptionalHeaderComponent } from "./components/optional-header/optional-header.component";
import { ActivateSessionComponent } from './components/activate-session/activate-session.component';
import { InputRigidZoneComponent } from "./components/input/input-rigid-zone/input-rigid-zone.component";

import { MatIconModule } from '@angular/material/icon'
import { AppService } from "./app.service";
import { IPublicClientApplication, PublicClientApplication, InteractionType, BrowserCacheLocation } from '@azure/msal-browser';
import { MsalGuard, MsalInterceptor, MsalBroadcastService, MsalInterceptorConfiguration, MsalModule, MsalService, MSAL_GUARD_CONFIG, MSAL_INSTANCE, MSAL_INTERCEPTOR_CONFIG, MsalGuardConfiguration, MsalRedirectComponent } from '@azure/msal-angular';
import { LogLevel as LogLevelMasl } from "@azure/msal-browser";

const httpLoaderFactory = (http: HttpClient): TranslateHttpLoader =>
  new TranslateHttpLoader(http, "./assets/i18n/", ".json");

export function loggerCallback(logLevel: LogLevelMasl, message: string) {
  console.log(message);
}

export function MSALInstanceFactory(): IPublicClientApplication {

  return new PublicClientApplication({
    auth: {
      clientId: environment.msalConfig.auth.clientId,
      authority: environment.b2cPolicies.authorities.signUpSignIn.authority,
      redirectUri: environment.msalConfig.auth.redirectUri,
      postLogoutRedirectUri: environment.msalConfig.auth.postLogoutRedirectUri,
      knownAuthorities: [environment.b2cPolicies.authorityDomain]
    },
    cache: {
      cacheLocation: BrowserCacheLocation.LocalStorage
    },
    system: {
      allowNativeBroker: false, // Disables WAM Broker
      loggerOptions: {
        loggerCallback,
        logLevel: LogLevelMasl.Verbose,
        piiLoggingEnabled: false
      }
    }
  });
}

export function MSALInterceptorConfigFactory(): MsalInterceptorConfiguration {
  const protectedResourceMap = new Map<string, Array<string>>();
  protectedResourceMap.set(environment.apiConfig.uri, environment.apiConfig.scopes);

  return {
    interactionType: InteractionType.Redirect,
    protectedResourceMap
  };
}

export function MSALGuardConfigFactory(): MsalGuardConfiguration {
  return {
    interactionType: InteractionType.Redirect,
    authRequest: {
      scopes: [...environment.apiConfig.scopes]
    },
    loginFailedRoute: '/login-failed'
  };
}

@NgModule({
  imports: [
    BrowserModule,
    AppRoutingModule,
    FormsModule,
    ReactiveFormsModule,
    HttpClientModule,
    DragDropModule,
    BrowserAnimationsModule,
    NgbModule,
    DataHelperModule,
    MatInputModule,
    MatRadioModule,
    MatExpansionModule,
    KeycloakAngularModule,
    provideFirebaseApp(() => initializeApp(environment.firebase)),
    provideAuth(() => getAuth()),
    provideFirestore(() => getFirestore()),
    TranslateModule.forRoot({
      loader: {
        provide: TranslateLoader,
        useFactory: httpLoaderFactory,
        deps: [HttpClient],
      },
    }),
    ScrollingModule,
    MatIconModule,
    MsalModule
  ],
  declarations: [
    AppComponent,
    MenuComponent,
    LoginDialogComponent,
    WaitDialogComponent,
    AlertDialogComponent,
    InputNodesComponent,
    InputMembersComponent,
    InputMemberDetailComponent,
    InputFixNodeComponent,
    InputElementsComponent,
    InputJointComponent,
    InputNoticePointsComponent,
    InputFixMemberComponent,
    InputLoadNameComponent,
    InputLoadComponent,
    InputDefineComponent,
    InputCombineComponent,
    InputPickupComponent,
    ResultDisgComponent,
    ResultReacComponent,
    ResultFsecComponent,
    ResultCombineDisgComponent,
    ResultPickupDisgComponent,
    ResultCombineReacComponent,
    ResultPickupReacComponent,
    ResultPickupFsecComponent,
    ResultCombineFsecComponent,
    ThreeComponent,
    InputPanelComponent,
    StartMenuComponent,
    PrintComponent,
    PresetComponent,
    PrintLayoutComponent,
    InvoiceComponent,
    PagerComponent,
    PagerDirectionComponent,
    SheetComponent,
    PrintThreeComponent,
    PrintCustomFsecComponent,
    PrintCustomThreeComponent,
    PrintCustomComponent,
    PrintCustomReacComponent,
    PrintCustomDisgComponent,
    MaxMinComponent,
    ColorPaletteComponent,
    ChatComponent,
    DocLayoutComponent,
    OptionalHeaderComponent,
    ActivateSessionComponent,
    InputRigidZoneComponent,
  ],
  providers: [
    AppService,
    InputDataService,
    InputNodesService,
    InputMembersService,
    InputMemberDetailService,
    InputFixNodeService,
    InputElementsService,
    InputJointService,
    InputNoticePointsService,
    InputFixMemberService,
    InputLoadService,
    InputDefineService,
    InputCombineService,
    InputPickupService,
    PrintService,
    PresetService,
    ResultDataService,
    ResultDisgService,
    ResultReacService,
    ResultFsecService,
    ResultCombineDisgService,
    ResultPickupDisgService,
    ResultCombineReacService,
    ResultPickupReacService,
    ResultPickupFsecService,
    ResultCombineFsecService,
    UserInfoService,
    SceneService,
    ElectronService,
    {
      provide: HTTP_INTERCEPTORS,
      useClass: MsalInterceptor,
      multi: true,
    },
    {
      provide: MSAL_INSTANCE,
      useFactory: MSALInstanceFactory,
    },
    {
      provide: MSAL_GUARD_CONFIG,
      useFactory: MSALGuardConfigFactory,
    },
    {
      provide: MSAL_INTERCEPTOR_CONFIG,
      useFactory: MSALInterceptorConfigFactory,
    },
    MsalService,
    MsalGuard,
    MsalBroadcastService
  ],
  bootstrap: [AppComponent, MsalRedirectComponent]
})
export class AppModule { }

export function HttpLoaderFactory(http: HttpClient) {
  return new TranslateHttpLoader(http);
}

