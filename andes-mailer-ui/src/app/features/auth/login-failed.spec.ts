import { ComponentFixture, TestBed } from '@angular/core/testing';
import { IPublicClientApplication } from '@azure/msal-browser';
import { signedOutMsal } from '@testing/msal';
import { provideSessionTesting } from '@testing/session';
import { vi } from 'vitest';
import { LoginFailed } from './login-failed';

describe('LoginFailed', () => {
  async function render(
    instance: IPublicClientApplication,
  ): Promise<ComponentFixture<LoginFailed>> {
    TestBed.configureTestingModule({ providers: provideSessionTesting(instance) });

    const fixture = TestBed.createComponent(LoginFailed);
    await fixture.whenStable();

    return fixture;
  }

  function element(fixture: ComponentFixture<LoginFailed>): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function button(fixture: ComponentFixture<LoginFailed>): HTMLButtonElement | null {
    return element(fixture).querySelector('button');
  }

  beforeEach(() => {
    sessionStorage.clear();
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('says the sign-in failed', async () => {
    const fixture = await render(signedOutMsal());

    expect(element(fixture).querySelector('[role=alert]')?.textContent).toContain(
      "We couldn't sign you in.",
    );
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
      expect(element(fixture).textContent).toContain("We couldn't reach Microsoft.");
    });

    expect(button(fixture)).toBeNull();
  });
});
