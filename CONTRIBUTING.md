# Contributing

This guide covers local setup, commands and pull requests. Coding conventions live in
[backend/AGENTS.md](backend/AGENTS.md) and [frontend/AGENTS.md](frontend/AGENTS.md); read
[ARCHITECTURE.md](ARCHITECTURE.md) before changing boundaries between projects or frontend layers. Runtime
configuration belongs in [DEPLOYMENT.md](DEPLOYMENT.md).

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 26.x](https://nodejs.org/) and npm
- [Docker](https://www.docker.com/), for the local PostgreSQL and Mailpit services and the backend integration
  tests
- The local .NET tools (`dotnet-ef`, ReportGenerator, CSharpier): `dotnet tool restore` in `backend/` installs
  the versions pinned in `backend/dotnet-tools.json`

## Local setup

### Database and backend

Docker Compose reads the root `.env` and `secrets/`; `dotnet run` reads neither. Start PostgreSQL and Mailpit
with Compose, then provide the API configuration as process environment variables:

```bash
cp .env.example .env
mkdir -m 700 secrets
openssl rand -base64 32 | tr -d '\r\n' > secrets/postgres_password
touch secrets/data_protection_certificate_password secrets/smtp_password
docker compose up -d db mailpit

export POSTGRES_PASSWORD="$(cat secrets/postgres_password)"
export BOOTSTRAP_ADMIN_EMAIL=admin@example.test
export SMTP_HOST=localhost SMTP_PORT=1025 SMTP_SECURITY=None SMTP_FROM_ADDRESS=no-reply@codigoactivo.local
cd backend
dotnet run --project src/CodigoActivo.API
```

Compose needs all three [secret files](DEPLOYMENT.md#secrets) even though Development ignores the Data
Protection password. In PowerShell, set the same variables with `$env:NAME="..."`. Mailpit catches every email
at <http://localhost:8025>: login codes, verification links and the password reset that sets the bootstrap
administrator's first password.

The API listens on <http://localhost:5150> (the `https` launch profile adds <https://localhost:7039>) and
serves Swagger at `/swagger` in Development, where it logs to the console as well as to the log tables of its
database. Missing variables fall back as described in [DEPLOYMENT.md](DEPLOYMENT.md#secrets).

### Frontend

```bash
cd frontend
npm ci
npm run dev
```

Vite serves <http://localhost:5173> and proxies `/api`, `/sitemap.xml` and `/robots.txt` to
<http://localhost:5150>, or to `VITE_API_PROXY_TARGET` when `frontend/.env.local` sets it.

### Complete Docker development stack

See [README.md](README.md#local-development-stack) and
[DEPLOYMENT.md](DEPLOYMENT.md#development-overlay).

## Commands

Run backend commands from `backend/`. The folder holds both `CodigoActivo.slnx` and `docker-compose.dcproj`, so
name the solution or project:

```bash
dotnet build CodigoActivo.slnx
dotnet test --solution CodigoActivo.slnx
dotnet test tests/CodigoActivo.UnitTests
dotnet test tests/CodigoActivo.IntegrationTests
dotnet test tests/CodigoActivo.UnitTests --filter "FullyQualifiedName~ClassName.MethodName"
```

Integration tests require Docker unless `CODIGOACTIVO_TEST_DB_CONNECTION` points to an empty, disposable
PostgreSQL database.

Create migrations with:

```bash
dotnet ef migrations add <Name> --context CodigoActivoDbContext --project src/CodigoActivo.Infrastructure --startup-project src/CodigoActivo.Infrastructure --output-dir Database/Migrations
```

Until the first deployment there is only `InitialCreate` ([AGENTS.md](AGENTS.md#not-deployed-yet)): after a
model change, delete `src/CodigoActivo.Infrastructure/Database/Migrations/` and run the command with that name.
EF Core cannot create a partitioned table, so then replace the generated `CreateTable` of `audit_log_entries`
and `diagnostic_log_entries` with the `CREATE TABLE ... PARTITION BY RANGE (occurred_at)` SQL of the deleted
migration. The API cannot migrate a database created from a previous `InitialCreate`; recreate it, locally
with `docker compose down -v`.

`--context` is required because Infrastructure also has `CodigoActivoReadDbContext`, the read side, which never
has migrations. The tools build the context through `CodigoActivoDesignTimeDbContextFactory`, whose placeholder
database never resolves, so commands that touch a database need `--connection`.
`Microsoft.EntityFrameworkCore.Design` stays out of the API.

Backend coverage merges both test projects and must reach 90% of lines, branches and methods. It uses Microsoft
Code Coverage with `tests/CodeCoverage.config`, which measures the production assemblies and excludes EF Core
migrations:

```bash
dotnet test tests/CodigoActivo.UnitTests --coverage --coverage-output-format cobertura --coverage-output unit.cobertura.xml --coverage-settings tests/CodeCoverage.config --results-directory TestResults/coverage
dotnet test tests/CodigoActivo.IntegrationTests --coverage --coverage-output-format cobertura --coverage-output integration.cobertura.xml --coverage-settings tests/CodeCoverage.config --results-directory TestResults/coverage
dotnet reportgenerator "-reports:TestResults/coverage/*.cobertura.xml" -targetdir:TestResults/coverage/report "-reporttypes:JsonSummary;Html"
dotnet run --file scripts/check-coverage.cs -- TestResults/coverage/report/Summary.json 90
```

`TestResults/coverage/report/index.html` shows the uncovered code.

Run frontend commands from `frontend/`:

```bash
npm run dev             # Vite development server
npm run build           # Type-check and production build
npm test                # Vitest unit and integration tests
npm run test:coverage   # Tests with the 90% coverage thresholds
npm run lint            # ESLint, i18n and accessibility rules
npm run lint:fix        # Safe ESLint fixes
npm run format          # Write Prettier formatting
npm run api:generate    # Regenerate the Orval client
npm run api:check       # Compare generated output with the committed client
npm run check           # Complete frontend gate, coverage included
```

The remaining scripts are in `package.json`.

## Changing the API

Follow the five steps in [ARCHITECTURE.md](ARCHITECTURE.md#changing-the-api-contract).

## Pull requests

CI runs only on pushes to `master`, so pull requests and `develop` are checked locally; see
[DEPLOYMENT.md](DEPLOYMENT.md#releases-and-upgrades). Dependabot (`.github/dependabot.yml`) opens one grouped
pull request against `develop` every Saturday at 00:00 (Europe/Madrid).

Before opening a pull request:

1. Run `dotnet build CodigoActivo.slnx` and `dotnet test --solution CodigoActivo.slnx` from `backend/`, and keep
   backend coverage at or above 90%.
2. Run `npm run check` from `frontend/`.
3. Update the owning document when behavior, configuration, architecture or security changes.
4. Use [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `chore:`, and so on); the
   commit types decide the next release version.
