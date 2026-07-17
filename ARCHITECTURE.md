# Architecture

`<Codigoactivo/>` is one product made of two independently developed apps that ship together
as a **same-origin** stack:

- **`backend/`** — ASP.NET Core Web API (.NET 10): a 5-project clean architecture over EF Core + PostgreSQL.
- **`frontend/`** — Vue 3 + Vite single-page app (TypeScript), organized with Feature-Sliced Design.

In every environment the browser talks to a **single origin** and the SPA calls the API with
relative `/api/...` URLs. In development the Vite dev server proxies `/api` to the backend; in
production the `web` (nginx) container serves the SPA and reverse-proxies `/api` to the `api`
container. The root `/sitemap.xml` and `/robots.txt` are also proxied (rewritten to
`/api/sitemap.xml` and `/api/robots.txt`) in both environments — the API generates them from
`APP_BASE_URL` and the public content. There is no CORS layer and no cross-origin token — see [SECURITY.md](SECURITY.md)
for the session/auth model and [DEPLOYMENT.md](DEPLOYMENT.md) for the runtime topology.

## Backend — clean architecture

Five projects with strict, one-directional dependencies:

| Project            | Responsibility                                                                          | Depends on                        |
| ------------------ | -------------------------------------------------------------------------------------- | --------------------------------- |
| **Domain**         | Entities, repository interfaces, `Result`/`Error`, domain constants                     | *(nothing)*                       |
| **Application**    | Services (business logic), DTOs, validation, entity→DTO mapping, pagination             | Domain                            |
| **Infrastructure** | EF Core `DbContext` + repositories, Argon2id hashing, local file storage, MailKit email | Domain                            |
| **Composition**    | The single place dependency injection is wired (`AddCodigoActivo`)                       | Domain, Application, Infrastructure |
| **API**            | Controllers, middleware, auth attributes, `Program` startup                             | Composition                       |

> [!IMPORTANT]
> The layering is enforced by the `ProjectReference` graph and developer discipline only —
> there is **no architecture test**, so a violating reference still compiles. Keep the graph clean by hand.

### The Result / Error contract

Services never throw for expected failures. They return `Task<Result<TResponse>>` (or
`Task<Result>` for body-less mutations). `Result` has implicit conversions, so success is
`return dto;` and failure is `return Error.NotFound(ErrorCode.UserNotFound);`.

- **`ErrorCode`** (`Domain/Common/ErrorCode.cs`) is a single enum serialized **as a string** —
  the stable contract the frontend switches on.
- Controllers derive from `ApiControllerBase` and translate results with `ToOk` / `ToCreated` /
  `ToNoContent`. Every failure — from services, model validation, auth, CSRF, or an unhandled
  exception — is emitted in one uniform shape: `ApiErrorResponse(Title, Status, Code, TraceId)`.

| `ErrorKind`   | HTTP status |
| ------------- | ----------- |
| BadRequest    | 400         |
| Unauthorized  | 401         |
| Forbidden     | 403         |
| NotFound      | 404         |
| Conflict      | 409         |
| *(unhandled)* | 500         |

### Layer conventions

- **Services** (`Application/Services/`, interfaces colocated in `Services/Abstractions/IServices.cs`)
  use primary-constructor DI on repository interfaces — plus `IUnitOfWork`/`IClock` for mutations,
  `IQueryExecutor` for paged reads, and `IPasswordHasher` where needed. They **never** touch `DbContext`.
- **Writes** are staged by repositories (`Add`/`Remove`) and committed with `IUnitOfWork.SaveChangesAsync`,
  which resolves to the same scoped `DbContext` the repositories share.
- **DTOs** (`Application/DTOs/*Dtos.cs`) are records suffixed `...Request` / `...Response`, validated with
  DataAnnotations plus custom attributes (`NotBlank`, `JsonString`, …). Validation failures become an
  `ApiErrorResponse` with `ErrorCode.RequestValidationFailed`.
- **Mapping** is hand-written (`ToResponse()` extensions + `Expression` projections for DB-side `Select`).
  No AutoMapper.
- **Pagination**: list queries derive from `PageQuery` (page size clamped to 100) and return
  `PagedResult<T>` (no `Result` wrapper on lists).
