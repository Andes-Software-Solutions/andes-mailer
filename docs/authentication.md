# Authentication

Both apps sign users in with Microsoft Entra ID. The UI has no login screen: a signed-out user goes straight to Microsoft, and the API accepts only tokens that carry the `access_as_user` scope. This page explains how the pieces fit, what the Entra app registration and the host must provide, and what is still missing.

## How sign-in works

1. A signed-out user opens a guarded route. `authGuard` (`src/app/core/auth/auth-guard.ts`) calls `Auth.signIn`, which starts an MSAL `loginRedirect` to Entra. The guard returns `false`, so the router stays put while the browser leaves.
2. Entra returns the browser to `<origin>/auth`. That path has no Angular route.
3. `src/main.ts` checks the URL before Angular boots. On `/auth` it runs MSAL's redirect bridge (`broadcastResponseToMainFrame`), which sends the browser back to the page that started the sign-in. The same code serves the hidden iframe of a silent token renewal, where the bridge posts the response to the waiting main frame. If `/auth` carries no response (for example, someone opened it directly), the app navigates to `/login-failed`.
4. On the starting page, `main.ts` loads `config.json`, initializes MSAL, and boots Angular. The guard runs again and awaits `Auth.ready()`, which calls `handleRedirectPromise`, consumes the response, and selects the account. The guard then lets the route through.
5. `authInterceptor` adds `Authorization: Bearer <token>` to requests whose URL starts with `/api/`, using `acquireTokenSilent`. If Entra needs interaction, it starts `acquireTokenRedirect`.

Why the bridge runs first: the router could rewrite the hash the bridge reads, and MSAL cannot be initialized inside a DI factory because that cannot await.

### One automatic attempt per trip

`Auth.signIn` sets `andes-mailer.signInAttempt` in `sessionStorage` before leaving. `Auth.ready()` clears it when an account comes back. If the user returns without an account, or the redirect carried an error, the guard sends them to `/login-failed` instead of redirecting again. Without this limit, a return with no account would loop through Entra, and Back would trap a user who abandons the Microsoft page.

`/login-failed` is the only sign-in screen and is the only unguarded route. Its "Try again" button starts a new sign-in that lands on the home page. Keep every other route behind `authGuard`.

## How the API validates tokens

`AddAndesMailerAuthentication` (`Andes.Mailer.Api/Configuration/AuthenticationConfiguration.cs`) registers JWT bearer authentication through `Microsoft.Identity.Web` (`AddMicrosoftIdentityWebApi`) and one policy: authenticated user plus the `access_as_user` scope (`ApiScopes.AccessAsUser`). The policy is both the default and the fallback policy, so every endpoint requires it even without `RequireAuthorization()`. An endpoint is public only if it calls `.AllowAnonymous()`. Today those are `/health/live`, `/health/ready`, and, in Development, the OpenAPI document and `/scalar`.

`Program.cs` calls `UseAuthentication()` and `UseAuthorization()` explicitly, after `UseStatusCodePages()`. The auto-added pair would run before it and leave 401 and 403 responses with empty bodies.

Because the fallback policy also covers unmatched routes, an anonymous request to an unknown route now returns 401 instead of 404. An authenticated caller with the scope still gets 404.

`GET /api/users/me` returns the caller's object id and display name; the UI shows the name on the home page.

## Configuration

The Entra values are public identifiers. There is no client secret anywhere in either app.

| App | File | Section |
| --- | --- | --- |
| API | `andes-mailer-api/Andes.Mailer.Api/appsettings.json` | `AzureAd`: `Instance`, `TenantId`, `ClientId` |
| UI | `andes-mailer-ui/public/config.json` | `auth`: `clientId`, `authority`, `apiScopes` |

- The API validates `AzureAd` on startup and refuses to start if `Instance` is not an absolute https URL or `TenantId` or `ClientId` is not a GUID. `appsettings.sample.json` shows the shape.
- The UI fetches `config.json` at startup (`cache: 'no-store'`, 10-second timeout) and rejects a bad `clientId`, a non-https `authority`, or an empty `apiScopes`. On failure the page shows "Andes Mailer could not start."
- `config.json` is not baked into the bundle, so each environment can replace it and one build can be promoted. Keep both files pointed at the same registration.

## Entra app registration requirements

- **Redirect URI.** `http://localhost:4200/auth` must be registered as a **single-page application** redirect URI, so serve the UI on port 4200. If it is missing, sign-in fails with `AADSTS50011`. Entra matches the URI exactly.
- **Exposed scope.** The registration exposes `access_as_user`, and `apiScopes` in `config.json` requests `api://<clientId>/access_as_user`.
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
- **Scalar cannot call protected endpoints.** `/scalar` lists `/api/users/me`, but it has no way to attach a token, so Try It returns 401.

## Testing

- **UI specs** use the fakes in `src/testing/msal.ts` (`signedInMsal`, `signedOutMsal`, `fakeMsalInstance`, `provideFakeMsal`) and `provideTestAppConfig()` from `src/testing/app-config.ts`. jsdom cannot run a real MSAL instance.
- **API integration tests** create the host with `new AndesMailerApiFactory(connectionString, useTestAuthentication: true)`, which swaps in `TestAuthHandler`. Send the `X-Test-Scopes` header with the scopes to grant, for example `access_as_user`. Without the header the request is anonymous. Without `useTestAuthentication`, the real JWT bearer handler runs and any request without a token gets 401.
