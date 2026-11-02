# Implementation Plan: Conversation History

**Spec**: [spec.md](./spec.md) | **Depends on**: 005 | **Status**: approved; 90-day retention, no user-initiated deletion.

## Summary
Persist per-user conversations (questions, answers, citations). Access is owner-only and re-evaluated against current permissions on every read.

## Key Decisions
- **Ownership**: `Conversation(Id, TenantId, OwnerUserId, Title, CreatedAt, UpdatedAt, ExpiresAt)`; `ConversationMessage(Id, ConversationId, Role, Text, CreatedAt)`; `ConversationCitation(MessageId, DocumentId, ChunkId, location fields)`.
- **Access**: every query filters by `TenantId` and `OwnerUserId` from the server; mismatches return 404.
- **Read-time recheck**: citations are joined to current, non-deleted, tenant-visible documents; others are omitted (snippet text is not stored, so nothing leaks).
- **Deletion**: no user-facing deletion endpoint; user-initiated deletion is not approved. Retention cleanup hard-deletes the conversation and children.
- **Retention**: `History:RetentionDays` defaults to 90 days per the product decision and is enforced by a periodic cleanup worker.

## API
`GET /api/conversations` (paged), `GET /api/conversations/{id}`; `POST /api/answers` gains an optional `conversationId`.

## Structure
`Application/Conversations/*`, `Infrastructure/Conversations/*`, and the conversation API controller implement persistence and retention. Authorization, citation visibility, and expiry are covered in `tests/backend/FinDoc.Tests/Search/SearchApiTests.cs`; answer integration is covered in `tests/backend/FinDoc.Tests/Answers/AnswerServiceTests.cs`. Angular exposes a paginated history page at `/history`.
