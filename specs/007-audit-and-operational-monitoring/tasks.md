# Tasks: Audit and Operational Monitoring

**Input**: [spec.md](./spec.md), [plan.md](./plan.md)

## Phase 1: Foundation
- [ ] T001 `AuditActions` constants and `IAuditWriter`; migrate existing features to use it
- [ ] T002 Correlation-id middleware and logging scope
- [ ] T003 Log redaction helper and configuration

## Phase 2: US1 Audit viewer (P1)
- [ ] T004 [US1] `AuditQueryService` with filters, paging and cap
- [ ] T005 [US1] `GET /api/audit` for Auditor and TenantAdmin
- [ ] T006 [US1] Tests: wrong role denied, cross-tenant invisible, filters work

## Phase 3: US2 Coverage (P1)
- [ ] T007 [US2] Audit gap review across 001–006 against the spec list
- [ ] T008 [US2] One test per covered action asserting exactly one event and no content

## Phase 4: US3 Operations (P2)
- [ ] T009 [US3] `/health/live` and `/health/ready` with database and worker heartbeat
- [ ] T010 [US3] Metrics instruments for requests, errors, queue depth, failures
- [ ] T011 [US3] Platform-only diagnostics endpoint
- [ ] T012 [US3] Tests: degraded readiness, redaction, correlation id in response and logs

## Phase 5: Polish
- [ ] T013 Retention purge job and documentation
- [ ] T014 Angular audit page for Auditors
- [ ] T015 Update `docs/tasks/007`, run full suite
