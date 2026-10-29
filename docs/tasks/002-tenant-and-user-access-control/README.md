# Task 002 — Tenant and User Access Control

**Feature specification:** `002-tenant-and-user-access-control`  
**Requirements:** Root README sections 3, 5.1, 8, 10, 12–14  
**Depends on:** [Task 001 — Secure Document Management](../001-secure-document-management/README.md)

## Goal

Complete tenant lifecycle and membership administration using the authentication and authorization foundation from Task 001. Ensure administrators can manage users only within their permitted scope.

## Scope for this task

- Define approved tenant creation, update, and lifecycle operations.
- Implement tenant membership changes and role assignment for authorized administrators.
- Apply least privilege, deny-by-default behavior, and server-side checks to every management operation.
- Add audit events for membership, role, and tenant-administration changes.
- Test unauthorized, cross-tenant, and role-escalation attempts.

Do not weaken Task 001's document authorization rules or add AI features.

## Work sequence

1. Confirm whether users can belong to multiple tenants and which roles and operations are allowed.
2. Specify administrator boundaries, membership lifecycle, and role-change behavior.
3. Implement the use cases and persistence changes with reviewed migrations.
4. Add API/UI flows and authorization tests for both allowed and denied operations.
5. Verify existing document-isolation tests still pass.

## Completion checklist

- Tenant and membership operations follow approved role rules.
- A tenant administrator cannot manage users or data outside their tenant.
- Role changes and membership removals are authorized and audited.
- Automated tests cover tenant boundaries and privilege escalation.
- Existing Task 001 tests and behavior remain intact.

## Copilot task prompt

> Read the root `README.md`, `docs/README.md`, and this task guide. Implement only tenant and membership administration on top of Task 001. First identify unresolved decisions and propose acceptance criteria. Preserve existing document authorization and add tests for cross-tenant administration and role escalation.

## Status: implemented

- Backend: members, roles, platform tenants and `/api/me`, with a tenant row lock for last-admin safety.
- Angular: role-aware nav, Team page (TenantAdmin) and Organizations page (PlatformAdmin).
- Configuration: `Bootstrap:PlatformAdminEmail` and `Bootstrap:PlatformAdminPassword` (user-secrets or environment) create the first platform admin.
- Tests: `dotnet test` needs local PostgreSQL; the database defaults to `findoc_test` and can be set with `FINDOC_TEST_DB`.
