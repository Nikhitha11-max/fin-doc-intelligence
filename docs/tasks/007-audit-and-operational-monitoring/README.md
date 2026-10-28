# Task 007 — Audit and Operational Monitoring

**Feature specification:** `007-audit-and-operational-monitoring`  
**Requirements:** Root README sections 5.2, 8–9, 11–15  
**Depends on:** Tasks 001–006 as applicable; baseline document audit events are part of Task 001

## Goal

Complete audit coverage and provide safe operational visibility for the implemented product. This task extends—not replaces—the baseline audit events required by earlier features.

## Scope for this task

- Confirm which events must be audited, who may view them, and retention requirements.
- Add structured logs, health checks, metrics, and correlation identifiers appropriate to the deployed components.
- Keep secrets, document contents, prompts, and unnecessary personal/financial data out of telemetry.
- Provide safe diagnostics for background processing and provider failures.
- Test audit authorization, required event creation, and redaction behavior.

Do not expose audit records or operational diagnostics to users without the approved authorization policy.

## Work sequence

1. Review existing audit and logging behavior from prior tasks.
2. Agree on event catalog, retention, access policy, and operational targets.
3. Implement missing audit events and monitoring signals consistently across API and worker components.
4. Verify sensitive values are redacted and errors remain actionable.
5. Test authorization, event creation, health checks, and failure diagnostics.

## Completion checklist

- Required security and business events are recorded and access-controlled.
- Operational health and failures can be diagnosed without logging sensitive content.
- Correlation identifiers connect relevant operations across components.
- Retention and monitoring behavior are documented and tested.
- Earlier feature tests remain passing.

## Copilot task prompt

> Read the root `README.md`, `docs/README.md`, and this task guide. Review the existing baseline audit events before implementing anything. Add only approved audit and monitoring scope, preserve access controls, and test redaction so secrets and financial document content do not leak into logs.
