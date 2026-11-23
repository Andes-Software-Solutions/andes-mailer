import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { IPublicClientApplication } from '@azure/msal-browser';
import { Shell } from '@core/layout/shell';
import { SignInStatus } from '@core/layout/sign-in-status';
import { LoginFailed } from '@features/auth/login-failed';
import { NoAccess } from '@features/auth/no-access';
import { provideTestAppConfig, testApiScope } from '@testing/app-config';
import { provideFakeMsal, signedInMsal, signedOutMsal } from '@testing/msal';
import { adminUser, meUrl, memberUser, noAccessUser, seedSession } from '@testing/session';
import { vi } from 'vitest';
import { routes } from './app.routes';

describe('routes', () => {
  function setup(instance: IPublicClientApplication): Promise<RouterTestingHarness> {
    TestBed.configureTestingModule({
      providers: [
        provideTestAppConfig(),
        provideFakeMsal(instance),
        provideRouter(routes, withComponentInputBinding()),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    return RouterTestingHarness.create();
  }

  function path(): string {
    return TestBed.inject(Router).url;
  }

  /** The page the shell renders, by its selector. */
  function page(harness: RouterTestingHarness, selector: string): Element | null {
    return harness.routeNativeElement?.querySelector(selector) ?? null;
  }

  beforeEach(() => {
    sessionStorage.clear();
    localStorage.clear();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('sends a signed-out visitor to Entra instead of opening home', async () => {
    const loginRedirect = vi.fn(() => Promise.resolve());
    const harness = await setup(signedOutMsal({ loginRedirect }));

    await harness.navigateByUrl('/');

    expect(loginRedirect).toHaveBeenCalledTimes(1);
    expect(loginRedirect).toHaveBeenCalledWith(expect.objectContaining({ scopes: [testApiScope] }));
    expect(harness.routeDebugElement).toBeNull();
  });

  it('loads the account after sign-in, then opens home', async () => {
    const harness = await setup(signedInMsal());
    const backend = TestBed.inject(HttpTestingController);

    const navigation = harness.navigateByUrl('/', Shell);
    await vi.waitFor(() => backend.expectOne(meUrl).flush(memberUser));
    await navigation;

    expect(page(harness, 'app-home')).not.toBeNull();
  });

  it('resumes the page a failed account load cancelled once the retry succeeds', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const harness = await setup(signedInMsal());
    const backend = TestBed.inject(HttpTestingController);

    const navigation = harness.navigateByUrl('/drafts');
    await vi.waitFor(() =>
      backend.expectOne(meUrl).flush(null, { status: 503, statusText: 'Unavailable' }),
    );
    await navigation;
    const splash = TestBed.createComponent(SignInStatus);
    await splash.whenStable();

    (splash.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('button.btn-primary')
      ?.click();
    await vi.waitFor(() => backend.expectOne(meUrl).flush(memberUser));

    await vi.waitFor(async () => {
      await harness.fixture.whenStable();
      expect(path()).toBe('/drafts');
      expect(page(harness, 'app-coming-soon')).not.toBeNull();
    });
  });

  it('sends an account without access to the no-access page', async () => {
    const harness = await setup(signedInMsal());
    seedSession(noAccessUser);

    await expect(harness.navigateByUrl('/', NoAccess)).resolves.toBeInstanceOf(NoAccess);
    expect(path()).toBe('/no-access');
  });

  it('sends an account with access away from the no-access page', async () => {
    const harness = await setup(signedInMsal());
    seedSession(memberUser);

    await harness.navigateByUrl('/no-access', Shell);

    expect(page(harness, 'app-home')).not.toBeNull();
  });

  it('opens a page the active team permits', async () => {
    const harness = await setup(signedInMsal());
    seedSession(memberUser);

    await harness.navigateByUrl('/drafts', Shell);

    expect(page(harness, 'app-coming-soon')?.textContent).toContain('Drafts');
  });

  it('sends a member away from a page their team role does not permit', async () => {
    const harness = await setup(signedInMsal());
    seedSession(memberUser);

    await harness.navigateByUrl('/team/members');

    expect(path()).toBe('/');
  });

  it('opens administration pages for an administrator', async () => {
    const harness = await setup(signedInMsal());
    seedSession(adminUser);

    await harness.navigateByUrl('/team/members', Shell);

    expect(page(harness, 'app-coming-soon')?.textContent).toContain('Team members');
  });

  it('sends a sign-in that came back failed to the failure page', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const harness = await setup(
      signedOutMsal({ handleRedirectPromise: () => Promise.reject(new Error('access_denied')) }),
    );

    await expect(harness.navigateByUrl('/', LoginFailed)).resolves.toBeInstanceOf(LoginFailed);
  });

  it('never guards the failure page, which would send the user straight back to Entra', async () => {
    const loginRedirect = vi.fn(() => Promise.resolve());
    const harness = await setup(signedOutMsal({ loginRedirect }));

    await expect(harness.navigateByUrl('/login-failed', LoginFailed)).resolves.toBeInstanceOf(
      LoginFailed,
    );
    expect(loginRedirect).not.toHaveBeenCalled();
  });

  it('sends an unknown address home', async () => {
    const harness = await setup(signedInMsal());
    seedSession(memberUser);

    await harness.navigateByUrl('/nowhere', Shell);

    expect(path()).toBe('/');
    expect(page(harness, 'app-home')).not.toBeNull();
  });
});
