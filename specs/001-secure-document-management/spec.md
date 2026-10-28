# Feature Spec 001 — Secure Document Management

**Status:** Draft for review (decisions below are safe defaults pending product-owner confirmation)  
**Guide:** [docs/tasks/001-secure-document-management](../../docs/tasks/001-secure-document-management/README.md)

## Decisions (defaults)

| Topic | Default |
|---|---|
| Identity | ASP.NET Core Identity (Identity API endpoints, bearer tokens) |
| Tenants | A user belongs to exactly one tenant (task 002 may extend this) |
| Roles | TenantAdmin, DocumentManager, Analyst, Auditor |
| Formats / size | PDF, CSV, XLSX; maximum 25 MB |
| Deletion | Soft delete; the original file is retained until a retention policy is approved |

## Permission matrix

| Action | TenantAdmin | DocumentManager | Analyst | Auditor |
|---|---|---|---|---|
| Upload, update metadata, delete | yes | yes | no | no |
| List, view, download | yes | yes | yes | no |

Users without a tenant membership have no document access. The tenant is always resolved on the server from the authenticated user's membership; it is never accepted from the client.

## Acceptance criteria

1. All `/api/documents` endpoints return 401 when unauthenticated.
2. Callers lacking the required role receive 403.
3. A document in another tenant, or a soft-deleted document, returns 404 for read, download, update and delete, and never appears in lists.
4. Uploads are rejected (400) when the file is empty, over 25 MB, has a disallowed extension, or its content does not match the extension (PDF `%PDF-` signature, XLSX ZIP signature, CSV must be text without NUL bytes). The browser-provided content type is ignored.
5. Originals are stored outside any public root under a server-generated key (`{tenantId}/{guid}`); the user filename is never used as a path. API responses never include storage keys or paths.
6. Downloads are authorized per request and sent as `application/octet-stream` with `X-Content-Type-Options: nosniff` and an attachment disposition.
7. Lists are paginated (default 20, maximum 100).
8. Audit events are written for upload, download, update and delete, containing actor, tenant, action, document id and time only — no file contents or titles.
9. Automated tests cover validation, the permission matrix and cross-tenant isolation.

## Out of scope

Text extraction, search, AI features, tenant/membership administration (task 002), and audit viewing (task 007).
