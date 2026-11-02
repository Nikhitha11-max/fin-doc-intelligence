# Implementation Plan: RAG Question Answering and Citations

**Spec**: [spec.md](./spec.md) | **Depends on**: 004

## Summary
Orchestrate: authorized retrieval (004) → prompt build → provider call → citation validation → response.

## Key Decisions
- **Provider decision**: only the deterministic local `FakeAnswerProvider` is registered. It selects a matching source sentence by exact word overlap; it is not an LLM and makes no network calls. No real adapter is enabled until privacy terms are approved.
- **Interface**: `IAnswerProvider.AnswerAsync(AnswerProviderRequest, ct)` in Application. Requests keep the question, numbered source passages, and fixed system policy separate.
- **Prompt**: passages are wrapped in explicit untrusted-data delimiters; delimiter characters in passage content are escaped. The policy says passage content is never instructions and cannot affect permissions.
- **Citations**: provider passage numbers are mapped only to the passages returned by authorized Feature 004 search. Unknown and duplicate numbers are removed; no valid citation means insufficient evidence.
- **Relevance threshold**: configurable minimum score; below it no answer-provider call is made.
- **Caps**: questions are limited to 1000 characters, passages to 8, answers to 1000 characters, and provider execution to 30 seconds by default.
- **Auditing**: one `answer.question` event per request stores tenant, actor, time, and cited document IDs as comma-separated 32-character GUIDs in `Detail` (maximum 8 ids). A no-evidence or provider-failure event stores no document ids. Search and answer audits never include question, prompt, passage, or answer text.
- **Failures**: provider timeout or declared provider failure returns HTTP 503 with a fixed safe message. Caller cancellation is preserved.

## API
`POST /api/answers { question }` → `{ answer, insufficientEvidence, citations: [ {documentId, documentTitle, chunkId, pageStart/End, sheet, rowStart/End} ] }`.

## Structure
`Application/Answers/*`, `Infrastructure/Answers/*`, tests in `Tests/Answers`. Angular: ask page.

## Constitution Check
Authorization and tenant filtering remain in application/database services before passages reach the provider. Provider choice is fake-only; no external data transfer occurs. Citation mapping uses only retrieved passages, prompt context treats document text as untrusted, errors are safe, and audit records contain no content.
