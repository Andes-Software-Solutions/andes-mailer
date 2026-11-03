import { ComponentFixture, TestBed } from '@angular/core/testing';
import { IPublicClientApplication } from '@azure/msal-browser';
import { provideTestAppConfig } from '@testing/app-config';
import { provideFakeMsal, signedOutMsal } from '@testing/msal';
import { vi } from 'vitest';
import { LoginFailed } from './login-failed';

describe('LoginFailed', () => {
  async function render(instance: IPublicClientApplication): Promise<ComponentFixture<LoginFailed>> {
    TestBed.configureTestingModule({
      providers: [provideTestAppConfig(), provideFakeMsal(instance)],
    });

    const fixture = TestBed.createComponent(LoginFailed);
    await fixture.whenStable();

    return fixture;
  }

  function button(fixture: ComponentFixture<LoginFailed>): HTMLButtonElement | null {
    return (fixture.nativeElement as HTMLElement).querySelector('button');
  }

  beforeEach(() => {
    sessionStorage.clear();
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('retries towards home, not back to this page', async () => {
    const loginRedirect = vi.fn(() => Promise.resolve());
    const fixture = await render(signedOutMsal({ loginRedirect }));

    button(fixture)?.click();
    await fixture.whenStable();

    expect(loginRedirect).toHaveBeenCalledWith(
      expect.objectContaining({ redirectStartPage: new URL('/', document.baseURI).href }),
    );
  });

  it('blocks a second press while the redirect starts', async () => {
    const fixture = await render(
      signedOutMsal({ loginRedirect: () => new Promise<void>(() => undefined) }),
    );

    button(fixture)?.click();
    await fixture.whenStable();

    expect(button(fixture)?.disabled).toBe(true);
  });

  it('tells the user to reload when Microsoft cannot be reached', async () => {
    const fixture = await render(
      signedOutMsal({ loginRedirect: () => Promise.reject(new Error('endpoints_resolution')) }),
    );

    button(fixture)?.click();
    await vi.waitFor(async () => {
      await fixture.whenStable();
      expect((fixture.nativeElement as HTMLElement).querySelector('[role=alert]')).not.toBeNull();
    });

    expect(button(fixture)).toBeNull();
  });
});
