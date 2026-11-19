import { ApplicationInitStatus } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MsalBroadcastService } from '@azure/msal-angular';
import {
  EventMessage,
  EventType,
  IPublicClientApplication,
  InteractionStatus,
} from '@azure/msal-browser';
import { provideTestAppConfig } from '@testing/app-config';
import {
  eventCallbacks,
  fakeAccount,
  fakeAuthResult,
  provideFakeMsal,
  signedInMsal,
  signedOutMsal,
} from '@testing/msal';
import { filter, firstValueFrom } from 'rxjs';
import { vi } from 'vitest';

describe('provideMsal', () => {
  async function setup(instance: IPublicClientApplication): Promise<void> {
    TestBed.configureTestingModule({
      providers: [provideTestAppConfig(), provideFakeMsal(instance)],
    });

    await TestBed.inject(ApplicationInitStatus).donePromise;
    await firstValueFrom(
      TestBed.inject(MsalBroadcastService).inProgress$.pipe(
        filter((status) => status === InteractionStatus.None),
      ),
    );
  }

  function event(eventType: EventType, payload: EventMessage['payload']): EventMessage {
    return {
      eventType,
      interactionType: null,
      payload,
      error: null,
      correlationId: '',
      timestamp: 0,
    };
  }

  it('handles the redirect response on load', async () => {
    const handleRedirectPromise = vi.fn(() => Promise.resolve(null));

    await setup(signedOutMsal({ handleRedirectPromise }));

    expect(handleRedirectPromise).toHaveBeenCalled();
  });

  it('selects the cached account once no interaction is in progress', async () => {
    const instance = signedInMsal();

    await setup(instance);

    expect(instance.getActiveAccount()).toEqual(fakeAccount());
  });

  it('selects the account a sign-in returns', async () => {
    const { callbacks, addEventCallback } = eventCallbacks();
    const instance = signedOutMsal({ addEventCallback });
    const signedIn = fakeAccount({ username: 'grace.hopper@example.com' });
    await setup(instance);

    for (const callback of callbacks) {
      callback(event(EventType.LOGIN_SUCCESS, fakeAuthResult({ account: signedIn })));
    }

    expect(instance.getActiveAccount()).toEqual(signedIn);
  });

  it('logs a failed redirect response and keeps going, so the guard can report it', async () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const cause = new Error('access_denied');

    await expect(
      setup(signedOutMsal({ handleRedirectPromise: () => Promise.reject(cause) })),
    ).resolves.toBeUndefined();
    expect(consoleError).toHaveBeenCalledWith(cause);

    consoleError.mockRestore();
  });
});
