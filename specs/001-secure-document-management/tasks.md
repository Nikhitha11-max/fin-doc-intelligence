# Tasks: Secure Document Management

**Input**: [spec.md](./spec.md), [plan.md](./plan.md). Retrospective: `[x]` means built and verified; `[ ]` is outstanding.

## Phase 1: Foundation
- [x] T001 Solution and projects (Domain, Application, Infrastructure, Api, Tests) in `FinDoc.sln`
- [x] T002 Entities in `FinDoc.Domain/Entities/Entities.cs`
- [x] T003 `AppDbContext` (Identity) and `InitialCreate` migration
- [x] T004 Abstractions and exceptions in `FinDoc.Application/Abstractions`
- [x] T005 Identity API endpoints, `CurrentUser`, exception handler in `FinDoc.Api`
- [x] T006 Development seeder (needs `Seed:Password`)

## Phase 2: Upload and validation
- [x] T007 `UploadValidator` (extension, signature, size)
- [x] T008 `LocalFileStorage` with server-generated keys and traversal check
- [x] T009 `DocumentService.UploadAsync` with audit event
- [x] T010 Tests: `UploadValidatorTests`

## Phase 3: Read, update, delete, authorization
- [x] T011 List (paged, default 20, max 100), get, download headers
- [x] T012 Update metadata and soft delete
- [x] T013 Permission matrix and tenant scoping (404 across tenants)
- [x] T014 `DocumentsController` routes
- [x] T015 Tests: `DocumentServiceTests`, `ApiAuthTests` (25 passing)

## Phase 4: Verification
- [x] T016 Live two-tenant check: 401, 403, cross-tenant 404, bad content 400, download headers, audit events

## Phase 5: Angular UI
- [x] T017 Auth service, interceptor, guard, login, dev proxy
- [x] T018 Documents list, upload, download, delete screens
- [ ] T019 Run `ng build` and `ng test`, fix failures
- [ ] T020 Manual browser test against running API

## Phase 6: Open items
- [ ] T021 Decide retention for soft-deleted files
- [ ] T022 Lock down `/api/auth/register` (feature 002 T005)
