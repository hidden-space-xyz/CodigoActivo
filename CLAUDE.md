# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository layout

Two independent apps, no root-level tooling:

- `backend/` — ASP.NET Core (.NET 10) Web API, Clean Architecture, PostgreSQL via EF Core.
- `frontend/` — Vue 3 + Vite SPA (PrimeVue, TanStack Query). Reads and writes both go through a REST client generated (Orval) from the backend's OpenAPI doc.

The **backend** has an xUnit v3 test suite (unit + integration) held to a **≥95% line-coverage floor** — see "Testing" under Backend. The **frontend** has no automated test suite (no Vitest/Jest); verify it by building and running.

## Backend

### Commands (run from `backend/`)

- Build / lint: `dotnet build` — `EnforceCodeStyleInBuild` is on and Roslynator analyzers run as part of the build, so style/analyzer violations fail the build.
- Run: `dotnet run --project CodigoActivo.API` → http://localhost:5150 (and https://localhost:7039), Swagger UI at `/swagger` in Development.
- Add a migration: `dotnet ef migrations add <Name> --project CodigoActivo.Infrastructure --startup-project CodigoActivo.API`. Migrations and seed data are applied automatically on startup (toggle via `Database:MigrateOnStartup` / `Database:SeedOnStartup`); there is no manual `database update` step in normal dev.
- Packages are centrally managed in `Directory.Packages.props` (no versions in individual `.csproj`). Add a `PackageVersion` there, then a versionless `PackageReference` in the project.

### Testing (run from `backend/`)

- **Stack:** xUnit v3 + **AwesomeAssertions** (the Apache-2.0/MIT fork of FluentAssertions — same API, `using AwesomeAssertions;`; chosen because FluentAssertions 8.x is commercially licensed) + NSubstitute, coverage via coverlet. Two projects under `tests/`: `CodigoActivo.UnitTests` and `CodigoActivo.IntegrationTests`. They live under `tests/Directory.Build.props`, which intentionally does **not** inherit the strict analyzer settings, so test code is not held to the production style bar.
- **Run:** `dotnet test CodigoActivo.slnx`. Coverage + summary: `./coverage.ps1` (fails if line coverage drops below the 95% floor; needs `dotnet tool install --global dotnet-reportgenerator-globaltool`).
- **Unit tests** mock the repository/`IUnitOfWork`/`IClock`/`IPasswordHasher` collaborators with NSubstitute. The read path runs against the real projection/`SortMap`/`TextSearch` via `TestSupport/FakeQueryExecutor` over `list.AsQueryable()`.
- **Integration tests** boot the real app through `WebApplicationFactory<Program>` (see `Infrastructure/CodigoActivoWebAppFactory`): real cookie auth, CSRF middleware, controllers, services and repositories, on an EF Core **in-memory** store. Only three things are swapped — the DB provider, a fast fake password hasher, and a fixed `TestClock`. Auth is exercised by real login (`LoginAsAdminAsync`/`LoginAsMemberAsync`); unsafe verbs auto-fetch a CSRF token. Fixed users/ids live in `Infrastructure/TestSeedData`. `Program` is exposed to the test host via `InternalsVisibleTo`. Docker is not assumed, so there is no Testcontainers/Postgres; the handful of relational-only methods (EF `ExecuteUpdate` in `SetExclusiveFeaturedAsync`) are covered by a small **SQLite**-backed test in `Repositories/RepositoryTests` instead.
- **Conventions:** `tests/TESTING_CONVENTIONS.md` is the authoritative guide for writing new tests.
- **Maintenance policy (keep this current):** keep line coverage **≥95%** as code changes — add/adjust tests alongside features. **Do not add regression tests when fixing bugs**; a bug fix should not ship with a test that only exists to pin that specific regression.

### Architecture — 5 projects, strict dependency direction

```
API ──> Composition ──> Application ──> Domain
                    └──> Infrastructure ─> Domain
```

- **Domain** — entities, per-entity repository interfaces, `Result`/`Error`. No external dependencies.
- **Application** — services (business logic), DTOs, mapping extensions. Depends on Domain only.
- **Infrastructure** — EF Core `CodigoActivoDbContext`, repository implementations, Argon2id hashing, local file storage, seeders.
- **Composition** — the only place DI is wired (`DependencyInjection.AddCodigoActivo`). Register every new repository and service here.
- **API** — controllers, middleware, attributes. References **Composition only** — it does not reference Infrastructure directly.

