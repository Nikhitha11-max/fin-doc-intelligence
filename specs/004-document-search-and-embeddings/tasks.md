# Tasks: Document Search and Embeddings

**Input**: [spec.md](./spec.md), [plan.md](./plan.md)

## Phase 1: Foundation
- [X] T001 `IEmbeddingProvider` and `FakeEmbeddingProvider`
- [X] T002 `ChunkEmbedding` entity, `Document.IndexStatus`, migration with vector column and HNSW index
- [X] T003 `EmbeddingOptions` (dimensions, batch size, provider name); secrets via configuration only

## Phase 2: US1 Indexing (P1)
- [X] T004 [US1] `IndexingService` (batching, atomic replace)
- [X] T005 [US1] `IndexingWorker` with lease and crash recovery
- [X] T006 [US1] Tests: one vector per chunk, idempotent retry, provider failure sets IndexFailed

## Phase 3: US2 Search (P1)
- [X] T007 [US2] `SearchService` with tenant, status and soft-delete filter before ranking
- [X] T008 [US2] `POST /api/search` with query/limit validation
- [X] T009 [US2] Tests: relevance on controlled data set, cross-tenant empty, deleted excluded, removed member denied

## Phase 4: US3 Consistency (P2)
- [X] T010 [US3] Re-index endpoint and delete-time exclusion
- [X] T011 [US3] Tests: no mix of old and new vectors; deleted never returned

## Phase 5: Audit, UI, polish
- [X] T012 Audit events for indexing, failure, search (no content or query text)
- [X] T013 Angular search page with citation metadata
- [X] T014 No real-provider adapter: product decision confirmed fake-only for this feature.
- [X] T015 Update `docs/tasks/004`, run full suite
