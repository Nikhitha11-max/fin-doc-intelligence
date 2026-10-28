# FinDoc AI — Project Requirements

**Repository:** `fin-doc-intelligence`  
**Product name:** FinDoc AI  
**Document type:** Product and system requirements  
**Status:** Initial planning draft  
**Audience:** Product owner, developer, GitHub Copilot, and Spec Kit

> For the proposed repository layout, local setup, and task-by-task implementation guides, see the [documentation hub](docs/README.md). This file remains the product requirements reference.

---

## 1. Project Overview

FinDoc AI is a secure financial-document intelligence platform. It will help authorized users upload, organize, search, and understand financial documents using AI.

The system will use **Retrieval-Augmented Generation (RAG)** to answer questions using information retrieved from documents the user is allowed to access. Answers should include citations so users can check the source.

The project will be built incrementally using **Spec-Driven Development** and GitHub Spec Kit. Requirements and acceptance criteria should be reviewed before implementation.

### 1.1 Main technology choices

These are the current proposed choices and can be revised before implementation:

- **Backend:** ASP.NET Core / .NET
- **Frontend:** Angular and TypeScript
- **Database:** PostgreSQL
- **Vector search:** PostgreSQL with the `pgvector` extension
- **Data access:** Entity Framework Core
- **AI:** Hosted AI API behind a provider abstraction
- **Development file storage:** Private local storage, outside the public web root
- **Authentication:** ASP.NET Core Identity or another reviewed identity solution
- **Architecture:** Modular monolith with clear separation between domain, application, infrastructure, and API layers
- **Development approach:** GitHub Spec Kit, small feature specifications, tests, and incremental implementation

Confirm the supported .NET, Angular, PostgreSQL, and AI-provider versions when the project is initialized.

---

## 2. Problem and Goals

### 2.1 Problem

Financial information is often spread across PDFs, scanned files, spreadsheets, and other documents. Finding a specific figure or understanding a document can take time. General-purpose AI may also give answers that are not grounded in the correct source or may expose information to the wrong user if access controls are poorly designed.

### 2.2 Goals

FinDoc AI should:

1. Provide secure access to financial documents.
2. Let authorized users upload, browse, view, and manage documents.
3. Extract text from supported documents.
4. Index document content for relevant search.
5. Answer questions using retrieved document passages.
6. Cite the source document and relevant page or section when available.
7. Enforce access permissions throughout search and AI workflows.
8. Record important security and document-management events.
9. Be maintainable, testable, and extensible.
10. Be developed from reviewed, testable specifications.

### 2.3 Non-goals for the first release

Unless explicitly approved later, the initial release will not:

- Make lending, investment, credit, or other financial decisions automatically.
- Replace professional financial, legal, or compliance judgment.
- Train a model on private customer documents.
- Support every possible file format.
- Guarantee that AI-generated answers are always correct.
- Expose uploaded files through public URLs.
- Implement complex enterprise integrations before core security and document workflows are established.

---

## 3. Users and Permissions

The system should support multiple organizations or tenants. A tenant is an organization whose users and documents must be isolated from those of other organizations.

Proposed roles:

| Role | Intended capabilities |
|---|---|
| Platform Administrator | Manage platform-level configuration and tenant lifecycle, subject to strict controls |
| Tenant Administrator | Manage users and permissions within their own tenant |
| Document Manager | Upload and manage documents allowed by tenant policy |
| Analyst | Search permitted documents and ask questions about them |
| Auditor | Review permitted audit records without automatically gaining access to document contents |

These are conceptual roles. Exact permissions must be specified and tested before implementation. Do not assume that every user can see every document in their tenant.

### 3.1 Authorization principles

- Deny access by default.
- Verify permissions on the server for every protected operation.
- Never rely only on frontend route guards or hidden buttons.
- Enforce tenant boundaries in document access, search, retrieval, downloads, conversations, and audit views.
- Do not allow a user to change a tenant identifier in a request to gain access to another tenant.
- Keep platform administration separate from tenant-level administration.
- Record sensitive administrative and access-control changes.

