# Task 005 — RAG Question Answering and Citations

**Feature specification:** `005-rag-question-answering-and-citations`  
**Requirements:** Root README sections 4 (Phase 3), 5.3, 6, 8, 11–14  
**Depends on:** [Task 004 — Document Search and Embeddings](../004-document-search-and-embeddings/README.md)

## Goal

Answer user questions using only relevant, permission-filtered document passages. Return citations that point to retrieved source material and clearly indicate when evidence is insufficient.

## Scope for this task

- Use only the deterministic local fake provider until a real provider's retention, region, and training terms are approved. Do not send content externally or add credentials.
- Retrieve authorized passages before constructing a prompt or sending content externally.
- Treat document text as untrusted input and keep authorization decisions outside the model.
- Return verifiable source citations and an insufficient-evidence response.
- Add provider-independent tests for citations, retrieval permissions, prompt-injection cases, and failures.

Do not add persistent conversation history; that is Task 006.

## Work sequence

1. Record the fake-only decision; defer any external provider until privacy terms are approved.
2. Specify prompt boundaries, citation shape, response validation, and no-evidence behavior.
3. Implement orchestration over the authorized retrieval from Task 004.
4. Ensure logs and errors do not expose full prompts or sensitive passages.
5. Test grounded answers, fabricated/invalid citations, insufficient evidence, and cross-tenant requests.

## Current provider and limits

The registered provider is a deterministic extractive fake. It selects a source sentence using exact word overlap; it is not a language model and makes no network calls. Defaults cap questions at 1000 characters, retrieved passages at 8, answers at 1000 characters, and provider execution at 30 seconds. The relevance threshold defaults to `0.1`; questions below it return an insufficient-evidence response without calling the answer provider.

One `answer.question` audit event per request stores tenant, actor, time, and cited document IDs only (comma-separated 32-character GUIDs, up to 8). Questions, prompts, passages, and answer text are not persisted in audit events or logged.

## Completion checklist

- Only authorized passages reach the AI provider.
- Citations point to retrieved source documents and page/section details when available.
- Insufficient evidence is clearly communicated instead of being presented as fact.
- Document instructions cannot override system policy or permissions.
- Automated tests cover permissions, citations, provider failures, and safe output handling.

## Copilot task prompt

> Read the root `README.md`, `docs/README.md`, and this task guide. Implement only RAG question answering over Task 004's authorized retrieval. Confirm provider and privacy decisions before sending data externally. Test citations, insufficient evidence, prompt-injection resistance, and tenant isolation. Do not add conversation persistence.
