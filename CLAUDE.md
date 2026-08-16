# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Official website of `<Codigoactivo/>`, a León-based nonprofit teaching kids programming. One product,
two independently developed apps shipping as a **same-origin** stack:

- `backend/` — ASP.NET Core Web API (.NET 10), 5-project Clean Architecture, EF Core + PostgreSQL.
- `frontend/` — Vue 3 + Vite SPA (TypeScript), Feature-Sliced Design, Element Plus, TanStack Query.

The SPA always calls the API with relative `/api/...` URLs (Vite proxy in dev, nginx in prod). There is
no CORS and no token auth — session cookie + CSRF header (`X-CSRF-TOKEN` from `GET /api/auth/csrf`).
Detailed docs: [ARCHITECTURE.md](ARCHITECTURE.md), [CONTRIBUTING.md](CONTRIBUTING.md),
[DEPLOYMENT.md](DEPLOYMENT.md), [SECURITY.md](SECURITY.md). Update them when you change what they describe.

## Commands

**Backend** (run from `backend/`):

```bash
dotnet build                                   # analyzer violations fail the build (TreatWarningsAsErrors)
dotnet run --project src/CodigoActivo.API      # http://localhost:5150, Swagger at /swagger (Development only)
dotnet test                                    # unit + integration tests
dotnet test tests/CodigoActivo.UnitTests       # one project (integration tests need Docker)
dotnet test tests/CodigoActivo.UnitTests --filter "FullyQualifiedName~ClassName.MethodName"   # single test

# New migration (migrations are applied automatically on API startup):
dotnet ef migrations add <Name> --project src/CodigoActivo.Infrastructure --startup-project src/CodigoActivo.API
```

The DB connection is built in code from `POSTGRES_HOST/PORT/DB/USER/PASSWORD` env vars — a bare
`dotnet run` does **not** read the root `.env` (that file is for Docker Compose). Easiest setup: copy
`.env.example` to `.env` and set `POSTGRES_PASSWORD`, `docker compose up -d db`, then set
`POSTGRES_PASSWORD` in the environment.

Integration tests provision their own throwaway PostgreSQL via Testcontainers (Docker daemon required,
no env vars, no pre-created DB). Set `CODIGOACTIVO_TEST_DB_CONNECTION` to reuse an empty disposable
database instead.

**Frontend** (run from `frontend/`):

```bash
npm run dev            # dev server on http://localhost:5173 (proxies /api to VITE_API_PROXY_TARGET)
npm run build          # vue-tsc typecheck + production build
npm run typecheck      # vue-tsc only
npm run lint           # ESLint (lint:fix to autofix)
npm run lint:fsd       # Steiger — enforces the FSD layer rules
npm run format         # Prettier
npm run api:generate   # Orval: regenerate typed client from swagger.json
```

There is **no frontend test suite** — `npm run typecheck` + `npm run lint` + `npm run lint:fsd` are the gate.

**Full stack in Docker**: `docker compose up --build` (the dev override builds both images from
source; API on 5150, DB published). The root `docker-compose.yml` alone is the deployment file — it
pulls the published GHCR images, no build; production from a clone must use
`docker compose -f docker-compose.yml up -d` to skip the dev override. The compose file carries no
env values or fallbacks of its own: everything resolves from the root `.env`, always created by
copying `.env.example` and adjusting values.

## Backend architecture

Five projects, strict one-directional references (enforced only by the `ProjectReference` graph and
discipline — no architecture test for layering, keep it clean by hand):

- **Domain** (zero references): entities, repository interfaces (`Domain/Repositories/IDbRepositories.cs`,
  one interface per aggregate root), ports (`IClock`, `IQueryExecutor`, `IEmailSender`, `IPasswordHasher`, …),
  `Result`/`Error`/`ErrorCode`, `SeedIds` constants.
- **Application** (→ Domain): CQRS handlers, DTOs, validation, hand-written mapping, pagination, `AppStrings` localization.
- **Infrastructure** (→ Domain): EF Core `DbContext` + repositories, Argon2id, MailKit, local file storage.
- **Composition** (→ all three): the single DI wiring point (`AddCodigoActivo`). Handler registration is
  explicit in `DependencyInjection.cs`; a wiring test fails if a handler is missing.
- **API** (→ Composition): controllers, middleware, auth attributes, `Program`.

**CQRS, handcrafted — no MediatR, no dispatcher.** One use case per file under
`Application/<Aggregate>/Commands|Queries/`: a sealed positional record (verb-first:
`CreateEventCommand`, `ListEventsQuery`) colocated with its sealed `<Message>Handler` implementing
`ICommandHandler<,>`/`IQueryHandler<,>`. Controllers inject concrete handlers via `[FromServices]`.
The two sides never mix:

- **Queries**: untracked (`Repository.Query()` is `AsNoTracking`), DB-side projection into response DTOs
  (shared `Expression` projections in `Application/Mapping/Projections.cs`), materialize only through
  `IQueryExecutor`, public/catalog reads wrapped in tag-based `HybridCache`. Never mutate, never invalidate.
