# Task 006 — Conversation History

**Feature specification:** `006-conversation-history`  
**Requirements:** Root README sections 5.3, 6, 8, 10, 12–14, decision 14 in section 16  
**Depends on:** [Task 005 — RAG Question Answering and Citations](../005-rag-question-answering-and-citations/README.md)

## Goal

If approved, let users save and revisit question-answering conversations without bypassing current permissions on documents or conversations.

## Scope for this task

- Confirm whether history is enabled, who can access a conversation, and whether/how users can delete it.
- Store questions, answers, and citations only as approved by the retention policy.
- Re-check current authorization when loading conversation history and cited documents.
- Prevent identifiers from exposing another user's or tenant's conversations.
- Avoid unnecessary sensitive content in logs and audit records.

If conversation history is not approved, document that decision and do not add persistence.

## Work sequence

1. Resolve the product decision and retention/deletion requirements.
2. Define ownership, tenant scope, sharing (if any), and current-permission behavior.
3. Implement conversation persistence and APIs/UI only if approved.
4. Add authorization and deletion tests, including changed or revoked document access.
5. Verify existing RAG behavior and citation handling remain correct.

## Completion checklist

- The approved history behavior is implemented, or the feature is explicitly deferred by decision.
- Users cannot read or modify another user's conversation without explicit permission.
- Revoked document permissions are not bypassed by saved answers or citations.
- Retention and deletion rules are tested and documented.

## Copilot task prompt

> Read the root `README.md`, `docs/README.md`, and this task guide. First confirm whether conversation history is approved and resolve access, retention, and deletion rules. If approved, implement only that scope and test authorization against current permissions. If not approved, do not add conversation persistence.
