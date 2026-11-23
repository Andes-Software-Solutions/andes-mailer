import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { EventType, IPublicClientApplication, InteractionType } from '@azure/msal-browser';
import { SessionStore } from '@core/auth/session-store';
import { eventCallbacks, signedInMsal } from '@testing/msal';
import { meUrl, memberUser, provideSessionTesting } from '@testing/session';
import { vi } from 'vitest';
import { SignInStatus } from './sign-in-status';

describe('SignInStatus', () => {
  async function render(
    instance: IPublicClientApplication = signedInMsal(),
  ): Promise<ComponentFixture<SignInStatus>> {
    TestBed.configureTestingModule({ providers: provideSessionTesting(instance) });

    const fixture = TestBed.createComponent(SignInStatus);
    await fixture.whenStable();

    return fixture;
  }

  function text(fixture: ComponentFixture<SignInStatus>): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('says it is taking the person to Microsoft during the redirect', async () => {
    const events = eventCallbacks();
    const fixture = await render(signedInMsal({ addEventCallback: events.addEventCallback }));

    for (const callback of events.callbacks) {
      callback({
        eventType: EventType.ACQUIRE_TOKEN_START,
        interactionType: InteractionType.Redirect,
        payload: null,
        error: null,
        timestamp: Date.now(),
        correlationId: '00000000-0000-0000-0000-000000000000',
      });
    }
    await fixture.whenStable();

    expect(text(fixture)).toContain('Taking you to Microsoft to sign in…');
    expect(text(fixture)).toContain('Continue to Microsoft');
  });

  it('says it is signing the person in while the account loads', async () => {
    const fixture = await render();

    void TestBed.inject(SessionStore).ensureLoaded();
    await fixture.whenStable();

    expect(text(fixture)).toContain('Signing you in…');
    TestBed.inject(HttpTestingController).expectOne(meUrl).flush(memberUser);
  });

  it('offers a retry when the account cannot load', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const fixture = await render();
    const session = TestBed.inject(SessionStore);
    const backend = TestBed.inject(HttpTestingController);

    const failed = session.ensureLoaded();
    backend.expectOne(meUrl).flush(null, { status: 503, statusText: 'Unavailable' });
    await failed;
    await fixture.whenStable();

    expect(text(fixture)).toContain("We couldn't load your account.");

    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('button.btn-primary')
      ?.click();
    backend.expectOne(meUrl).flush(memberUser);
    await vi.waitFor(() => expect(session.status()).toBe('loaded'));
  });
});
