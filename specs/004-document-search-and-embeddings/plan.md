# Implementation Plan: Document Search and Embeddings

**Spec**: [spec.md](./spec.md) | **Depends on**: 003

## Summary
Index chunks into pgvector via a provider-independent embedding interface, and serve tenant-filtered semantic search.

## Key Decisions
- **Interface**: `IEmbeddingProvider.EmbedAsync(IReadOnlyList<string>, ct)` in Application. `FakeEmbeddingProvider` (deterministic hashing) for tests and dev; a real provider (e.g. Azure OpenAI / OpenAI) is added only after the product owner confirms provider, model and data terms.
- **Dimension**: configurable `Embeddings:Dimensions` (default 1536); column `vector(N)` fixed by migration.
- **Storage**: `ChunkEmbedding(ChunkId PK, TenantId, DocumentId, Vector, Model, CreatedAt)` with an HNSW cosine index; `Document.IndexStatus` (NotIndexed, Indexing, Indexed, IndexFailed) and safe reason.
- **Indexing**: worker picks Completed documents with NotIndexed, batches chunks, writes all vectors in one transaction (delete-then-insert for re-index) so documents flip atomically.
- **Search**: SQL joins chunks to documents with `TenantId = @tenant AND DeletedAt IS NULL AND IndexStatus = Indexed`, then orders by cosine distance, `LIMIT` capped (max 20). Query embedding is the only data sent to the provider.
- **Failures**: provider errors produce `IndexFailed` with code (`provider_unavailable`, `rate_limited`, `invalid_config`) and retry with backoff.

## API
`POST /api/search { query, limit }` → `[ { documentId, chunkId, ordinal, pageStart/End, sheet, rowStart/End, score, snippet } ]`; `POST /api/documents/{id}/reindex` for TenantAdmin/DocumentManager.

## Structure
`Application/Search/*`, `Infrastructure/Embeddings/*` (providers, worker, store), tests in `Tests/Search`. Angular: search page.

## Constitution Check
Tenant filter before ranking, no secrets in source, provider isolated, audit without content: OK.
