# Task 003 — Document Processing and Text Extraction

**Feature specification:** `003-document-processing-and-text-extraction`  
**Requirements:** Root README sections 4 (Phase 2), 5.3, 6, 8, 10–14  
**Depends on:** [Task 001 — Secure Document Management](../001-secure-document-management/README.md), [Task 002 — Tenant and User Access Control](../002-tenant-and-user-access-control/README.md)

## Goal

Process approved uploaded file types asynchronously, extract text, retain source metadata needed for citations, and expose safe processing status and failure behavior.

## Scope for this task

- Confirm supported formats, size limits, OCR needs, and extraction/provider choices.
- Add background processing with explicit Pending, Processing, Completed, and Failed states.
- Extract text and preserve page or section metadata where available.
- Store derived content with the same tenant and document access protections as the original.
- Make retries safe and prevent duplicate or inconsistent derived records.
- Add tests for successful processing, invalid input, failures, retries, and access boundaries.

Do not add embeddings, semantic search, or generated answers.

## Work sequence

1. Approve file formats, parser/OCR choices, processing limits, and retention/deletion behavior.
2. Specify state transitions, retry rules, and user-visible failure behavior.
3. Implement extraction behind an application-owned interface and process work asynchronously.
4. Verify derived text and metadata inherit document authorization and deletion rules.
5. Test supported and rejected files, failure recovery, and duplicate/retry behavior.

## Completion checklist

- Processing is asynchronous where required and status is observable.
- Extracted text and source metadata are stored securely and scoped to the source document.
- Failures are diagnosable without exposing sensitive content and retries are safe.
- Automated tests cover extraction, state transitions, authorization, deletion, and retry behavior.
- No search or AI answer generation has been added.

## Copilot task prompt

> Read the root `README.md`, `docs/README.md`, and this task guide. Implement only secure asynchronous text extraction and processing status. Propose acceptance criteria for file types, OCR, retries, and retention before coding if those decisions are unresolved. Do not add embeddings, search, or AI answers.

## Status: implemented

- A PostgreSQL-backed queue (`FOR UPDATE SKIP LOCKED` plus a lease) feeds a hosted `ProcessingWorker`. Expired leases are reclaimed, so a crashed worker's document is picked up again; after `Processing:MaxAttempts` (default 5) it fails with `internal`.
- Chunks and the status change commit in one transaction; stale claims, deleted documents and re-claimed documents are discarded, so retries never leave duplicates or half-processed state.
- Extractors: PdfPig (pages), a streaming CSV reader (rows), and Open XML (sheet/rows) with a decompression-bomb guard. Limits live in `ProcessingOptions` and can be overridden under the `Processing:` configuration section (for example `Processing:Timeout`, `Processing:MaxRows`, `Processing:WorkerEnabled`).
- Failure codes: `corrupted`, `encrypted`, `empty`, `no_extractable_text`, `too_large`, `timeout`, `internal`. Messages are fixed text per code.
- API: documents expose `status`, `failureCode`, `failureMessage`; `POST /api/documents/{id}/retry` (TenantAdmin, DocumentManager; Failed only, otherwise 409). Chunks are never returned.
- Soft-deleted documents are skipped and their chunks are hidden by a query filter. Derived data is kept with the document until a retention policy is approved, same as feature 001.
- Audit actions: `document.processing_started`, `document.processing_completed`, `document.processing_failed` (detail = failure code, actor `system`), `document.retry`.
- Known limits: a password-protected `.xlsx` is an OLE file and is rejected at upload; parser CPU limits rely on cooperative cancellation between pages and rows.
- Tests: `dotnet test` (extractors, service, Postgres integration, background worker) and Angular status/Retry tests.
