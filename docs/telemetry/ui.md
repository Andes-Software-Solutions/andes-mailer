# UI telemetry

The API's telemetry shows what the server did. The UI's shows what the person saw: which screen they were on, which call it made, and what broke in the browser. Because the UI starts a trace for each screen and sends its id to the API, the two halves join up. This page explains how the UI side works, how to configure it, and how a feature reports its own events. For the setup both apps share, see the [overview](README.md). For lookups, see [Queries](queries.md).

## How it fits together

The UI uses `@microsoft/applicationinsights-web` 3.4.6. Everything lives in `andes-mailer-ui/src/app/core/telemetry/`:

| File | Holds |
| --- | --- |
| `provide-telemetry.ts` | `provideTelemetry()`, the awaited startup, page views, and the signed-in user |
| `telemetry.ts` | The `Telemetry` facade service and the `TELEMETRY_LOADER` token |
| `application-insights.ts` | Loads the SDK as a lazy chunk and sets its configuration |
| `telemetry-enrichment.ts` | The telemetry initializer: role name, page, API operation, query-string stripping |
| `telemetry-error-handler.ts` | `TelemetryErrorHandler`, Angular's `ErrorHandler` with a report to Application Insights |

`provideTelemetry()` is registered in `app.config.ts` after `provideMsal()`, because it reads the MSAL account.

Feature code reports only through the `Telemetry` service. It never imports the SDK.

## Configuration

Telemetry is on when `public/config.json` has a `telemetry` section:

```json
{
  "auth": { "clientId": "...", "authority": "...", "apiScopes": ["..."] },
  "telemetry": {
    "connectionString": "InstrumentationKey=<guid>;IngestionEndpoint=https://<region>.in.applicationinsights.azure.com/;LiveEndpoint=..."
  }
}
```

- The section is optional. Without it, `provideTelemetry()` swaps in the error handler and does nothing else, and every `Telemetry` call is a no-op.
- `loadAppConfig` (`core/config/load-app-config.ts`) validates it. A section whose `connectionString` has no `InstrumentationKey=` part fails startup with "telemetry.connectionString must be the Application Insights resource's connection string."
- The committed `config.json` has no `telemetry` section, so telemetry is off locally until you add one. Do not commit it.
- The connection string is public by nature in a browser. It allows sending items, not reading them.

## How startup works

The SDK is large, so it loads as a chunk of its own through a dynamic `import()`. A deployment without telemetry never downloads it, and the initial bundle stays under its budget (warning at 900 kB, error at 1 MB).

An awaited app initializer waits for the chunk, up to 5 seconds, so the first navigation and the API calls it makes are tracked. If the chunk fails to load or the wait runs out, the app starts without telemetry and logs a `console.warn`: "Telemetry is off: Application Insights could not start."

While it waits, the initializer collects any sign-in MSAL completes, so a redirect that finishes during the load still records `UserSignedIn`.

## What gets recorded

| What | How | Where to find it |
| --- | --- | --- |
| Page views | A `NavigationEnd` event | `pageViews` |
| The signed-in user | `setAuthenticatedUserContext` | `user_AuthenticatedId` on every item |
| Sign-ins | The custom event `UserSignedIn` | `customEvents` |
| API calls | Fetch and XHR, collected by the SDK | `dependencies` |
| Errors | `TelemetryErrorHandler` | `exceptions` |
| Anything a feature adds | `Telemetry.trackEvent` | `customEvents` |

All items carry `cloud_RoleName` = `andes-mailer-ui`.

### Page views

A page view is recorded on every `NavigationEnd`:

- The **name** is the route's `title`, or the route path template when the route has none.
- `customDimensions.route` is the route path template, such as `/teams/:teamId`, so every team's page groups as one.
- The **URI** excludes the query string, which can carry what the user searched for.
- Each page view **starts a new operation id**. The API calls the screen makes carry it in `traceparent`, so they share `operation_Id` with the API's `requests`. The id is a random W3C trace id.
- `autoTrackPageVisitTime` is on. The SDK's own route tracking is off (`enableAutoRouteTracking: false`), because the router events are the source of truth.

A route change in a single-page app can report a `duration` of 0 on `pageViews`; that is how Application Insights treats SPA route changes, not a fault.

### The signed-in user

The user is set to the Entra object id from the MSAL account, `idTokenClaims.oid`, with `localAccountId` as the fallback. Never the name or the username.

