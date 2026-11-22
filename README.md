# Andes Mailer

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-14-239120?logo=dotnet&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-Minimal_APIs-512BD4?logo=dotnet&logoColor=white)](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis)
[![EF Core](https://img.shields.io/badge/EF_Core-10.0-512BD4?logo=dotnet&logoColor=white)](https://learn.microsoft.com/ef/core/)
[![SQL Server](https://img.shields.io/badge/SQL_Server-2025-CC2927?logo=microsoftsqlserver&logoColor=white)](https://learn.microsoft.com/sql/sql-server/)
[![Azure Blob Storage](https://img.shields.io/badge/Azure-Blob_Storage-0078D4?logo=microsoftazure&logoColor=white)](https://learn.microsoft.com/azure/storage/blobs/)
[![Microsoft Entra ID](https://img.shields.io/badge/Microsoft_Entra-ID-0078D4?logo=microsoft&logoColor=white)](https://learn.microsoft.com/entra/identity/)
[![Microsoft Graph](https://img.shields.io/badge/Microsoft-Graph-0078D4?logo=microsoft&logoColor=white)](https://learn.microsoft.com/graph/)

[![Angular](https://img.shields.io/badge/Angular-22-DD0031?logo=angular&logoColor=white)](https://angular.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-6.0-3178C6?logo=typescript&logoColor=white)](https://www.typescriptlang.org/)
[![NgRx Signals](https://img.shields.io/badge/NgRx-Signal_Store-BA2BD2)](https://ngrx.io/guide/signals)
[![Bootstrap](https://img.shields.io/badge/Bootstrap-5.3-7952B3?logo=bootstrap&logoColor=white)](https://getbootstrap.com/)
[![MSAL](https://img.shields.io/badge/MSAL-Browser-0078D4?logo=microsoft&logoColor=white)](https://github.com/AzureAD/microsoft-authentication-library-for-js)

[![xUnit](https://img.shields.io/badge/xUnit-v3-5E2B97)](https://xunit.net/)
[![Testcontainers](https://img.shields.io/badge/Testcontainers-SQL_Server_%2B_Azurite-291A3F?logo=docker&logoColor=white)](https://dotnet.testcontainers.org/)
[![Vitest](https://img.shields.io/badge/Vitest-5-6E9F18?logo=vitest&logoColor=white)](https://vitest.dev/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Andes Mailer lets teams prepare email together and send it from a shared mailbox, so a message comes from "Press Office" rather than from whoever clicked the button. Team members build up drafts with formatted bodies, recipients, and attachments, start new drafts from reusable templates, and duplicate existing work.

> **Status:** under active development. The API covers users, teams, drafts, attachments, and templates. Sending through Microsoft Graph and the compose and template screens in the UI are not built yet. See [CHANGELOG.md](CHANGELOG.md) for what has shipped.

## Features

- **Microsoft Entra sign-in.** No login screen: signed-out users go straight to Entra, and the API accepts only tokens with the `access_as_user` scope. Access tokens renew in the background.
- **Users, roles, and permissions.** Users are added by email and looked up in Microsoft Graph. `Administrator` is a global role; `TeamAdministrator` and `TeamMember` are held per team. Every write checks a permission.
- **Teams.** Each team sends from one verified service mailbox that no other team can use. Team routes answer 404 to anyone outside the team.
- **Drafts.** Any team member can edit any of the team's drafts. Edits use row versions to catch conflicts, and HTML bodies are sanitized. Recipients are matched to directory accounts, and external addresses are kept as plain text.
- **Attachments.** PDF, DOCX, PPTX, and common image formats, up to 25 MB per email. Uploads return 202 and are pushed to Azure Blob Storage in the background with retries; clients poll for the result. The same document can't be attached twice to one email or template (SHA-256 content check).
- **Templates.** Reusable subjects, bodies, recipients, and documents per team. A draft started from a template is independent of it afterwards.
- **Duplication.** Copy an email or a template, documents included.

## Repository layout

| Path | Holds |
| --- | --- |
| [andes-mailer-api/](andes-mailer-api/) | .NET 10 Minimal API solution (`Andes.Mailer.slnx`): Api, Service, Repository, Entity, Dto, and Common projects, plus unit and integration tests |
| [andes-mailer-ui/](andes-mailer-ui/) | Angular 22 workspace with a feature-first `core` / `features` / `shared` layout and lint-enforced layer boundaries |
| [docs/](docs/) | Developer documentation by topic |
| [CHANGELOG.md](CHANGELOG.md) | Release notes ([Keep a Changelog](https://keepachangelog.com/en/1.1.0/)) |
| [AGENTS.md](AGENTS.md) | Engineering standards and repository conventions for contributors and coding agents |

## Prerequisites

- [.NET SDK 10.0.403](https://dotnet.microsoft.com/download) or a later feature band (see [global.json](andes-mailer-api/global.json))
- [Node.js](https://nodejs.org/) and npm for the UI
- SQL Server 2025 (a local `MSSQLSERVER` instance works)
- [Docker](https://www.docker.com/), for Azurite locally and for the integration tests
- A Microsoft Entra app registration exposing the `access_as_user` scope, with the Graph application permission `User.Read.All` (admin consented) and a client secret. See [docs/authentication.md](docs/authentication.md) and [docs/users-and-permissions.md](docs/users-and-permissions.md#microsoft-graph-setup).

## Getting started

### 1. Configure the API (once per machine)

From `andes-mailer-api/`:

```bash
sqlcmd -S localhost -E -C -Q "CREATE DATABASE AndesMailer"
dotnet user-secrets set "SqlDb:ConnectionString" "Server=localhost;Database=AndesMailer;Integrated Security=True;TrustServerCertificate=True" --project Andes.Mailer.Api
dotnet user-secrets set "BlobStorage:ConnectionString" "UseDevelopmentStorage=true" --project Andes.Mailer.Api
dotnet user-secrets set "AzureAd:ClientSecret" "<client-secret>" --project Andes.Mailer.Api
```

Secrets go in user secrets only, never in committed config. The API refuses to start without `SqlDb:ConnectionString` or `BlobStorage:ConnectionString`.

### 2. Start Azurite and apply migrations

```bash
docker run -d -p 10000:10000 mcr.microsoft.com/azure-storage/azurite azurite-blob --blobHost 0.0.0.0 --skipApiVersionCheck
dotnet ef database update --project Andes.Mailer.Repository --startup-project Andes.Mailer.Api
```

Read the upgrade notes in [docs/teams.md](docs/teams.md#upgrading-the-addteams-migration) before migrating a database that already holds emails.

### 3. Run the API and the UI

```bash
# from andes-mailer-api/
dotnet run --launch-profile https --project Andes.Mailer.Api

# from andes-mailer-ui/
npm install
npx ng serve
```

Open `http://localhost:4200`. The UI forwards `/api/**` to `https://localhost:7093`, which only the API's `https` profile serves. Keep the UI on port 4200: `http://localhost:4200/auth` is the registered redirect URI.

| URL | What it is |
| --- | --- |
| `http://localhost:4200` | The UI |
| `https://localhost:7093/scalar` | API reference (Development only) |
| `https://localhost:7093/health/live` | Liveness: the host is up |
| `https://localhost:7093/health/ready` | Readiness: database and blob storage |

## Build and test

### API

From `andes-mailer-api/`. The integration suite starts SQL Server 2025 and Azurite containers, so Docker must be running.

```bash
dotnet build
dotnet test
dotnet format --verify-no-changes
```

### UI

From `andes-mailer-ui/`. Use `npx ng` so the workspace's own CLI runs.

```bash
npx ng build
npx ng lint
npx ng test --watch=false
```

## Documentation

| Topic | Covers |
| --- | --- |
| [Authentication](docs/authentication.md) | Entra sign-in, token validation, background renewal, configuration |
| [Users and permissions](docs/users-and-permissions.md) | `/api/users`, directory search, roles, permission checks, Microsoft Graph setup |
| [Teams](docs/teams.md) | `/api/teams`, members, service mailboxes, team access checks |
| [Emails and attachments](docs/emails-and-attachments.md) | Drafts, recipients, the background upload pipeline, duplicate detection |
| [Email templates](docs/email-templates.md) | Templates, starting a draft from one, duplicating |
| [Contact groups](docs/contact-groups.md) | A team's named address lists, adding a group's addresses to an email |
| [Telemetry](docs/telemetry/README.md) | Application Insights in the API and the UI, the request log, privacy rules, KQL queries |

## Contributing

Read [AGENTS.md](AGENTS.md) before you change code: it sets the C#, Angular, testing, and documentation standards and the repository's conventions. Every pull request adds an entry under `## [Unreleased]` in [CHANGELOG.md](CHANGELOG.md).

## License

[MIT](LICENSE) © 2026 Andes Software Solutions
