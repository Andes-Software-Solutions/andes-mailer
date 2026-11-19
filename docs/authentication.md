# Authentication

Both apps sign users in with Microsoft Entra ID. The UI has no login screen: a signed-out user goes straight to Microsoft, and the API accepts only tokens that carry the `access_as_user` scope. This page explains how the pieces fit, what the Entra app registration and the host must provide, and what is still missing.

## How sign-in works

The UI uses [`@azure/msal-angular`](https://learn.microsoft.com/en-us/entra/msal/angular/) (6.3.3) on top of `@azure/msal-browser` (5.30.0), wired the way Microsoft documents it for standalone Angular apps. That keeps the app on a path Microsoft maintains and tests, instead of a hand-written layer that had to track MSAL's internals.

`provideMsal()` (`src/app/core/auth/provide-msal.ts`) is the one place that sets it up, and `app.config.ts` spreads it into the providers. It registers:

- `MSAL_INSTANCE`, a factory that builds the `PublicClientApplication` from `config.json` and the page's base URI (`src/app/core/auth/msal-config.ts`). Tokens live in `sessionStorage`, so they do not outlive the tab. MSAL's logger writes warnings and errors to the console; PII logging stays off.
- `MSAL_GUARD_CONFIG`: redirect interaction, the API scopes from `config.json`, and `loginFailedRoute` set to `/login-failed`.
- `MSAL_INTERCEPTOR_CONFIG`: redirect interaction and a `protectedResourceMap` that holds `/api/*` only, with `strictMatching: true`.
- `MsalService`, `MsalGuard`, `MsalBroadcastService`, and `MsalInterceptor`.
- An app initializer that handles the redirect response; see [What the initializer does](#what-the-initializer-does).

The sequence for a signed-out user:

1. The user opens a guarded route. `authGuard` (`src/app/core/auth/auth-guard.ts`), a functional wrapper around `MsalGuard`, finds no account and starts an MSAL `loginRedirect` to Entra. The router stays put while the browser leaves.
2. Entra returns the browser to `<origin>/auth`. That path has no Angular route.
3. `src/main.ts` checks the URL before Angular boots. On `/auth` it runs MSAL's redirect bridge (`broadcastResponseToMainFrame`), which sends the browser back to the page that started the sign-in. The same code serves the hidden iframe of a silent token renewal, where the bridge posts the response to the waiting main frame. If `/auth` carries no response (for example, someone opened it directly), the app logs the error and navigates to `/login-failed`.
4. On the starting page, `main.ts` loads `config.json` and boots Angular. It does not create or initialize MSAL; MSAL Angular initializes the instance on first use. The app initializer processes the response, the guard sees the account, and the route loads.
5. `authInterceptor` (`src/app/core/auth/auth-interceptor.ts`), a functional wrapper around `MsalInterceptor`, adds `Authorization: Bearer <token>` to requests that match `protectedResourceMap`, which means URLs under `/api/`. Requests to `config.json` or any other host never get a token. If Entra needs interaction, MSAL starts `acquireTokenRedirect`.

Why the bridge still runs first: the router could rewrite the hash the bridge reads, and a hidden renewal iframe has no use for the rest of the app.

### What the initializer does

The initializer in `provideMsal()` is what Microsoft puts in a standalone app's root component. Angular does not await it, so it never holds up bootstrap. It does three things:

- It subscribes to `handleRedirectObservable()` so the redirect response is processed on every page load. A failure is logged with `console.error`. `MsalGuard` awaits the same response and sends the user to `/login-failed`.
- It selects the active account on `LOGIN_SUCCESS` and `ACQUIRE_TOKEN_SUCCESS`.
- It selects the first cached account when `inProgress$` reaches `None` and none is active, such as on a reload of a signed-in tab.

### The login-failed page

`/login-failed` is the only sign-in screen and the only unguarded route; keep every other route behind `authGuard`. The guard sends users there when sign-in fails, and the redirect bridge does the same for a bare `/auth`. Its "Try again" button calls `MsalService.loginRedirect` and lands on the home page. If that call rejects, the page shows an error and only a reload can try again, because MSAL has no API to release its redirect lock.

### Back from the Microsoft page

MSAL Angular has no limit on automatic sign-in attempts. The UI used to count one attempt per trip with a `sessionStorage` flag; that flag was removed to follow Microsoft's behavior. The consequences:

- A user who abandons the Entra page and presses Back can be sent to Entra again.
- If the browser restores the page from its back-forward cache, `loginRedirect` rejects with `user_cancelled` and the guard sends the user to `/login-failed`.

Neither case loops, but expect the second Entra visit.

## Token renewal

Access tokens are short-lived, and MSAL does not renew them in the background. Every request that passes through `authInterceptor` asks MSAL for a token with `acquireTokenSilent`, and MSAL returns the cached token unless it is within its default 5-minute window of expiry (`tokenRenewalOffsetSeconds`, 300 s). Inside that window it renews on the spot, with the refresh token first. If that fails because Entra needs the user, the interceptor starts `acquireTokenRedirect`. There is no timer, no visibility or online listener, and no pre-emptive renewal, so a request after a long idle period pays for the renewal.

A 401 from the API reaches the caller as it is: there is no retry with a fresh token. A 403 never was retried either, because a new token carries the same scopes.

### Why a redirect can still happen

Entra's refresh tokens for a single-page application expire 24 hours after sign-in, and using one does not extend that ([Microsoft Learn: refresh token lifetime](https://learn.microsoft.com/en-us/entra/identity-platform/refresh-tokens)). After that, renewal depends on a hidden iframe and the Entra session cookie. When the browser blocks third-party cookies or the Entra session has ended, the iframe fails and a redirect is unavoidable. The redirect reloads the page, so any unsaved state is lost. A future compose form must save its draft before it can be interrupted this way.

## How the API validates tokens

`AddAndesMailerAuthentication` (`Andes.Mailer.Api/Configuration/AuthenticationConfiguration.cs`) registers JWT bearer authentication through `Microsoft.Identity.Web` (`AddMicrosoftIdentityWebApi`) and one policy: authenticated user plus the `access_as_user` scope (`ApiScopes.AccessAsUser`). The policy is both the default and the fallback policy, so every endpoint requires it even without `RequireAuthorization()`. An endpoint is public only if it calls `.AllowAnonymous()`. Today those are `/health/live`, `/health/ready`, and, in Development, the OpenAPI document and `/scalar`.

`Program.cs` calls `UseAuthentication()` and `UseAuthorization()` explicitly, after `UseStatusCodePages()`. The auto-added pair would run before it and leave 401 and 403 responses with empty bodies.

Because the fallback policy also covers unmatched routes, an anonymous request to an unknown route now returns 401 instead of 404. An authenticated caller with the scope still gets 404.

`GET /api/users/me` returns the caller's object id and display name; the UI shows the name on the home page. A valid token proves who the caller is; what they may do is decided by roles and permissions, described in [Users and permissions](users-and-permissions.md).

Besides validating tokens, the API acts as itself when it calls Microsoft Graph (to look up a directory account when someone adds a user). That app-only call uses the same registration and needs its own credential; see [Configuration](#configuration).

## Configuration

The Entra values (`Instance`, `TenantId`, `ClientId`, `clientId`, `authority`, `apiScopes`) are public identifiers. The UI holds no secret. The API holds a client secret for Microsoft Graph in `AzureAd:ClientSecret`, and that value is a secret: keep it out of `appsettings.json` and the repository.

| App | File | Section |
| --- | --- | --- |
| API | `andes-mailer-api/Andes.Mailer.Api/appsettings.json` | `AzureAd`: `Instance`, `TenantId`, `ClientId` |
| API | User secrets | `AzureAd:ClientSecret` |
| UI | `andes-mailer-ui/public/config.json` | `auth`: `clientId`, `authority`, `apiScopes` |

- The API validates `AzureAd` on startup and refuses to start if `Instance` is not an absolute https URL or `TenantId` or `ClientId` is not a GUID. `appsettings.sample.json` shows the shape, including `ClientSecret`. The startup check does not look at `ClientSecret`: a missing secret surfaces only when something calls Graph.
- `ClientSecret` needs the Graph application permission `User.Read.All` with admin consent. [Users and permissions](users-and-permissions.md#microsoft-graph-setup) has the steps and the `dotnet user-secrets` commands.
- The UI fetches `config.json` at startup (`cache: 'no-store'`, 10-second timeout) and rejects a bad `clientId`, a non-https `authority`, or an empty `apiScopes`. On failure the page shows "Andes Mailer could not start."
- `config.json` is not baked into the bundle, so each environment can replace it and one build can be promoted. Keep both files pointed at the same registration.

## Entra app registration requirements

- **Redirect URI.** `http://localhost:4200/auth` must be registered as a **single-page application** redirect URI, so serve the UI on port 4200. If it is missing, sign-in fails with `AADSTS50011`. Entra matches the URI exactly.
- **Exposed scope.** The registration exposes `access_as_user`, and `apiScopes` in `config.json` requests `api://<clientId>/access_as_user`.
- **Graph permission.** The registration needs the Microsoft Graph **application** permission `User.Read.All`, with admin consent granted, and a credential (a client secret for local work). See [Users and permissions](users-and-permissions.md#microsoft-graph-setup).
- **Other environments.** Each deployed environment needs its own `<origin>/auth` registered and its own `config.json` with the right values.

## Production host requirements

No deployment configuration exists in this repository yet. Whatever hosts the UI must:

- Serve `/auth` as `index.html`. The app has no separate bridge page; `main.ts` detects the path, so `/auth` must fall through to the SPA entry point.
- Send `Cache-Control: no-store` on `/auth`, so caches never keep a URL that carries an authorization response.
- Send no `Cross-Origin-Opener-Policy` header on `/auth`, because it breaks the channel back to the main app.

Both header rules come from MSAL's [redirect bridge documentation](https://raw.githubusercontent.com/AzureAD/microsoft-authentication-library-for-js/dev/lib/msal-browser/docs/redirect-bridge.md).

## Known limitations

- **No sign-out.** Because `authGuard` redirects automatically, a sign-out added later must land on a page that does not auto-redirect, or the user is signed straight back in.
- **No renewal ahead of time, no 401 retry.** The UI renews a token only when a request needs one, and a 401 from the API reaches the caller as it is; see [Token renewal](#token-renewal).
- **Shared app registration.** Both apps currently use the registration that `enterprise-gpt` uses, so a token issued for one app is accepted by the other's API. andes-mailer needs its own registration before production.
- **Scalar cannot call protected endpoints.** `/scalar` lists the `/api/users` endpoints, but it has no way to attach a token, so Try It returns 401.
- **The API needs a secret.** The API cannot start Graph lookups without `AzureAd:ClientSecret`, which lives in user secrets, never in committed configuration. Because the registration is shared with `enterprise-gpt`, the secret and the `User.Read.All` consent apply to that app too, one more reason to give andes-mailer its own registration.

## Testing

- **UI specs** run the app's real `provideMsal()` wiring around a fake MSAL instance: `provideFakeMsal(instance)` in `src/testing/msal.ts` swaps only `MSAL_INSTANCE`, so the guard, interceptor, and initializer under test are the production ones. Build the instance with `signedInMsal`, `signedOutMsal`, or `fakeMsalInstance`, which spread msal-browser's `stubbedPublicClientApplication` so a call you did not stub rejects with a named MSAL error; `eventCallbacks()` captures what MSAL Angular registers so a spec can raise events. Add `provideTestAppConfig()` from `src/testing/app-config.ts`, which `provideMsal()` reads. jsdom cannot run a real MSAL instance.
- **API integration tests** create the host with `new AndesMailerApiFactory(connectionString, useTestAuthentication: true)`, which swaps in `TestAuthHandler`. Send the `X-Test-Scopes` header with the scopes to grant, for example `access_as_user`. Without the header the request is anonymous. Without `useTestAuthentication`, the real JWT bearer handler runs and any request without a token gets 401.