---

## 4. Scope of the Initial Release

The first release should establish a secure, usable document-management foundation before adding AI features.

### Phase 1 — Secure foundation

- Project structure and development environment
- Authentication
- Tenant and membership model
- Server-side authorization
- Private document storage
- Upload validation
- Document metadata and listing
- Document details and download
- Document update and deletion rules
- Audit logging
- Automated tests for tenant isolation and authorization

### Phase 2 — Document processing

- Background processing workflow
- Text extraction from approved file types
- Processing status and failure handling
- Page or section metadata where available
- Chunking extracted text into searchable passages
- Secure storage of extracted text

### Phase 3 — Search and RAG

- Embedding generation through a provider abstraction
- Vector storage and similarity search
- Permission-filtered retrieval
- Question-answering workflow
- Source citations
- Clear handling when the source documents do not support an answer

### Phase 4 — Conversation and operational improvements

- Conversation history
- User feedback on answers
- Operational monitoring and diagnostics
- Improved retry and recovery processes
- Additional file formats and integrations after review

Phases may change after the first specifications and technical risks are reviewed.

---

## 5. Functional Requirements

Each requirement should eventually be converted into testable acceptance criteria in the relevant Spec Kit feature specification.

### 5.1 Authentication and tenant management

**FR-001 — Authentication:** Users must authenticate before accessing protected application functionality.

**FR-002 — Tenant membership:** The system must associate users with the tenant or tenants they are authorized to access.

**FR-003 — Tenant administration:** Authorized administrators must be able to manage tenant users and memberships within their permitted scope.

**FR-004 — Authorization:** The server must verify the current user's permission for every protected action.

### 5.2 Document management

**FR-005 — Upload:** Authorized users must be able to upload supported financial documents.

**FR-006 — Validation:** The server must validate file size, file type, file signature where practical, and other configured upload restrictions. It must not trust the filename or browser-provided content type alone.

**FR-007 — Document listing:** Users must be able to list only documents they are authorized to view.

**FR-008 — Document details:** Authorized users must be able to view permitted metadata and processing status.

**FR-009 — Download:** The server must authorize each download and must not expose private storage paths or public file URLs.

**FR-010 — Update and delete:** Authorized users must be able to update permitted metadata and delete documents according to documented retention and deletion rules.

**FR-011 — Audit events:** The system must record important events, including uploads, downloads where required, deletions, permission changes, and administrative actions. Audit records must not contain passwords, access tokens, or unnecessary sensitive document content.

### 5.3 Processing and AI

**FR-012 — Document processing:** The system must process supported documents asynchronously when appropriate and expose a status such as Pending, Processing, Completed, or Failed.

**FR-013 — Text extraction and chunking:** The system must extract text from supported files and split it into passages while retaining source metadata needed for citations.

**FR-014 — Search:** The system must retrieve relevant passages only from documents the requesting user is permitted to access.

**FR-015 — AI question answering:** The system must construct answers from retrieved, authorized passages rather than treating the AI model as the source of truth.

**FR-016 — Citations and uncertainty:** Answers should cite supporting documents and page or section details when available. If there is insufficient evidence, the system must say so instead of inventing a supported answer.

**FR-017 — Conversation history:** If enabled, users must be able to view only conversations they are authorized to access. Conversation history must not bypass current document permissions.

**FR-018 — Provider abstraction:** AI and embedding providers must be accessed through an application-owned interface so the provider can be changed without rewriting business logic.

**FR-019 — Processing failures:** The system must record a safe, diagnosable failure status and support an approved retry process without creating duplicate or inconsistent data.

---

## 6. RAG Explained Simply

**RAG** means Retrieval-Augmented Generation. It lets the application look up relevant information in approved documents before asking an AI model to produce an answer.

