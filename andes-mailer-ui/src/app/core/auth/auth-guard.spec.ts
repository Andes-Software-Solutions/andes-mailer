import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { IPublicClientApplication } from '@azure/msal-browser';
import { provideTestAppConfig } from '@testing/app-config';
import { provideFakeMsal, signedInMsal, signedOutMsal } from '@testing/msal';
import { vi } from 'vitest';
import { authGuard } from './auth-guard';

describe('authGuard', () => {
  function run(instance: IPublicClientApplication, url = '/') {
    TestBed.configureTestingModule({
      providers: [provideTestAppConfig(), provideFakeMsal(instance), provideRouter([])],
    });

    return TestBed.runInInjectionContext(() =>
      authGuard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot),
    ) as Promise<boolean | UrlTree>;
  }

  function loginFailed(): string {
    return TestBed.inject(Router).serializeUrl(TestBed.inject(Router).parseUrl('/login-failed'));
  }

  beforeEach(() => {
    sessionStorage.clear();
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('activates for a signed-in account', async () => {
    await expect(run(signedInMsal())).resolves.toBe(true);
  });

  it('goes to Entra with no login screen when nobody is signed in', async () => {
    const loginRedirect = vi.fn(() => Promise.resolve());

    await expect(run(signedOutMsal({ loginRedirect }))).resolves.toBe(false);

    expect(loginRedirect).toHaveBeenCalledTimes(1);
  });

  it('keeps the requested URL across the redirect', async () => {
    const loginRedirect = vi.fn(() => Promise.resolve());

    await run(signedOutMsal({ loginRedirect }), '/settings?tab=profile');

    expect(loginRedirect).toHaveBeenCalledWith(
      expect.objectContaining({
        redirectStartPage: new URL('/settings?tab=profile', document.baseURI).href,
      }),
    );
  });

  it('stops at the failure page when the redirect came back failed', async () => {
    const loginRedirect = vi.fn(() => Promise.resolve());

    const result = await run(
      signedOutMsal({
        handleRedirectPromise: () => Promise.reject(new Error('access_denied')),
        loginRedirect,
      }),
    );

    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe(loginFailed());
    expect(loginRedirect).not.toHaveBeenCalled();
  });

  it('stops at the failure page instead of redirecting twice', async () => {
    sessionStorage.setItem('andes-mailer.signInAttempt', 'true');
    const loginRedirect = vi.fn(() => Promise.resolve());

    const result = await run(signedOutMsal({ loginRedirect }));

    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe(loginFailed());
    expect(loginRedirect).not.toHaveBeenCalled();
  });

  it('stops at the failure page when the redirect cannot start', async () => {
    const result = await run(
      signedOutMsal({ loginRedirect: () => Promise.reject(new Error('endpoints_resolution')) }),
    );

    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe(loginFailed());
  });
});
