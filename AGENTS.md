<!-- andes:begin v1.16.0 -->
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
| `Andes.Mailer.Repository/Sql/` | SQL Server plumbing: `AndesMailerDbContext`, base configurations, interceptors, options, health probe |
| `Andes.Mailer.Entity/` | Plain entity classes |
| `Andes.Mailer.Dto/` | Request and response DTOs |
| `Andes.Mailer.Common/` | Enums, constants, options validation |
| `test/Andes.Mailer.Unit.Test/` | Unit tests; no Docker needed |
| `test/Andes.Mailer.Integration.Test/` | Host tests against a SQL Server 2025 container |

`andes-mailer-ui/` holds the Angular 22 workspace, laid out per `angular-ui-architecture`:

| Path | Holds |
| --- | --- |
| `src/app/core/` | App-wide singletons by topic: `api/` wire DTOs, `auth/` MSAL sign-in, `config/` runtime config, `navigation/` route paths |
| `src/app/features/` | One folder per feature; `home/` is the eager landing page, `auth/` holds the sign-in failure page |
| `src/app/shared/` | Reusable pieces with no business logic |
| `src/testing/` | Fakes, fixtures, shared test providers |
| `src/styles.scss` | Bootstrap compiled from Sass with the Andes palette, light and dark |
| `src/proxy.conf.json` | Dev proxy from `/api/**` to the API's `https` profile |
| `eslint-layer-bans.js` | Layer lint from `angular-ui-architecture`; features never import each other |

## Build, test, run

### API

Run these from `andes-mailer-api/`. `dotnet test` needs Docker running; the integration suite starts the `2025-CU9-ubuntu-24.04` SQL Server image.

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
```

Then:

```bash
dotnet run --project Andes.Mailer.Api
```

The `http` profile listens on `http://localhost:5145`. `/health/live` reports the host and `/health/ready` reports the database; `/scalar` serves the API reference in Development. The API refuses to start without `SqlDb:ConnectionString`, and `dotnet ef` needs it too because it starts the host.

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

- **Authentication:** both apps use the Microsoft Entra app registration in `AzureAd` (`appsettings.json`) and `auth` (`andes-mailer-ui/public/config.json`). The API holds no client secret. Its default and fallback authorization policy requires the `access_as_user` scope, so every endpoint is protected unless it calls `.AllowAnonymous()`.
- **Sign-in in `andes-mailer-ui/`:** use `@azure/msal-browser` directly, not `@azure/msal-angular`. There is no login screen: `authGuard` redirects signed-out users to Entra. Guard every route except `login-failed`. `/auth`, the registered redirect URI, has no route because `src/main.ts` handles it before Angular boots. `http://localhost:4200/auth` must stay registered as a SPA redirect URI, so serve the UI on port 4200.
- **Bootstrap in `andes-mailer-ui/`:** use the npm `bootstrap` and `@popperjs/core` packages, without ng-bootstrap. This overrides the `angular-ui-architecture` default. Import Bootstrap's JavaScript per component (`import { Dropdown } from 'bootstrap'`) rather than as a global script; create each instance in `afterNextRender` and call `dispose()` on destroy.
