import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { IPublicClientApplication } from '@azure/msal-browser';
import { LoginFailed } from '@features/auth/login-failed';
import { Home } from '@features/home/home';
import { provideTestAppConfig } from '@testing/app-config';
import { provideFakeMsal, signedInMsal, signedOutMsal } from '@testing/msal';
import { vi } from 'vitest';
import { routes } from './app.routes';

describe('routes', () => {
  function setup(instance: IPublicClientApplication): Promise<RouterTestingHarness> {
    TestBed.configureTestingModule({
      providers: [
        provideTestAppConfig(),
        provideFakeMsal(instance),
        provideRouter(routes),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    return RouterTestingHarness.create();
  }

  beforeEach(() => {
    sessionStorage.clear();
  });

  it('sends a signed-out visitor to Entra instead of opening home', async () => {
    const loginRedirect = vi.fn(() => Promise.resolve());
    const harness = await setup(signedOutMsal({ loginRedirect }));

    await harness.navigateByUrl('/');

    expect(loginRedirect).toHaveBeenCalledTimes(1);
    expect(harness.routeDebugElement).toBeNull();
  });

  it('opens home for a signed-in user', async () => {
    const harness = await setup(signedInMsal());

    await expect(harness.navigateByUrl('/', Home)).resolves.toBeInstanceOf(Home);
  });

  it('never guards the failure page, which would send the user straight back to Entra', async () => {
    const loginRedirect = vi.fn(() => Promise.resolve());
    const harness = await setup(signedOutMsal({ loginRedirect }));

    await expect(harness.navigateByUrl('/login-failed', LoginFailed)).resolves.toBeInstanceOf(
      LoginFailed,
    );
    expect(loginRedirect).not.toHaveBeenCalled();
  });
});
