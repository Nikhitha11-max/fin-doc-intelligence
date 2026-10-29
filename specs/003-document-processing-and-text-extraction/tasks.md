# Tasks: Document Processing and Text Extraction

**Input**: [spec.md](./spec.md), [plan.md](./plan.md)

## Phase 1: Foundation
- [x] T001 Add `DocumentStatus`, processing fields on `Document`, and `DocumentChunk` entity
- [x] T002 Migration `AddDocumentProcessing`; new uploads default to Pending
- [x] T003 Add parser packages (PdfPig, OpenXml) to `FinDoc.Infrastructure`
- [x] T004 `ProcessingOptions` (timeout, size and decompression limits) in configuration

## Phase 2: US1 Background processing (P1)
- [x] T005 [US1] `IDocumentExtractor` and `ExtractedUnit` model in Application
- [x] T006 [P] [US1] PDF extractor with page ranges
- [x] T007 [P] [US1] CSV extractor with row ranges
- [x] T008 [P] [US1] XLSX extractor with sheet/row ranges and decompression guard
- [x] T009 [US1] `ChunkBuilder` (ordering, overlap, metadata)
- [x] T010 [US1] Claim queue with lease and `SKIP LOCKED`; `ProcessingService`
- [x] T011 [US1] `ProcessingWorker` hosted service; upload returns without waiting
- [x] T012 [US1] Tests: valid PDF/CSV/XLSX reach Completed with ordered chunks and metadata
- [x] T013 [US1] Tests: crash recovery and no duplicate chunks

## Phase 3: US2 Status and failure reasons (P2)
- [x] T014 [US2] Add status and safe reason to document DTOs
- [x] T015 [US2] Tests: corrupted, encrypted, empty, scanned, oversized, XLSX bomb give safe Failed reasons
- [x] T016 [US2] Tests: cross-tenant status invisible

## Phase 4: US3 Retry (P2)
- [x] T017 [US3] `POST /api/documents/{id}/retry` with role check and Failed-only rule
- [x] T018 [US3] Tests: retry permissions, idempotency, cross-tenant 404

## Phase 5: US4 Deletion (P3)
- [x] T019 [US4] Worker skips and aborts for soft-deleted documents; chunks inaccessible
- [x] T020 [US4] Tests: deleted documents never processed

## Phase 6: Audit, UI, polish
- [x] T021 Audit events: started, completed, failed, retried (no content) plus tests
- [x] T022 Angular status badge and Retry button
- [x] T023 Update `docs/tasks/003`, run full suite
