# Authentication

Both apps sign users in with Microsoft Entra ID. The UI has no login screen: a signed-out user goes straight to Microsoft, and the API accepts only tokens that carry the `access_as_user` scope. This page explains how the pieces fit, what the Entra app registration and the host must provide, and what is still missing.

## How sign-in works

1. A signed-out user opens a guarded route. `authGuard` (`src/app/core/auth/auth-guard.ts`) calls `Auth.signIn`, which starts an MSAL `loginRedirect` to Entra. The guard returns `false`, so the router stays put while the browser leaves.
2. Entra returns the browser to `<origin>/auth`. That path has no Angular route.
3. `src/main.ts` checks the URL before Angular boots. On `/auth` it runs MSAL's redirect bridge (`broadcastResponseToMainFrame`), which sends the browser back to the page that started the sign-in. The same code serves the hidden iframe of a silent token renewal, where the bridge posts the response to the waiting main frame. If `/auth` carries no response (for example, someone opened it directly), the app navigates to `/login-failed`.
4. On the starting page, `main.ts` loads `config.json`, initializes MSAL, and boots Angular. The guard runs again and awaits `Auth.ready()`, which calls `handleRedirectPromise`, consumes the response, and selects the account. The guard then lets the route through.
5. `authInterceptor` adds `Authorization: Bearer <token>` to requests whose URL starts with `/api/`, using `acquireTokenSilent`. If Entra needs interaction, it starts `acquireTokenRedirect`. A 401 response gets one retry; see [Token renewal](#token-renewal).

Why the bridge runs first: the router could rewrite the hash the bridge reads, and MSAL cannot be initialized inside a DI factory because that cannot await.

### One automatic attempt per trip

`Auth.signIn` sets `andes-mailer.signInAttempt` in `sessionStorage` before leaving. `Auth.ready()` clears it when an account comes back. If the user returns without an account, or the redirect carried an error, the guard sends them to `/login-failed` instead of redirecting again. Without this limit, a return with no account would loop through Entra, and Back would trap a user who abandons the Microsoft page.

`/login-failed` is the only sign-in screen and is the only unguarded route. Its "Try again" button starts a new sign-in that lands on the home page. Keep every other route behind `authGuard`.

## Token renewal

Access tokens are short-lived, and MSAL has no background renewal of its own: it only looks at the cache when something asks for a token. Without help, a tab left open for hours would find its token expired on the next click, and that request would pay for the renewal (or for a redirect to Entra). `TokenRenewal` (`src/app/core/auth/token-renewal.ts`) renews the token ahead of time so requests find a valid one in the cache.

`provideTokenRenewal()` in `app.config.ts` starts it through an app initializer. The initializer does not await `start()`, so renewal never holds up bootstrap. `start()` waits for `Auth.ready()` and does nothing when there is no signed-in account; a signed-out user is handled by `authGuard`. The listeners and timer are removed when the service is destroyed.

### How renewal runs

Renewal goes through `Auth.acquireToken({ refresh, interactive })`:

- `refresh: true` skips the cached access token (`CacheLookupPolicy.RefreshTokenAndNetwork`) and uses the refresh token. If that fails with `InteractionRequiredAuthError`, MSAL falls back to its hidden iframe, which completes through the same `/auth` redirect bridge described in [How sign-in works](#how-sign-in-works). MSAL's iframe timeout is 10 seconds.
- `interactive: false` means a sign-in that Entra requires is reported as an error and never started, so a call the user didn't make can't navigate away from their work. `interactive` defaults to `true`, which starts `acquireTokenRedirect` instead.
- `Auth.getAccessToken({ refresh })` wraps `acquireToken` and returns only the token string.

Four triggers call it:

| Trigger | `refresh` | `interactive` | Why |
| --- | --- | --- | --- |
| Startup (`start()`) | no | no | If Entra needs the user right now, the page's first request starts the sign-in anyway. |
| Timer | yes | no | Never redirects from a timer. |
| Tab becomes visible | no | yes | See below. |
| Browser comes back online | no | no | The connection can return while the tab is hidden or the user is mid-task. |

Only the timer forces a refresh. The other three ask MSAL for a token with its default lookup, which returns the cached one unless it is within MSAL's own 5-minute window of expiry (`tokenRenewalOffsetSeconds`, 300 s in `@azure/msal-browser` 5.27.0).

### When the timer fires

After every successful renewal, `TokenRenewal` schedules the next one from the new token's `expiresOn`:

- It renews 10 minutes before expiry, ahead of MSAL's 5-minute window, leaving room for a 10-second iframe attempt and a retry.
- For a token that lives less than 20 minutes, it renews halfway through the lifetime instead, so a short-lived token isn't refreshed on every attempt.
- The delay is never less than 60 seconds.

Each new schedule replaces the previous timer, so there is at most one pending timer.

### Returning to the tab

Browsers throttle timers in hidden tabs, and a sleeping PC pauses them, so the timer can be late by hours. When `document.visibilityState` becomes `visible`, `TokenRenewal` renews right away. This trigger is interactive on purpose: if Entra needs the user, the redirect happens when they return, before they start work that the page reload would lose.

### When renewal fails

| Failure | What happens |
| --- | --- |
| Entra needs interaction (`InteractionRequiredAuthError`) | No new timer is scheduled. The next return to the tab, or the next API request, starts the redirect. |
| Anything else, such as being offline or an iframe timeout | The error is logged with `console.error` and renewal retries after 1, 2, 4, 8, then 15 minutes (the delay is capped at 15 minutes, so a persistent failure doesn't call Entra every minute from every tab). A successful renewal resets the count. |

### Retry on 401

`authInterceptor` retries a 401 from an `/api/` request once, with a token fetched using `refresh: true`. This covers an API that rejects a token MSAL still trusts, for example when the PC's clock is behind. The retry is interactive, like any request. A 403 is never retried, because a new token carries the same scopes. A second 401 reaches the caller.

### Why a redirect can still happen

Entra's refresh tokens for a single-page application expire 24 hours after sign-in, and using one does not extend that ([Microsoft Learn: refresh token lifetime](https://learn.microsoft.com/en-us/entra/identity-platform/refresh-tokens)). After that, renewal depends on the hidden iframe and the Entra session cookie. When the browser blocks third-party cookies or the Entra session has ended, the iframe fails and a redirect is unavoidable. The redirect reloads the page, so any unsaved state is lost. A future compose form must save its draft before it can be interrupted this way.

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
- **Shared app registration.** Both apps currently use the registration that `enterprise-gpt` uses, so a token issued for one app is accepted by the other's API. andes-mailer needs its own registration before production.
- **Scalar cannot call protected endpoints.** `/scalar` lists the `/api/users` endpoints, but it has no way to attach a token, so Try It returns 401.
- **The API needs a secret.** The API cannot start Graph lookups without `AzureAd:ClientSecret`, which lives in user secrets, never in committed configuration. Because the registration is shared with `enterprise-gpt`, the secret and the `User.Read.All` consent apply to that app too, one more reason to give andes-mailer its own registration.

## Testing

- **UI specs** use the fakes in `src/testing/msal.ts` (`signedInMsal`, `signedOutMsal`, `fakeMsalInstance`, `provideFakeMsal`) and `provideTestAppConfig()` from `src/testing/app-config.ts`. jsdom cannot run a real MSAL instance.
- **API integration tests** create the host with `new AndesMailerApiFactory(connectionString, useTestAuthentication: true)`, which swaps in `TestAuthHandler`. Send the `X-Test-Scopes` header with the scopes to grant, for example `access_as_user`. Without the header the request is anonymous. Without `useTestAuthentication`, the real JWT bearer handler runs and any request without a token gets 401.
