# Implementation Plan: Document Search and Embeddings

**Spec**: [spec.md](./spec.md) | **Depends on**: 003

## Summary
Index chunks into pgvector via an application-owned embedding interface, and serve tenant-filtered vector search. The confirmed implementation uses a local deterministic fake, not an AI model.

## Key Decisions
- **Provider decision**: fake only for this feature; no Ollama, hosted model, external calls, model install, or provider credentials. `FakeEmbeddingProvider` hashes exact tokens into a deterministic normalized vector. It supports lexical word-overlap ranking and is not semantically intelligent.
- **Interface**: `IEmbeddingProvider.EmbedAsync(IReadOnlyList<string>, ct)` in Application. Keep the fake provider behind this interface so a real provider can be considered separately after model and data terms are approved.
- **Dimension**: 1536, fixed by the migration. `Embeddings:Dimensions` is validated against the schema; changing it requires a migration and compatible provider.
- **Configuration**: `Embeddings:Provider=Fake`, `Embeddings:Dimensions=1536`, `Embeddings:BatchSize=32`, `Embeddings:WorkerEnabled=true`.
- **Storage**: `ChunkEmbedding(ChunkId PK, TenantId, DocumentId, Vector, Model, CreatedAt)` with an HNSW cosine index; `Document.IndexStatus` (NotIndexed, Indexing, Indexed, IndexFailed) and safe reason.
- **Indexing**: worker picks Completed documents with NotIndexed, batches chunks, writes all vectors in one transaction (delete-then-insert for re-index) so documents flip atomically.
- **Search**: SQL joins chunks to documents with `TenantId = @tenant AND DeletedAt IS NULL AND IndexStatus = Indexed`, then orders by cosine distance, `LIMIT` capped (max 20). Query embedding is the only data sent to the provider.
- **Failures**: provider errors produce `IndexFailed` with a fixed safe reason (`provider_unavailable`, `rate_limited`, `invalid_config`, `no_passages`, `too_large`, `internal`). Tenant Admins and Document Managers explicitly re-index a failed or indexed document.

## API
`POST /api/search { query, limit }` → `[ { documentId, chunkId, ordinal, pageStart/End, sheet, rowStart/End, score, snippet } ]`; `POST /api/documents/{id}/reindex` for TenantAdmin/DocumentManager.

## Structure
`Application/Search/*`, `Infrastructure/Embeddings/*` (providers, worker, store), tests in `Tests/Search`. Angular: search page.

## Constitution Check
Tenant and deletion filters are in the database query before ranking and returning results. The only configured provider is local and deterministic; no passages leave the process. Audit events do not include queries or document content. Access remains consistent with feature 001: auditors are not document readers and cannot search.
