# FinDoc AI documentation

Start with the [project README](../README.md) for an overview and technology
stack. These guides describe the current implementation:

- [Local development](local-development.md): prerequisites, database setup,
  running the API and UI, test commands, and troubleshooting.
- [Application workflows](application-workflows.md): roles, documents, search,
  answers, conversations and audit.
- [Architecture](architecture.md): project boundaries, data flow, configuration
  and current limitations.

## Feature documentation

The following guides and the [feature specifications](../specs/) record the
project's spec-driven development. Some task guides contain historical planning
or implementation snapshots; consult the current guides above and the code for
present behavior.

| Feature | Task guide | Specification |
|---|---|---|
| Secure document management | [Task 001](tasks/001-secure-document-management/README.md) | [Spec 001](../specs/001-secure-document-management/spec.md) |
| Tenant and user access | [Task 002](tasks/002-tenant-and-user-access-control/README.md) | [Spec 002](../specs/002-tenant-and-user-access-control/spec.md) |
| Document processing | [Task 003](tasks/003-document-processing-and-text-extraction/README.md) | [Spec 003](../specs/003-document-processing-and-text-extraction/spec.md) |
| Search and embeddings | [Task 004](tasks/004-document-search-and-embeddings/README.md) | [Spec 004](../specs/004-document-search-and-embeddings/spec.md) |
| Questions and citations | [Task 005](tasks/005-rag-question-answering-and-citations/README.md) | [Spec 005](../specs/005-rag-question-answering-and-citations/spec.md) |
| Conversation history | [Task 006](tasks/006-conversation-history/README.md) | [Spec 006](../specs/006-conversation-history/spec.md) |
| Audit and monitoring | [Task 007](tasks/007-audit-and-operational-monitoring/README.md) | [Spec 007](../specs/007-audit-and-operational-monitoring/spec.md) |
