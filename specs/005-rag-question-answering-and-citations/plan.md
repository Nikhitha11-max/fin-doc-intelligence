# Implementation Plan: RAG Question Answering and Citations

**Spec**: [spec.md](./spec.md) | **Depends on**: 004

## Summary
Orchestrate: authorized retrieval (004) → prompt build → provider call → citation validation → response.

## Key Decisions
- **Interface**: `IAnswerProvider.AnswerAsync(AnswerRequest, ct)` in Application, where `AnswerRequest` has the question and a numbered list of passages. `FakeAnswerProvider` for tests; a real provider is added only after privacy terms are confirmed.
- **Prompt**: fixed system policy; passages wrapped in delimiters and labelled `[1]..[n]`; explicit instruction that passage text is data, not instructions.
- **Citations**: the model must cite by passage number; the service maps numbers back to retrieved passages and drops any unknown number. An answer with no valid citation becomes the insufficient-evidence response.
- **Relevance threshold**: configurable minimum score; below it no provider call is made at all.
- **Caps**: max question length, max passages (default 8), max answer tokens.
- **Logging**: log only ids, counts and timings, never prompt text, passages or answers.

## API
`POST /api/answers { question }` → `{ answer, insufficientEvidence, citations: [ {documentId, chunkId, pageStart/End, sheet, rowStart/End} ] }`.

## Structure
`Application/Answers/*`, `Infrastructure/Ai/*`, tests in `Tests/Answers`. Angular: ask page.

## Constitution Check
Authorization outside the model, untrusted document text, safe errors, audit without content: OK.
