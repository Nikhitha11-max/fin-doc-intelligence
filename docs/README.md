# FinDoc AI — Project Structure and Setup

This guide describes the repository layout, local development prerequisites, and the order for building FinDoc AI. The projects are scaffolded only; no feature code or database setup exists yet.

Use the root [README.md](../README.md) as the product and security requirements source. Complete one task guide at a time; each guide links to the relevant requirements and states its prerequisites and completion checks.

## Proposed repository structure

```text
fin-doc-intelligence/
├── README.md                         # Product and system requirements
├── .gitignore                        # Ignore local secrets and generated files
├── docs/
│   ├── README.md                     # This guide: structure, setup, task index
│   └── tasks/
│       ├── 001-secure-document-management/README.md
│       ├── 002-tenant-and-user-access-control/README.md
│       ├── 003-document-processing-and-text-extraction/README.md
│       ├── 004-document-search-and-embeddings/README.md
│       ├── 005-rag-question-answering-and-citations/README.md
│       ├── 006-conversation-history/README.md
│       └── 007-audit-and-operational-monitoring/README.md
├── specs/                            # Reviewed feature specifications
├── src/
│   ├── backend/
│   │   ├── FinDoc.Api/               # ASP.NET Core HTTP API
│   │   ├── FinDoc.Application/       # Use cases and application policies
│   │   ├── FinDoc.Domain/            # Business entities and rules
│   │   ├── FinDoc.Infrastructure/    # PostgreSQL, storage, and provider adapters
│   │   └── FinDoc.Worker/            # Background document processing
│   └── frontend/                     # Angular application
└── tests/
    ├── backend/                      # Unit and integration tests
    └── frontend/                     # Angular tests
```

Create only the projects needed for the approved task. The worker, application layers, and test folders can be introduced when their first feature needs them. Keep names and exact project boundaries consistent with the reviewed technical plan; this diagram is a starting point, not a requirement to create empty projects.

## Initial local setup

### 1. Review decisions and install tools

Before creating application projects, confirm the supported versions and local choices in section 16 of the root README. Install:

- Git
- The approved .NET SDK
- Node.js and npm versions supported by the selected Angular release
- PostgreSQL with the `pgvector` extension available for the development database
- GitHub Spec Kit tooling if the team will use it for feature specifications

Check installed tools:

```sh
git --version
dotnet --info
node --version
npm --version
psql --version
```

Do not assume that a globally installed Angular CLI or a particular AI provider is required; use the versions and setup method agreed for this repository.

### 2. Prepare the development environment

1. Clone the repository and create a feature branch.
2. Read the root requirements README and the guide for the task being started.
3. Configure a local PostgreSQL database with `pgvector` when the selected task requires database work.
4. Configure provider credentials only for tasks that call an external provider. Keep secrets in local user-secrets, environment variables, or another approved secret store—never in source files or committed configuration.
5. Add and verify ignore rules for local settings, generated build output, and secrets before creating those files.

### 4. Scaffolded projects and commands

The initial scaffold (no feature code) uses .NET 8, Angular 19, EF Core 8 with Npgsql and `Pgvector.EntityFrameworkCore` 0.2.x (0.3.x requires EF Core 9). `FinDoc.Worker` is not created yet; add it with Task 003.

```sh
dotnet build FinDoc.sln                    # build backend
dotnet test FinDoc.sln                     # run backend tests
dotnet run --project src/backend/FinDoc.Api
cd src/frontend && npm install && npm start   # Angular dev server
```

Local database (macOS/Homebrew): PostgreSQL 17 with `pgvector` (the Homebrew `pgvector` formula supports PostgreSQL 17 and 18 only, not 16).

```sh
brew install postgresql@17 pgvector
brew services start postgresql@17
export PATH=/opt/homebrew/opt/postgresql@17/bin:$PATH
createdb findoc_dev
psql findoc_dev -c "CREATE EXTENSION vector;"
```

A `findoc_dev` database with the `vector` extension has been created locally. Connection strings belong in user-secrets or environment variables, not source control. No migrations exist yet.

### 3. Work one task at a time

For each task:

1. Read the task's `README.md` and the linked requirements in the root README.
2. Resolve open decisions with the product owner before coding.
3. Write and review the feature specification and acceptance criteria.
4. Implement only that task's scope, including tests and documentation.
5. Run the task's quality checks and review the diff.
6. Confirm the completion checklist before moving to the next task.

## Task order

The order below follows the feature specifications in the root requirements. Task 001 must establish the authorization and tenant-scoping foundation needed for secure documents. Task 002 builds out tenant and membership administration on top of that foundation. Later tasks depend on the document lifecycle and permission-filtered retrieval being in place.

| Order | Task guide | Goal |
|---|---|---|
| 001 | [Secure document management](tasks/001-secure-document-management/README.md) | Establish the secure foundation and document lifecycle |
| 002 | [Tenant and user access control](tasks/002-tenant-and-user-access-control/README.md) | Manage tenant membership and role-based access |
| 003 | [Document processing and text extraction](tasks/003-document-processing-and-text-extraction/README.md) | Extract and store searchable source text safely |
| 004 | [Document search and embeddings](tasks/004-document-search-and-embeddings/README.md) | Add permission-filtered semantic search |
| 005 | [RAG question answering and citations](tasks/005-rag-question-answering-and-citations/README.md) | Answer questions from authorized sources with citations |
| 006 | [Conversation history](tasks/006-conversation-history/README.md) | Persist and authorize conversations, if approved |
| 007 | [Audit and operational monitoring](tasks/007-audit-and-operational-monitoring/README.md) | Complete audit coverage and operational visibility |

These are implementation guides, not substitutes for reviewed feature specifications. Keep the task guide, specification, tests, and delivered behavior aligned.
