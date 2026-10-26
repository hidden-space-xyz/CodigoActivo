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
openssl rand -base64 32 | tr -d '\r\n' > secrets/data_protection_key
touch secrets/smtp_password
docker compose up -d db mailpit

export POSTGRES_PASSWORD="$(cat secrets/postgres_password)"
export DATA_PROTECTION_KEY="$(cat secrets/data_protection_key)"
export BOOTSTRAP_ADMIN_EMAIL=admin@example.test
export SMTP_HOST=localhost SMTP_PORT=1025 SMTP_SECURITY=None SMTP_FROM_ADDRESS=no-reply@codigoactivo.local
cd backend
dotnet run --project src/CodigoActivo.API
```

Compose needs all three [secret files](DEPLOYMENT.md#secrets). As in the container, the API keeps its Data
Protection key ring in `/home/app/.aspnet/DataProtection-Keys` (`C:\home\app\.aspnet\DataProtection-Keys` on
Windows); delete that folder after changing `DATA_PROTECTION_KEY`. In PowerShell, set the same variables with
`$env:NAME="..."`. Mailpit catches every email at <http://localhost:8025>: login codes, verification links
and the password reset that sets the bootstrap administrator's first password.

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
dotnet test tests/CodigoActivo.ArchitectureTests
dotnet test tests/CodigoActivo.UnitTests
dotnet test tests/CodigoActivo.IntegrationTests
dotnet test tests/CodigoActivo.UnitTests --filter "FullyQualifiedName~ClassName.MethodName"
```

Integration tests require Docker unless `CODIGOACTIVO_TEST_DB_CONNECTION` points to an empty, disposable
PostgreSQL database whose role may create databases: it becomes the template each test class copies.

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

Backend coverage merges the unit and integration tests and must reach 90% of lines, branches and methods; the
architecture tests check structure, not behavior, and stay out of it. It uses Microsoft Code Coverage with
`tests/CodeCoverage.config`, which measures the production assemblies and excludes EF Core migrations:

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
npm test                # Vitest unit, integration and architecture tests
npm run test:coverage   # Tests with the 90% coverage thresholds
npm run lint            # ESLint, i18n and accessibility rules
npm run lint:fix        # Safe ESLint fixes
npm run format          # Write Prettier formatting
npm run api:generate    # Regenerate the Orval client
npm run api:check       # Compare generated output with the committed client
npm run check           # Complete frontend gate, coverage included
```

The remaining scripts are in `package.json`.

### Performance tests

`backend/tests/performance/` is a [Grafana k6](https://grafana.com/docs/k6/) suite that calls every API endpoint
under concurrent load to find what needs optimizing. It runs only in Docker, as its own Compose project with its
own volumes: PostgreSQL, Mailpit, the API image built from the working tree (in `Staging`, with demo data) and
k6. From the repository root:

```bash
docker compose -f backend/tests/performance/compose.yaml run --rm --build -e PROFILE=load k6
docker compose -f backend/tests/performance/compose.yaml down -v
```

`PROFILE` is `smoke` (every endpoint at least once, about four minutes, to check the suite; no latency budgets),
`load` (the expected peak for ten minutes, the default), `stress` (that peak multiplied by up to four in steps
over twelve minutes) or `capacity`. `capacity` raises the people using the site at once from 1,000 to 10,000 in
eight two-minute steps, with a few administrators working throughout, and reports the largest step whose API
requests kept p95 under 500 ms, p99 under 1.5 s and failures under 1%. Of those people, 70% browse anonymously
(80-second visits), 25% browse as members (70 s) and 5% sign up their household (60 s), one page every ten
seconds; k6 replays their visits at the matching arrival rate with short pauses of its own. The account scenario
waits out the one-minute code cooldowns, so a run ends up to six minutes after its last stage. The first start
also seeds the demo data, which downloads images from `picsum.photos`. Setup signs in the bootstrap
administrator, registers administrators and members, takes over demo members through a password reset and
creates the events the scenarios share, reading every code from Mailpit; its requests stay out of the results.
k6 calls the API directly as its trusted proxy, sending a private `X-Forwarded-For` per simulated client and
`X-Forwarded-Proto: https`, so nginx is not measured and the rate limits apply per client and account as in
production. Scenarios stay within those limits; a `429` therefore points at a capacity limit.

`backend/tests/performance/results/` receives `summary.md` (every endpoint with its p50, p95, p99, errors and
the p95 budget of `k6/lib/catalog.js`, worst first, plus each step of `capacity`), `summary.json` and the k6
dashboard in `report.html`. k6 exits with code 99 when an endpoint misses its budget or fails more than 1% of
its requests, which `capacity` does once it goes past the capacity. Run `down -v` between runs: the API's
automatic-email limiter (1,000 messages per hour) only resets with a new process.

The containers reproduce the production VPS (4 vCPUs, 8 GB): the API and PostgreSQL share CPUs 0–3 with 4 GB
and 3 GB, and k6 and Mailpit run on CPUs 4–11 so they take nothing from them. On another host, or one with
fewer than 12 CPUs, set `PERF_SERVER_CPUSET`, `PERF_TOOLS_CPUSET`, `PERF_API_MEMORY` and `PERF_DB_MEMORY` in the
shell. nginx and the TLS proxy share the VPS in production but are not measured, and the host's CPUs may be
faster than the VPS's, so leave headroom on any capacity figure.

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
