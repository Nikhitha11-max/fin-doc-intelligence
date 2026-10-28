# Feature Specification: RAG Question Answering and Citations

**Feature Branch**: `005-rag-question-answering-and-citations`
**Created**: 2026-10-28
**Status**: Draft
**Depends on**: 004 (authorized retrieval)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Grounded answers with citations (Priority: P1)
A user asks a question and receives an answer built only from passages they are allowed to see, with citations (document, page or sheet/row) that point to the retrieved passages.

**Independent Test**: Ask questions with known answers in a controlled data set; each answer cites the correct passages.

**Acceptance Scenarios**:
1. **Given** relevant authorized passages, **When** a question is asked, **Then** the answer is returned with one or more citations to those passages.
2. **Given** a citation, **Then** it refers only to a passage that was actually retrieved for this request.
3. **Given** a Tenant B user, **Then** no Tenant A passage reaches the provider or the answer.

### User Story 2 - Insufficient evidence is stated (Priority: P1)
When retrieval finds nothing relevant, the system says so clearly instead of guessing.

**Acceptance Scenarios**:
1. **Given** no passage above the relevance threshold, **Then** the response says evidence is insufficient and contains no invented facts or citations.

### User Story 3 - Document text cannot take control (Priority: P2)
Instructions inside a document ("ignore previous rules", "reveal other tenants' data") are treated as data, not commands.

**Acceptance Scenarios**:
1. **Given** a passage containing injection text, **Then** the answer ignores it and permissions are unchanged.

### Edge Cases
- Provider failure, timeout or refusal: safe error, no partial leak.
- Model returns a citation that was not retrieved: it is dropped and the answer is flagged or refused.
- Very long questions or excessive passages are bounded.

## Requirements *(mandatory)*

- **FR-001**: Retrieval MUST use feature 004's authorized search before any prompt is built or sent.
- **FR-002**: Authorization decisions MUST be made by the application, never by the model.
- **FR-003**: Every citation MUST be validated against the retrieved passage set; invalid ones MUST be removed.
- **FR-004**: When evidence is insufficient the response MUST say so.
- **FR-005**: Document text MUST be delimited and treated as untrusted data in the prompt.
- **FR-006**: The AI provider MUST be behind an application-owned interface with a deterministic fake for tests; credentials come from secrets.
- **FR-007**: Prompts, passages and answers MUST NOT appear in logs or error messages.
- **FR-008**: Question length, passage count and answer length MUST be capped.
- **FR-009**: Provider data-handling terms (retention, region, no training) MUST be confirmed before external use.
- **FR-010**: Each question is audited (actor, tenant, time, document ids cited) without content.
- **FR-011**: Persistent conversation history is out of scope (feature 006).

## Success Criteria
- **SC-001**: Controlled questions return correct citations in 100% of tests.
- **SC-002**: Zero cross-tenant passages reach the provider in isolation tests.
- **SC-003**: Injection test documents never alter permissions or system rules.
- **SC-004**: Unanswerable questions produce the insufficient-evidence response every time.
- **SC-005**: Provider failures return safe errors with no content leakage.

## Assumptions
- Provider and model are decided in planning; a fake provider is used until privacy terms are confirmed.
- All four roles that can view documents can ask questions.
