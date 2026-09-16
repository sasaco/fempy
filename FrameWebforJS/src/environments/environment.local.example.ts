// Local development defaults. setup-local.ps1 copies this to the ignored
// environment.local.ts. Configure real authentication there when needed.
export const environment = {
  production: false,
  calcURL: 'http://127.0.0.1:8080/',
  printURL: 'http://127.0.0.1:7071/api/Function1',
  loginURL: 'http://127.0.0.1:4200/',
  mypageUrl: 'http://127.0.0.1:7072',
  productionRole: 'local-development',
  firebase: {
    apiKey: 'local-development-placeholder',
    authDomain: 'local-development.invalid',
    projectId: 'local-development',
    appId: '1:123456789:web:local-development'
  },
  msalConfig: {
    auth: {
      clientId: '00000000-0000-0000-0000-000000000001',
      redirectUri: 'http://127.0.0.1:4200/',
      postLogoutRedirectUri: 'http://127.0.0.1:4200/'
    },
    authElectron: {
      clientId: '00000000-0000-0000-0000-000000000001',
      redirectUri: 'http://127.0.0.1:4200/',
      postLogoutRedirectUri: 'http://127.0.0.1:4200/'
    }
  },
  b2cPolicies: {
    names: { signUpSignIn: 'B2C_1_local', resetPassword: 'B2C_1_reset' },
    authorities: {
      signUpSignIn: { authority: 'https://local-development.b2clogin.com/local-development.onmicrosoft.com/B2C_1_local' },
      resetPassword: { authority: 'https://local-development.b2clogin.com/local-development.onmicrosoft.com/B2C_1_reset' }
    },
    authorityDomain: 'local-development.b2clogin.com'
  },
  apiConfig: { uri: 'http://127.0.0.1:7072', scopes: ['openid'] }
};
