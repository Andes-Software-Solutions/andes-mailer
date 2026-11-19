import { DOCUMENT } from '@angular/common';
import {
  DestroyRef,
  EnvironmentProviders,
  Provider,
  inject,
  provideAppInitializer,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  MSAL_GUARD_CONFIG,
  MSAL_INSTANCE,
  MSAL_INTERCEPTOR_CONFIG,
  MsalBroadcastService,
  MsalGuard,
  MsalInterceptor,
  MsalService,
} from '@azure/msal-angular';
import {
  AuthenticationResult,
  EventMessage,
  EventType,
  InteractionStatus,
  PublicClientApplication,
} from '@azure/msal-browser';
import { APP_CONFIG } from '@core/config/app-config-token';
import { filter } from 'rxjs';
import { buildGuardConfig, buildInterceptorConfig, buildMsalConfig } from './msal-config';

/**
 * MSAL Angular, wired as Microsoft documents it for standalone apps. MSAL Angular v6 initializes
 * the instance on first use, so no factory has to await it.
 */
export function provideMsal(): (Provider | EnvironmentProviders)[] {
  return [
    {
      provide: MSAL_INSTANCE,
      useFactory: () =>
        new PublicClientApplication(buildMsalConfig(inject(APP_CONFIG), inject(DOCUMENT).baseURI)),
    },
    { provide: MSAL_GUARD_CONFIG, useFactory: () => buildGuardConfig(inject(APP_CONFIG)) },
    {
      provide: MSAL_INTERCEPTOR_CONFIG,
      useFactory: () => buildInterceptorConfig(inject(APP_CONFIG)),
    },
    MsalService,
    MsalGuard,
    MsalBroadcastService,
    MsalInterceptor,
    provideAppInitializer(handleRedirects),
  ];
}

/**
 * What Microsoft puts in a standalone app's root component: process the redirect response on every
 * load, and keep an active account selected for token requests. Not awaited, so it never holds up
 * bootstrap.
 */
function handleRedirects(): void {
  const msal = inject(MsalService);
  // Before the redirect is handled, so its LOGIN_SUCCESS event has a listener.
  const broadcast = inject(MsalBroadcastService);
  const destroyRef = inject(DestroyRef);

  broadcast.msalSubject$
    .pipe(
      filter(
        (message: EventMessage) =>
          message.eventType === EventType.LOGIN_SUCCESS ||
          message.eventType === EventType.ACQUIRE_TOKEN_SUCCESS,
      ),
      takeUntilDestroyed(destroyRef),
    )
    .subscribe((message) => {
      const { account } = message.payload as AuthenticationResult;

      msal.instance.setActiveAccount(account);
    });

  broadcast.inProgress$
    .pipe(
      filter((status) => status === InteractionStatus.None),
      takeUntilDestroyed(destroyRef),
    )
    .subscribe(() => {
      const accounts = msal.instance.getAllAccounts();

      if (!msal.instance.getActiveAccount() && accounts.length > 0) {
        msal.instance.setActiveAccount(accounts[0]);
      }
    });

  msal
    .handleRedirectObservable()
    .pipe(takeUntilDestroyed(destroyRef))
    .subscribe({
      // Logged here only: MsalGuard awaits the same cached response, sends a failure to
      // `loginFailedRoute`, and reports the cause through MSAL's logger with PII, which is off.
      error: (cause: unknown) => console.error(cause),
    });
}
