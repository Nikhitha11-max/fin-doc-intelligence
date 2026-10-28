# Tasks: Conversation History

**Input**: [spec.md](./spec.md), [plan.md](./plan.md). Start only after history is approved; otherwise record the deferral in `docs/tasks/006`.

## Phase 1: Foundation
- [ ] T001 Entities `Conversation`, `Message`, `MessageCitation`, migration
- [ ] T002 `HistoryOptions` (retention days)

## Phase 2: US1 Save and revisit (P1)
- [ ] T003 [US1] `ConversationService` (create/append, list paged, get)
- [ ] T004 [US1] Integrate with `AnswerService` via optional `conversationId`
- [ ] T005 [US1] `ConversationsController`
- [ ] T006 [US1] Tests: ordering, pagination, owner-only listing

## Phase 3: US2 Recheck access (P1)
- [ ] T007 [US2] Join citations to currently visible documents on read
- [ ] T008 [US2] Tests: deleted doc and revoked access hide citations; removed member denied; other user/tenant gets 404

## Phase 4: US3 Delete (P2)
- [ ] T009 [US3] Delete endpoint with audit (id only)
- [ ] T010 [US3] Retention cleanup worker and tests for expiry

## Phase 5: UI, polish
- [ ] T011 Angular history list and delete
- [ ] T012 Update `docs/tasks/006`, run full suite
