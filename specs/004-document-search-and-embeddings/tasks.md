# Tasks: Document Search and Embeddings

**Input**: [spec.md](./spec.md), [plan.md](./plan.md)

## Phase 1: Foundation
- [ ] T001 `IEmbeddingProvider` and `FakeEmbeddingProvider`
- [ ] T002 `ChunkEmbedding` entity, `Document.IndexStatus`, migration with vector column and HNSW index
- [ ] T003 `EmbeddingOptions` (dimensions, batch size, provider name); secrets via configuration only

## Phase 2: US1 Indexing (P1)
- [ ] T004 [US1] `IndexingService` (batching, atomic replace)
- [ ] T005 [US1] `IndexingWorker` with lease and crash recovery
- [ ] T006 [US1] Tests: one vector per chunk, idempotent retry, provider failure sets IndexFailed

## Phase 3: US2 Search (P1)
- [ ] T007 [US2] `SearchService` with tenant, status and soft-delete filter before ranking
- [ ] T008 [US2] `POST /api/search` with query/limit validation
- [ ] T009 [US2] Tests: relevance on controlled data set, cross-tenant empty, deleted excluded, removed member denied

## Phase 4: US3 Consistency (P2)
- [ ] T010 [US3] Re-index endpoint and delete-time exclusion
- [ ] T011 [US3] Tests: no mix of old and new vectors; deleted never returned

## Phase 5: Audit, UI, polish
- [ ] T012 Audit events for indexing, failure, search (no content or query text)
- [ ] T013 Angular search page with citation metadata
- [ ] T014 Real-provider adapter only after provider decision is confirmed
- [ ] T015 Update `docs/tasks/004`, run full suite