### Key patterns (follow these, they are consistent across the codebase)

- **Result pattern, no exceptions for flow.** Services return `Result<T>` / `Result` (`Domain/Common`). `Error` carries an `ErrorKind` (BadRequest/NotFound/Forbidden/Unauthorized). Write controllers extend `CommandControllerBase` and translate with `ToOk(result)` / `ToNoContent(result)` / `ToProblem(error)` — `ErrorKind` maps to the HTTP status there. Don't hand-roll status codes in controllers.
- **One repository interface per entity** in `Domain/Repositories/IDbRepositories.cs`, each extending `IDbRepository<TEntity>` (generic CRUD) plus entity-specific methods. Never inject the generic `IDbRepository<T>` directly. Implementations live in `Infrastructure/Database/Repositories` and are registered in `Composition/DependencyInjection.cs`.
- **Unit of Work.** Repositories never call `SaveChanges`. Services inject `IUnitOfWork` (which is the `DbContext`) and call `uow.SaveChangesAsync(ct)` once per operation. One deliberate exception: the thumbnail-orphan cascade (`FileService.DeleteIfOrphanedAsync`, best-effort) runs after the entity's own save and performs its own save per cleaned file, because its in-use check must observe the already-persisted reference drop.
- **Identity is passed explicitly.** There is no ambient `ICurrentUser`. Controllers read `UserId` (from `ApiControllerBase`, backed by the auth cookie) and pass `Guid userId` into service methods. Auditable entities (`AuditableEntity`) get `CreatedBy`/`CreatedAt`/`UpdatedBy`/`UpdatedAt` set in the service.
- **DTO naming:** `XRequest` / `XResponse` by purpose; never reuse a DTO across entities — duplicate instead. Entity→DTO conversion is via `ToResponse()` extension methods in `Application/Mapping`.
- **Auth** is cookie-session based (no JWT). `Program.cs` configures the cookie + antiforgery; `CsrfValidationMiddleware` validates `X-CSRF-TOKEN` on unsafe methods. Authorize endpoints with `[AllowAnonymous]`, `[AllowOnlyAdmin]`, or `[AllowOnlySelf]` (in `API/Attributes`).
- **REST + CQRS-ish split.** One REST controller per resource (`XController : ApiControllerBase`, `[Route("api/x")]`) hosts both reads and writes; the read/write split lives in the Application layer. Read services return `Task<PagedResult<XResponse>> ListAsync(XListQuery, ct)` and `Task<Result<XResponse>> GetByIdAsync(...)`, composed over the EF-translatable `Application/Mapping/Projections` via the small `Application/Querying` kernel: `PageQuery` (typed `?page=&pageSize=&sort=` base, self-clamping, max page size 100), `SortMap<T>` (whitelisted dynamic sort + deterministic default), `TextSearch` (accent/case-insensitive `LOWER`+`REPLACE` fold, EF-translated), and `Domain.Common.IQueryExecutor` (materializes queries in Infrastructure so Application stays EF-free). Filters are typed query params per resource (`XListQuery`); list endpoints return a `PagedResult<T>` (`{ items, total, page, pageSize }`) envelope. Write methods return `Result<T>`. `Program.cs` serializes enums as strings and applies `JsonResponseMediaTypeFilter` + `CamelCaseQueryParametersFilter` (`API/OpenApi/`). (There is **no** OData and **no** response-caching layer.)
- **PostgreSQL** uses snake_case via `EFCore.NamingConventions` (`UseSnakeCaseNamingConvention`). Seed data uses fixed GUIDs in `Domain/Constants/DomainConstants.cs` (`SeedIds`).

## Frontend

### Commands (run from `frontend/`)

- Dev server: `npm run dev` → http://localhost:5173. Vite proxies `/api` to `VITE_API_PROXY_TARGET` (default `https://localhost:5001`; set it to the backend you're running, e.g. `https://localhost:7039`).
- Build (includes typecheck): `npm run build`. Typecheck only: `npm run typecheck`.
- Lint: `npm run lint` / `npm run lint:fix`. Format: `npm run format`.
- **FSD architecture lint:** `npm run lint:fsd` (Steiger, config in `steiger.config.ts`). Enforces the Feature-Sliced Design rules (layer import direction, public API per slice, `@x` cross-imports). `generated/` is ignored and `fsd/insignificant-slice` is intentionally disabled. Must stay green.
- **Regenerate the API client:** `npm run api:generate` (Orval reads `frontend/swagger.json`). After a backend API change, refresh `swagger.json` from the running backend first, then regenerate. Never hand-edit anything under `src/shared/api/generated/` — it's overwritten and is excluded from ESLint.

