# Tasks: RAG Question Answering and Citations

**Input**: [spec.md](./spec.md), [plan.md](./plan.md)

## Phase 1: Foundation
- [x] T001 `IAnswerProvider`, `AnswerRequest/Result`, `FakeAnswerProvider`
- [x] T002 `AnswerOptions` (threshold, caps, timeout, fake-provider gate)
- [x] T003 `PromptBuilder` with delimiters and untrusted-data policy

## Phase 2: US1 Grounded answers (P1)
- [x] T004 [US1] `AnswerService` orchestrating 004 retrieval, prompt, provider
- [x] T005 [US1] `CitationValidator` mapping to retrieved passages only
- [x] T006 [US1] `POST /api/answers`
- [x] T007 [US1] Tests: correct citations, invalid citations dropped, cross-tenant passages never sent to provider

## Phase 3: US2 Insufficient evidence (P1)
- [x] T008 [US2] Threshold short-circuit with no provider call
- [x] T009 [US2] Tests: unanswerable questions, no invented citations

## Phase 4: US3 Injection resistance (P2)
- [x] T010 [US3] Tests with injection passages: policy and permissions unchanged

## Phase 5: Failures, audit, UI
- [x] T011 Provider timeout/error handling with safe messages; tests that logs contain no content
- [x] T012 Audit event per question (ids only)
- [x] T013 Angular ask page with citations
- [x] T014 No real-provider adapter is enabled pending privacy approval; documentation updated and full suite verified
