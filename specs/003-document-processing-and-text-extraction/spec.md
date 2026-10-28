# Feature Specification: Document Processing and Text Extraction

**Feature Branch**: `003-document-processing-and-text-extraction`

**Created**: 2026-10-28

**Status**: Draft

**Input**: User description: "Document processing and text extraction for FinDoc AI (feature 003). Uploaded documents are currently only stored. This feature processes them in the background and extracts their text so later features can search them."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Uploaded documents are processed in the background (Priority: P1)

A Document Manager or Tenant Administrator uploads a PDF, CSV or XLSX file. The upload finishes immediately and the document is shown as Pending. The system then processes the document without further action, moving it to Processing and finally Completed. Once Completed, the document's text has been extracted and divided into ordered passages that remember where in the document they came from, so later features can search and cite them.

**Why this priority**: Without extracted text no later search or question-answering feature is possible. This is the core value of the feature.

**Independent Test**: Upload one valid file of each supported type and confirm each reaches Completed, the upload itself returned before processing finished, and the stored passages are in reading order with correct page, sheet or row locations.

**Acceptance Scenarios**:

1. **Given** an authorized user, **When** they upload a valid PDF, CSV or XLSX, **Then** the upload completes without waiting for processing and the document shows Pending.
2. **Given** a Pending document, **When** the system processes it, **Then** it moves through Processing to Completed and its passages are stored in order.
3. **Given** a Completed PDF, **When** its passages are inspected, **Then** each passage records its document, tenant, order and page range.
4. **Given** a Completed CSV or XLSX, **When** its passages are inspected, **Then** each passage records its sheet (where applicable) and row range.
5. **Given** a processing run that is interrupted part-way, **When** the system next looks for work, **Then** the document is picked up again and ends with a complete, non-duplicated set of passages.

---

### User Story 2 - Users see processing status and failure reasons (Priority: P2)

Users who can view a document can see whether it is Pending, Processing, Completed or Failed. When it has Failed they see a short, safe reason (a code and plain message) that never includes file contents or technical internals. The document list in the web application shows the status of each document.

**Why this priority**: Users need to know whether a document is ready to be used and why it was not, but this builds on processing existing.

**Independent Test**: Force each outcome (success, corrupted, password-protected, empty, scanned PDF) and confirm the displayed status and reason are correct and safe for users with view permission, and not visible to other tenants.

**Acceptance Scenarios**:

1. **Given** a document being processed, **When** an Analyst, Document Manager or Tenant Administrator views the document or the list, **Then** they see its current status.
2. **Given** a corrupted, password-protected or empty file, **When** processing ends, **Then** the document is Failed with a short reason code and message and the service keeps running.
3. **Given** a scanned PDF with no extractable text, **When** processing ends, **Then** the document is Failed with a "no extractable text" reason.
4. **Given** a user from another tenant, **When** they request the document or its status, **Then** it appears not to exist.

---

### User Story 3 - Failed documents can be retried (Priority: P3)

A Document Manager or Tenant Administrator can retry a Failed document. The retry returns it to Pending and it is processed again. A retry never creates duplicate or partial passages. Analysts and Auditors cannot retry.

**Why this priority**: Recovery from transient failures is valuable but secondary to processing and visibility.

**Independent Test**: Fail a document, retry it as each role, and confirm only permitted roles can retry, the document ends Completed or Failed with a consistent result, and no duplicate passages exist.

**Acceptance Scenarios**:

1. **Given** a Failed document, **When** a Document Manager or Tenant Administrator retries it, **Then** it becomes Pending and is processed again.
2. **Given** a document that is not Failed, **When** a retry is requested, **Then** the request is refused and nothing changes.
3. **Given** an Analyst or Auditor, **When** they request a retry, **Then** they are denied.
4. **Given** a retry that fails again, **When** processing ends, **Then** the document is Failed and holds no leftover passages from the earlier attempt.
5. **Given** the retry is requested twice at once, **When** both are handled, **Then** the document is processed once and has no duplicate passages.

---

### User Story 4 - Deleted documents are never processed (Priority: P3)

When a document is deleted, any pending or retried processing for it stops, and its extracted passages can no longer be reached by anyone.

**Why this priority**: Required to keep deletion trustworthy, but only matters once processing exists.

**Independent Test**: Delete documents in each state (Pending, Processing, Failed, Completed) and confirm processing stops or is not started and the passages become unreachable.

**Acceptance Scenarios**:

1. **Given** a Pending document, **When** it is deleted, **Then** it is never processed.
2. **Given** a document currently being processed, **When** it is deleted, **Then** no passages remain reachable after the run ends.
3. **Given** a Completed document, **When** it is deleted, **Then** its passages become inaccessible.
4. **Given** a deleted Failed document, **When** a retry is requested, **Then** it is treated as not found.

---

### Edge Cases

