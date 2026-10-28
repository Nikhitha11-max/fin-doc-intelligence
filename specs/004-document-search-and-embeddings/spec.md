# Feature Specification: Document Search and Embeddings

**Feature Branch**: `004-document-search-and-embeddings`
**Created**: 2026-10-28
**Status**: Draft
**Depends on**: 003 (chunks), 002 (roles), 001 (tenant-scoped documents)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Completed documents become searchable (Priority: P1)
When a document reaches Completed (feature 003), the system creates a vector representation of each passage in the background. Document status shows whether the document is searchable.

**Independent Test**: Process a document; confirm every passage has exactly one embedding and the document is marked searchable.

**Acceptance Scenarios**:
1. **Given** a Completed document, **When** indexing runs, **Then** each passage has one embedding and the document is Searchable.
2. **Given** indexing is interrupted or retried, **Then** there are no duplicate or orphan embeddings.
3. **Given** the embedding provider fails, **Then** the document shows a safe indexing-failed reason and can be retried; processing status is unaffected.

### User Story 2 - Authorized semantic search (Priority: P1)
An Analyst, Document Manager, Tenant Administrator or Auditor-with-document-view enters a query and receives the most relevant passages from documents they can view, each with source metadata (document, page or sheet/row range, passage order, score).

**Independent Test**: Search a controlled data set with known relevant passages; confirm the expected passages rank in the top results and only the caller's tenant data appears.

**Acceptance Scenarios**:
1. **Given** a query, **When** searched, **Then** results are ordered by relevance with a capped result count.
2. **Given** a Tenant B user, **When** they search for text that exists only in Tenant A, **Then** nothing is returned.
3. **Given** a soft-deleted document, **Then** its passages never appear.
4. **Given** a removed member or deactivated tenant, **Then** search is denied immediately.

### User Story 3 - Deletion and re-indexing stay consistent (Priority: P2)
Soft-deleting a document removes its passages from search at once. Re-indexing replaces vectors atomically.

**Acceptance Scenarios**:
1. **Given** a deleted document, **Then** no search result references it.
2. **Given** a re-index, **Then** queries never see a mix of old and new vectors for one document.

### Edge Cases
- Empty, over-long or whitespace-only query is rejected with a clear message.
- Provider timeout, rate limit or invalid key produces a safe error, no content leaked.
- Passages larger than the model limit are split or rejected deterministically.

## Requirements *(mandatory)*

- **FR-001**: Embeddings MUST be generated only for Completed, non-deleted documents.
- **FR-002**: Embeddings MUST carry the tenant and document identifiers and be stored with the same tenant scope.
- **FR-003**: Search MUST filter by tenant, document status and soft-delete state **before** ranking and before returning anything.
- **FR-004**: The tenant MUST come from the signed-in user on the server, never from the request.
- **FR-005**: Results MUST include document id, passage id, order, page or sheet/row range, and score, suitable for citations.
- **FR-006**: Result count and query length MUST be capped.
- **FR-007**: The embedding provider MUST sit behind an application-owned interface; a deterministic fake MUST be available for tests.
- **FR-008**: Provider credentials MUST come from configuration/secrets, never source control.
- **FR-009**: Only passages from documents the caller may view MUST be sent to a provider or returned.
- **FR-010**: Indexing, failure, retry and re-index MUST be audited without document content.
- **FR-011**: Indexing MUST be idempotent and safe after a crash.
- **FR-012**: Natural-language answers are out of scope (feature 005).

## Success Criteria
- **SC-001**: A controlled data set returns the expected passages in the top 3 for every test query.
- **SC-002**: Zero cross-tenant results across all isolation tests.
- **SC-003**: Deleted documents never appear in results.
- **SC-004**: Retried or interrupted indexing leaves exactly one embedding per passage.
- **SC-005**: Search over a 10,000-passage tenant returns in under 2 seconds.

## Assumptions
- Provider/model and vector dimension are chosen in planning (default: a configurable dimension, pgvector with cosine similarity); the decision must be confirmed before wiring a real provider.
- Search is available to all four roles that can view documents.
