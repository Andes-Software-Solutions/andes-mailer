<!-- andes:begin v1.16.1 -->
# Andes engineering standards

Shared by Claude Code and GitHub Copilot. The `andes-init` skill manages this block and replaces it on refresh — put project-specific instructions after the `andes:end` marker (`andes-init` scaffolds those sections on first install).

## Communication & comments

- Lead with the answer or the change; no preamble, no restating the request. Don't re-summarize what the user already saw — report only what changed or went wrong. Match length to substance.
- Comment only what code cannot say: why a decision was made, constraints, non-obvious invariants, workarounds with links. Never narrate what code does or what changed. XML doc comments on public APIs are API documentation, not comments.

## Load the standards before you edit

Detailed standards live in skills that load on demand. Load the matching skill before writing or reviewing code:

| Working on | Load |
| --- | --- |
| Any `*.cs` | `csharp-standards`, plus `aspnet-rest-apis` (web APIs), `azure-functions-csharp` (Functions), `csharp-mcp-server` (MCP servers), `ef-core` (EF Core; `ef-core-base-entities` / `ef-core-enum-reference-tables` for entities and lookup tables), `csharp-async`, `csharp-docs` (public APIs) as the change needs; `dotnet-api-architecture` when adding, moving, renaming, or registering files, folders, or projects. Non-negotiables: Minimal APIs only (no controllers), FluentValidation only (no DataAnnotations), services query the `DbContext` directly (no repositories), primary constructors, collection expressions, `var`, and the `csharp-standards` file layout |
| .NET tests | `csharp-xunit` — xUnit v3 + NSubstitute only; never FluentAssertions, Shouldly, Moq, NUnit, or MSTest. Test databases: Testcontainers → SQLite in-memory → dedicated test database → EF Core InMemory as a last resort |
| `*.razor`, `*.razor.cs` | `blazor-wasm` |
| Angular code | `angular-standards`; `ngrx-signal-store` for any state; `angular-ui-architecture` when adding, moving, or naming files or wiring layer lint; `angular-developer` references for depth |
| `*.tf` | `terraform-conventions` |
| `.github/workflows/*.yml`, `action.yml` | `github-actions-hardening`, plus `github-actions-efficiency` / `github-actions-runtime-upgrade-conventions` when relevant |
| Microsoft Agent Framework | `microsoft-agent-framework` |

Ground version-specific answers in the MCP servers when they are installed — `microsoft-learn` (.NET, Azure, other Microsoft docs; ships with `andes-core`), `angular-cli` (Angular), `context7` (any other library; ships with `andes-core`), `terraform` (providers, modules) — instead of memory.

## Review loop

