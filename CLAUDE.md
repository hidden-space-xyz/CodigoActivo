# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository layout

Two independent apps, no root-level tooling:

- `backend/` — ASP.NET Core (.NET 10) Web API, Clean Architecture, PostgreSQL via EF Core.
- `frontend/` — Vue 3 + Vite SPA (PrimeVue, Pinia, TanStack Query), API client generated from the backend's OpenAPI doc.

There is no automated test suite in either app (no test projects, no Vitest/Jest). Don't suggest "run the tests" — verify by building and running.

## Backend

### Commands (run from `backend/`)

- Build / lint: `dotnet build` — `EnforceCodeStyleInBuild` is on and Roslynator analyzers run as part of the build, so style/analyzer violations fail the build.
- Run: `dotnet run --project CodigoActivo.API` → http://localhost:5150 (and https://localhost:7039), Swagger UI at `/swagger` in Development.
- Add a migration: `dotnet ef migrations add <Name> --project CodigoActivo.Infrastructure --startup-project CodigoActivo.API`. Migrations and seed data are applied automatically on startup (toggle via `Database:MigrateOnStartup` / `Database:SeedOnStartup`); there is no manual `database update` step in normal dev.
- Packages are centrally managed in `Directory.Packages.props` (no versions in individual `.csproj`). Add a `PackageVersion` there, then a versionless `PackageReference` in the project.

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

- **Result pattern, no exceptions for flow.** Services return `Result<T>` / `Result` (`Domain/Common`). `Error` carries an `ErrorKind` (BadRequest/NotFound/Forbidden/Unauthorized). Controllers extend `ApiControllerBase` and translate with `ToOk(result)` / `ToNoContent(result)` / `ToProblem(error)` — `ErrorKind` maps to the HTTP status there. Don't hand-roll status codes in controllers.
- **One repository interface per entity** in `Domain/Repositories/IDbRepositories.cs`, each extending `IDbRepository<TEntity>` (generic CRUD) plus entity-specific methods. Never inject the generic `IDbRepository<T>` directly. Implementations live in `Infrastructure/Database/Repositories` and are registered in `Composition/DependencyInjection.cs`.
- **Unit of Work.** Repositories never call `SaveChanges`. Services inject `IUnitOfWork` (which is the `DbContext`) and call `uow.SaveChangesAsync(ct)` once per operation.
- **Identity is passed explicitly.** There is no ambient `ICurrentUser`. Controllers read `UserId` (from `ApiControllerBase`, backed by the auth cookie) and pass `Guid userId` into service methods. Auditable entities (`AuditableEntity`) get `CreatedBy`/`CreatedAt`/`UpdatedBy`/`UpdatedAt` set in the service.
- **DTO naming:** `XRequest` / `XResponse` by purpose; never reuse a DTO across entities — duplicate instead. Entity→DTO conversion is via `ToResponse()` extension methods in `Application/Mapping`.
- **Auth** is cookie-session based (no JWT). `Program.cs` configures the cookie + antiforgery; `CsrfValidationMiddleware` validates `X-CSRF-TOKEN` on unsafe methods. Authorize endpoints with `[AllowAnonymous]`, `[AllowOnlyAdmin]`, or `[AllowOnlySelf]` (in `API/Attributes`).
- **Response caching** is attribute-driven: `[Cached("Group")]` on GETs and `[InvalidatesCache("Group", ...)]` on writes, backed by the in-memory `IResponseCacheService`. The group name is usually `nameof(Entity)`. Keep cache groups in sync when adding write endpoints.
- **PostgreSQL** uses snake_case via `EFCore.NamingConventions` (`UseSnakeCaseNamingConvention`). Seed data uses fixed GUIDs in `Domain/Constants/DomainConstants.cs` (`SeedIds`).

## Frontend

### Commands (run from `frontend/`)

- Dev server: `npm run dev` → http://localhost:5173. Vite proxies `/api` to `VITE_API_PROXY_TARGET` (default `https://localhost:5001`; set it to the backend you're running, e.g. `https://localhost:7039`).
- Build (includes typecheck): `npm run build`. Typecheck only: `npm run typecheck`.
- Lint: `npm run lint` / `npm run lint:fix`. Format: `npm run format`.
- **Regenerate the API client:** `npm run api:generate` (Orval reads `frontend/swagger.json`). After a backend API change, refresh `swagger.json` from the running backend first, then regenerate. Never hand-edit anything under `src/shared/api/generated/` — it's overwritten and is excluded from ESLint.

### Architecture — two top-level areas, two different styles

- **`src/modules/`** — the **public-facing site**, organized in Clean Architecture per module (`domain/`, `application/`, `infrastructure/`, `presentation/`). Domain defines entities + repository interfaces; infrastructure provides `Http*Repository` implementations that wrap the generated client and map responses to domain entities; application holds use-cases; presentation holds pages/components/composables/`routes.ts`.
- **`src/features/`** — the **admin area**, deliberately flat: each feature is roughly a `XPage.vue` + `XFormDialog.vue` + `useX.ts` composable that calls the generated vue-query hooks directly (no domain/infrastructure layering). Admin routes (`src/features/admin/router/admin.routes.ts`) use `meta.layout: 'admin'` and are guarded by `requireAdmin`.
- **`src/shared/`** — `api/generated/` (Orval output: vue-query endpoints + models), `api/http-client.ts` (the fetch mutator), `config/env.ts`.
- **`src/app/`** — bootstrap: `main.ts`, `providers/` (Pinia, PrimeVue, TanStack Query client), and the router. All module/feature route arrays are aggregated in `src/app/router/routes.ts`.

### Key patterns

- **Generated client + mutator.** Orval is configured (`orval.config.ts`) for `client: 'vue-query'` with `httpClient` as the mutator. `http-client.ts` is where cross-cutting HTTP behavior lives: `credentials: 'include'` for the session cookie, lazy fetch + cache of the CSRF token (re-fetched and the request retried once on 400/403), and `ApiError` shaping from ProblemDetails responses. Consume the generated `useXxx` hooks; put shared HTTP concerns in the mutator.
- **No i18n.** UI strings are in Spanish and written inline; there is no translation layer.
- **Rich text** (event/announcement/resource descriptions) uses TipTap and is stored as TipTap JSON; editor images upload to the files API and only the URL is persisted.