### 6.1 Document ingestion flow

1. An authorized user uploads a document.
2. The API validates the upload and stores the original file in private storage.
3. The system records metadata and an audit event.
4. A processing worker extracts text.
5. The text is divided into smaller passages, called chunks.
6. The system creates embeddings for the chunks through the configured AI provider.
7. The system stores the chunks, embeddings, and source metadata.
8. The document becomes searchable after processing completes.

### 6.2 Question-answering flow

1. An authenticated user asks a question.
2. The API checks the user's identity, tenant, and permissions.
3. The system searches only the documents and passages the user may access.
4. Relevant passages are assembled into a controlled prompt.
5. The AI provider generates an answer grounded in those passages.
6. The application returns the answer with citations and any appropriate uncertainty notice.
7. The system records permitted operational and audit information without logging unnecessary sensitive content.

### 6.3 Important RAG rules

- Retrieval must be permission-filtered before content is sent to the AI provider.
- A model response must never grant access to a document.
- Treat document contents as untrusted input; a document may contain instructions designed to manipulate the model.
- Do not follow instructions embedded in a document that conflict with system policy or user permissions.
- Do not claim a citation supports an answer unless it points to the retrieved source passage.
- If no suitable evidence is found, say that the available documents do not provide enough information.
- Define how original files, extracted text, chunks, embeddings, and backups are deleted when a document is deleted.

---

## 7. Proposed Architecture

Use a modular monolith initially. This keeps deployment and development manageable while preserving clear boundaries between responsibilities.

### 7.1 Components

**Angular frontend**
- Login and application screens
- Document upload and listing
- Document details and processing status
- Search and question-answering interface
- Client-side usability controls, not the final security boundary

**ASP.NET Core API**
- Authentication and authorization
- Input validation
- Application use cases
- Tenant and document access enforcement
- API error handling
- Audit-event creation

**Application layer**
- Use cases and orchestration
- Interfaces for storage, text extraction, AI, embeddings, and background jobs
- Validation and application-level policies

**Domain layer**
- Core business entities and rules
- Tenant, membership, document, and permission concepts
- Business invariants independent of infrastructure

**Infrastructure layer**
- Entity Framework Core and PostgreSQL
- `pgvector` integration
- Private file storage
- AI-provider adapters
- Text extraction and background processing
- Logging and operational integrations

**PostgreSQL**
- Users or identity-related data as appropriate
- Tenants and memberships
- Document metadata
- Extracted text chunks and embeddings
- Conversations and messages, if enabled
- Audit events

**Private file storage**
- Original uploaded documents
- Storage paths never directly exposed to the browser
- Access mediated by the backend

**Hosted AI provider**
- Text generation and embedding generation through an application-owned abstraction
- Only the minimum necessary, permission-checked content sent to the provider
- Provider data-retention and training policies reviewed before production use

### 7.2 Architecture rules

- Keep business logic out of controllers and UI components.
- Keep database and provider-specific code behind interfaces where practical.
- Use dependency injection.
- Use database migrations for schema changes.
- Use background jobs for long-running document processing.
- Make operations retry-safe where possible.
- Avoid microservices until there is a clear operational need.
- Keep secrets out of source control.
- Document architectural decisions that affect security, cost, or long-term maintainability.

---

## 8. Security and Privacy Requirements

Financial documents may contain confidential and personal information. Security must be designed in from the start, not added after the AI features.

### 8.1 Identity and access

- Require authentication for protected endpoints.
- Apply least-privilege authorization.
- Validate tenant membership and document-level permissions server-side.
- Prevent insecure direct object reference (IDOR) vulnerabilities.
- Apply appropriate session, token, and account-protection practices.
- Protect administrative functions with stronger controls appropriate to the deployment.

### 8.2 File security