- A file passes upload checks but is corrupted or truncated: the document Fails with a safe reason and the system continues processing other documents.
- A password-protected or encrypted file: the document Fails with a reason stating it is protected; the file is never opened without permission.
- An empty file or a file containing only blank pages or cells: the document Fails with a "no extractable text" reason.
- A spreadsheet that is small on disk but expands to a very large size when read (a decompression bomb): processing stops at the size limit and the document Fails.
- Processing takes longer than the allowed time or exceeds the memory allowance: processing is stopped and the document Fails.
- The system stops or restarts during processing: the document is picked up again and no duplicate or partial passages result.
- A document is retried while it is still Processing: the request is refused.
- Two processing workers pick up the same document: only one processes it.
- A very large number of documents is uploaded at once: they wait as Pending and are processed over time; uploads are never blocked.
- A tenant is deactivated or a user loses access while a document is Pending: the document does not become visible to any unauthorized user.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Every newly uploaded document MUST start as Pending and MUST be processed asynchronously. Uploading MUST NOT wait for processing to finish.
- **FR-002**: Each document MUST have exactly one status: Pending, Processing, Completed or Failed.
- **FR-003**: The system MUST extract text from PDF, CSV and XLSX documents and divide it into ordered passages.
- **FR-004**: Each passage MUST record its source document, owning tenant, position in reading order, and its page range (PDF) or sheet and row range (CSV and XLSX) where available.
- **FR-005**: The status of a document, and for Failed documents a safe reason consisting of a short code and message, MUST be available to users who are already allowed to view that document, in both the document detail and the list.
- **FR-006**: A failure reason MUST NOT contain document contents, file paths, storage identifiers or technical error details.
- **FR-007**: Tenant Administrators and Document Managers MUST be able to retry a Failed document. Analysts and Auditors MUST NOT.
- **FR-008**: A retry MUST be refused for any document that is not Failed or that has been deleted.
- **FR-009**: Processing and retries MUST be safe to repeat: the same document MUST never have duplicate passages, and a document MUST never end up Completed with only part of its passages.
- **FR-010**: If processing is interrupted, the document MUST be automatically picked up again and processed to a consistent result.
- **FR-011**: At most one processing run MUST be active for a document at a time.
- **FR-012**: Extracted text and passages MUST be stored under the same tenant as the document, and MUST be unreachable by users from any other tenant.
- **FR-013**: Extracted text and passages MUST NOT be returned to users or exposed through any interface in this feature.
- **FR-014**: Files MUST be treated as untrusted. Processing MUST enforce limits on time, memory and extracted size, and MUST guard against files that expand far beyond their stored size.
- **FR-015**: Corrupted, password-protected, empty, over-limit and scanned (no extractable text) files MUST end as Failed with a safe reason and MUST NOT stop or crash the processing service.
- **FR-016**: Deleting a document MUST stop any pending or future processing for it and MUST make its passages inaccessible. Permanent removal of derived data MUST follow the same retention decision as feature 001.
- **FR-017**: Processing start, completion, failure and retry MUST each be recorded as an audit event containing the actor (or the system), tenant, action, document and time, and MUST NOT contain document content.
- **FR-018**: The web application's document list MUST show each document's status and failure reason, and MUST offer a Retry action on Failed documents only to users permitted to retry.
- **FR-019**: Authorization for status viewing and retry MUST be enforced by the system on every request, not only by the web interface, and the tenant MUST be determined from the signed-in user rather than from the request.
- **FR-020**: Optical character recognition, embeddings, search, AI answers, reprocessing after tool upgrades, and new file types MUST NOT be part of this feature.

### Key Entities *(include if feature involves data)*

- **Document**: An uploaded file belonging to one tenant. Gains a processing status, a safe failure reason (code and message), and processing timestamps.
- **Document Passage**: A piece of extracted text belonging to one document and tenant, with an order number and its page range or sheet and row range. Not visible to users in this feature.
- **Processing Attempt**: One run of the extraction for a document, with a start, an outcome and, if it failed, a safe reason. Used to coordinate retries and recovery.
- **Audit Event**: A record of processing start, completion, failure or retry containing the actor (or system), tenant, action, document and time.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In automated tests, 100% of valid sample PDF, CSV and XLSX files reach Completed with passages in correct reading order and with the expected page, sheet or row locations.
- **SC-002**: In automated tests, 100% of corrupted, password-protected, empty, scanned (no text) and over-limit sample files reach Failed with a safe reason, and none expose file contents or technical details.
- **SC-003**: An upload of a valid document returns to the user in the same time as before this feature, regardless of how many documents are waiting to be processed.
- **SC-004**: A typical document of up to 25 MB reaches a final status (Completed or Failed) within 2 minutes under normal load.
- **SC-005**: After repeated retries, concurrent retries and simulated interruptions, tests find zero duplicate passages and zero documents with partial passage sets.
- **SC-006**: Tests confirm a user from another tenant cannot see the status of, retry, or reach the passages of any document, and that Analysts and Auditors cannot retry.
- **SC-007**: Tests confirm deleted documents are never processed and their passages become unreachable.
- **SC-008**: Every status change (start, completion, failure, retry) has a matching audit event in 100% of tested cases, with no document content.
- **SC-009**: A user can tell whether a document is ready, still processing or failed, and why, directly from the document list without contacting anyone.

## Assumptions

- Features 001 (secure document management) and 002 (tenant and user access control) are in place: documents, tenants, roles and audit events already exist, and the roles are Tenant Administrator, Document Manager, Analyst and Auditor.
- Supported file types remain PDF, CSV and XLSX, with the 25 MB upload limit from feature 001.
- Scanned PDFs are not read by character recognition in this feature; they fail with a "no extractable text" reason.
- Only Tenant Administrators and Document Managers can retry; the set of roles that may view status matches those that may view the document today.
- Specific limits for processing time, memory and extracted size are set during planning, within the 2-minute target in SC-004.
- Existing documents uploaded before this feature are treated as Pending and processed the first time the system runs after release.
- Passages are stored but not exposed; interfaces for searching or reading them belong to later features.
- Retention and permanent deletion of derived data follow the decision still pending for feature 001; until then, deletion hides passages rather than permanently erasing them.
- Audit events for the system actor use a fixed "system" identity.
