import { ApplicationInitStatus } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  BrowserAuthError,
  CacheLookupPolicy,
  IPublicClientApplication,
  InteractionRequiredAuthError,
} from '@azure/msal-browser';
import { provideTestAppConfig } from '@testing/app-config';
import { fakeAuthResult, provideFakeMsal, signedInMsal, signedOutMsal } from '@testing/msal';
import { vi } from 'vitest';
import { TokenRenewal, provideTokenRenewal } from './token-renewal';

const minutes = (count: number) => count * 60_000;

describe('TokenRenewal', () => {
  let visibility: DocumentVisibilityState;

  function setup(instance: IPublicClientApplication): TokenRenewal {
    TestBed.configureTestingModule({
      providers: [provideTestAppConfig(), provideFakeMsal(instance)],
    });

    return TestBed.inject(TokenRenewal);
  }

  /** Every call hands out a token that expires `lifetime` from the moment it is issued. */
  function silentTokens(lifetime = minutes(60)) {
    return vi.fn<IPublicClientApplication['acquireTokenSilent']>(() =>
      Promise.resolve(fakeAuthResult({ expiresOn: new Date(Date.now() + lifetime) })),
    );
  }

  function policies(acquireTokenSilent: ReturnType<typeof silentTokens>): unknown[] {
    return acquireTokenSilent.mock.calls.map(([request]) => request.cacheLookupPolicy);
  }

  async function showTab(): Promise<void> {
    visibility = 'visible';
    document.dispatchEvent(new Event('visibilitychange'));

    await vi.advanceTimersByTimeAsync(0);
  }

  beforeEach(() => {
    vi.useFakeTimers({
      now: new Date('2026-11-02T09:00:00Z'),
      toFake: ['setTimeout', 'clearTimeout', 'Date'],
    });
    sessionStorage.clear();
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    visibility = 'hidden';
    Object.defineProperty(document, 'visibilityState', {
      configurable: true,
      get: () => visibility,
    });
  });

  afterEach(() => {
    Reflect.deleteProperty(document, 'visibilityState');
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  it('renews 10 minutes before the token expires, then again from the new token', async () => {
    const acquireTokenSilent = silentTokens();
    await setup(signedInMsal({ acquireTokenSilent })).start();

    await vi.advanceTimersByTimeAsync(minutes(50) - 1);
    expect(acquireTokenSilent).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(1);
    await vi.advanceTimersByTimeAsync(minutes(50));

    expect(policies(acquireTokenSilent)).toEqual([
      CacheLookupPolicy.Default,
      CacheLookupPolicy.RefreshTokenAndNetwork,
      CacheLookupPolicy.RefreshTokenAndNetwork,
    ]);
  });

  it('renews a short-lived token halfway through its life', async () => {
    const acquireTokenSilent = silentTokens(minutes(12));
    await setup(signedInMsal({ acquireTokenSilent })).start();

    await vi.advanceTimersByTimeAsync(minutes(6) - 1);
    expect(acquireTokenSilent).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(1);
    expect(acquireTokenSilent).toHaveBeenCalledTimes(2);
  });

  it('waits at least a minute when the token is nearly expired', async () => {
    const acquireTokenSilent = silentTokens(minutes(1));
    await setup(signedInMsal({ acquireTokenSilent })).start();

    await vi.advanceTimersByTimeAsync(minutes(1) - 1);
    expect(acquireTokenSilent).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(1);
    expect(acquireTokenSilent).toHaveBeenCalledTimes(2);
  });

  it('does nothing while nobody is signed in', async () => {
    const acquireTokenSilent = silentTokens();
    await setup(signedOutMsal({ acquireTokenSilent })).start();

    await vi.advanceTimersByTimeAsync(minutes(24 * 60));
    await showTab();

    expect(acquireTokenSilent).not.toHaveBeenCalled();
  });

  it('stops once the app is destroyed', async () => {
    const acquireTokenSilent = silentTokens();
    await setup(signedInMsal({ acquireTokenSilent })).start();

    TestBed.resetTestingModule();
    await vi.advanceTimersByTimeAsync(minutes(24 * 60));
    await showTab();
    window.dispatchEvent(new Event('online'));
    await vi.advanceTimersByTimeAsync(0);

    expect(acquireTokenSilent).toHaveBeenCalledTimes(1);
  });

  it('starts from the app initializer without holding up bootstrap', async () => {
    const acquireTokenSilent = vi.fn<IPublicClientApplication['acquireTokenSilent']>(
      () => new Promise(() => undefined),
    );
    TestBed.configureTestingModule({
      providers: [
        provideTestAppConfig(),
        provideFakeMsal(signedInMsal({ acquireTokenSilent })),
        provideTokenRenewal(),
      ],
    });

    await TestBed.inject(ApplicationInitStatus).donePromise;
    await vi.advanceTimersByTimeAsync(0);

    expect(acquireTokenSilent).toHaveBeenCalledTimes(1);
  });

  it('never redirects on the first renewal, leaving that to the page', async () => {
    const acquireTokenSilent = silentTokens();
    const acquireTokenRedirect = vi.fn(() => Promise.resolve());
    acquireTokenSilent.mockRejectedValueOnce(
      new InteractionRequiredAuthError('interaction_required', 'correlation-id'),
    );
    await setup(signedInMsal({ acquireTokenSilent, acquireTokenRedirect })).start();

    expect(acquireTokenRedirect).not.toHaveBeenCalled();

    acquireTokenSilent.mockRejectedValueOnce(
      new InteractionRequiredAuthError('interaction_required', 'correlation-id'),
    );
    await showTab();

    expect(acquireTokenRedirect).toHaveBeenCalledTimes(1);
  });

  it('ignores the user leaving the tab', async () => {
    const acquireTokenSilent = silentTokens();
    await setup(signedInMsal({ acquireTokenSilent })).start();

    document.dispatchEvent(new Event('visibilitychange'));
    await vi.advanceTimersByTimeAsync(0);

    expect(acquireTokenSilent).toHaveBeenCalledTimes(1);
  });

  it('catches up as soon as the user returns to the tab', async () => {
    const acquireTokenSilent = silentTokens();
    await setup(signedInMsal({ acquireTokenSilent })).start();

    await showTab();

    expect(policies(acquireTokenSilent)).toEqual([
      CacheLookupPolicy.Default,
      CacheLookupPolicy.Default,
    ]);
  });

  it('catches up when the connection comes back', async () => {
    const acquireTokenSilent = silentTokens();
    await setup(signedInMsal({ acquireTokenSilent })).start();

    window.dispatchEvent(new Event('online'));
    await vi.advanceTimersByTimeAsync(0);

    expect(acquireTokenSilent).toHaveBeenCalledTimes(2);
  });

  it('never redirects when the connection comes back, even if Entra needs the user', async () => {
    const acquireTokenSilent = silentTokens();
    const acquireTokenRedirect = vi.fn(() => Promise.resolve());
    await setup(signedInMsal({ acquireTokenSilent, acquireTokenRedirect })).start();
    acquireTokenSilent.mockRejectedValue(
      new InteractionRequiredAuthError('interaction_required', 'correlation-id'),
    );

    window.dispatchEvent(new Event('online'));
    await vi.advanceTimersByTimeAsync(0);

    expect(acquireTokenSilent).toHaveBeenCalledTimes(2);
    expect(acquireTokenRedirect).not.toHaveBeenCalled();
  });

  it('never redirects from the timer, and stops until the user is back', async () => {
    const acquireTokenSilent = silentTokens();
    const acquireTokenRedirect = vi.fn(() => Promise.resolve());
    await setup(signedInMsal({ acquireTokenSilent, acquireTokenRedirect })).start();
    acquireTokenSilent.mockRejectedValue(
      new InteractionRequiredAuthError('interaction_required', 'correlation-id'),
    );

    await vi.advanceTimersByTimeAsync(minutes(24 * 60));

    expect(acquireTokenSilent).toHaveBeenCalledTimes(2);
    expect(acquireTokenRedirect).not.toHaveBeenCalled();
  });

  it('redirects when the user returns and Entra needs them', async () => {
    const acquireTokenSilent = silentTokens();
    const acquireTokenRedirect = vi.fn(() => Promise.resolve());
    await setup(signedInMsal({ acquireTokenSilent, acquireTokenRedirect })).start();
    acquireTokenSilent.mockRejectedValue(
      new InteractionRequiredAuthError('interaction_required', 'correlation-id'),
    );

    await showTab();

    expect(acquireTokenRedirect).toHaveBeenCalledTimes(1);
  });

  it('retries a minute after a temporary failure, such as being offline', async () => {
    const acquireTokenSilent = silentTokens();
    await setup(signedInMsal({ acquireTokenSilent })).start();
    acquireTokenSilent.mockRejectedValueOnce(
      new BrowserAuthError('no_network_connectivity', 'correlation-id'),
    );

    await vi.advanceTimersByTimeAsync(minutes(50));
    expect(acquireTokenSilent).toHaveBeenCalledTimes(2);

    await vi.advanceTimersByTimeAsync(minutes(1));
    expect(policies(acquireTokenSilent).at(-1)).toBe(CacheLookupPolicy.RefreshTokenAndNetwork);
    expect(acquireTokenSilent).toHaveBeenCalledTimes(3);
  });

  it('backs off while the failure persists, up to 15 minutes, and resets once it clears', async () => {
    const acquireTokenSilent = silentTokens();
    await setup(signedInMsal({ acquireTokenSilent })).start();
    acquireTokenSilent.mockRejectedValue(new BrowserAuthError('timed_out', 'correlation-id'));
    await vi.advanceTimersByTimeAsync(minutes(50));

    const calls: number[] = [];
    for (const wait of [1, 2, 4, 8, 15, 15]) {
      await vi.advanceTimersByTimeAsync(minutes(wait) - 1);
      calls.push(acquireTokenSilent.mock.calls.length);
      await vi.advanceTimersByTimeAsync(1);
    }

    expect(calls).toEqual([2, 3, 4, 5, 6, 7]);

    acquireTokenSilent.mockImplementation(() =>
      Promise.resolve(fakeAuthResult({ expiresOn: new Date(Date.now() + minutes(60)) })),
    );
    await vi.advanceTimersByTimeAsync(minutes(15));
    acquireTokenSilent.mockRejectedValue(new BrowserAuthError('timed_out', 'correlation-id'));
    await vi.advanceTimersByTimeAsync(minutes(50));
    const beforeRetry = acquireTokenSilent.mock.calls.length;
    await vi.advanceTimersByTimeAsync(minutes(1));

    expect(acquireTokenSilent).toHaveBeenCalledTimes(beforeRetry + 1);
  });
});
