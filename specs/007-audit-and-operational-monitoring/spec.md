# Feature Specification: Audit and Operational Monitoring

**Feature Branch**: `007-audit-and-operational-monitoring`
**Created**: 2026-10-28
**Status**: Draft
**Depends on**: 001–006 (extends their audit events)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Auditors review tenant activity (Priority: P1)
An Auditor or Tenant Administrator views and filters their own tenant's audit events (who, what, when, target) with pagination.

**Independent Test**: Perform actions in two tenants; each auditor sees only their own tenant's events.

**Acceptance Scenarios**:
1. **Given** an Auditor, **When** they list events, **Then** they see their tenant's events filtered by date, actor, action.
2. **Given** an Analyst or Document Manager, **Then** access is denied.
3. **Given** Tenant B, **Then** Tenant A events are never visible.

### User Story 2 - Complete audit coverage (Priority: P1)
All security-relevant actions across features 001–006 produce audit events with no content.

**Acceptance Scenarios**:
1. **Given** any covered action (upload, download, delete, processing, retry, membership change, search, question, conversation delete), **Then** one audit event exists.
2. **Given** an audit event, **Then** it contains no document text, prompts, passwords or tokens.

### User Story 3 - Operators see system health (Priority: P2)
Operators get health checks, structured logs with correlation ids, and basic metrics for the API and background workers, without sensitive data.

**Acceptance Scenarios**:
1. **Given** the running system, **Then** liveness and readiness endpoints report API, database and worker status.
2. **Given** a request, **Then** its correlation id appears in all related log lines.
3. **Given** a failing background job or provider, **Then** a safe diagnostic is visible to operators only.

### Edge Cases
- Audit records are append-only; nobody can edit or delete them through the API.
- Diagnostics endpoints are not reachable by tenant users.

## Requirements *(mandatory)*

- **FR-001**: Audit viewing MUST be limited to Auditor and Tenant Administrator, scoped to their own tenant.
- **FR-002**: Audit records MUST be append-only through the application.
- **FR-003**: Audit queries MUST be paginated with a capped size and filterable by date, actor and action.
- **FR-004**: Audit and telemetry MUST NOT contain secrets, document content, prompts or answers.
- **FR-005**: Every request MUST carry a correlation id returned in a response header and logged.
- **FR-006**: Logs MUST be structured and redact configured sensitive fields.
- **FR-007**: Health endpoints MUST expose liveness and readiness (database, worker heartbeat).
- **FR-008**: Metrics MUST cover request counts and errors, processing queue depth, failures, and provider failures.
- **FR-009**: Operational diagnostics MUST be restricted to operators (Platform Administrator or infrastructure access), never tenant users.
- **FR-010**: Retention for audit data MUST be configurable and documented.

## Success Criteria
- **SC-001**: Every listed action has a passing test proving an audit event is created.
- **SC-002**: Redaction tests find no secrets or content in logs or audit rows.
- **SC-003**: Cross-tenant and wrong-role audit access tests all return denied or empty.
- **SC-004**: Health endpoints report degraded status when the database or worker is down.

## Assumptions
- Standard ASP.NET Core health checks, ILogger scopes and OpenTelemetry-style metrics; no external monitoring vendor is chosen yet.
- Audit retention default is 1 year (confirm with product owner).
