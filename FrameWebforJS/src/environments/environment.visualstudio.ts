import { environment as localEnvironment } from './environment.local';

export const environment = {
  ...localEnvironment,
  production: false,
  allowAnonymousCalculation: true,
};
