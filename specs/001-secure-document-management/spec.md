# Feature Specification: Secure Document Management

**Feature Branch**: `001-secure-document-management`
**Created**: 2026-10-28
**Status**: Implemented (decisions below are safe defaults pending product-owner confirmation)
**Guide**: [docs/tasks/001-secure-document-management](../../docs/tasks/001-secure-document-management/README.md)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Upload and manage documents (Priority: P1)

A Document Manager or Tenant Administrator uploads a PDF, CSV or XLSX file, can correct its title, and can delete it. Only valid files are accepted, and the file is stored privately for their organization.

**Why this priority**: Nothing else in the product works without trustworthy stored documents.

**Independent Test**: Upload one valid file of each type and several invalid ones; confirm valid files are listed and invalid ones are rejected with a clear message.

**Acceptance Scenarios**:
1. **Given** an authorized user, **When** they upload a valid PDF, CSV or XLSX up to 25 MB, **Then** it is stored and appears in their organization's list.
2. **Given** a file that is empty, over 25 MB, has a disallowed extension, or whose content does not match its extension, **When** it is uploaded, **Then** it is rejected with a clear error and nothing is stored.
3. **Given** an authorized user, **When** they rename a document, **Then** its title changes and nothing else does.
4. **Given** an authorized user, **When** they delete a document, **Then** it disappears from lists and can no longer be read or downloaded.

---

### User Story 2 - View and download documents (Priority: P1)

A Tenant Administrator, Document Manager or Analyst browses their organization's documents in a paginated list and downloads originals.

**Why this priority**: Viewing is the main daily use of the system.

**Independent Test**: As each role, list and download a document and confirm the outcome matches the permission matrix.

**Acceptance Scenarios**:
1. **Given** a permitted user, **When** they list documents, **Then** they see only their organization's non-deleted documents, 20 per page by default and never more than 100.
2. **Given** a permitted user, **When** they download a document, **Then** the file is sent as an attachment that browsers cannot execute or sniff.
3. **Given** an Auditor, **When** they try to list or download, **Then** access is denied.

---

### User Story 3 - Organizations are isolated (Priority: P1)

A user can reach only their own organization's documents. Another organization's documents, and soft-deleted documents, appear not to exist.

**Why this priority**: Cross-tenant leakage is the highest-impact risk for a financial-document product.

**Independent Test**: With two organizations, try every document action using organization B's user against organization A's document ids.

**Acceptance Scenarios**:
1. **Given** a document in organization A, **When** an organization B user reads, downloads, updates or deletes it, **Then** the response is "not found".
2. **Given** a soft-deleted document, **When** anyone requests it, **Then** the response is "not found" and it never appears in lists.
3. **Given** a user with no organization membership, **When** they call any document endpoint, **Then** access is denied.
4. **Given** an unauthenticated caller, **When** they call any document endpoint, **Then** the response is "unauthorized".

---

### User Story 4 - Document actions are audited (Priority: P2)

Every upload, download, update and delete records who did what, in which organization, to which document, and when, without recording file contents or titles.

**Why this priority**: Required for accountability; viewing audit data is a later feature.

**Independent Test**: Perform each action and confirm exactly one audit record with only actor, organization, action, document id and time.

**Acceptance Scenarios**:
1. **Given** any of the four actions succeeds, **Then** one audit event is recorded.
2. **Given** an audit event, **Then** it contains no file contents or titles.

---

### User Story 5 - Web interface for documents (Priority: P2)

Users sign in on a web page and see a document list with upload, download, rename and delete actions appropriate to their permissions, with clear messages for errors and missing permission.

**Independent Test**: Sign in as an administrator, upload, download and delete a document in the browser; sign in as an Analyst and confirm write actions are refused with a clear message.

**Acceptance Scenarios**:
1. **Given** an unauthenticated visitor, **When** they open the app, **Then** they are sent to sign-in.
2. **Given** a signed-in user, **When** the list loads, **Then** their documents are shown with pagination.
3. **Given** a refused or failed action, **Then** a clear, non-technical message is shown.

### Edge Cases
- A filename containing path characters never influences where the file is stored.
- A file with a valid extension but the wrong content (for example an executable renamed to .pdf) is rejected.
- A browser-supplied content type that disagrees with the extension is ignored.
- Requesting page sizes above 100 is capped.

## Requirements *(mandatory)*

### Permission matrix

| Action | Tenant Administrator | Document Manager | Analyst | Auditor |
|---|---|---|---|---|
| Upload, update metadata, delete | yes | yes | no | no |
| List, view, download | yes | yes | yes | no |

### Functional Requirements

- **FR-001**: The system MUST resolve the organization on the server from the signed-in user's membership and MUST NOT accept it from the client.
- **FR-002**: Users without an organization membership MUST have no document access.
- **FR-003**: Every document endpoint MUST require authentication and enforce the permission matrix on the server.
- **FR-004**: A document in another organization, or a soft-deleted document, MUST be indistinguishable from a missing one for read, download, update and delete, and MUST NOT appear in lists.
- **FR-005**: Uploads MUST be rejected when the file is empty, over 25 MB, has an extension other than PDF, CSV or XLSX, or its content does not match its extension (PDF and XLSX by file signature, CSV must be text without binary bytes). The client-supplied content type MUST be ignored.
- **FR-006**: Originals MUST be stored outside any publicly served location under a server-generated identifier; the user's filename MUST NOT be used as a path; storage locations MUST NOT appear in responses.
- **FR-007**: Downloads MUST be authorized on every request and delivered as a generic binary attachment that browsers must not sniff or render.
- **FR-008**: Lists MUST be paginated (default 20, maximum 100).
- **FR-009**: Delete MUST be a soft delete; the stored original is retained until a retention policy is approved.
- **FR-010**: Upload, download, update and delete MUST each create an audit event with actor, organization, action, document and time only.
- **FR-011**: Errors returned to callers MUST NOT expose internals, paths or file contents.
- **FR-012**: The web interface MUST send callers to sign-in when unauthenticated and show clear messages for permission and validation errors; it MUST NOT be the only enforcement of authorization.
- **FR-013**: Open self-registration MUST NOT grant any organization membership or document access (user creation moves to administrators in feature 002).

### Key Entities
- **Tenant**: an organization. **TenantMembership**: links one user to one tenant with one role.
- **Document**: metadata for a stored original (title, file name, type, size, checksum, uploader, timestamps, deleted-at).
- **AuditEvent**: actor, tenant, action, document id, time.

## Success Criteria *(mandatory)*

- **SC-001**: 100% of document endpoint calls without credentials are refused.
- **SC-002**: In two-organization tests, 0 cross-organization reads, downloads, updates or deletes succeed.
- **SC-003**: 100% of invalid uploads (empty, oversize, wrong type, mismatched content) are rejected and store nothing.
- **SC-004**: Each of the four audited actions produces exactly one audit event in tests.
- **SC-005**: Automated tests cover validation, the permission matrix and cross-organization isolation, and all pass.
- **SC-006**: A user can sign in and upload, download, rename and delete a document through the web interface.

## Assumptions
- ASP.NET Core Identity with bearer tokens provides sign-in.
- A user belongs to exactly one organization (feature 002 may extend this).
- Roles: Tenant Administrator, Document Manager, Analyst, Auditor.
- **Retention decision (confirmed 2026-10-29)**: soft-deleted originals are kept indefinitely until a retention policy is approved; hard deletion is out of scope here.

## Out of Scope
Text extraction, search, AI features, tenant and membership administration (feature 002), and audit viewing (feature 007).
