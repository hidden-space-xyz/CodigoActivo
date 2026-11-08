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
export DEMO_MODE=false BOOTSTRAP_ADMIN_EMAIL=admin@example.test
export SMTP_HOST=localhost SMTP_PORT=1025 SMTP_SECURITY=None SMTP_FROM_ADDRESS=no-reply@codigoactivo.local
cd backend
dotnet run --project src/CodigoActivo.API
```

Compose needs all three [secret files](DEPLOYMENT.md#secrets). As in the container, the API keeps its Data
Protection key ring in `/home/app/.aspnet/DataProtection-Keys` (`C:\home\app\.aspnet\DataProtection-Keys` on
Windows); delete that folder after changing `DATA_PROTECTION_KEY`. In PowerShell, set the same variables with
`$env:NAME="..."`. Mailpit catches every email at <http://localhost:8025>: login codes, verification links
and the password reset that sets the initial administrator's first password.

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
dotnet csharpier format .
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

### End-to-end tests

`tests/e2e/` is a [Playwright](https://playwright.dev/) suite in TypeScript that drives the SPA in Chromium
through the journeys of each kind of user: visitors, participants, members, sponsors, guardians, activity
leaders, editors, administrators, the privacy officer and the initial administrator. Like the performance suite
it runs in Docker as its own Compose project with its own volumes: PostgreSQL, Mailpit, the API (`Development`, without
demo data) and the nginx image built from the working tree, plus the Playwright runner. From the repository root:

```bash
docker compose -f tests/e2e/compose.yaml run --rm --build playwright
docker compose -f tests/e2e/compose.yaml down -v
```

Arguments after `playwright` replace its command, such as `npx playwright test specs/account` or
`npm run check`, the suite's gate (types, ESLint, Knip and Prettier). The stack publishes the site on
<http://localhost:8180> and Mailpit on <http://localhost:8125>, so after
`docker compose -f tests/e2e/compose.yaml up -d --build --wait web` the suite also runs from `tests/e2e/` on
the host (`npm ci`, `npx playwright install chromium`, then `npm test` or `npm run test:ui`).

The `setup` project takes over the initial administrator through a password reset and has it designate the
privacy officer, a member it makes an administrator; every other account and all content are created through
the API by the tests that need them, so the suite runs again on the same volumes. Logins complete the second factor with
the code read from Mailpit. The `desktop` project runs every test except those tagged `@mobile`, which only
`mobile` runs; a full run takes about three minutes with the default four workers. Each worker sends its own
`X-Forwarded-For`, so the rate limits treat workers as different visitors. The email guard allows 1,000
verification and reset emails per hour and only resets with a new API process: after several runs in an hour,
restart `api` or run `down -v`. The HTML report and the traces of failed tests are written to
`tests/e2e/results/`; conventions are in [tests/e2e/AGENTS.md](tests/e2e/AGENTS.md).

### Performance tests

`tests/performance/` is a [Grafana k6](https://grafana.com/docs/k6/) suite that loads the API the way the SPA
does, to find what needs optimizing before people do. It runs only in Docker, as its own Compose project with
its own volumes: PostgreSQL, Mailpit, the API image built from the working tree (in `Staging`, with demo data)
and k6. From the repository root:

```bash
docker compose -f tests/performance/compose.yaml run --rm --build -e PROFILE=load k6
docker compose -f tests/performance/compose.yaml down -v
```

Every run needs new volumes, so `down -v` goes between runs. The first start seeds the demo data, which
downloads images from `picsum.photos`, and `volume.sql` then adds what a grown association would hold: 10,000
people in 5,000 households, about 430 of them subscribed to the news so one email stays under its 500 recipients,
100 past events with their signups and ratings, and 100,000 audit and 20,000 diagnostic entries. Without it
lists and reports would be measured on a few dozen rows. These figures are an assumption, set in the file;
replace them when real ones exist.

Each scenario sends the requests the SPA sends when its pages open: anonymous visitors (home, events, an event
and its activities, news, resources), members (home, an event with their signups, profile, history,
certificates), accounts signing up for the shared event, alone or with their household, members signing in, new
families that register, confirm their email and sign in, and administrators who manage an event, its signups and
news, read reports and logs (the audit log as the initial administrator, which holds the data protection
designation in this stack), upload images and send email. The suite measures only the requests that carry the
site's traffic or its heavy work; the rare account, authenticator, unsubscribe and blocked data flows are left
to the integration tests.

`PROFILE` selects the load and what it must prove. A window is a stretch of steady load; its objective is p95
under 500 ms, p99 under 1.5 s and failures under 1% on the API, with at least 95% of the iterations its load
promises actually started, so a slow k6 cannot pass for a fast API.

| Profile    | Load                                                                                        | k6 exits with code 99 unless                                                                                                 |
| ---------- | ------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| `smoke`    | One or two iterations per scenario, to check the suite                                      | Every catalogued endpoint is requested and fails under 1%                                                                    |
| `load`     | The expected peak for eight minutes after a one-minute ramp; the default                    | The same, every endpoint keeps the p95 budget of `k6/lib/catalog.js`, `peak` meets the objective and codes arrive within 5 s |
| `spike`    | Signups open: signups, logins and new families jump to ten times the peak for three minutes | The same as `smoke`, every window gets its load, `recovery` meets the objective and codes arrive within 5 s                  |
| `capacity` | From 1,000 to 10,000 people using the site at once, in eight two-minute steps               | Every catalogued endpoint is requested; the summary reports the largest step that met the objective                          |

In `capacity`, 70% of the people browse anonymously, 25% as members and 5% sign up their household, one page
every ten seconds, and one member visit in ten starts with a login. k6 replays those visits at the matching
arrival rate with short pauses of its own, while new families and administrators keep a fixed pace.

Setup signs in the initial administrator, registers administrators and members, takes over demo members
through a password reset and creates the events the scenarios share, reading every code from Mailpit. Its
requests stay out of the results, and any unexpected answer stops the run. k6 calls the API directly as its
trusted proxy, sending a private `X-Forwarded-For` per simulated client and `X-Forwarded-Proto: https`, so nginx
is not measured and the rate limits apply per client and account as in production. Scenarios stay within those
limits; a `429` therefore points at a capacity limit. The API delivers to Mailpit without the provider pace
(`SMTP_MAX_PER_MINUTE`), but the email guard still allows 1,000 verification and reset emails per hour, which
bounds new families and the account pools.

`tests/performance/results/` receives `summary.md`, also printed at the end of the run (each window with its
verdict, delivered load and p95 by kind of endpoint, then every endpoint with its p50, p95, p99, errors and
budget, worst first), `summary.json` and the k6 dashboard in `report.html`. Conventions are in
[tests/performance/AGENTS.md](tests/performance/AGENTS.md).

The containers reproduce the production server (4 vCPUs, 8 GB): the API and PostgreSQL share CPUs 0–3 with
4 GB and 3 GB, and k6 and Mailpit run on CPUs 4–11 so they take nothing from them. On another host, or one
with fewer than 12 CPUs, set `PERF_SERVER_CPUSET`, `PERF_TOOLS_CPUSET`, `PERF_API_MEMORY` and `PERF_DB_MEMORY`
in the shell. nginx and the TLS proxy share the server in production but are not measured, and the host's
CPUs may be faster than the server's, so leave headroom on any capacity figure.

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
