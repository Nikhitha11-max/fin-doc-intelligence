# Implementation Plan: Conversation History

**Spec**: [spec.md](./spec.md) | **Depends on**: 005 | **Gate**: product decision 14 must be approved first.

## Summary
Persist per-user conversations (questions, answers, citations). Access is owner-only and re-evaluated against current permissions on every read.

## Key Decisions
- **Ownership**: `Conversation(Id, TenantId, OwnerUserId, Title, CreatedAt, UpdatedAt, ExpiresAt)`; `Message(Id, ConversationId, Role, Text, CreatedAt)`; `MessageCitation(MessageId, DocumentId, ChunkId, location fields)`.
- **Access**: every query filters by `TenantId` and `OwnerUserId` from the server; mismatches return 404.
- **Read-time recheck**: citations are joined to current, non-deleted, tenant-visible documents; others are omitted (snippet text is not stored, so nothing leaks).
- **Deletion**: hard delete of the conversation and children; audited by id only.
- **Retention**: `History:RetentionDays` (default 90) enforced by a periodic cleanup worker.

## API
`GET /api/conversations` (paged), `GET /api/conversations/{id}`, `DELETE /api/conversations/{id}`; `POST /api/answers` gains an optional `conversationId`.

## Structure
`Application/Conversations/*`, Infrastructure store and cleanup worker, tests in `Tests/Conversations`, Angular history sidebar.
