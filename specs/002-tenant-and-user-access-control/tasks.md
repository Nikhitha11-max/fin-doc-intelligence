# Tasks: Tenant and User Access Control

**Input**: [spec.md](./spec.md), [plan.md](./plan.md)

## Phase 1: Foundation
- [ ] T001 Add `Tenant.IsActive`, `AuditEvent.TargetUserId/Detail`, `TenantMembership.CreatedAt` in `FinDoc.Domain/Entities/Entities.cs`
- [ ] T002 Add migration `AddAccessControl` in `FinDoc.Infrastructure/Persistence/Migrations`
- [ ] T003 Make membership lookup reject inactive tenants in `DocumentStore.GetMembershipAsync`
- [ ] T004 Add `PlatformAdmin` Identity role and bootstrap in `FinDoc.Api/DevSeeder.cs` plus config bootstrap
- [ ] T005 Disable public `/api/auth/register` in `FinDoc.Api/Program.cs`

## Phase 2: US1 Tenant admin manages team (P1)
- [ ] T006 [US1] `IMemberStore` in `Application/Abstractions` and implementation in Infrastructure
- [ ] T007 [US1] `MemberService` (list paged, add user, change role, remove) in `Application/Access/MemberService.cs`
- [ ] T008 [US1] `MembersController` in `Api/Controllers`
- [ ] T009 [US1] Tests: admin-only access, cross-tenant 404, paging cap in `Tests/Access/MemberServiceTests.cs`

## Phase 3: US2 Safety rules (P1)
- [ ] T010 [US2] `RoleRules` (no self-change, no escalation) in `Application/Access`
- [ ] T011 [US2] Last-admin guard with transaction/locking in store
- [ ] T012 [US2] Tests: last admin, self-demotion, escalation, concurrent demotion
- [ ] T013 [US2] Tests: removed member and deactivated tenant denied immediately (incl. document APIs)

## Phase 4: US3 Platform admin (P2)
- [ ] T014 [US3] `TenantAdminService` (create, rename, deactivate, reactivate, first admin)
- [ ] T015 [US3] `PlatformTenantsController`, policy `PlatformAdmin`
- [ ] T016 [US3] Tests: platform admin cannot read documents/members; tenant users cannot call platform APIs

## Phase 5: US4 Own tenant and role (P3)
- [ ] T017 [US4] `GET /api/me` and test

## Phase 6: Audit, UI, polish
- [ ] T018 Audit events for every tenant/member/role change plus tests (no secrets)
- [ ] T019 Angular team-management and platform screens, role-aware nav
- [ ] T020 Update `docs/tasks/002` status, run full test suite
