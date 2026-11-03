import { Provider } from '@angular/core';
import { APP_CONFIG } from '../app/core/config/app-config-token';
import { AppConfig } from '../app/core/config/app-config.model';

export const testApiScope = 'api://b40defa0-0000-4000-8000-000000000000/access_as_user';

export const testAppConfig: AppConfig = {
  auth: {
    clientId: 'b40defa0-0000-4000-8000-000000000000',
    authority: 'https://login.microsoftonline.com/9c8b7a65-4321-4f0e-9d8c-7b6a5f4e3d2c',
    apiScopes: [testApiScope],
  },
};

export function provideTestAppConfig(): Provider {
  return { provide: APP_CONFIG, useValue: testAppConfig };
}