- Store files outside the publicly served web root or in an appropriately private object store.
- Generate storage identifiers rather than using user filenames as storage paths.
- Restrict file size and supported types.
- Validate content as well as filename extensions.
- Consider malware scanning and safe PDF/document parsing.
- Do not execute uploaded files.
- Set safe download headers and content types.
- Protect against path traversal, archive expansion attacks, and parser vulnerabilities where applicable.

### 8.3 AI security

- Enforce authorization before retrieval and before sending content to the AI provider.
- Treat retrieved document text as untrusted data.
- Defend against prompt injection and attempts to reveal unrelated documents or system instructions.
- Do not send secrets, credentials, or unrelated documents to the model.
- Review the AI provider's retention, training, processing-location, and contractual terms.
- Do not rely on the model to make authorization decisions.
- Validate and safely render model output.
- Avoid logging full prompts or document passages unless there is a reviewed, justified need.

### 8.4 Data protection

- Use HTTPS in deployed environments.
- Protect database credentials, API keys, and encryption keys.
- Define encryption-at-rest and backup requirements for the target environment.
- Define data retention and deletion policies.
- Consider whether extracted text, embeddings, audit records, and conversation history need separate retention periods.
- Avoid placing sensitive financial data in telemetry or error messages.
- Test that users cannot retrieve data across tenant boundaries.

### 8.5 Security acceptance tests

At minimum, automated tests must verify that:

- A user from Tenant A cannot list, read, download, update, or delete Tenant B's documents.
- A user cannot obtain another user's conversation by changing an identifier.
- Search and RAG never return passages from unauthorized documents.
- Unauthorized requests fail even if they bypass the Angular interface.
- Invalid or oversized uploads are rejected.
- File paths and secrets are not returned in API responses.
- Deleting a document follows the defined deletion policy across derived data.
- Audit records are created for required events and do not leak unnecessary sensitive content.

Before production, define measurable limits for file size, accepted formats, retention, backup recovery, rate limiting, and performance.

---

## 9. Non-Functional Requirements

These requirements need measurable targets agreed before production.

### Security
The system must enforce server-side authorization and tenant isolation on all protected data paths.

### Privacy
Only necessary data should be collected, stored, logged, or sent to external providers.

### Reliability
Processing failures must be visible and recoverable through a documented retry or remediation process.

### Performance
Agree on targets for upload response time, document processing time, search latency, and AI response time using representative document sizes and loads.

### Scalability
The architecture should allow processing workers and storage capacity to scale without requiring a complete redesign.

### Maintainability
Use clear project boundaries, consistent naming, code review, and documented architectural decisions.

### Testability
Core business logic and access rules must be testable without depending on a live AI provider.

### Observability
Provide structured logs, health checks, metrics, and correlation identifiers while avoiding sensitive data in telemetry.

### Usability
Show clear processing states, actionable validation errors, understandable citations, and a clear distinction between source evidence and generated text.

### Recoverability
Define backup, restore, and disaster-recovery procedures before production. Set measurable recovery-point and recovery-time objectives.

---

## 10. Conceptual Data Model

These are proposed concepts, not a final database schema.

| Entity | Purpose |
|---|---|
| Tenant | Represents an organization or workspace |
| User | Represents an authenticated person |
| TenantMembership | Connects a user to a tenant and role |
| Role / Permission | Defines allowed actions |
| Document | Stores document metadata, ownership scope, and processing status |
| DocumentAccessPolicy | Represents document-specific access rules if required |
| DocumentChunk | Stores extracted text passages and source metadata |
| DocumentEmbedding | Stores vector representations used for similarity search |
| AuditEvent | Records important security and business events |
| Conversation | Represents an AI question-answering conversation |
| Message | Stores a user question or assistant response, if history is enabled |

### Data-model rules

- Every tenant-owned record must have a clear tenant scope or a safe, enforceable relationship to one.
- Use database constraints and indexes to support integrity and expected query patterns.
- Do not rely on a tenant ID supplied by the client as proof of access.
- Define document lifecycle states and deletion behavior.
- Define whether document access is tenant-wide, role-based, document-specific, or a combination before implementation.
- Store enough source metadata to produce reliable citations.