After changing code, run the matching reviewer on the diff: `andes-csharp-code-reviewer` (C#, including Blazor), `andes-angular-code-reviewer` (Angular), `andes-github-actions-reviewer` (workflows, composite actions). Terraform has no reviewer — run `terraform fmt -check` and `terraform validate` instead. On Copilot CLI, every subagent (`task`) call also needs `name`, such as `csharp-review-1`.

1. Reviewers report only High and Medium findings plus a verdict. They never edit files or hand work back.
2. The implementer fixes every reported finding, then runs the reviewer once more on only the files changed since round 1.
3. Two rounds maximum. If High findings remain after round 2, stop and report them to the user instead of iterating; list any open Medium findings in the final summary.
4. After a passing verdict (**Approve** or **Approve with changes**), invoke `andes-se-technical-writer` to update `docs/` and add the `CHANGELOG.md` entry — unless your caller said it handles documentation. Only then is the change done; the final summary gives the verdict and the round count.

## Docs, changelog & requirements

- `andes-se-technical-writer` owns `docs/` and the root `CHANGELOG.md` ([Keep a Changelog](https://keepachangelog.com/en/1.1.0/)): one reader-facing entry per PR under `## [Unreleased]` in the matching subsection. Routine cleanups with no behavior change still get a one-line entry.
- To write a PRD, spec a feature, or break it into epics and user stories, delegate to `andes-prd-generator` (writes `docs/prd/`). If its report starts `PRD-STATUS: NEEDS-INPUT`, show its questions to the user verbatim and re-invoke it with the answers. It creates GitHub issues only after the user explicitly approves. PRDs and the implementation plans under `docs/plans/` get no changelog entry; plans reference story IDs (`US-xxx`). Plans are working files for following the implementation: never commit `docs/plans/`; keep it in `.gitignore`.
- When `andes-azure-devops` is installed, delegate creating, updating, or removing Azure DevOps epics, features, and stories (including from a PRD) to `andes-ado-backlog-manager`. It previews every change and always asks who to assign and which iteration; if its report starts `ADO-STATUS: NEEDS-INPUT` or `ADO-STATUS: NEEDS-SETUP`, show it to the user verbatim and re-invoke it with the answers and the user's explicit approval.
<!-- andes:end -->

## About this repository

<!-- What this repository is, who uses it, and what it deliberately does not contain. -->

## Layout

`andes-mailer-api/` holds the .NET 10 Minimal API solution, `Andes.Mailer.slnx`, laid out per `dotnet-api-architecture`:

| Path | Holds |
| --- | --- |
| `Andes.Mailer.Api/` | Host: `Program.cs`, exception handling, Problem Details, OpenAPI, health endpoints |
| `Andes.Mailer.Service/` | Business logic; services query the `DbContext` directly |
| `Andes.Mailer.Repository/Sql/` | SQL Server plumbing: `AndesMailerDbContext`, entity and base configurations, migrations, interceptors, options, health probe |
| `Andes.Mailer.Repository/Blob/` | Azure Blob Storage plumbing: `IAttachmentStore` gateway, options, container provisioner, health probe |
| `Andes.Mailer.Entity/` | Plain entity classes |
| `Andes.Mailer.Dto/` | Request and response DTOs |
| `Andes.Mailer.Common/` | Enums, constants, options validation |
| `test/Andes.Mailer.Unit.Test/` | Unit tests; no Docker needed |
| `test/Andes.Mailer.Integration.Test/` | Host tests against a SQL Server 2025 container and an Azurite container |

`andes-mailer-ui/` holds the Angular 22 workspace, laid out per `angular-ui-architecture`:

| Path | Holds |
| --- | --- |
| `src/app/core/` | App-wide singletons by topic: `api/` wire DTOs and the `idempotency/` interceptor and key helpers, `auth/` MSAL sign-in, session store, guards, and the permission directive, `config/` runtime config, `feedback/` toasts, `layout/` the signed-in shell and the sign-in splash, `navigation/` route paths and nav items, `theme/` light and dark |
| `src/app/features/` | One folder per feature; `home/` is the landing page, lazy under the lazy shell, `auth/` holds the login-failed and no-access pages, `coming-soon/` holds the placeholder for pages not built yet, `team-members/`, `team-settings/`, and `teams/` are the team screens |
| `src/app/shared/` | Reusable pieces with no business logic: `dialog/` (native `<dialog>` modals), `email-autocomplete/` (the directory-suggesting email field), `forms/` (the unsaved-changes guard and the `UnsavedChanges` service), `layout/` (media-query signals), `rich-text/` (the TipTap editor), `team-avatar/` |
| `src/testing/` | Fakes, fixtures, shared test providers |
| `src/styles.scss` | Bootstrap compiled from Sass with the Andes palette, light and dark |
| `src/proxy.conf.json` | Dev proxy from `/api/**` to the API's `https` profile |
| `eslint-layer-bans.js` | Layer lint from `angular-ui-architecture`; features never import each other |

## Build, test, run

### API

Run these from `andes-mailer-api/`. `dotnet test` needs Docker running; the integration suite starts the `2025-CU9-ubuntu-24.04` SQL Server image and the `azurite:3.37.0` Blob emulator.

```bash
dotnet build
dotnet test
dotnet format --verify-no-changes
```

The target database is SQL Server 2025. EF Core is configured with compatibility level 170 in `SqlPersistenceConfiguration`; keep the database at the same level.

To run the API locally against the default `MSSQLSERVER` instance, once per machine:

```bash
sqlcmd -S localhost -E -C -Q "CREATE DATABASE AndesMailer"
dotnet user-secrets set "SqlDb:ConnectionString" "Server=localhost;Database=AndesMailer;Integrated Security=True;TrustServerCertificate=True" --project Andes.Mailer.Api
dotnet user-secrets set "BlobStorage:ConnectionString" "UseDevelopmentStorage=true" --project Andes.Mailer.Api
```

Then start the Blob emulator and the API:

```bash
docker run -d -p 10000:10000 mcr.microsoft.com/azure-storage/azurite azurite-blob --blobHost 0.0.0.0 --skipApiVersionCheck
dotnet run --project Andes.Mailer.Api
```

The `http` profile listens on `http://localhost:5145`. `/health/live` reports the host and `/health/ready` reports the database and blob storage; `/scalar` serves the API reference in Development. The API refuses to start without `SqlDb:ConnectionString` or `BlobStorage:ConnectionString`, and `dotnet ef` needs both too because it starts the host. In Development, `appsettings.Development.json` defaults `SqlDb:ConnectionString` to LocalDB (`(localdb)\MSSQLLocalDB`, database `AndesMailer`), so the user secret is only needed for another server; other environments must set it.

### UI

Run these from `andes-mailer-ui/`. Use `npx ng`, not a global `ng`, so the workspace's CLI runs.

```bash
npx ng build
npx ng lint
npx ng test --watch=false
```

To run the UI against the API, start the API on its `https` profile from `andes-mailer-api/`, then serve the UI from `andes-mailer-ui/`:

```bash
dotnet run --launch-profile https --project Andes.Mailer.Api
npx ng serve
```

The UI listens on `http://localhost:4200` and forwards `/api/**` to `https://localhost:7093`, which only the `https` profile serves. Map API endpoints under `/api` so the proxy reaches them; call them from the UI with relative `/api/...` URLs.

## Conventions

- **Authentication:** both apps use the Microsoft Entra app registration in `AzureAd` (`appsettings.json`) and `auth` (`andes-mailer-ui/public/config.json`). Its default and fallback authorization policy requires the `access_as_user` scope, so every endpoint is protected unless it calls `.AllowAnonymous()`. In Development, `/scalar` signs in through Entra (authorization code with PKCE) as `AzureAd:ClientId`; the OpenAPI document's `Entra` scheme and per-operation requirements come from the transformers in `Andes.Mailer.Api/OpenApi/`, and `https://localhost:7093/scalar/` and `http://localhost:5145/scalar/` (trailing slash included) must stay registered as SPA redirect URIs.
- **Microsoft Graph:** the API calls Graph app-only (`User.Read.All` application permission) to look users up by email. The credential is `AzureAd:ClientSecret`, set in user secrets locally and never in committed config. Code reaches Graph only through `IDirectoryUserService`, which tests substitute. Directory search (`GET /api/users/directory`) is gated to active users by `ActiveUserEndpointFilter`, rate limited per caller by `RateLimiting:DirectorySearch`, and cached for 2 minutes in `HybridCache`, shared by every caller.
- **Users and permissions:** `[Core].[User].Id` is the Entra object id, so a token's `oid` claim is the user key. Roles and permissions are the `Roles` and `Permissions` enums, mirrored by the `[Core.Ref]` tables; a grant changes only in `RoleGrantConfiguration` plus a new migration. `RoleScopes` splits the roles: `Administrator` is global and held through `[Core].[UserRole]`; `TeamAdministrator` and `TeamMember` are held per team through `[Core].[TeamMember]`, and check constraints keep each role in its table. Endpoints that need a global permission add `PermissionEndpointFilter`.
- **Teams:** only a global `Administrator` creates or deactivates a team, and only one changes its service mailbox (`TeamService.UpdateAsync` answers 403 to a team administrator who tries); `ModifyTeam` is held by `Administrator` and `TeamAdministrator`, so `PUT /api/teams/{teamId}` runs `TeamPermissionEndpointFilter` and a team administrator edits their own team's details and settings. Each team sends from one service mailbox, verified through `IDirectoryUserService`. The settings on `[Core].[Team]` (avatar colour, signature sanitized with `EmailBodySanitizer`, `ConfirmRecipientsAbove`) are stored only, and nothing enforces them yet; a `PUT` replaces them all. `DELETE /api/teams/{teamId}/members/me` lets a member leave (404 to a non-member, even a global `Administrator`; 409 for the last team administrator). Routes under `/api/teams/{teamId:guid}` add `TeamPermissionEndpointFilter`, which grants a global role's permission or the caller's role in that team, and answers 404 to anyone outside the team. Removing a member deactivates the `TeamMember` row, never the user. Demoting or removing a team's last team administrator through `TeamMemberService` returns 409; it saves the team row with the membership so the team's rowversion serializes concurrent demotions. A new team starts with no members, and a global `Administrator` deactivating a user skips the rule, so a team can still have no team administrator.
- **Idempotency:** every team and team-member write (`POST`, `PUT`, and `DELETE` on `/api/teams` and `/api/teams/{teamId}/members`, including `/members/me`) takes an optional `Idempotency-Key` header, added with `.WithIdempotency()` ahead of the other endpoint filters, so a replay returns the caller's earlier success even when the permission filters would now refuse it. `IdempotencyStore` keeps the first 2xx response in process memory for `Idempotency:Expiration` (5 minutes, counted from completion) per caller `oid`, so it assumes one API instance like attachment staging; the same key with another body is 422, a repeat while the first request runs is 409 with `Retry-After` however long it runs (a running key never expires, and each reservation is identified so a request completes or frees only its own), and failures are not stored. Team and member writes read their result back with `CancellationToken.None` after the commit, so a client that disconnects still gets a replay on retry. In the UI, `idempotencyInterceptor` (registered before `authInterceptor`) sends a key set through `idempotent(key)` and retries transient failures with it, and each store holds an `IdempotentAction` per user intent; `docs/idempotency.md` has the contract.
- **Emails and attachments:** an email belongs to a team and lives under `/api/teams/{teamId}/emails`; every member of the team can read and change any of its drafts, and callers outside the team get 404. `AuthorId` records who started it; the team's service account sends it. Attachments upload in the background: the request stages the file on local disk and returns 202, `AttachmentUploadProcessor` pushes it to the `attachments` container, and clients poll the attachment's status. Uploading or removing one needs `CanAttachDocuments`. An email or template never holds the same document twice: `AttachmentLifecycle` hashes each upload (SHA-256, `ContentHash`) and answers 409 when a non-failed attachment of the same owner has that hash, whatever its file name; copies carry the hash over. Local staging and startup recovery assume a single API instance; set `AttachmentUpload:RecoverOnStartup` to `false` on any instance that shares the database with another, as the integration factory does.
- **Email sending:** `POST /api/teams/{teamId}/emails/{id}/send` sends a draft now or at a `ScheduledFor` time; it, `/unschedule`, and `/retry` need `SendTeamEmails`, held by `Administrator` and `TeamAdministrator`. Each recipient gets a message of their own from the team's service mailbox, one `[Core].[EmailDelivery]` row each, and those rows are the queue: `EmailDispatchProcessor` runs on the `EmailSending:Schedule` CRON expression (Cronos, UTC) or when `EmailDispatchSignal` wakes it, claims due rows with a conditional `ExecuteUpdate` and a lease, and tries each one up to `EmailSending:MaxAttempts` (3) times. Code reaches Graph mail only through `IMailboxClient`, which creates a draft, saves its id, then sends it; a retry that finds the draft gone records the delivery as sent instead of sending again, so keep `GraphMessageId` unless the draft is known to be unsent. Graph needs the `Mail.Send` and `Mail.ReadWrite` application permissions. Claims are safe across instances; the integration factory sets `EmailSending:Enabled` to `false`, and only `EmailSendingEndpointsIntegrationTests` turns it on.
- **Contact groups:** a team's named address lists live under `/api/teams/{teamId}/contact-groups`; every team member lists and reads them (`ComposeTeamEmails`), and writes need `ManageTeamContactGroups`, held by `Administrator` and `TeamAdministrator`. A group holds 1 to 500 plain addresses (the email recipient cap and the same `EmailAddressRules`), and its name is unique among the team's active groups. Using a group copies its addresses into an email's recipients on the client, so later changes never reach existing drafts. Removing an address or deactivating a group soft-deletes the row and stamps `RemovedById` or `DeactivatedById`, so set those by hand, not through `_ctx.Remove`, and re-adding an address creates a new row.
- **Telemetry:** both apps report to Application Insights; `docs/telemetry/` explains the setup and holds the KQL queries. The API uses the Microsoft OpenTelemetry Distro (`Microsoft.OpenTelemetry`), wired in `Observability/TelemetryRegistration.cs`, and exports only when `ApplicationInsights:ConnectionString` (user secrets locally) or `APPLICATIONINSIGHTS_CONNECTION_STRING` is set, so tests export nothing. `RequestLoggingMiddleware` runs first, ahead of `UseExceptionHandler`, so its one line per request logs the final status; `RequestContextMiddleware` runs right after `UseAuthentication` and puts the caller on the request trace (`enduser.id`) and the log scope. Background work starts activities from `AndesMailerActivitySource` with the names and tags in `TelemetryNames`. The UI connects through `provideTelemetry()` (`src/app/core/telemetry/`) when `config.json` has a `telemetry.connectionString`, loads the SDK as a lazy chunk, and feature code reports through the `Telemetry` service only. People are identified by Entra object id only, never a name or an email address, and query strings, headers, and bodies are never logged.
- **Blob storage:** the connection string is `BlobStorage:ConnectionString`, set in user secrets locally and never in committed config. Code reaches Blob only through `IAttachmentStore`, which tests substitute. Enums go over the wire by name (`JsonStringEnumConverter`).
- **Migrations:** run `dotnet ef` from `andes-mailer-api/` with `--project Andes.Mailer.Repository --startup-project Andes.Mailer.Api`, and add `--output-dir Sql/Migrations` to `migrations add`. Rows that operators may change later, such as the default administrator, go in as migration `InsertData`, not `HasData`.
- **Sign-in in `andes-mailer-ui/`:** use `@azure/msal-angular` as Microsoft documents it for standalone apps, wired once in `provideMsal()` (`src/app/core/auth/provide-msal.ts`): `MsalService`, `MsalBroadcastService`, and `MsalGuard` and `MsalInterceptor` behind the functional `authGuard` and `authInterceptor`. Only the interceptor's `protectedResourceMap` (`/api/*`) decides which requests get a token. React to auth state through signals (`toSignal(inProgress$)`), because the app is zoneless. There is no login screen: `authGuard` redirects users who are not signed in to Entra and sends failures to `login-failed`. Guard every route except `login-failed`, the one unguarded route: a guard there would send a failed sign-in straight back to Entra. The UI never offers sign-out, by design: add no sign-out button, `logoutRedirect` call, or `/signed-out` route or redirect URI. People switch accounts through "Use a different account" on the no-access page (`prompt=select_account`). `/auth`, the registered redirect URI, has no route because `src/main.ts` handles it before Angular boots. `http://localhost:4200/auth` must stay registered as a SPA redirect URI, so serve the UI on port 4200.
- **Permissions in `andes-mailer-ui/`:** `SessionStore` (`core/auth/session-store.ts`) loads `GET /api/users/me` once and holds the user, the active team, and `permissions` (global plus the active team's); ask it through `can(perms, 'any' | 'all')`, never by reading roles. The shell's account check is the `canActivateChild` guard `accessGuard`, because guards in one `canActivate` array run concurrently and the account call needs the token `authGuard` acquires first; `noAccessGuard` mirrors it, and `permissionGuard(perms)` protects a route. `*appHasPermission` only hides UI, so every route keeps its guard and the API checks every call. The menu and its routes come from `core/navigation/nav-items.ts`: add a page there with its permissions rather than hard-coding a link. Team switching must stay reactive through the store's signals.
- **Theming in `andes-mailer-ui/`:** the design system is `docs/mockups/design-system.html` compiled into Bootstrap in `src/styles.scss`, with extra `--am-*` tokens in `src/styles/_tokens.scss` and mockup-only components in `src/styles/_components.scss`; `docs/ui-design-system.md` has the rules. Colours come only through `--bs-*` and `--am-*` variables, never hex values in a component, and each token needs a light and a dark value. `ThemeStore` (`core/theme/`) is the only writer of `data-bs-theme` on `<html>` after boot. Before that, an inline script in `src/index.html` sets it before the first paint, and `main.ts` calls `applyInitialTheme`; the script mirrors `applyInitialTheme` (`core/theme/theme-preference.ts`), so change them together. Fonts are self-hosted through `@fontsource`; the logo is `AppLogo`, never a hand-made `<img>`.
- **Bootstrap in `andes-mailer-ui/`:** use the npm `bootstrap` and `@popperjs/core` packages, without ng-bootstrap. This overrides the `angular-ui-architecture` default. Import Bootstrap's JavaScript per component (`import { Dropdown } from 'bootstrap'`) rather than as a global script; create each instance in `afterNextRender` and call `dispose()` on destroy.
