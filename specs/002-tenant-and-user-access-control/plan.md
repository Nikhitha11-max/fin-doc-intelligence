# Implementation Plan: Tenant and User Access Control

**Spec**: [spec.md](./spec.md) | **Depends on**: 001

## Summary
Add tenant, member and role management on top of 001's `Tenant` / `TenantMembership`. Platform Administrators manage tenants; Tenant Administrators manage their own members. Safety rules (last admin, no self-role-change, no escalation) are enforced in the application layer and backed by a database guard.

## Technical Context
.NET 8, ASP.NET Core Identity (bearer tokens), EF Core 8 + PostgreSQL, Angular 19, xUnit + `WebApplicationFactory`.

## Key Decisions
- **Platform Administrator**: an Identity role `PlatformAdmin`, separate from `TenantRole`. Bootstrapped only in Development seeding and by a CLI/config-driven one-time bootstrap (`Bootstrap:PlatformAdminEmail` + password from secrets) in other environments. It has no tenant membership and cannot call document or member endpoints.
- **Self-registration**: `/api/auth/register` is removed from the public surface; users are created only by admin endpoints.
- **Tenant status**: `Tenant.IsActive`; membership checks reject inactive tenants on every request (no caching), so changes apply immediately.
- **Last-admin rule**: checked inside a serializable transaction (or `SELECT ... FOR UPDATE` on the tenant's admin rows) so concurrent demotions cannot both succeed.
- **Removal**: membership row deleted/deactivated; documents and audit rows untouched.
- **Audit**: extend `AuditEvent` with `TargetUserId` (nullable) and `Detail` (non-sensitive short code such as the new role).

## Data Model Changes
- `Tenant`: `IsActive`, `DeactivatedAt`.
- `TenantMembership`: `CreatedAt`; unique index on `UserId` (exists).
- `AuditEvent`: `TargetUserId`, `Detail`; `DocumentId` already nullable.
- Migration `AddAccessControl`.

## API Contracts
- `GET /api/me` — own tenant and role.
- `GET/POST /api/members`, `PUT /api/members/{userId}/role`, `DELETE /api/members/{userId}` — TenantAdmin only, paginated.
- `GET/POST /api/platform/tenants`, `PUT /api/platform/tenants/{id}` (rename), `POST .../deactivate|reactivate`, `POST .../admins` — PlatformAdmin only.

## Project Structure
- `FinDoc.Application/Access/` (`MemberService`, `TenantAdminService`, `RoleRules`)
- `FinDoc.Infrastructure/Persistence/` (store + migration), `FinDoc.Api/Controllers/{Me,Members,PlatformTenants}Controller.cs`
- Tests: `tests/backend/FinDoc.Tests/Access/*`
- Frontend: `src/frontend/src/app/{team,platform,core}`

## Constitution Check
Server-side authorization, tenant from server, audit without secrets, tests for isolation: all satisfied by design. No violations.