---

## 11. API and Error-Handling Principles

Exact endpoint paths should be decided in the feature specifications. The API should follow these principles:

- Use consistent request and response formats.
- Validate inputs on the server.
- Return appropriate HTTP status codes.
- Do not disclose stack traces, storage paths, secrets, or internal implementation details to clients.
- Return clear validation errors without exposing confidential data.
- Use pagination for potentially large document and audit lists.
- Apply rate limits and request-size limits where appropriate.
- Define safe retry behavior for background jobs and provider calls.
- Use correlation identifiers to help diagnose failures.

---

## 12. Testing Strategy

### Unit tests
Test domain rules, validation, permission policies, chunking behavior, and application use cases.

### Integration tests
Test database persistence, migrations, storage access, processing workflows, and API behavior.

### Authorization tests
Test tenant isolation, role permissions, document-level access, and unauthorized identifiers.

### RAG tests
Use a controlled set of documents and questions to test retrieval relevance, citations, refusal when evidence is insufficient, and permission filtering.

### End-to-end tests
Test important user journeys such as login, upload, processing status, listing, download, search, and asking a question.

### Provider-independent tests
Mock AI and embedding interfaces for most automated tests. Use a small, controlled set of integration tests for the real provider.

### Quality gates
Before merging a feature:
- Build successfully.
- Run relevant automated tests.
- Apply database migrations in a test environment.
- Review authorization and data-handling changes.
- Update specifications and documentation when behavior changes.
- Do not silently weaken a security requirement to make tests pass.

---

## 13. Spec-Driven Development Workflow

Use GitHub Spec Kit to define what should be built before asking the coding agent to implement it.

For each feature:

1. Read this requirements document.
2. Identify unclear decisions and resolve them with the product owner.
3. Write a feature specification with user stories and measurable acceptance criteria.
4. Review edge cases, security, privacy, and failure behavior.
5. Create a technical plan consistent with the approved architecture.
6. Break the plan into small, reviewable tasks.
7. Implement the tasks incrementally.
8. Run tests and inspect the changes.
9. Confirm the implementation meets the specification.
10. Update the specification and documentation if an approved requirement changes.

### Proposed feature specifications

1. `001-secure-document-management`
2. `002-tenant-and-user-access-control`
3. `003-document-processing-and-text-extraction`
4. `004-document-search-and-embeddings`
5. `005-rag-question-answering-and-citations`
6. `006-conversation-history`
7. `007-audit-and-operational-monitoring`

The exact order and grouping may change after dependencies are reviewed. Secure document access and tenant isolation must be established before AI retrieval is enabled.

### Suggested first feature

Start with `001-secure-document-management`, including the necessary tenant and authorization foundations. It should cover upload validation, private storage, metadata, listing, authorized download, update/delete rules, audit events, and automated security tests.

Do not implement the entire platform in one coding-agent request.

---

## 14. Definition of Done

A feature is complete only when:

- Its approved acceptance criteria are met.
- Authorization is enforced on the server.
- Tenant isolation is tested where relevant.
- Error and failure cases are handled.
- Automated tests pass.
- Database changes use reviewed migrations.
- Secrets and sensitive content are not accidentally exposed.
- Logs and audit events meet the applicable requirements.
- Documentation and specifications match the delivered behavior.
- The change has been reviewed before merging.

---

## 15. Risks and Mitigations

