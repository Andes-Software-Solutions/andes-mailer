import { InjectionToken } from '@angular/core';
import {
  BrowserCacheLocation,
  Configuration,
  IPublicClientApplication,
} from '@azure/msal-browser';
import { AppConfig } from '@core/config/app-config.model';
import { redirectUri } from './redirect-bridge';

/**
 * Ours, not `@azure/msal-angular`'s: its `MsalBroadcastService` relies on `NgZone.run`, a no-op
 * when zoneless.
 */
export const MSAL_INSTANCE = new InjectionToken<IPublicClientApplication>('MSAL_INSTANCE');

export function buildMsalConfig(config: AppConfig, baseUri: string): Configuration {
  return {
    auth: {
      clientId: config.auth.clientId,
      authority: config.auth.authority,
      redirectUri: redirectUri(baseUri),
    },
    cache: { cacheLocation: BrowserCacheLocation.SessionStorage },
    system: { allowRedirectInIframe: false },
  };
}
