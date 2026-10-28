# Tasks: Document Processing and Text Extraction

**Input**: [spec.md](./spec.md), [plan.md](./plan.md)

## Phase 1: Foundation
- [ ] T001 Add `DocumentStatus`, processing fields on `Document`, and `DocumentChunk` entity
- [ ] T002 Migration `AddDocumentProcessing`; new uploads default to Pending
- [ ] T003 Add parser packages (PdfPig, OpenXml) to `FinDoc.Infrastructure`
- [ ] T004 `ProcessingOptions` (timeout, size and decompression limits) in configuration

## Phase 2: US1 Background processing (P1)
- [ ] T005 [US1] `IDocumentExtractor` and `ExtractedUnit` model in Application
- [ ] T006 [P] [US1] PDF extractor with page ranges
- [ ] T007 [P] [US1] CSV extractor with row ranges
- [ ] T008 [P] [US1] XLSX extractor with sheet/row ranges and decompression guard
- [ ] T009 [US1] `ChunkBuilder` (ordering, overlap, metadata)
- [ ] T010 [US1] Claim queue with lease and `SKIP LOCKED`; `ProcessingService`
- [ ] T011 [US1] `ProcessingWorker` hosted service; upload returns without waiting
- [ ] T012 [US1] Tests: valid PDF/CSV/XLSX reach Completed with ordered chunks and metadata
- [ ] T013 [US1] Tests: crash recovery and no duplicate chunks

## Phase 3: US2 Status and failure reasons (P2)
- [ ] T014 [US2] Add status and safe reason to document DTOs
- [ ] T015 [US2] Tests: corrupted, encrypted, empty, scanned, oversized, XLSX bomb give safe Failed reasons
- [ ] T016 [US2] Tests: cross-tenant status invisible

## Phase 4: US3 Retry (P2)
- [ ] T017 [US3] `POST /api/documents/{id}/retry` with role check and Failed-only rule
- [ ] T018 [US3] Tests: retry permissions, idempotency, cross-tenant 404

## Phase 5: US4 Deletion (P3)
- [ ] T019 [US4] Worker skips and aborts for soft-deleted documents; chunks inaccessible
- [ ] T020 [US4] Tests: deleted documents never processed

## Phase 6: Audit, UI, polish
- [ ] T021 Audit events: started, completed, failed, retried (no content) plus tests
- [ ] T022 Angular status badge and Retry button
- [ ] T023 Update `docs/tasks/003`, run full suite