- **Repositories** derive from `Repository<TEntity>`; reads are `AsNoTracking()` except `FindAsync`,
  which returns a tracked entity. Options are plain singletons built from configuration — **not** `IOptions<T>`.

### Persistence

EF Core + Npgsql with **snake_case** naming (`FirstName` → `first_name`). Ids are client-generated `Guid`s;
entities extend base classes (`IdentifiableEntity`, `AuditableEntity`, `NamedEntity`, `IFeaturable`).
On startup the API always applies migrations, then runs the idempotent `DatabaseSeeder` (lookup catalogs
keyed by fixed GUIDs in `DomainConstants.SeedIds`). Optional demo data is handled by a separate
`DemoDataSeeder` — see [DEPLOYMENT.md](DEPLOYMENT.md#demo-mode).

## Frontend — Feature-Sliced Design

Layers under `src/`; imports flow **downward only**, and slices in the same layer must not import each
other. The rules are enforced by **Steiger** (`npm run lint:fsd`), not ESLint.

| Layer         | Responsibility                                                                              |
| ------------- | ------------------------------------------------------------------------------------------ |
| **`app/`**    | Composition root: providers (PrimeVue, TanStack Query), centralized router, layouts         |
| **`pages/`**  | One slice per route (public pages + admin under `pages/admin/`)                             |
| **`widgets/`** | Composite cross-page blocks (e.g. `content-entity-page`, the admin CRUD table widget)       |
| **`features/`** | User interactions: `auth`, `register`, `account`, admin `manage-*`                          |
| **`entities/`** | Business entities — `model` (types + reactive state), `api` (requests/mapper/queries/mutations), `ui` (cards) |
| **`shared/`** | Reusable base: API client, UI kit, lib helpers, config                                     |

Slices are **kebab-case** and expose a public API via `index.ts`; other slices import only through it (the
sole deep-import exception is `@/shared/api/generated/…`). `@` → `./src` is the only path alias.

### API client

The typed client is **generated** by Orval from the committed `frontend/swagger.json` into
`src/shared/api/generated/` (`endpoints/` = plain request functions; `models/` = DTO types + the
`ErrorCode` enum). **Generated files are never hand-edited.** Orval is configured with `client: 'vue-query'`,
but the app ignores the generated hooks; instead each entity wraps the generated request functions in
`api/requests.ts`, maps DTO → domain in `api/mapper.ts`, and exposes hand-written TanStack Query
composables in `api/queries.ts` / `api/mutations.ts`.

`src/shared/api/http-client.ts` is the Orval mutator: native `fetch`, `credentials: 'include'`,
same-origin relative `/api/...` URLs, transparent CSRF handling, and `ApiError` on failure. User-facing
copy is resolved from `ErrorCode` to Spanish in `src/shared/api/error-messages.ts`.

### Routing, state & theming

- **Routing**: per-route `beforeEnter` guards (`requireAuth`, `requireAdmin`, `redirectIfAuthenticated`);
  admin routes are lazy-imported and use the admin layout.
- **Session**: a module-level reactive singleton (no Pinia) that lazily resolves `GET /api/auth/me`.
  Auth is a server-set cookie; only the theme is stored in `localStorage`.
- **Theming**: custom light/dark via CSS variables (`:root` + `.ca-dark` on `<html>`, all `--ca-*` tokens),
  with PrimeVue's Aura preset re-skinned by mapping `--p-*` tokens to `--ca-*`.

## How the two apps stay in sync (the API contract)

Any change that crosses the API boundary must be made on both sides in the same pass:

1. **DTOs / endpoints** change in the backend. Records suffixed `...Request`/`...Response` define the wire
   shape; enums serialize as strings.
2. **`frontend/swagger.json`** — the committed contract — is refreshed from the running backend's
   Development-only Swagger endpoint.
3. **`npm run api:generate`** (Orval) regenerates the typed client.
4. **Errors**: a new failure mode adds an `ErrorCode` member in the backend and a Spanish message in the
   frontend's `error-messages.ts`.
5. **Auth**: a session cookie plus a CSRF token from `GET /api/auth/csrf` (sent as `X-CSRF-TOKEN` on unsafe
   methods). Authorization is a boolean admin flag, not roles — details in [SECURITY.md](SECURITY.md).

The mechanical workflow for steps 1–4 is in [CONTRIBUTING.md](CONTRIBUTING.md#changing-the-api).
