# Implementation Plan: Audit and Operational Monitoring

**Spec**: [spec.md](./spec.md) | **Depends on**: 001–006

## Summary
Provide an audit viewer, close audit gaps across features, and add health, correlation ids, structured logging and metrics, all without sensitive data.

## Key Decisions
- **Audit schema**: reuse `AuditEvent` (actor, tenant, action, document, target, detail, time); add an `AuditActions` constants class and a single `IAuditWriter` so every feature writes events the same way. Table is append-only at the application level (no update/delete APIs); a DB trigger or revoked privileges can be added later.
- **Viewer**: `GET /api/audit?from&to&actor&action&page` for Auditor and TenantAdmin, tenant-scoped, size-capped.
- **Correlation**: middleware reads or generates `X-Correlation-Id`, returns it, and pushes it into the `ILogger` scope.
- **Logging**: structured console JSON; a redaction helper and a rule that prompts, passages and tokens are never passed to loggers.
- **Health**: `/health/live` and `/health/ready` (database, worker heartbeat table updated by workers).
- **Metrics**: `System.Diagnostics.Metrics` counters and gauges (requests, errors, queue depth, processing and provider failures), exposed to OpenTelemetry when an exporter is chosen.
- **Operator diagnostics**: `/api/platform/diagnostics` restricted to PlatformAdmin; health endpoints on an internal path.
- **Retention**: `Audit:RetentionDays` (default 365) with a documented purge job.

## Structure
`Application/Audit/*`, `Api/Middleware/*`, `Infrastructure/Health/*`, tests in `Tests/Audit` and `Tests/Observability`; Angular audit page for Auditors.
