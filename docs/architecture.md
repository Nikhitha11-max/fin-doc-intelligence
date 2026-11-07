# Architecture

FinDoc AI is a modular monolith: the ASP.NET Core API hosts the application and
background workers, and the Angular frontend communicates with it over HTTP.

```text
Angular UI -- /api --> ASP.NET Core API
                           |
                           v
                 Application services
                  /       |        \
                 v        v         v
           PostgreSQL  private   local fake
           + pgvector   files    AI providers
                 ^
                 |
         processing and indexing workers
         hosted in the API process
```

## Code organization

| Project | Responsibility |
|---|---|
| [FinDoc.Api](../src/backend/FinDoc.Api/) | HTTP endpoints, Identity, middleware, startup and health checks |
| [FinDoc.Application](../src/backend/FinDoc.Application/) | Use cases, authorization checks, provider contracts and orchestration |
| [FinDoc.Domain](../src/backend/FinDoc.Domain/) | Domain entities and roles |
| [FinDoc.Infrastructure](../src/backend/FinDoc.Infrastructure/) | EF Core persistence, storage, extractors, search and hosted workers |
| [Frontend](../src/frontend/) | Angular screens, client-side navigation and API calls |
| [Backend tests](../tests/backend/) | Unit and PostgreSQL-backed API and integration tests |

There is no separate worker project: processing, indexing, history cleanup and
audit cleanup are hosted services in the API process.

## Data flow

**Ingestion:** upload -> validation -> private file storage and document record
-> processing worker -> text chunks with source metadata -> indexing worker ->
1536-dimensional vectors in PostgreSQL/pgvector.

**Question:** authenticate and check tenant role -> search accessible indexed
chunks -> construct numbered context -> local answer provider -> validate
citations -> save the exchange and audit the operation -> return answer and
sources. The fake providers run locally; no external model is called.

Original files are stored under the configured local storage root, not served
as public static files. Document downloads pass through the authorized API.
Conversation content is stored for the owner, while audit records are intended
to avoid storing document passages and prompts.

## Configuration and operations

Default settings are in [appsettings.json](../src/backend/FinDoc.Api/appsettings.json);
development database and storage defaults are in
[appsettings.Development.json](../src/backend/FinDoc.Api/appsettings.Development.json).
ASP.NET Core environment variables use double underscores for nested keys.

| Setting | Purpose |
|---|---|
| `ConnectionStrings__Default` | PostgreSQL connection |
| `Storage__RootPath` | Private local file storage root |
| `Seed__Password` | Opt-in Development-only demo user seeding |
| `Bootstrap__PlatformAdminEmail`, `Bootstrap__PlatformAdminPassword` | Bootstrap the initial platform administrator |
| `Processing__WorkerEnabled`, `Embeddings__WorkerEnabled` | Enable processing and indexing workers |
| `Embeddings__Provider`, `Answers__Provider` | Currently only `Fake` is supported |
| `History__RetentionDays`, `Audit__RetentionDays` | History and audit retention |
| `RateLimiting__LoginPermitsPerMinute` | Per-IP auth-path request limit; default 10 per minute |

The API adds security headers and HSTS outside Development. The auth-path rate
limit is an in-process fixed-window limit based on the connection's remote IP:
reverse proxies and multiple API instances require an appropriate deployment
design. Do not assume it is a distributed or per-user limit.

The frontend stores its bearer token in session storage. Swagger is enabled
only in Development. Production deployment requires additional review of
secrets, HTTPS, storage, logging, backups, provider privacy, rate limiting,
retention, deletion, and other security controls.

## External AI is future work

Provider interfaces exist, but dependency injection rejects providers other
than `Fake`. Adding a hosted model requires a real adapter, configuration,
secret management, data-handling review and tests. A new embedding model also
requires an indexing and migration strategy: matching vector dimensions alone
does not make vectors from different models compatible. The current schema uses
1536 dimensions.
