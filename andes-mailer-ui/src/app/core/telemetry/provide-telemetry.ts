import { DOCUMENT } from '@angular/common';
import {
  DestroyRef,
  EnvironmentProviders,
  ErrorHandler,
  Provider,
  inject,
  provideAppInitializer,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRouteSnapshot, NavigationEnd, Router } from '@angular/router';
import { MsalBroadcastService, MsalService } from '@azure/msal-angular';
import {
  AccountInfo,
  AuthenticationResult,
  EventMessage,
  EventType,
  InteractionStatus,
} from '@azure/msal-browser';
import { APP_CONFIG } from '@core/config/app-config-token';
import { Observable, filter, map } from 'rxjs';
import { TELEMETRY_LOADER, Telemetry, TelemetryClient } from './telemetry';
import { enrichTelemetry, withoutQuery } from './telemetry-enrichment';
import { TelemetryErrorHandler } from './telemetry-error-handler';

/** The custom event an interactive sign-in records. */
export const SIGNED_IN_EVENT = 'UserSignedIn';

/** Past this, the app starts without telemetry rather than wait on a blocked or wedged host. */
const LOAD_TIMEOUT_MS = 5_000;

/**
 * Application Insights for the whole app: page views per route, the signed-in user on every item,
 * API calls tagged with the screen that made them, and errors. Does nothing beyond swapping in the
 * error handler when `config.json` has no `telemetry` section. Needs `provideMsal()`.
 */
export function provideTelemetry(): (Provider | EnvironmentProviders)[] {
  return [{ provide: ErrorHandler, useClass: TelemetryErrorHandler }, provideAppInitializer(start)];
}

/**
 * Awaited, so the first navigation and the API calls it makes are tracked. Everything is injected
 * before the first `await`, which ends the injection context.
 */
async function start(): Promise<void> {
  const config = inject(APP_CONFIG).telemetry;

  if (!config) {
    return;
  }

  const load = inject(TELEMETRY_LOADER);
  const context: StartContext = {
    telemetry: inject(Telemetry),
    router: inject(Router),
    msal: inject(MsalService),
    broadcast: inject(MsalBroadcastService),
    baseUri: inject(DOCUMENT).baseURI,
    destroyRef: inject(DestroyRef),
  };

  // msalSubject$ replays nothing, and provideMsal() may finish the redirect while the SDK loads.
  const earlySignIns: AccountInfo[] = [];
  const early = signIns(context.broadcast).subscribe((account) => earlySignIns.push(account));
  let client: TelemetryClient;

  try {
    client = await withTimeout(load(config), LOAD_TIMEOUT_MS);
    client.loadAppInsights();
  } catch (cause: unknown) {
    console.warn('Telemetry is off: Application Insights could not start.', cause);
    return;
  } finally {
    early.unsubscribe();
  }

  client.addTelemetryInitializer((item) => {
    enrichTelemetry(item, currentRoute(context.router.routerState.snapshot.root).template);
  });
  context.telemetry.connect(client);
  for (const account of earlySignIns) {
    recordSignIn(context.telemetry, account);
  }

  trackPageViews(context);
  trackSignedInUser(context);
}

interface StartContext {
  readonly telemetry: Telemetry;
  readonly router: Router;
  readonly msal: MsalService;
  readonly broadcast: MsalBroadcastService;
  readonly baseUri: string;
  readonly destroyRef: DestroyRef;
}

function trackPageViews({ telemetry, router, baseUri, destroyRef }: StartContext): void {
  router.events
    .pipe(
      filter((event) => event instanceof NavigationEnd),
      takeUntilDestroyed(destroyRef),
    )
    .subscribe((event) => {
      const { template, title } = currentRoute(router.routerState.snapshot.root);
      // Without the query string, which can carry what the user searched for.
      const uri = new URL(withoutQuery(event.urlAfterRedirects).replace(/^\//, ''), baseUri).href;

      telemetry.trackPageView(title ?? template, uri, template);
    });
}

function trackSignedInUser({ telemetry, msal, broadcast, destroyRef }: StartContext): void {
  // Covers a session MSAL restores from its cache, which raises no sign-in event.
  broadcast.inProgress$
    .pipe(
      filter((status) => status === InteractionStatus.None),
      takeUntilDestroyed(destroyRef),
    )
    .subscribe(() => {
      const account = msal.instance.getActiveAccount() ?? msal.instance.getAllAccounts()[0];

      if (account) {
        telemetry.setUser(objectId(account));
      } else {
        telemetry.clearUser();
      }
    });

  signIns(broadcast)
    .pipe(takeUntilDestroyed(destroyRef))
    .subscribe((account) => recordSignIn(telemetry, account));

  broadcast.msalSubject$
    .pipe(
      filter((message: EventMessage) => message.eventType === EventType.LOGOUT_SUCCESS),
      takeUntilDestroyed(destroyRef),
    )
    .subscribe(() => telemetry.clearUser());
}

/** The accounts interactive sign-ins return. */
function signIns(broadcast: MsalBroadcastService): Observable<AccountInfo> {
  return broadcast.msalSubject$.pipe(
    filter((message: EventMessage) => message.eventType === EventType.LOGIN_SUCCESS),
    map((message) => (message.payload as AuthenticationResult).account),
  );
}

function recordSignIn(telemetry: Telemetry, account: AccountInfo): void {
  telemetry.setUser(objectId(account));
  telemetry.trackEvent(SIGNED_IN_EVENT);
}

/** The Entra object id, the API's user key; never the name or the username, which are personal data. */
function objectId(account: AccountInfo): string {
  const oid = account.idTokenClaims?.oid;

  return typeof oid === 'string' && oid !== '' ? oid : account.localAccountId;
}

/** The deepest route's path template (`/teams/:teamId/emails`) and resolved title. */
function currentRoute(root: ActivatedRouteSnapshot): { template: string; title?: string } {
  const segments: string[] = [];
  let route = root;

  while (route.firstChild) {
    route = route.firstChild;

    if (route.routeConfig?.path) {
      segments.push(route.routeConfig.path);
    }
  }

  return { template: `/${segments.join('/')}`, title: route.title };
}

function withTimeout<T>(work: Promise<T>, ms: number): Promise<T> {
  let timer: ReturnType<typeof setTimeout> | undefined;
  const timeout = new Promise<never>((_, reject) => {
    timer = setTimeout(() => reject(new Error(`Timed out after ${ms} ms.`)), ms);
  });

  return Promise.race([work, timeout]).finally(() => clearTimeout(timer));
}
