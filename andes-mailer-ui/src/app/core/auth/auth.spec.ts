import { TestBed } from '@angular/core/testing';
import {
  BrowserAuthError,
  CacheLookupPolicy,
  IPublicClientApplication,
  InteractionRequiredAuthError,
} from '@azure/msal-browser';
import { testApiScope, provideTestAppConfig } from '@testing/app-config';
import {
  fakeAccount,
  fakeAuthResult,
  provideFakeMsal,
  signedInMsal,
  signedOutMsal,
} from '@testing/msal';
import { vi } from 'vitest';
import { Auth } from './auth';

describe('Auth', () => {
  function setup(instance: IPublicClientApplication): Auth {
    TestBed.configureTestingModule({
      providers: [provideTestAppConfig(), provideFakeMsal(instance)],
    });

    return TestBed.inject(Auth);
  }

  beforeEach(() => {
    sessionStorage.clear();
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('ready', () => {
    it('resolves the redirect once however many callers ask', async () => {
      const handleRedirectPromise = vi.fn(() => Promise.resolve(null));
      const auth = setup(signedOutMsal({ handleRedirectPromise }));

      await Promise.all([auth.ready(), auth.ready()]);

      expect(handleRedirectPromise).toHaveBeenCalledTimes(1);
    });

    it('returns the account a redirect brought back', async () => {
      const account = fakeAccount({ name: 'Grace Hopper' });
      const auth = setup(
        signedOutMsal({
          handleRedirectPromise: () => Promise.resolve(fakeAuthResult({ account })),
        }),
      );

      await expect(auth.ready()).resolves.toEqual({ account, failed: false });
    });

    it('falls back to a cached account when no redirect is outstanding', async () => {
      const auth = setup(signedInMsal());

      await expect(auth.ready()).resolves.toEqual({ account: fakeAccount(), failed: false });
    });

    it('reports a failed redirect instead of rejecting', async () => {
      const auth = setup(
        signedOutMsal({ handleRedirectPromise: () => Promise.reject(new Error('access_denied')) }),
      );

      await expect(auth.ready()).resolves.toEqual({ account: null, failed: true });
    });

    it('clears the sign-in attempt once an account is back', async () => {
      sessionStorage.setItem('andes-mailer.signInAttempt', 'true');
      const auth = setup(signedInMsal());

      await auth.ready();

      expect(auth.signInAttempted).toBe(false);
    });

    it('keeps the sign-in attempt when no account came back', async () => {
      sessionStorage.setItem('andes-mailer.signInAttempt', 'true');
      const auth = setup(signedOutMsal());

      await auth.ready();

      expect(auth.signInAttempted).toBe(true);
    });
  });

  describe('signIn', () => {
    it('redirects for the API scope, returning to the requested URL', async () => {
      const loginRedirect = vi.fn(() => Promise.resolve());
      const auth = setup(signedOutMsal({ loginRedirect }));

      await auth.signIn('/settings?tab=profile');

      expect(loginRedirect).toHaveBeenCalledWith({
        scopes: [testApiScope],
        redirectStartPage: new URL('/settings?tab=profile', document.baseURI).href,
      });
    });

    it('records the attempt', async () => {
      const auth = setup(signedOutMsal({ loginRedirect: () => Promise.resolve() }));

      await auth.signIn('/');

      expect(auth.signInAttempted).toBe(true);
    });

    it('resolves any outstanding redirect first, releasing its lock', async () => {
      const calls: string[] = [];
      const auth = setup(
        signedOutMsal({
          handleRedirectPromise: () => {
            calls.push('handleRedirectPromise');

            return Promise.resolve(null);
          },
          loginRedirect: () => {
            calls.push('loginRedirect');

            return Promise.resolve();
          },
        }),
      );

      await auth.signIn('/');

      expect(calls).toEqual(['handleRedirectPromise', 'loginRedirect']);
    });
  });

  describe('getAccessToken', () => {
    it('returns a silently acquired token for the API scope', async () => {
      const acquireTokenSilent = vi.fn(() => Promise.resolve(fakeAuthResult()));
      const auth = setup(signedInMsal({ acquireTokenSilent }));

      await expect(auth.getAccessToken()).resolves.toBe('access-token');
      expect(acquireTokenSilent).toHaveBeenCalledWith({
        scopes: [testApiScope],
        account: fakeAccount(),
        cacheLookupPolicy: CacheLookupPolicy.Default,
      });
    });

    it('skips the cached token when asked to refresh', async () => {
      const acquireTokenSilent = vi.fn(() => Promise.resolve(fakeAuthResult()));
      const auth = setup(signedInMsal({ acquireTokenSilent }));

      await auth.getAccessToken({ refresh: true });

      expect(acquireTokenSilent).toHaveBeenCalledWith(
        expect.objectContaining({ cacheLookupPolicy: CacheLookupPolicy.RefreshTokenAndNetwork }),
      );
    });

    it('rejects when nobody is signed in', async () => {
      const auth = setup(signedOutMsal());

      await expect(auth.getAccessToken()).rejects.toThrow('No account is signed in.');
    });

    it('redirects when Entra needs the user, and still rejects this call', async () => {
      const failure = new InteractionRequiredAuthError('interaction_required', 'correlation-id');
      const acquireTokenRedirect = vi.fn(() => Promise.resolve());
      const auth = setup(
        signedInMsal({
          acquireTokenSilent: () => Promise.reject(failure),
          acquireTokenRedirect,
        }),
      );

      await expect(auth.getAccessToken()).rejects.toBe(failure);
      expect(acquireTokenRedirect).toHaveBeenCalledTimes(1);
    });

    it('does not redirect for any other failure', async () => {
      const failure = new BrowserAuthError('timed_out', 'correlation-id');
      const acquireTokenRedirect = vi.fn(() => Promise.resolve());
      const auth = setup(
        signedInMsal({
          acquireTokenSilent: () => Promise.reject(failure),
          acquireTokenRedirect,
        }),
      );

      await expect(auth.getAccessToken()).rejects.toBe(failure);
      expect(acquireTokenRedirect).not.toHaveBeenCalled();
    });
  });

  describe('acquireToken', () => {
    it('returns the whole result, so a caller can read when it expires', async () => {
      const result = fakeAuthResult({ expiresOn: new Date('2026-11-02T10:00:00Z') });
      const auth = setup(signedInMsal({ acquireTokenSilent: () => Promise.resolve(result) }));

      await expect(auth.acquireToken()).resolves.toBe(result);
    });

    it('never redirects when not interactive, even if Entra needs the user', async () => {
      const failure = new InteractionRequiredAuthError('interaction_required', 'correlation-id');
      const acquireTokenRedirect = vi.fn(() => Promise.resolve());
      const auth = setup(
        signedInMsal({
          acquireTokenSilent: () => Promise.reject(failure),
          acquireTokenRedirect,
        }),
      );

      await expect(auth.acquireToken({ interactive: false })).rejects.toBe(failure);
      expect(acquireTokenRedirect).not.toHaveBeenCalled();
    });
  });
});
