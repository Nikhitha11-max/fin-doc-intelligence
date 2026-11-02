# Tasks: Conversation History

**Input**: [spec.md](./spec.md), [plan.md](./plan.md). Product decision: history approved for 90 days; owner deletion not approved.

## Phase 1: Foundation
- [x] T001 Entities `Conversation`, `Message`, `MessageCitation`, migration
- [x] T002 `HistoryOptions` (retention days)

## Phase 2: US1 Save and revisit (P1)
- [x] T003 [US1] `ConversationService` (create/append, list paged, get)
- [x] T004 [US1] Integrate with `AnswerService` via optional `conversationId`
- [x] T005 [US1] `ConversationsController`
- [x] T006 [US1] Tests: ordering, pagination, owner-only listing

## Phase 3: US2 Recheck access (P1)
- [x] T007 [US2] Join citations to currently visible documents on read
- [x] T008 [US2] Tests: deleted doc and revoked access hide citations; removed member denied; other user/tenant gets 404

## Phase 4: Retention
- [x] T009 Retention cleanup worker and tests for expiry

## Phase 5: UI, polish
- [x] T010 Angular history list
- [x] T011 Update `docs/tasks/006`, run full suite
