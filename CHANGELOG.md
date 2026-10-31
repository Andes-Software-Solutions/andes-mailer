# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- Initial .NET 10 Minimal API solution in `andes-mailer-api/`, targeting SQL Server 2025. It has no mail features yet; it sets up the foundation for them: `/health/live` and `/health/ready` endpoints, Problem Details responses for every error, an OpenAPI reference in Development, and soft-delete and audit stamping for future entities.
