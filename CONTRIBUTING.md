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
export SMTP_HOST=localhost
export SMTP_PORT=1025
export SMTP_SECURITY=None
export SMTP_FROM_ADDRESS=no-reply@codigoactivo.local
cd backend
dotnet run --project src/CodigoActivo.API
```

In PowerShell, set variables with `$env:POSTGRES_PASSWORD="..."` and the equivalent names above. SMTP is
always required because every login is completed with an emailed one-time code; the Mailpit service from
the development override catches that mail at <http://localhost:8025>, which is where you read the login
codes and the verification links of new accounts.

The API starts at <http://localhost:5150>; the `https` launch profile also uses
<https://localhost:7039>. Swagger is available at `/swagger` only in Development. Startup applies migrations,
seeds catalogs and requires bootstrap credentials when the user table is empty.

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

From the repository root:

```bash
cp .env.example .env
# Set the database and bootstrap values; the override delivers all mail to Mailpit.
docker compose up --build
```

Compose automatically merges `docker-compose.override.yml`: it builds both applications, serves the SPA on
port `8080`, publishes the API on `5150` and PostgreSQL on `5432`, adds a Mailpit mail catcher on `8025`
(where verification links and login codes arrive), and relaxes API hardening for debugging. These ports bind
to all host interfaces, so use the overlay only on a trusted development machine.

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
Coverage uses Microsoft Code Coverage with `tests/CodeCoverage.config`, which measures the production
assemblies and excludes EF Core migrations. To reproduce the check locally:

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
npm run typecheck       # vue-tsc only
npm test                # Vitest unit and integration tests
npm run test:watch      # Vitest in watch mode
npm run test:coverage   # Tests with the 90% coverage thresholds
npm run lint            # ESLint, i18n and accessibility rules
npm run lint:fix        # Safe ESLint fixes
npm run lint:fsd        # Feature-Sliced Design checks
npm run lint:styles     # Stylelint
npm run lint:styles:fix # Stylelint fixes
npm run lint:unused     # Knip unused-code/dependency checks
npm run format          # Write Prettier formatting
npm run format:check    # Verify Prettier formatting
npm run api:generate    # Regenerate the Orval client
npm run api:check       # Compare generated output with the committed client
npm run check           # Complete frontend CI gate
```

`npm run check` is the mandatory frontend quality gate and includes `npm run test:coverage`.

## Changing the API

Keep the backend, OpenAPI document and generated frontend client in one change:

1. Update backend endpoints, request/response records and error codes.
2. Run the backend in Development.
3. Replace `frontend/swagger.json` with the document from
   `http://localhost:5150/swagger/v1/swagger.json`.
4. Run `npm run api:generate` from `frontend/`.
5. Add a Spanish `errors.*` message in `frontend/src/shared/i18n/locales/es.json` for every new
   `ErrorCode`.
6. Run `dotnet test` and `npm run check`.

Never edit `frontend/src/shared/api/generated/`; Orval deletes and recreates it.

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
  emitted event and `defineExpose` member. ESLint enforces it and rejects comments that only repeat the name, so
  explain purpose and non-obvious behavior instead; the TypeScript signature already documents types. Tests
  (`tests/`, `*.spec.ts`, `*.test.ts`, `__tests__/`) are exempt because their names must explain themselves.
- Keep strict TypeScript, ESLint, accessibility, i18n, Stylelint, Steiger, Knip and Prettier checks green.

## Tests and pull requests

Backend unit tests use xUnit v3, AwesomeAssertions and NSubstitute. Integration tests share a disposable
PostgreSQL 18 Testcontainers instance, reset and reseed data between tests, and disable parallel execution.
The test host lifts the email guard and the request rate limits, whose wall-clock windows would otherwise
carry over between tests; tests that exercise them use the factory's `WithEmailGuard` or `WithRateLimits`.

Frontend tests use Vitest, Vue Test Utils and jsdom, and live in `frontend/tests/`, outside the
Feature-Sliced `src/` tree. `tests/unit/` covers functions, mappers and composables in isolation;
`tests/integration/` mounts components, pages or the whole `App.vue` with the real i18n, Element Plus,
TanStack Query and router plugins. Both mirror the `src/` path of the code under test.

- The backend is never required. `tests/setup.ts` starts an MSW server that fails any request without a
  handler; declare the API responses a test needs with `server.use(...)` from `tests/support/server.ts`.
  Requests still go through the generated client and `httpClient`, including CSRF handling.
- Mount with `renderWithProviders` or `renderApp` from `tests/support/render.ts`. Element Plus dialogs,
  message boxes and notifications render in `document.body`.
- Shared state (session, CSRF token, handlers, storage, fake timers, media queries) is reset after each test.
- Coverage covers `src/**/*.{ts,vue}` except the generated client and `main.ts`. Statements, branches,
  functions and lines must each stay at or above 90%.

Before opening a pull request:

1. Run `dotnet build` and `dotnet test` from `backend/`, and keep backend coverage at or above 90%.
2. Run `npm run check` from `frontend/`.
3. Update the relevant documentation when behavior, configuration, architecture or security changes.
4. Use [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `chore:`, and so on).
