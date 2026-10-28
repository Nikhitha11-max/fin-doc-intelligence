# Task 001 — Secure Document Management

**Feature specification:** `001-secure-document-management`  
**Requirements:** Root README sections 3, 4 (Phase 1), 5.1–5.2, 7, 8, 10–14  
**Depends on:** Initial setup in [docs/README.md](../../README.md)

## Goal

Deliver the first secure application foundation and document lifecycle: authentication, tenant-scoped authorization, private file storage, validated uploads, document metadata, authorized listing/download, update and deletion rules, and baseline audit events.

The root requirements specify that this first feature includes the tenant and authorization foundations. Task 002 extends those foundations with tenant and membership administration; do not defer authorization until then.

## Scope for this task

- Establish the solution and the smallest backend/frontend structure required for the feature.
- Define and enforce authentication, tenant scope, and server-side authorization for document operations.
- Store original files privately; validate uploads on the server and avoid exposing storage paths.
- Implement permitted document metadata operations and lifecycle rules.
- Record required baseline document and access-control audit events without sensitive content.
- Add automated tests for validation, access control, and cross-tenant isolation.

Do not add text extraction, vector search, or AI question answering in this task.

## Work sequence

1. Confirm identity, tenant-membership, document-visibility, accepted-file, size-limit, storage, and deletion decisions.
2. Write the feature specification with measurable acceptance criteria and failure cases.
3. Implement the domain and application rules, then persistence and private storage adapters, then API and UI flows.
4. Add unit, integration, and authorization tests, including attempts to access another tenant's documents.
5. Run the agreed build and tests; review API responses, logs, and the diff for sensitive data exposure.

## Completion checklist

- Protected document operations enforce server-side authorization and tenant scoping.
- Invalid and oversized uploads are rejected; original files are not publicly reachable.
- Authorized users can perform only the approved document operations.
- Cross-tenant access tests pass for list, read, download, update, and delete.
- Required audit events are recorded without secrets or unnecessary document contents.
- Migrations, tests, and documentation match the approved specification.

## Copilot task prompt

> Read the root `README.md`, `docs/README.md`, and this task guide. Implement only Task 001 after confirming unresolved product decisions. Start by proposing the feature specification and acceptance criteria for review. Do not implement processing, embeddings, or AI features. Enforce authorization on the server and add tests for cross-tenant access.
