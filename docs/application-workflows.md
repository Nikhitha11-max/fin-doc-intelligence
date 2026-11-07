# Application workflows

FinDoc AI organizes users and documents by tenant (organization). Authorization
is enforced by the API; frontend navigation and route guards are not a security
boundary. Sign in with an account appropriate to the workflow.

## Roles

| Role | Main capabilities |
|---|---|
| PlatformAdmin | Manage organizations and their administrators; view platform diagnostics. Platform membership alone does not grant tenant document access. |
| TenantAdmin | Manage tenant members and documents, search and ask questions, and view tenant audit records. |
| DocumentManager | Manage documents and use search, questions and personal history. |
| Analyst | Read and download documents and use search, questions and personal history. |
| Auditor | View tenant audit records; this role does not automatically grant access to document contents. |

Tenant administrators cannot change their own role, and safeguards prevent
removing or demoting the last tenant administrator. Public self-registration
is disabled; administrators create users.

## Document lifecycle

1. A tenant administrator or document manager uploads a PDF, CSV or XLSX file
   (maximum 25 MiB). The API validates the extension and file content.
2. The original is saved in private local storage, and document metadata and an
   audit event are stored in PostgreSQL.
3. Background processing extracts text, preserving source locations where
   available, and splits it into chunks. The UI shows processing status.
4. An indexing worker creates vectors for the chunks. Search becomes useful
   after indexing finishes; processing and indexing have separate statuses.
5. Authorized users can download, rename, delete, retry failed processing or
   re-index a completed document as permitted by their role.

Deletion is currently a soft delete; it should not be interpreted as immediate
physical erasure from file storage, backups, or all derived data. Plan retention
and secure deletion separately before production. Scanned-image OCR is not
provided by the current extractors.

## Search and questions

Search retrieves tenant-scoped indexed passages and shows source information.
For a question, the API retrieves relevant passages, prepares numbered context,
and validates returned citation numbers against those retrieved passages.
If there is no suitable evidence or valid citation, it returns an
insufficient-evidence response.

The current providers are **local fakes**: embeddings are deterministic
token-hash vectors and the answer provider quotes a passage based on word
overlap. They do not understand meaning, perform reliable calculations, or
generate polished model-based answers. Results and citations should be checked
against the original source. External AI providers are not implemented.

## History, audit and monitoring

Questions and answers are stored in conversations owned by the requesting user.
Only the owner can access their conversation, subject to current access checks.
The default history retention is 90 days; a background worker performs cleanup.

Authorized auditors and tenant administrators can inspect tenant audit events.
Platform administrators can access platform diagnostics. The API also exposes
`/health/live` and `/health/ready`; readiness checks the database and configured
workers. Audit retention defaults to 365 days. Both retention periods are
configurable; neither is a substitute for an approved production policy.
