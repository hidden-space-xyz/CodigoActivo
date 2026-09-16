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
docker compose up -d db

export POSTGRES_PASSWORD=...
export ACCOUNT_VERIFICATION_REQUIRED=false
export BOOTSTRAP_ADMIN_EMAIL=admin@example.test
export BOOTSTRAP_ADMIN_PASSWORD=...
cd backend
dotnet run --project src/CodigoActivo.API
```

In PowerShell, set variables with `$env:POSTGRES_PASSWORD="..."` and the equivalent names above. If account
verification remains enabled, also set valid `SMTP_HOST` and `SMTP_FROM_ADDRESS` values; verification defaults
to enabled when the variable is absent.

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
# Set database/bootstrap values and configure SMTP, or disable account verification.
docker compose up --build
```

Compose automatically merges `docker-compose.override.yml`: it builds both applications, serves the SPA on
port `8080`, publishes the API on `5150` and PostgreSQL on `5432`, and relaxes API hardening for debugging.
These ports bind to all host interfaces, so use the overlay only on a trusted development machine.

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

Run frontend commands from `frontend/`:

```bash
npm run dev             # Vite development server
npm run build           # Type-check and production build
npm run typecheck       # vue-tsc only
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

There is currently no frontend test suite; `npm run check` is its mandatory quality gate.

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
- Keep strict TypeScript, ESLint, accessibility, i18n, Stylelint, Steiger, Knip and Prettier checks green.

## Tests and pull requests

Backend unit tests use xUnit v3, AwesomeAssertions and NSubstitute. Integration tests share a disposable
PostgreSQL 18 Testcontainers instance, reset and reseed data between tests, and disable parallel execution.

Before opening a pull request:

1. Run `dotnet build` and `dotnet test` from `backend/`.
2. Run `npm run check` from `frontend/`.
3. Update the relevant documentation when behavior, configuration, architecture or security changes.
4. Use [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `chore:`, and so on).
