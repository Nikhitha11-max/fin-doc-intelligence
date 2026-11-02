# Task 004 — Document Search and Embeddings

**Feature specification:** `004-document-search-and-embeddings`  
**Requirements:** Root README sections 4 (Phase 3), 5.3, 6–8, 10–14  
**Depends on:** [Task 003 — Document Processing and Text Extraction](../003-document-processing-and-text-extraction/README.md)

## Goal

Create vectors for processed document passages and provide tenant-scoped search without returning content the requesting user is not allowed to access. The fake provider ranks exact word overlap and does not provide semantic AI.

## Confirmed provider decision

Feature 004 uses a deterministic, local fake embedding provider only. It makes no external API calls, needs no model installation or credentials, and does not provide semantic AI; search ranking is based on exact word overlap. A real model/provider requires a separate decision about the model, vector dimension, privacy and data terms.

The database schema and fake provider use 1536 dimensions. `Embeddings:Dimensions` must remain `1536` unless a schema migration is prepared. Configuration is under `Embeddings:`; defaults are `Provider=Fake`, `Dimensions=1536`, `BatchSize=32` and `WorkerEnabled=true`.

## Scope for this task

- Keep the confirmed fake-only provider and 1536-dimension schema; resolve any real-provider privacy decision separately.
- Define how passages are selected and how source metadata is retained.
- Add provider-independent interfaces and tests; keep credentials outside source control.
- Store embeddings and implement permission-filtered retrieval.
- Test relevance on a controlled data set and test cross-tenant isolation.

Do not generate natural-language answers; that belongs to Task 005.

## Work sequence

1. Record the fake-only provider decision, vector schema, and expected search behavior.
2. Specify indexing, re-indexing, deletion, and provider-failure behavior.
3. Implement embedding generation and storage for successfully processed documents.
4. Apply authorization filters before returning passages or sending content to any provider.
5. Run relevance, failure, deletion, and cross-tenant tests.

## Completion checklist

- Search only considers indexed, processed documents the caller is authorized to access.
- Embedding provider integration is isolated behind an application-owned interface.
- Re-indexing and deletion do not leave inconsistent or unauthorized vectors.
- Tests cover relevance, provider-independent behavior, and permission filtering.
- Search results include source identifiers/metadata suitable for later citations.
- Search over 10,000 passages is verified under two seconds in the integration test.

## Copilot task prompt

> Read the root `README.md`, `docs/README.md`, and this task guide. Implement only embedding generation, vector storage, and authorized semantic search. Confirm provider/model decisions before wiring a provider. Add tests proving unauthorized passages are never returned. Do not implement generated answers or conversation history.