### Architecture — Feature-Sliced Design (FSD)

The whole app follows FSD. Layers, top→bottom, import only downward (`app → pages → widgets → features → entities → shared`); slices in the same layer never import each other (use `@x` cross-import for genuine entity↔entity type deps). Each slice exposes a public API (`index.ts`) and is consumed only through it — never deep-import another slice's internals.

- **`src/shared/`** — reusable, app-agnostic base. Segments: `api/` (`generated/` Orval output, `rest.ts` read helpers e.g. `unwrapOrNull`, `http-client.ts` mutator), `ui/` (PrimeVue-based kit, flat components), `lib/` (formatting, richtext, media, `use-crud-feedback`, `use-server-table` — the REST-backed lazy DataTable composable, …), `config/` (`env`, app constants, nav config). Stays free of any upward (`entities`) import.
- **`src/entities/`** — business entities (`event`, `activity`, `announcement`, `resource`, `partner`, `user`, `account`, `session`, `catalog`, `file`, `organization`). Segments: `model/` (domain types, value-objects, global state — e.g. `session` exports the `useSession()` module-level reactive singleton; **no Pinia**), `api/` (request functions that wrap the generated REST client for reads and writes + map responses + read vue-query hooks + `query-keys.ts`; mapper is internal), `ui/` (entity cards). The Clean Architecture ceremony (repo interfaces, providers, one-line use-cases) was removed — composables call the `api` functions directly.
- **`src/features/`** — user interactions: `auth`, `register`, `account`, and admin CRUD `manage-{events,activities,partners,users,catalogs}` (`model/` composables with vue-query mutations + invalidation, `ui/` form dialogs). Features compose entities, never other features.
- **`src/widgets/`** — composite cross-page blocks. Currently `content-entity-page` (generic admin CRUD page used by the announcements & resources admin pages).
- **`src/pages/`** — one slice per route (`ui/` page + optional `model/`). Public pages at the top level; admin pages grouped under `pages/admin/`. Page-only sections (home sections, the event-detail activity timeline) live inside their page's `ui/`/`model/`, not as widgets.
- **`src/app/`** — bootstrap: `main.ts`, `App.vue`, `providers/`, `router/` (all routes centralized in `router/routes.ts`, lazy-importing pages via their public API and guarded by `@/features/auth` guards), and `layouts/` (`DefaultLayout`, `AdminLayout`, header/footer chrome).

### Key patterns

- **Typed REST reads and writes via the generated client.** Reads wrap the Orval-generated request functions in per-entity `api/` functions; list reads consume the backend's `PagedResult<T>` envelope through `shared/api/rest.ts` helpers (`toPage`, `fetchAllPages` — use it for any "fetch everything" read so results are never truncated at one page, `unwrapOrNull` for 404→null detail reads) or `shared/lib/use-server-table` for lazy admin tables. Writes use the generated `useXxx` hooks — configured (`orval.config.ts`) for `client: 'vue-query'` with `httpClient` as the mutator. `http-client.ts` is where cross-cutting HTTP behavior lives: `credentials: 'include'` for the session cookie, lazy fetch + cache of the CSRF token (re-fetched and the request retried once on 400/403), and `ApiError` shaping from ProblemDetails responses. Put shared HTTP concerns in the mutator.
- **Query keys live with their entity.** Each entity owns its TanStack Query keys in `entities/<x>/api/query-keys.ts` (e.g. `eventQueryKeys`); there is no central `query-keys.ts`. Report/dashboard keys that don't map to an entity are inlined in the feature/page that uses them.
- **No i18n.** UI strings are in Spanish and written inline; there is no translation layer.
- **Rich text** (event/announcement/resource descriptions) uses TipTap and is stored as TipTap JSON; editor images upload to the files API and only the URL is persisted. The backend treats those embedded `/api/files/{id}/content` URLs as references (`RichTextFileReferences`): a file embedded in any description cannot be deleted (FileInUse), and images dropped from a document — or belonging to a deleted entity — are cascade-cleaned like thumbnails.