- **Commands**: tracked entities, commit once via `IUnitOfWork.SaveChangesAsync`, invalidate cache tags
  after the commit. Handlers never touch `DbContext`. Two set-based escapes (`RemoveAsync` /
  `SetFeaturedAsync`) commit immediately — never combine them with staged changes.

Reflection-based convention tests in `tests/CodigoActivo.UnitTests/Architecture/` enforce handler
naming/namespaces, one handler per message, query handlers never depending on `IUnitOfWork` or email
ports, and the data-shape rules — keep them green.

**Result/Error contract**: handlers never throw for expected failures; they return `Task<Result<T>>`
(implicit conversions: `return dto;` / `return Error.NotFound(ErrorCode.UserNotFound);`). `ErrorCode`
(`Domain/Common/ErrorCode.cs`) serializes **as a string** and is the stable contract the frontend
switches on. Controllers derive from `ApiControllerBase` (`ToOk`/`ToCreated`/`ToNoContent`); every
failure becomes a uniform `ApiErrorResponse`.

**Persistence**: Npgsql with **snake_case** naming (account for it in raw SQL), client-generated `Guid`
ids, non-catalog closed value sets as enums stored as strings. Startup always applies migrations, then
the idempotent `DatabaseSeeder` (catalog rows keyed by `SeedIds` — reference them by constant, never by name).

## Frontend architecture

Feature-Sliced Design under `src/`: `app/` → `pages/` → `widgets/` → `features/` → `entities/` →
`shared/`. Imports flow downward only; same-layer slices never import each other; every slice exposes a
public API via `index.ts` (Steiger enforces all of this). `@` → `./src` is the only path alias.

**API client**: Orval generates `src/shared/api/generated/` from the committed `frontend/swagger.json`.
**Never hand-edit generated files** — `npm run api:generate` wipes the folder. Generated endpoint
functions are imported **only** from `api/requests.ts` wrapper files (entity or feature) — nowhere
else. Each entity wraps the generated request functions in `api/requests.ts`, maps DTO → domain in
`api/mapper.ts`, and exposes hand-written TanStack Query composables in `api/queries.ts` /
`api/mutations.ts`. The generated vue-query hooks are deliberately ignored.

**Composable placement**: entity-scoped TanStack composables (public/catalog reads, single-aggregate
mutations) live in the entity's `api/queries.ts` / `api/mutations.ts`. Anything that needs the session,
another entity, or a feature workflow (admin tables/forms, signup orchestration) lives in a feature
slice's `model/` — never in a page: page `model/` holds only view-shaping helpers and types.
Query keys always come from the entity's `api/query-keys.ts` factory — an `all` root array named after
the entity plus functions that spread it; never assemble key arrays inline at call sites.

**Session**: module-level reactive singleton (no Pinia) resolving `GET /api/auth/me`. Authorization is a
boolean admin flag, not roles. Theming via `--ca-*` CSS variables (`.ca-dark` on `<html>`), Element Plus
re-skinned by mapping `--el-*` to `--ca-*`.

## Changing the API (both sides, same pass)

1. Change DTOs/endpoints in the backend (`...Request`/`...Response` records; enums serialize as strings).
2. Refresh `frontend/swagger.json` from the running backend's Development-only Swagger endpoint.
3. `npm run api:generate`.
4. New failure mode → new `ErrorCode` member in the backend **and** a Spanish message under the
   `errors.*` namespace in `frontend/src/shared/i18n/locales/es.ts`.

## Conventions the build/tests enforce

- **Never** `DateTime.Now`/`DateTime.UtcNow` — inject `IClock`.
- **Never** put `Version=` on a `PackageReference` — versions are central in `backend/Directory.Packages.props`.
- **No hardcoded user-facing strings, either side.** Backend: every member/admin-visible string is a key
  in `Application/Resources/Localization/AppStrings.resx` via the typed `AppStrings` accessor
  (composites are methods with typed parameters — no `string.Format` at call sites); exception messages,
  log templates and seeded catalog text are deliberately excluded. Frontend: every string is a Vue
  I18n key in `es.ts` (`$t` / `useI18n()` / `i18n.global.t`).
- Type colocation is intentional: all repository interfaces in one file, request+response DTOs per
  aggregate in one `*Dtos.cs`, one use case per file. Private fields are `camelCase`, no leading underscore.
- Formatting: CSharpier (backend), Prettier (frontend). Analyzer config lives in
  `Directory.Build.Analyzers.props` and `Directory.Build.targets`; per-rule severity exceptions go in
  `.editorconfig`. Warnings are errors (`TreatWarningsAsErrors`); those files carry no comments.
- Configuration is flat env vars only — no `IOptions<T>`, no user-secrets, no `ConnectionStrings` section.
- Test naming: `MethodUnderTestScenarioExpectedBehavior` — three PascalCase segments, **no underscores**
  (CA1707 is enforced in the test projects too); unit tests of CQRS handlers always start with
  `HandleAsync` — the test class name carries the use case.
- Composable file naming: features camelCase (`useLogin.ts`); entities and `shared/lib` kebab-case (`use-theme.ts`).
- Conventional Commits (`feat:`, `fix:`, `chore:`, …).
