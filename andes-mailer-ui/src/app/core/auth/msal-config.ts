import { MsalGuardConfiguration, MsalInterceptorConfiguration } from '@azure/msal-angular';
import {
  BrowserCacheLocation,
  Configuration,
  InteractionType,
  LogLevel,
} from '@azure/msal-browser';
import { AppConfig } from '@core/config/app-config.model';
import { routePaths } from '@core/navigation/route-paths';
import { redirectUri } from './redirect-bridge';

/** Only our API gets the bearer token, never `config.json` or a third-party host. */
const API_RESOURCE = '/api/*';

/**
 * Only the Entra values come from {@link AppConfig}; the security posture is fixed here so a
 * deployment cannot weaken it. `sessionStorage` keeps tokens from outliving the tab on a shared
 * machine.
 */
export function buildMsalConfig(config: AppConfig, baseUri: string): Configuration {
  return {
    auth: {
      clientId: config.auth.clientId,
      authority: config.auth.authority,
      redirectUri: redirectUri(baseUri),
    },
    cache: { cacheLocation: BrowserCacheLocation.SessionStorage },
    system: {
      allowRedirectInIframe: false,
      // MSAL's default callback discards every message, whatever the level.
      loggerOptions: {
        logLevel: LogLevel.Warning,
        piiLoggingEnabled: false,
        loggerCallback: (level, message) =>
          level === LogLevel.Error ? console.error(message) : console.warn(message),
      },
    },
  };
}

export function buildGuardConfig(config: AppConfig): MsalGuardConfiguration {
  return {
    interactionType: InteractionType.Redirect,
    authRequest: { scopes: [...config.auth.apiScopes] },
    loginFailedRoute: `/${routePaths.loginFailed}`,
  };
}

export function buildInterceptorConfig(config: AppConfig): MsalInterceptorConfiguration {
  return {
    interactionType: InteractionType.Redirect,
    protectedResourceMap: new Map([[API_RESOURCE, [...config.auth.apiScopes]]]),
    // Already the default; set so MSAL Angular stops warning that it isn't.
    strictMatching: true,
  };
}
