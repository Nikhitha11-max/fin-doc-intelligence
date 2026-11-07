# Local development

This guide starts the API and Angular frontend with local fake AI providers; no
external AI account or key is required. Commands assume Bash on a Unix-like system and
start from the repository root unless stated otherwise.

## Prerequisites

- .NET 8 SDK.
- Node.js and npm compatible with Angular 19; see the
  [Angular version compatibility table](https://angular.dev/reference/versions).
- PostgreSQL with the pgvector extension installed and available to the server.
- A PostgreSQL role able to create and access a development database. The test
  suite additionally needs permission to create and drop test databases.
- Chrome or Chromium for the frontend tests.

For example, on macOS with Homebrew and PostgreSQL 17:

```sh
brew install postgresql@17 pgvector
brew services start postgresql@17
export PATH="$(brew --prefix postgresql@17)/bin:$PATH"
```

Check `dotnet --version`, `node --version`, `npm --version` and `psql --version`
if you need to verify your installation.

## 1. Prepare PostgreSQL

Create a **new development database** and enable pgvector:

```sh
createdb findoc_dev
psql -d findoc_dev -c "CREATE EXTENSION IF NOT EXISTS vector;"
```

Use an existing development database only if it is appropriate to change it.
Extension creation may require a database owner or administrator. The default
development connection string is `Host=localhost;Database=findoc_dev`; it uses
the current OS user unless another user is configured. Override it with an
environment variable when necessary:

```sh
export ConnectionStrings__Default='Host=localhost;Database=findoc_dev;Username=YOUR_DB_USER'
```

Supply database credentials through environment variables, .NET user secrets,
or another local secret store, not through committed files.

## 2. Start the API and create demo accounts

In the API terminal, provide a new strong development-only password without
putting it in source control. The `read` command hides what you type:

```sh
read -rs -p "Demo password: " Seed__Password; echo
export Seed__Password
dotnet run --project src/backend/FinDoc.Api --launch-profile http
```

In the Development environment, `Seed__Password` triggers database migrations
and seeds these accounts if they do not already exist:

| Email | Role |
|---|---|
| `platform@findoc.test` | PlatformAdmin |
| `admin@acme.test` | Acme TenantAdmin |
| `analyst@acme.test` | Acme Analyst |
| `admin@globex.test` | Globex TenantAdmin |

Newly created accounts use the password you entered. **Existing users are not
reset** when the seed password changes. The seed creates tenants and users, not
sample documents. Do not use demo accounts or passwords in production.

Alternatively, configure both `Bootstrap__PlatformAdminEmail` and
`Bootstrap__PlatformAdminPassword` to create a platform administrator and
apply migrations at startup. This administrator can create organizations and
their first tenant administrators. Platform-only administrators are not
automatically members of a tenant.

The HTTP profile listens at <http://localhost:5276>; Swagger is available at
<http://localhost:5276/swagger> in Development. Stop the process with Ctrl+C.
Without either seeding or bootstrap configured, startup does not automatically
run migrations; apply them separately before using a new database.

## 3. Start the frontend

In a second terminal:

```sh
cd src/frontend
npm ci
npm start
```

Open <http://localhost:4200> and sign in with a tenant account to upload and
search documents. The Angular development proxy forwards `/api` requests to
`http://localhost:5276`, so keep the API running with its HTTP profile. Sign
in with the platform account to manage organizations.

Try a small CSV containing financial data. After upload, wait for the document
to show **Completed** and **Indexed**, then search for a word in the file or
ask a question containing that word. See [application workflows](application-workflows.md)
for details.

## Tests and builds

**Warning:** backend integration tests drop and recreate databases. By default
they use `findoc_test` and derived names such as `findoc_test_search`; the
configured PostgreSQL user needs database creation and deletion privileges.
Never set `FINDOC_TEST_DB` to a development or production database. Use a
dedicated disposable PostgreSQL instance and test database names.

```sh
dotnet build FinDoc.sln
dotnet test FinDoc.sln

cd src/frontend
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
```

If Chrome is not found, set `CHROME_BIN` to the Chrome or Chromium executable
before running frontend tests. To use a non-default disposable test database,
set `FINDOC_TEST_DB` to its PostgreSQL connection string before `dotnet test`;
tests may also derive additional database names from it.

## Troubleshooting

- **Database connection or extension error:** confirm PostgreSQL is running,
  the connection string and role are correct, and pgvector is installed for
  that PostgreSQL server. The role needs permission to create the extension.
- **Login fails after changing the seed password:** seeding does not update
  existing users. Use the original password or create a fresh local account
  through the appropriate administrator.
- **Frontend cannot reach API:** confirm the API is listening on port 5276 and
  the frontend proxy configuration matches that address.
- **Documents stay pending or search finds nothing:** check both processing and
  indexing status, API logs, and `/health/ready`; workers run inside the API.
  The fake providers match words, so try exact terms from the document.
- **Health is degraded:** `/health/ready` reports database and worker status.
  Inspect server logs and platform diagnostics rather than assuming documents
  have finished processing.
