# Contributing

Thanks for helping improve `<Codigoactivo/>`. This guide covers local setup, the day-to-day
commands, and the conventions the build enforces. For the design behind the code see
[ARCHITECTURE.md](ARCHITECTURE.md); for the environment-variable reference see
[DEPLOYMENT.md](DEPLOYMENT.md).

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) and npm
- [PostgreSQL](https://www.postgresql.org/) — a local install, or the Dockerized `db` service (below)
- [Docker](https://www.docker.com/) — required by the backend integration tests, which start their own PostgreSQL
- EF Core tools (for migrations): `dotnet tool install -g dotnet-ef`

## Local setup

Configuration is read from **flat environment variables** — there are no `dotnet user-secrets` and no
`ConnectionStrings` section. The full list with defaults lives in [DEPLOYMENT.md](DEPLOYMENT.md#environment-variables).

### Backend

The DB connection string is built in code from `POSTGRES_HOST` / `POSTGRES_PORT` / `POSTGRES_DB` /
`POSTGRES_USER` / `POSTGRES_PASSWORD` (defaults: `localhost:5432`, db/user `codigoactivo`, empty password).
The simplest path is to run just the database in Docker and point the API at it:

```bash
cp .env.example .env               # set POSTGRES_PASSWORD
docker compose up db               # Postgres published on 127.0.0.1:5432

export POSTGRES_PASSWORD=...        # same value as in .env (PowerShell: $env:POSTGRES_PASSWORD="...")
cd backend && dotnet run --project src/CodigoActivo.API
```

The API listens on <http://localhost:5150> (add `--launch-profile https` to also serve
<https://localhost:7039>), with Swagger at `/swagger` (Development only). On startup it **always** applies
migrations and seeds the lookup catalogs.

> [!NOTE]
> A bare `dotnet run` does **not** read the root `.env` file — that file is consumed by Docker Compose.
> Provide `POSTGRES_*` as real environment variables, or run the database with `docker compose up db`.

### Frontend

```bash
cd frontend
npm install
cp .env.example .env.local          # then set VITE_API_PROXY_TARGET=http://localhost:5150
npm run dev                         # http://localhost:5173
```

The dev server proxies `/api` to `VITE_API_PROXY_TARGET`; its code fallback is `https://localhost:5001`, so
set `http://localhost:5150` to reach a local backend.

## Commands

**Backend** (run from `backend/`)

```bash
dotnet build                                   # build + analyzers; style violations fail the build
dotnet run --project src/CodigoActivo.API      # run the API
dotnet test                                    # unit + integration tests (integration needs Docker, see Testing below)

# Add a migration (applied automatically on next startup):
dotnet ef migrations add <Name> \
  --project src/CodigoActivo.Infrastructure \
  --startup-project src/CodigoActivo.API
```

**Frontend** (run from `frontend/`)

```bash
npm run dev          # dev server with HMR
npm run build        # vue-tsc typecheck + production build
npm run typecheck    # vue-tsc typecheck only
npm run lint         # ESLint (lint:fix to autofix)
npm run lint:fsd     # Steiger — Feature-Sliced Design layer rules
npm run format       # Prettier
npm run api:generate # regenerate the typed API client from swagger.json (Orval)
```

## Changing the API

The apps are contractually linked; keep both sides in sync in the same change:

1. Change the DTOs / endpoints in the backend.
2. Refresh `frontend/swagger.json` from the running backend's Swagger endpoint.
3. Run `npm run api:generate` — Orval wipes and regenerates `src/shared/api/generated/`.
4. If you added a failure mode, add the `ErrorCode` member in the backend and its Spanish message under
   the `errors.*` namespace in `frontend/src/shared/i18n/locales/es.ts`.

> [!CAUTION]
> Never hand-edit anything under `src/shared/api/generated/` — `npm run api:generate` wipes and rewrites the
> entire folder, so manual changes are silently lost.

See [ARCHITECTURE.md](ARCHITECTURE.md#how-the-two-apps-stay-in-sync-the-api-contract) for the reasoning.

## Coding conventions

> [!IMPORTANT]
> Style is enforced at build/lint time on both sides — fix the code, don't disable the rule.

**Backend**

- **Never** use `DateTime.Now` / `DateTime.UtcNow`; inject `IClock`.
- **Never** put `Version=` on a `PackageReference` — versions are central in `Directory.Packages.props`.
- The database is **snake_case**; account for it in raw SQL.
- Type colocation is intentional (all repository interfaces in one file, request+response DTOs per aggregate
  in one `*Dtos.cs`); private fields are `camelCase` with no leading underscore.
- Formatting is CSharpier; Meziantou.Analyzer + SonarAnalyzer run in-build.

**Frontend**

- **Never** hand-edit `src/shared/api/generated/`.
- Import across slices only through a slice's `index.ts`; Steiger enforces the FSD layer rules.
- All UI strings are **Spanish** (no i18n). TypeScript is very strict; Prettier formats the code.
- Composable file naming: **features** use camelCase (`useLogin.ts`); **entities and `shared/lib`** use
  kebab-case (`use-theme.ts`).

## Testing

- **Backend**: xUnit v3, AwesomeAssertions, NSubstitute. Integration tests run against a **real** PostgreSQL that
  the test run provisions itself: a throwaway `postgres:17-alpine` container (Testcontainers) is started once,
  migrated, shared by the whole assembly, and destroyed at the end. No `POSTGRES_*` env vars and no pre-created
  database — just a running Docker daemon. Each test truncates and reseeds (parallelization is disabled). Set
  `CODIGOACTIVO_TEST_DB_CONNECTION` to an Npgsql connection string for an empty, disposable database to reuse
  that instead of spawning a container. Test method names follow `MethodUnderTest_Scenario_ExpectedBehavior`
  (PascalCase segments), e.g. `RegisterAsync_NewAdult_ReturnsCreatedAndSendsOtp`.
- **Frontend**: there is no automated test suite; rely on `npm run typecheck` and `npm run lint`.

## Commits & pull requests

- Follow [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `chore:`, …), matching
  the existing history.
- Keep the build green: `dotnet build` / `dotnet test` and `npm run build` / `npm run lint:fsd` must pass.
- Update the docs when you change architecture, tech stack, testing conventions, security posture, or the
  deployment/config surface.

> [!NOTE]
> AI coding agents follow separate, machine-oriented guidance in the `CLAUDE.md` files (root, `backend/`,
> `frontend/`). Update those alongside these docs when conventions change.
