# Contributing

This guide covers local setup, routine commands and repository conventions. Read
[ARCHITECTURE.md](ARCHITECTURE.md) before changing boundaries between projects or frontend layers. Runtime
configuration belongs in [DEPLOYMENT.md](DEPLOYMENT.md).

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 26.x](https://nodejs.org/) and npm
- [Docker](https://www.docker.com/), for the local PostgreSQL service and backend integration tests
- `dotnet-ef` when creating migrations: `dotnet tool install --global dotnet-ef`

A separate PostgreSQL installation is optional.

## Local setup

### Database and backend

Docker Compose reads the root `.env`; `dotnet run` does not. Start PostgreSQL with Compose, then provide the
API configuration as real process environment variables:

```bash
cp .env.example .env
# Set POSTGRES_PASSWORD in .env.
docker compose up -d db mailpit

export POSTGRES_PASSWORD=...
export BOOTSTRAP_ADMIN_EMAIL=admin@example.test
export BOOTSTRAP_ADMIN_PASSWORD=...
export SMTP_HOST=localhost SMTP_PORT=1025 SMTP_SECURITY=None SMTP_FROM_ADDRESS=no-reply@codigoactivo.local
cd backend
dotnet run --project src/CodigoActivo.API
```

In PowerShell, set variables with `$env:POSTGRES_PASSWORD="..."` and the equivalent names above. SMTP is
always required because every login is completed with an emailed one-time code; the Mailpit service from the
development override catches that mail at <http://localhost:8025>, which is where you read login codes and
verification links.

The API starts at <http://localhost:5150>; the `https` launch profile also uses <https://localhost:7039>.
Swagger is available at `/swagger` only in Development. Startup applies migrations, seeds catalogs and
requires bootstrap credentials when the user table is empty. Without `LOG_DIRECTORY` set, the API logs to the
console; see [DEPLOYMENT.md](DEPLOYMENT.md#development-overlay) for the containerized case.

### Frontend

```bash
cd frontend
npm ci
cp .env.example .env.local
# Change VITE_API_PROXY_TARGET to http://localhost:5150.
npm run dev
```

Vite serves <http://localhost:5173> and proxies `/api`, `/sitemap.xml` and `/robots.txt` to the configured
backend.

### Complete Docker development stack

For the full containerized dev stack instead of running services individually, see
[README.md#quick-start-with-docker](README.md#quick-start-with-docker) and
[DEPLOYMENT.md#development-overlay](DEPLOYMENT.md#development-overlay).

## Commands

Run backend commands from `backend/`:

```bash
dotnet restore CodigoActivo.slnx
dotnet build CodigoActivo.slnx
dotnet test
dotnet test tests/CodigoActivo.UnitTests
dotnet test tests/CodigoActivo.IntegrationTests
dotnet test tests/CodigoActivo.UnitTests --filter "FullyQualifiedName~ClassName.MethodName"

dotnet ef migrations add <Name> --project src/CodigoActivo.Infrastructure --startup-project src/CodigoActivo.API
```

Integration tests require Docker unless `CODIGOACTIVO_TEST_DB_CONNECTION` points to an empty, disposable
PostgreSQL database.

CI merges the coverage of both test projects and fails when line, branch or method coverage is below 90%.
Dependabot (`.github/dependabot.yml`) opens a single pull request against `develop` every Saturday at 00:00
(Europe/Madrid) with all NuGet, npm, Docker, Compose and GitHub Actions updates grouped together.
Coverage uses Microsoft Code Coverage with `tests/CodeCoverage.config`, which measures the production
assemblies and excludes EF Core migrations. To reproduce locally:

```bash
dotnet tool restore
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
npm run check           # Complete frontend CI gate
```

The remaining scripts are in `package.json`. `npm run check` is the mandatory frontend quality gate and
includes `npm run test:coverage`.

## Changing the API

The five required steps are documented once, in
[ARCHITECTURE.md](ARCHITECTURE.md#changing-the-api-contract). In short: change the backend, refresh
`frontend/swagger.json` from the Development Swagger endpoint, run `npm run api:generate`, add Spanish
`errors.*` messages for new codes, then run `dotnet test` and `npm run check`. Never edit
`frontend/src/shared/api/generated/`; Orval deletes and recreates it.

## Backend conventions

- Keep project dependencies in the direction documented in [ARCHITECTURE.md](ARCHITECTURE.md).
- Put one command or query and its sealed handler in each use-case file. Controllers inject handlers; they do
  not contain business logic.
- Queries are no-tracking and do not mutate, commit, invalidate caches or send email. Commands commit through
  `IUnitOfWork` and invalidate caches after the commit.
- Never access `DateTime.Now` or `DateTime.UtcNow`; inject `IClock`.
- Do not add `Version` attributes to `PackageReference`; versions are centralized in
  `backend/Directory.Packages.props`.
- Account for PostgreSQL snake-case naming in raw SQL.
- Put wire request and response records in `Application/DTOs`, binding query types in
  `Application/Querying`, and configuration types outside Domain.
- User-facing backend text belongs in
  `backend/src/CodigoActivo.Application/Resources/Localization/AppStrings.resx`. Log messages, diagnostic
  exceptions and seeded content are not UI text.
- Declare every log event with `[LoggerMessage]` in the layer's `*Log` class (`SecurityLog`, `ApplicationLog`,
  `InfrastructureLog`, `ApiLog`); `CA1848` is an error. Use only the placeholder types the
  `LoggingConventionTests`/`LoggingCallSiteTests` allow-list accepts, and never a user id, personal data, a
  value typed by the client, a credential, a code, a token or a third-party reply that may quote them.
  Exception messages you throw follow the same rule because they are logged. See
  [SECURITY.md](SECURITY.md#logging).
- Use `camelCase` private fields without a leading underscore.
- Keep CSharpier formatting and all SDK analyzer rules clean. Warnings are errors.
- Document every public type and member with XML comments (`CS1591` is a warning, so the build fails without
  them). Test projects are exempt.
- Name tests with three PascalCase segments and no underscores, for example
  `RegisterAsyncNewAdultReturnsCreatedAndSendsOtp`. CQRS handler unit tests start with `HandleAsync`.

## Frontend conventions

- Respect the Feature-Sliced import direction and import a slice through its `index.ts` public API.
- Keep generated request functions behind handwritten `api/requests.ts` modules.
- Put entity-scoped server state in the entity; put multi-entity or session-dependent workflows in a feature.
- Build TanStack Query keys through the entity's `api/query-keys.ts` factory.
- Do not hardcode user-facing text. Add Vue I18n keys to `src/shared/i18n/locales/es.json`.
- Feature composables use camelCase filenames such as `useLogin.ts`; entity and `shared/lib` composables use
  kebab-case names such as `use-theme.ts`.
- Document the public API with JSDoc: every top-level export, public class member, and every component prop,
  emitted event and `defineExpose` member. ESLint enforces it and rejects comments that only repeat the name,
  so explain purpose and non-obvious behavior instead. Tests (`tests/`, `*.spec.ts`, `*.test.ts`,
  `__tests__/`) are exempt.
- Keep strict TypeScript, ESLint, accessibility, i18n, Stylelint, Steiger, Knip and Prettier checks green.

## Tests and pull requests

Backend unit tests use xUnit v3, AwesomeAssertions and NSubstitute; integration tests share a disposable
PostgreSQL 18 Testcontainers instance and disable parallel execution. The test host lifts the email guard and
request rate limits by default; use the factory's `WithEmailGuard`/`WithRateLimits` to exercise them. The
test host also delivers the email outbox inline instead of running the background worker; tests that queue
mail indirectly or advance the clock to a scheduled retry call `Factory.DrainEmailOutboxAsync()`.

Frontend tests use Vitest, Vue Test Utils and jsdom in `frontend/tests/`, mirroring `src/`: `tests/unit/`
covers isolated units, `tests/integration/` mounts components or `App.vue` with real plugins.

- The backend is never required: `tests/setup.ts` runs an MSW server that fails unhandled requests, so
  declare responses with `server.use(...)` from `tests/support/server.ts` (requests still go through the
  generated client and `httpClient`, including CSRF).
- Mount with `renderWithProviders` or `renderApp` from `tests/support/render.ts`.
- Shared state (session, CSRF token, handlers, storage, fake timers, media queries) resets after each test.
- Coverage covers `src/**/*.{ts,vue}` except the generated client and `main.ts`, at or above 90% for
  statements, branches, functions and lines.
- `tests/unit/docker/nginx-config.spec.ts` statically pins the `frontend/docker/*.conf` files (headers, proxy
  routing, caching); update it whenever those files change. It does not replace `nginx -t`.

Before opening a pull request:

1. Run `dotnet build` and `dotnet test` from `backend/`, and keep backend coverage at or above 90%.
2. Run `npm run check` from `frontend/`.
3. Update the relevant documentation when behavior, configuration, architecture or security changes.
4. Use [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `chore:`, and so on).
