# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- Initial .NET 10 Minimal API solution in `andes-mailer-api/`, targeting SQL Server 2025. It has no mail features yet; it sets up the foundation for them: `/health/live` and `/health/ready` endpoints, Problem Details responses for every error, an OpenAPI reference in Development, and soft-delete and audit stamping for future entities.
- Initial Angular 22 workspace in `andes-mailer-ui/`. It has no mail features yet; it sets up the foundation for them: a feature-first `src/app` layout (`core`, `features`, `shared`) with path aliases and ESLint rules that enforce the layer boundaries, Bootstrap 5.3 compiled from Sass with the Andes palette in light and dark themes, a landing page, and a dev proxy that forwards `/api/**` to the API at `https://localhost:7093`. `@ngrx/signals` is installed and ready for the first store.
- Microsoft Entra sign-in for the API and the UI. Signed-out users go straight to Entra with no login screen (one automatic attempt, then a `/login-failed` page), and the API accepts only JWT bearer tokens that carry the `access_as_user` scope. Every endpoint now requires that scope unless it opts out with `.AllowAnonymous()` (health checks, and OpenAPI and Scalar in Development). `GET /api/users/me` returns the signed-in user, and the home page shows the name. Behavior change: an anonymous request to an unknown API route now returns 401 instead of 404. Both apps currently share an app registration with `enterprise-gpt`, and there is no sign-out yet; see [docs/authentication.md](docs/authentication.md).
