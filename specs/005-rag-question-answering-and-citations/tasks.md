# Tasks: RAG Question Answering and Citations

**Input**: [spec.md](./spec.md), [plan.md](./plan.md)

## Phase 1: Foundation
- [ ] T001 `IAnswerProvider`, `AnswerRequest/Result`, `FakeAnswerProvider`
- [ ] T002 `AnswerOptions` (threshold, caps, provider config via secrets)
- [ ] T003 `PromptBuilder` with delimiters and untrusted-data policy

## Phase 2: US1 Grounded answers (P1)
- [ ] T004 [US1] `AnswerService` orchestrating 004 retrieval, prompt, provider
- [ ] T005 [US1] `CitationValidator` mapping to retrieved passages only
- [ ] T006 [US1] `POST /api/answers`
- [ ] T007 [US1] Tests: correct citations, invalid citations dropped, cross-tenant passages never sent to provider

## Phase 3: US2 Insufficient evidence (P1)
- [ ] T008 [US2] Threshold short-circuit with no provider call
- [ ] T009 [US2] Tests: unanswerable questions, no invented citations

## Phase 4: US3 Injection resistance (P2)
- [ ] T010 [US3] Tests with injection passages: policy and permissions unchanged

## Phase 5: Failures, audit, UI
- [ ] T011 Provider timeout/error handling with safe messages; tests that logs contain no content
- [ ] T012 Audit event per question (ids only)
- [ ] T013 Angular ask page with citations
- [ ] T014 Real-provider adapter only after privacy decision; update `docs/tasks/005`, run full suite
