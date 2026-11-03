import { Provider } from '@angular/core';
import {
  AccountInfo,
  AuthenticationResult,
  IPublicClientApplication,
  stubbedPublicClientApplication,
} from '@azure/msal-browser';
import { MSAL_INSTANCE } from '../app/core/auth/msal-instance-token';
import { testApiScope } from './app-config';

/**
 * Spreads msal-browser's `stubbedPublicClientApplication`, whose methods all reject, so an
 * unstubbed call fails with a named MSAL error. jsdom cannot run a real one.
 */
export function fakeMsalInstance(
  overrides: Partial<IPublicClientApplication> = {},
): IPublicClientApplication {
  return { ...stubbedPublicClientApplication, ...overrides };
}

export function fakeAccount(overrides: Partial<AccountInfo> = {}): AccountInfo {
  return {
    homeAccountId: 'f1e2d3c4-b5a6-4978-8a9b-0c1d2e3f4a5b.9c8b7a65-4321-4f0e-9d8c-7b6a5f4e3d2c',
    environment: 'login.microsoftonline.com',
    tenantId: '9c8b7a65-4321-4f0e-9d8c-7b6a5f4e3d2c',
    localAccountId: 'f1e2d3c4-b5a6-4978-8a9b-0c1d2e3f4a5b',
    username: 'ada.lovelace@example.com',
    name: 'Ada Lovelace',
    ...overrides,
  };
}

export function fakeAuthResult(
  overrides: Partial<AuthenticationResult> = {},
): AuthenticationResult {
  const account = overrides.account ?? fakeAccount();

  return {
    authority: 'https://login.microsoftonline.com/9c8b7a65-4321-4f0e-9d8c-7b6a5f4e3d2c',
    uniqueId: account.localAccountId,
    tenantId: account.tenantId,
    scopes: [testApiScope],
    account,
    idToken: '',
    idTokenClaims: {},
    accessToken: 'access-token',
    fromCache: false,
    expiresOn: null,
    tokenType: 'Bearer',
    correlationId: '00000000-0000-0000-0000-000000000000',
    ...overrides,
  };
}

/** Nobody signed in and no redirect outstanding. */
export function signedOutMsal(
  overrides: Partial<IPublicClientApplication> = {},
): IPublicClientApplication {
  return fakeMsalInstance({
    handleRedirectPromise: () => Promise.resolve(null),
    getAllAccounts: () => [],
    ...overrides,
  });
}

/** An account already in the cache and no redirect outstanding. */
export function signedInMsal(
  overrides: Partial<IPublicClientApplication> = {},
): IPublicClientApplication {
  return fakeMsalInstance({
    handleRedirectPromise: () => Promise.resolve(null),
    getAllAccounts: () => [fakeAccount()],
    acquireTokenSilent: () => Promise.resolve(fakeAuthResult()),
    ...overrides,
  });
}

export function provideFakeMsal(instance: IPublicClientApplication): Provider {
  return { provide: MSAL_INSTANCE, useValue: instance };
}
