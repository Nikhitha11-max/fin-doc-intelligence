# Feature Specification: Conversation History

**Feature Branch**: `006-conversation-history`
**Created**: 2026-10-28
**Status**: Draft
**Depends on**: 005
**Decision needed**: README decision 14 — is history approved? This spec assumes yes; if not, defer and add no persistence.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Save and revisit my conversations (Priority: P1)
A user's questions, answers and citations are saved in a private conversation they can list and reopen.

**Independent Test**: Ask two questions, reload, and see both in order with citations.

**Acceptance Scenarios**:
1. **Given** a question, **When** answered, **Then** it is added to a conversation owned by the asker.
2. **Given** the owner, **When** they list conversations, **Then** they see only their own, newest first, paginated.

### User Story 2 - Access is rechecked on every load (Priority: P1)
Reopening a conversation re-checks current document permissions; citations to documents the user can no longer view (deleted, or access removed) are hidden.

**Acceptance Scenarios**:
1. **Given** a cited document later deleted, **Then** the citation and its text are hidden.
2. **Given** a user removed from the tenant, **Then** all conversations become inaccessible.

### User Story 3 - Delete my conversation (Priority: P2)
A user can delete their own conversation; deleted content is no longer retrievable.

**Acceptance Scenarios**:
1. **Given** a deletion, **Then** the conversation disappears for the owner and no one else could see it before or after.

### Edge Cases
- Guessing another user's or tenant's conversation id returns not found.
- Retention period expiry removes old conversations.

## Requirements *(mandatory)*

- **FR-001**: Conversations MUST be owned by one user and scoped to one tenant; owner and tenant come from the server.
- **FR-002**: Other users, including Tenant Administrators, MUST NOT read a conversation (no sharing in this feature).
- **FR-003**: Loading a conversation MUST re-check current membership and document access; revoked citations MUST be hidden.
- **FR-004**: Identifiers MUST NOT reveal existence of other users' conversations (404).
- **FR-005**: Users MUST be able to delete their own conversations; deletion MUST be audited without content.
- **FR-006**: A retention period MUST be configurable and enforced.
- **FR-007**: Logs and audit records MUST NOT contain question or answer text.
- **FR-008**: Lists MUST be paginated with a capped size.

## Success Criteria
- **SC-001**: A user cannot read, change or delete another user's conversation in any test.
- **SC-002**: Revoked document access is never bypassed by saved citations.
- **SC-003**: Deleted and expired conversations are unrecoverable through the API.

## Assumptions
- History is approved, private to the owner, with a default retention of 90 days (confirm with product owner).
