# Task 006 — Conversation History

**Feature specification:** `006-conversation-history`  
**Requirements:** Root README sections 5.3, 6, 8, 10, 12–14, decision 14 in section 16  
**Depends on:** [Task 005 — RAG Question Answering and Citations](../005-rag-question-answering-and-citations/README.md)

## Goal

Let users save and revisit question-answering conversations with 90-day default retention without bypassing current permissions on documents or conversations. User-initiated deletion is not approved; automatic retention cleanup hard-deletes expired history.

## Scope for this task

- History is approved, private to its owner, and retained for 90 days by default. `History:RetentionDays` is configurable. Do not add a user-facing deletion endpoint.
- Store questions, answers, and citations only as approved by the retention policy.
- Re-check current authorization when loading conversation history and cited documents.
- Prevent identifiers from exposing another user's or tenant's conversations.
- Avoid unnecessary sensitive content in logs and audit records.

## Work sequence

1. Implement only the approved owner-only history with 90-day retention; no owner deletion.
2. Define ownership, tenant scope, sharing (if any), and current-permission behavior.
3. Implement conversation persistence, API and UI.
4. Add authorization and retention tests, including changed or revoked document access.
5. Verify existing RAG behavior and citation handling remain correct.

## Completion checklist

- [x] Approved history is implemented with 90-day retention and no owner deletion endpoint.
- [x] Users cannot read or modify another user's conversation without explicit permission.
- [x] Revoked document permissions are not bypassed by saved answers or citations.
- [x] Retention expiry is tested and documented.

Validation completed: backend tests (135 passed), frontend tests (24 passed), and the production frontend build.

## Copilot task prompt

> Read the root `README.md`, `docs/README.md`, and this task guide. Implement approved owner-only conversation history with 90-day retention, no user-initiated deletion, and current-permission checks on saved citations. Test retention and authorization boundaries. Do not add sharing.