| Risk | Mitigation |
|---|---|
| Cross-tenant data exposure | Centralized authorization policies, scoped queries, and automated isolation tests |
| Incorrect AI answers | Source-grounded retrieval, citations, uncertainty handling, and evaluation tests |
| Prompt injection in documents | Treat document content as untrusted and keep authorization outside the model |
| Sensitive data sent to an AI provider | Minimize transmitted content and review provider terms and configuration |
| Unsafe uploaded files | Validate file content, limit formats and sizes, store privately, and assess malware scanning |
| Expensive AI usage | Apply quotas, rate limits, monitoring, caching where safe, and model/provider configuration |
| Processing failures | Track status, record safe diagnostics, and provide controlled retry |
| Accidental deletion or incomplete cleanup | Define lifecycle, retention, backup, and deletion behavior for originals and derived data |
| Scope becoming too large | Deliver small, specified features in phases |

---

## 16. Decisions to Confirm Before Production

These decisions should be recorded rather than guessed by the coding agent.

1. **Identity:** Which identity provider and sign-in method will be used?
2. **Tenant model:** Can a user belong to multiple tenants?
3. **Document permissions:** Are documents visible to all authorized tenant members, specific roles, or selected users?
4. **Supported files:** Which formats and maximum sizes are required for the first release?
5. **Scanned documents:** Is OCR required initially, and which OCR solution is acceptable?
6. **AI provider:** Which provider, model, embedding model, region, and data-retention settings are approved?
7. **Hosting:** Where will the API, frontend, database, and file storage run?
8. **File storage:** Is local development storage sufficient for development, and which private storage service will production use?
9. **Retention:** How long should original files, extracted text, embeddings, conversations, and audit events be retained?
10. **Deletion:** Must deletion be immediate, soft-deleted, or subject to a retention/legal-hold policy?
11. **Compliance:** Which regulatory, contractual, or organizational obligations apply?
12. **Operational targets:** What are the expected user count, document volume, file sizes, latency targets, and availability needs?
13. **Backups:** What are the required recovery-point and recovery-time objectives?
14. **AI history:** Should conversations be saved, and can users delete them?
15. **Human review:** Which use cases require a human to verify AI output before acting on it?

Until these are confirmed, use safe development defaults and mark unresolved choices clearly.

---

## 17. Glossary

- **API:** The interface through which the frontend communicates with the backend.
- **ASP.NET Core:** The .NET framework used to build the backend web application.
- **Angular:** The frontend framework used to build the web interface.
- **Embedding:** A numerical representation of text used to compare semantic similarity.
- **Tenant:** An organization or workspace whose data is isolated from other tenants.
- **Chunk:** A smaller passage of text extracted from a document for retrieval.
- **OCR:** Optical Character Recognition, which converts text in scanned images into machine-readable text.
- **RAG:** Retrieval-Augmented Generation; retrieving relevant source material before generating an AI answer.
- **Vector search:** Search that compares embeddings to find semantically similar content.
- **Spec-Driven Development:** An approach in which reviewed specifications guide planning, implementation, and testing.
- **GitHub Spec Kit:** A toolkit for organizing a specification-led development workflow.
- **IDOR:** Insecure Direct Object Reference; a vulnerability where changing an identifier can expose another user's resource.
- **Audit event:** A record of an important security or business action.

---

## 18. First Instructions for GitHub Copilot

Before implementation, ask Copilot to review the requirements and documentation:

> Read the root `README.md` completely as the product requirements reference, then read `docs/README.md` and the relevant task guide. Do not implement anything until the requirements and decisions for that task are clear.
>
> 1. Identify ambiguous, conflicting, or missing requirements.
> 2. Identify security requirements that need measurable acceptance criteria.
> 3. Separate first-release requirements from future features.
> 4. List decisions that must be confirmed before architecture planning.
> 5. Confirm the proposed project structure and task dependencies.
> 6. Do not silently change requirements or invent product decisions.
> 7. Return your findings and proposed changes for review.

---

## 19. Summary

FinDoc AI will be developed as a secure financial-document platform, starting with authentication, tenant isolation, document management, private storage, and auditability. Document processing, semantic search, and RAG question answering will be added only after the secure foundation is tested.

The guiding principle is: **users can retrieve only what they are authorized to access, and AI answers must be grounded in identifiable source documents.**
