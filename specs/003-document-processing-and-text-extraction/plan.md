# Implementation Plan: Document Processing and Text Extraction

**Spec**: [spec.md](./spec.md) | **Depends on**: 001, 002

## Summary
A background worker claims Pending documents, extracts text with hardened parsers, and stores ordered chunks atomically. Status and a safe failure reason are exposed through existing document APIs; failed documents can be retried.

## Key Decisions
- **Queue**: PostgreSQL-backed. Worker (`BackgroundService`) claims work with `UPDATE ... WHERE status='Pending' ... FOR UPDATE SKIP LOCKED`, setting a lease (`ProcessingLeaseUntil`). Expired leases are reclaimed, giving crash recovery.
- **Atomicity**: chunks are written in one transaction together with the status change to Completed; retries delete the document's old chunks first inside that transaction, so no duplicates or half-processed state.
- **Libraries**: PdfPig (PDF text), CsvHelper or a hand-rolled streaming reader (CSV), DocumentFormat.OpenXml / ClosedXML in read-only streaming mode (XLSX).
- **Limits** (configurable): 2 min timeout via `CancellationToken`, max extracted characters, max rows/sheets, XLSX decompression ratio and uncompressed-size cap checked before parsing.
- **Failure codes**: `corrupted`, `encrypted`, `empty`, `no_extractable_text`, `too_large`, `timeout`, `internal`.
- **Chunking**: ~1000 characters with small overlap, preserving page (PDF) or sheet and row range (CSV/XLSX).
- **Retry**: TenantAdmin and DocumentManager only; only from Failed.
- **Soft delete**: worker re-checks `DeletedAt` when claiming and before commit; chunks excluded from any query when the document is deleted.

## Data Model
- `Document`: `Status`, `FailureCode`, `FailureMessage`, `ProcessingStartedAt`, `ProcessingLeaseUntil`, `Attempts`.
- `DocumentChunk`: `Id`, `TenantId`, `DocumentId`, `Ordinal`, `Text`, `PageStart/End`, `SheetName`, `RowStart/End`; unique `(DocumentId, Ordinal)`; index `TenantId`.
- Migration `AddDocumentProcessing`.

## API
- Existing document DTOs add `status`, `failureCode`, `failureMessage`.
- `POST /api/documents/{id}/retry`.
- Chunks are never returned.

## Structure
`FinDoc.Application/Processing/` (`IDocumentExtractor`, `ChunkBuilder`, `ProcessingService`), `FinDoc.Infrastructure/Processing/` (extractors, `ProcessingWorker`), tests with generated sample files under `tests/backend/FinDoc.Tests/Processing/`. Angular: status badge and Retry button in `documents`.

## Constitution Check
Untrusted-file handling, tenant scope on chunks, audit without content, safe errors: covered. No violations.
