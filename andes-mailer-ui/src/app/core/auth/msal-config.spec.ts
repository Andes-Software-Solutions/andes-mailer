import { BrowserCacheLocation, InteractionType } from '@azure/msal-browser';
import { testApiScope, testAppConfig } from '@testing/app-config';
import { buildGuardConfig, buildInterceptorConfig, buildMsalConfig } from './msal-config';

describe('msal-config', () => {
  const baseUri = 'https://mailer.example.com/';

  it('redirects to the bridge, keeps tokens per tab, and never logs PII', () => {
    const config = buildMsalConfig(testAppConfig, baseUri);

    expect(config.auth.redirectUri).toBe('https://mailer.example.com/auth');
    expect(config.cache?.cacheLocation).toBe(BrowserCacheLocation.SessionStorage);
    expect(config.system?.allowRedirectInIframe).toBe(false);
    expect(config.system?.loggerOptions?.piiLoggingEnabled).toBe(false);
  });

  it('signs in by redirect for the API scope and fails to the failure page', () => {
    expect(buildGuardConfig(testAppConfig)).toEqual({
      interactionType: InteractionType.Redirect,
      authRequest: { scopes: [testApiScope] },
      loginFailedRoute: '/login-failed',
    });
  });

  it('protects only the API', () => {
    const { protectedResourceMap, strictMatching } = buildInterceptorConfig(testAppConfig);

    expect([...protectedResourceMap]).toEqual([['/api/*', [testApiScope]]]);
    expect(strictMatching).toBe(true);
  });
});
