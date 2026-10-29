# Implementation Plan: Secure Document Management

**Spec**: [spec.md](./spec.md) | **Status**: Implemented and verified; retrospective plan recording what was built.

## Summary
Tenant-scoped upload, list, view, download, update and soft-delete of PDF/CSV/XLSX documents, stored privately, authorized per request, with audit events.

## Technical Context
.NET 8 Web API (controllers), ASP.NET Core Identity API endpoints (bearer tokens), EF Core 8 + Npgsql (PostgreSQL 17), Angular 19, xUnit + `WebApplicationFactory`.

## Key Decisions
- **Tenant resolution**: server-side from the user's `TenantMembership` in `DocumentService`; never from the request. Every query filters by `TenantId`, so other tenants' ids return 404.
- **Permissions**: role arrays (`Writers`, `Readers`) in `DocumentService` implement the spec's matrix.
- **Validation**: `UploadValidator` checks extension allow-list, content signature (PDF `%PDF-`, XLSX ZIP header, CSV no NUL bytes) and size 1 B to 25 MB. Client content-type is ignored.
- **Storage**: `IFileStorage` / `LocalFileStorage` under `Storage:RootPath`, key `{tenantId:N}/{docId:N}`, path-traversal check; keys never exposed.
- **Download**: `application/octet-stream`, `nosniff`, attachment disposition.
- **Delete**: soft delete (`DeletedAt`); file retained pending a retention decision.
- **Audit**: `AuditEvent` with actor, tenant, action, document id, time only.

## Data Model
`Tenant`, `TenantMembership` (unique on `UserId`), `Document`, `AuditEvent`; migration `InitialCreate`.

## API
`POST/GET /api/documents`, `GET/PUT/DELETE /api/documents/{id}`, `GET /api/documents/{id}/download`; auth at `/api/auth/*`.

## Project Structure
- `src/backend/FinDoc.Domain/Entities/Entities.cs`
- `src/backend/FinDoc.Application/{Abstractions,Documents}`
- `src/backend/FinDoc.Infrastructure/{Persistence,Storage,DependencyInjection.cs}`
- `src/backend/FinDoc.Api/{Program.cs,ApiSupport.cs,DevSeeder.cs,Controllers}`
- `tests/backend/FinDoc.Tests/*`; `src/frontend/src/app/{core,documents,login}`

## Constitution Check
Server-side authz, tenant from server, safe errors, private storage, audit without content, isolation tests: satisfied.

## Resolved Gaps
- `/api/auth/register` now returns 404; users are created by the dev seeder until feature 002 adds administrator-created users.
- Retention of soft-deleted files: keep indefinitely until a policy is approved (confirmed 2026-10-29).
- Angular UI verified: `ng build`, 8 unit tests, and a browser run through the dev proxy.
