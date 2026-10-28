# FinDoc AI Constitution

## Core Principles

### I. Tenant Isolation and Server-Side Authorization (NON-NEGOTIABLE)
Every protected operation MUST authenticate the caller and authorize the action on the server, denying
access by default. The tenant MUST be resolved on the server from the caller's membership and MUST NEVER
be accepted from the client. Another tenant's resources MUST be indistinguishable from missing ones
(404). Frontend route guards and hidden controls are usability aids only.
Rationale: financial documents are confidential; a single cross-tenant leak is a product-ending failure.

### II. Secure Handling of Documents and Files
Uploads MUST be validated by size, extension and file content, never by the browser-supplied content type.
Originals MUST be stored privately under server-generated keys, and storage paths, keys and secrets MUST
NEVER appear in API responses, logs or errors. Downloads MUST be authorized per request and sent as
attachments with `X-Content-Type-Options: nosniff`. Uploaded files MUST NEVER be executed.
Rationale: uploaded files are untrusted input and the primary attack surface.

### III. Grounded and Permission-Filtered AI
Retrieval MUST be filtered by the caller's permissions before any content reaches an AI provider, and a model
response MUST NEVER grant access. Document text MUST be treated as untrusted input. Answers MUST cite
retrieved sources and MUST state when the documents do not provide enough evidence. AI and embedding
providers MUST be accessed through application-owned interfaces. Full prompts and document passages MUST NOT
be logged without a reviewed justification.
Rationale: answers must be verifiable, and prompt injection must not defeat access control.

### IV. Test-First Verification of Security Rules
Behavior defined by an approved specification MUST have automated tests. Tenant isolation, role permissions,
upload validation and audit creation MUST be covered by tests that fail when the rule is removed. Most tests
MUST NOT depend on a live AI provider. A security requirement MUST NOT be weakened to make a test pass.
Rationale: access-control regressions are silent and costly.

### V. Clean Modular-Monolith Architecture
The solution MUST keep Domain, Application, Infrastructure and Api layers separate: business rules live in
Domain and Application, and controllers and UI components hold no business logic. Database, storage and
provider code MUST sit behind interfaces and use dependency injection. Schema changes MUST use reviewed EF
Core migrations. Long-running work MUST use background jobs and be retry-safe. Microservices MUST NOT be
introduced without a documented operational need.
Rationale: clear boundaries keep the system testable and replaceable.

### VI. Auditability and Privacy by Default
Security-relevant events (uploads, downloads, updates, deletions, permission and administrative changes) MUST
produce audit records containing actor, tenant, action, target and time. Audit records, logs and telemetry
MUST NOT contain passwords, tokens, document contents or unnecessary personal or financial data. Only the
minimum necessary data MAY be sent to external providers.
Rationale: accountability and privacy must not conflict.

### VII. Small, Specified Increments
Work MUST be specified before implementation, using the Spec Kit flow, and delivered one reviewed feature at
a time. Secure document access and tenant isolation MUST exist before AI retrieval is enabled. Open product
decisions MUST be recorded rather than guessed.
Rationale: small, reviewed increments keep scope and risk manageable.

## Technology and Security Constraints

- Backend: ASP.NET Core on .NET 8; frontend: Angular and TypeScript; database: PostgreSQL with `pgvector`;
  data access: Entity Framework Core. Changing a major technology choice requires a constitution amendment
  or a recorded architectural decision.
- Secrets (connection strings, API keys, seed passwords) MUST come from user-secrets, environment variables
  or an approved secret store and MUST NEVER be committed.
- Errors returned to clients MUST NOT expose stack traces, storage paths or internal details. List endpoints
  MUST be paginated and request sizes limited.
- Deployed environments MUST use HTTPS. Retention, deletion, backup and recovery rules MUST be defined before
  production use.

## Development Workflow and Quality Gates

- Each feature follows `/speckit-specify`, `/speckit-plan`, `/speckit-tasks`, `/speckit-implement`, in the
  order given by `docs/README.md` and `README.md` section 13.
- Before merging, the build MUST succeed, relevant tests MUST pass, migrations MUST apply to a clean
  database, and authorization and data-handling changes MUST be reviewed.
- A feature is done only when it meets the Definition of Done in `README.md` section 14, and specifications
  and documentation match delivered behavior.

## Governance

This constitution supersedes other practices. Amendments MUST be documented in a pull request that states the
reason and any migration impact, and MUST be approved by the product owner. Versions follow semantic
versioning: MAJOR for removed or redefined principles, MINOR for new principles or materially expanded
guidance, PATCH for clarifications. Every plan and review MUST verify compliance with these principles, and
any deviation MUST be justified in writing. Product requirements live in `README.md`; task guidance lives in
`docs/`.

**Version**: 1.0.0 | **Ratified**: 2026-10-28 | **Last Amended**: 2026-10-28