- It is set when the UI restores a session (MSAL reports `InteractionStatus.None` with an account) and on `LOGIN_SUCCESS`.
- It is cleared when MSAL reports no account. The UI has no sign-out, so nothing in the app raises `LOGOUT_SUCCESS`; the listener for it in `provide-telemetry.ts` is dormant.
- An interactive sign-in also records the custom event `UserSignedIn`. A restored session records none, so `UserSignedIn` counts sign-ins, not active users.
- The user context is stored in a cookie (`setAuthenticatedUserContext(oid, undefined, true)`), so items sent before the next account check still carry the user.

The SDK sets three cookies: `ai_user` (anonymous id), `ai_session`, and `ai_authUser` (the authenticated id).

### API calls

The SDK collects every fetch and XHR call as a dependency. A telemetry initializer (`enrichTelemetry`) then, for each one:

- adds `customDimensions.page`, the route template on screen when the SDK reports the call;
- adds `customDimensions.apiOperation`, the method and path with every GUID replaced by `{id}`, so every send of every draft groups as one: `POST /api/teams/{id}/emails/{id}/send`;
- strips the query string from the dependency's `name` and `target`. The directory search puts what the user typed, often an email address, in its query.

It also sets `ai.cloud.role` to `andes-mailer-ui` on every item.

The SDK runs in W3C distributed tracing mode only (`DistributedTracingModes.W3C`), because the API's OpenTelemetry reads nothing else. Request and response header tracking are off.

### Errors

`TelemetryErrorHandler` replaces Angular's `ErrorHandler`. It calls `trackException`, then logs to the console as Angular's default does. `provideBrowserGlobalErrorListeners` forwards uncaught errors and unhandled rejections to it, so the SDK's own exception hooks are off (`disableExceptionTracking: true`) and nothing is reported twice.

The service reports by kind:

| Thrown | Reported as |
| --- | --- |
| An `Error` | As is |
| An `HttpErrorResponse` | `HTTP <status> <apiOperation>`, named `HttpErrorResponse`, with no query string |
| A string | The string |
| Anything else | `Non-error thrown: [object ...]` |

## Add a custom event

Inject the facade and record what the user did:

```typescript
import { inject } from '@angular/core';
import { Telemetry } from '@core/telemetry/telemetry';

export class SendEmailButton {
  private readonly telemetry = inject(Telemetry);

  protected onSent(): void {
    this.telemetry.trackEvent('EmailSent', { recipientCount: '12' });
  }
}
```

- Keep event names few and stable, such as `EmailSent`, so they group.
- Properties are strings. Keep them free of personal data: ids, counts, and flags are fine; names, email addresses, subjects, and anything the user typed are not.
- The call is a no-op when telemetry is off, so you never check first.

## Known limitations

- **Cross-origin calls are not correlated.** The SDK adds `traceparent` only to same-origin calls unless CORS correlation is on, and it is not. The UI's relative `/api/...` URLs are same-origin, which the dev proxy keeps true locally. If a deployment hosts the API on another origin, correlation needs `enableCorsCorrelation` and a CORS policy that allows the header.
- **Items sent before the account is known have no user.** For example, while a redirect to Entra completes. `ai_user` still identifies the browser.
- **A slow or blocked SDK means no telemetry for that visit.** The wait is 5 seconds, and the app does not retry. Browser extensions that block analytics can also drop what the SDK sends.
- **`page` is read when the SDK reports the call.** The SDK reports a call when it completes, so a call that outlives a navigation is tagged with the screen the user moved to, not the one that started it.
- **Only a few events exist.** Page views, `UserSignedIn`, API calls, and errors. Features add their own.
- **The cookies need a decision.** Telemetry sets `ai_user`, `ai_session`, and `ai_authUser`. If your deployment needs cookie consent, telemetry has none built in.

## Testing

`npx ng test --watch=false` from `andes-mailer-ui/` covers it without the SDK. The specs are `provide-telemetry.spec.ts` and `telemetry-enrichment.spec.ts` in `src/app/core/telemetry/`, and `load-app-config.spec.ts` for the config check.

- `src/testing/telemetry.ts` holds `fakeTelemetryClient()`, a client whose every call is a spy and which keeps the initializers and the trace context the app touched, and `provideFakeTelemetryClient(load)`, which hands `provideTelemetry()` the fake through the `TELEMETRY_LOADER` token.
- `src/testing/app-config.ts` holds `testTelemetryConfig`. `provideTestAppConfig` takes a config, so a spec turns telemetry on with `provideTestAppConfig({ ...testAppConfig, telemetry: testTelemetryConfig })`.
- A spec that injects `Telemetry` and does not connect a client gets the no-op behavior.
- `provideTelemetry()` needs the MSAL providers around it. Use `provideFakeMsal` from `src/testing/msal.ts`.
