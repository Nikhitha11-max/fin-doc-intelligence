# Feature Specification: Conversation History

**Feature Branch**: `006-conversation-history`
**Created**: 2026-10-28
**Status**: Approved
**Depends on**: 005
**Product decision**: History is approved with a 90-day default automatic retention. User-initiated deletion is not approved.

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

### Edge Cases
- Guessing another user's or tenant's conversation id returns not found.
- Retention period expiry removes old conversations.

## Requirements *(mandatory)*

- **FR-001**: Conversations MUST be owned by one user and scoped to one tenant; owner and tenant come from the server.
- **FR-002**: Other users, including Tenant Administrators, MUST NOT read a conversation (no sharing in this feature).
- **FR-003**: Loading a conversation MUST re-check current membership and document access; revoked citations MUST be hidden.
- **FR-004**: Identifiers MUST NOT reveal existence of other users' conversations (404).
- **FR-005**: Conversations MUST expire after the configured retention period and MUST be hard-deleted with their messages and citations.
- **FR-006**: Logs and audit records MUST NOT contain question or answer text.
- **FR-007**: Lists MUST be paginated with a capped size.

## Success Criteria
- **SC-001**: A user cannot read, change or delete another user's conversation in any test.
- **SC-002**: Revoked document access is never bypassed by saved citations.
- **SC-003**: Expired conversations are unrecoverable through the API.
- **SC-004**: Answers associated with any no-longer-visible citation are redacted on read.

## Assumptions
- History is approved, private to the owner, with a 90-day default retention period.
- User-initiated deletion is not approved; conversations are deleted only by retention cleanup.
