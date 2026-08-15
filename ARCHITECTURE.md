# Architecture

`<Codigoactivo/>` is one product made of two independently developed apps that ship together
as a **same-origin** stack:

- **`backend/`** — ASP.NET Core Web API (.NET 10): a 5-project Clean Architecture with DDD-style domain modeling and a lightweight CQRS read/write split, over EF Core + PostgreSQL.
- **`frontend/`** — Vue 3 + Vite single-page app (TypeScript), organized with Feature-Sliced Design.

In every environment the browser talks to a **single origin** and the SPA calls the API with
relative `/api/...` URLs. In development the Vite dev server proxies `/api` to the backend; in
production the `web` (nginx) container serves the SPA and reverse-proxies `/api` to the `api`
container. The root `/sitemap.xml` and `/robots.txt` are also proxied (rewritten to
`/api/sitemap.xml` and `/api/robots.txt`) in both environments — the API generates them from
`APP_BASE_URL` and the public content. There is no CORS layer and no cross-origin token — see [SECURITY.md](SECURITY.md)
for the session/auth model and [DEPLOYMENT.md](DEPLOYMENT.md) for the runtime topology.

## Backend — Clean Architecture, DDD and CQRS

The backend combines three complementary patterns, each in a deliberately pragmatic flavor:

- **Clean Architecture** — five projects with strict, one-directional dependencies (table below); all
  ports are defined in Domain and implemented in Infrastructure, and the Domain project has zero
  package or project references.
- **Domain-driven design** — aggregate-scoped repositories plus a unit of work, seeded lookup
  catalogs referenced through named constants, and error codes that spell out the business
  invariants. Tactical and lightweight: no domain events, no value objects (see
  [the domain model](#the-domain-model-ddd-pragmatic)).
- **CQRS** — every use case is an explicit message plus a handcrafted handler class
  (`XCommand`/`XQuery` records handled by sealed `ICommandHandler`/`IQueryHandler`
  implementations) over the single PostgreSQL database: untracked, DB-side-projected queries vs
  tracked-entity commands committed through the unit of work. No MediatR, no dispatcher, no bus,
  no separate read store — controllers inject each handler directly (see
  [the read/write split](#the-readwrite-split-cqrs-handcrafted-handlers)).

Five projects with strict, one-directional dependencies:

| Project            | Responsibility                                                                          | Depends on                        |
| ------------------ | -------------------------------------------------------------------------------------- | --------------------------------- |
| **Domain**         | Entities, repository interfaces, `Result`/`Error`, domain constants                     | *(nothing)*                       |
| **Application**    | Command/query handlers (business logic), DTOs, validation, entity→DTO mapping, pagination | Domain                            |
| **Infrastructure** | EF Core `DbContext` + repositories, Argon2id hashing, local file storage, MailKit email | Domain                            |
| **Composition**    | The single place dependency injection is wired (`AddCodigoActivo`)                       | Domain, Application, Infrastructure |
| **API**            | Controllers, middleware, auth attributes, `Program` startup                             | Composition                       |

> [!IMPORTANT]
> The layering is enforced by the `ProjectReference` graph and developer discipline only —
> there is **no architecture test**, so a violating reference still compiles. Keep the graph clean by hand.

### The domain model (DDD, pragmatic)

- **Aggregates**: seven roots (`User`, `Event`, `Activity`, `Announcement`, `Resource`, `Partner`,
  `FileEntity`) plus the lookup catalogs (six seeded ones and the admin-managed
  `EventCategoryType` and `TermsDocument`). The composite-key children (`EventCategory`,
  `ActivityRoleCapacity`, `ActivityUserRoleAssignment`, `EventTermsAcceptance`) have no repository
  and no surrogate id — they are loaded and mutated only through their root.
- **One repository interface per aggregate root** (`Domain/Repositories/IDbRepositories.cs`);
  `IUnitOfWork` exposes the single `SaveChangesAsync` that commits a use case's staged changes
  (root + children) atomically — it resolves to the same scoped `DbContext` the repositories share.
- **Ports live in Domain**, implemented in Infrastructure: `IClock`, `IQueryExecutor`,
  `IEmailSender`, `IEmailDispatcher`, `IEmailTransport`, `IPasswordHasher`,
  `ILocalFileSystemRepository`. Email is three ports on purpose: `IEmailSender` is the rate-limited
  one every automatic flow injects (its only implementation is the `ThrottledEmailSender`
  decorator, which spends the quota on the request thread and then hands the message off);
  `IEmailDispatcher` is the in-process background queue it hands off to, so no request ever waits
  on SMTP; and `IEmailTransport` is the raw relay, injected only by the queue's drain loop and by
  the admin-written email service, whose bulk send stays synchronous and exempt. See
  [SECURITY.md](SECURITY.md).
- **Ubiquitous language**: the `ErrorCode` enum names every business invariant
  (`UserCannotRemoveLastAdmin`, `ActivityScheduleOutsideEventRange`, …), and seeded catalog rows
  are referenced via `SeedIds` constants — never looked up by name.
- **Deliberate limits**: entities are mostly data holders — business invariants are guard chains
  in Application handlers returning `Result`/`Error`. `User` is the one entity with behavior
  (OTP, password-reset, verification and login transitions); pure domain rules with no
  dependencies live as static helpers in Domain (`RichTextDocument`, `RichTextFileReferences`).
  There are no domain value objects (email, phone, color and rich-text are raw strings) and no
  domain events. This is a trade-off, not an accident — new invariants follow the existing
  handler-guard style.

### The read/write split (CQRS, handcrafted handlers)

Every use case is one file under `Application/<Aggregate>/Commands|Queries/` holding a sealed
positional record (the message, verb-first: `CreateEventCommand`, `ListEventsQuery`) and its
sealed `<Message>Handler` implementing `ICommandHandler<,>` or `IQueryHandler<,>`
(`Application/Abstractions/Messaging/`). Controllers inject the concrete handler per action via
`[FromServices]` — there is no dispatcher and no MediatR. Binding DTOs (`*ListQuery`, `*Request`)
never implement the message interfaces; the controller wraps them, together with the caller's
identity, into the message. Cross-handler logic lives in per-aggregate collaborators
(`SignupGate`, `TermsGate`, `ActivityValidator`, `ActivitySignupNotifier`, `AccountEmails`,
`OtpValidator`, `EventCategoryChecker`, `FileUploadValidator`, `OrphanFileCleaner`,
`ManualEmailDispatcher`).
The two sides use different mechanisms and never mix:

- **Queries** compose untracked `IQueryable`s (`Repository.Query()` is `AsNoTracking`) projected
  DB-side into response DTOs — shared `Expression` projections in `Application/Mapping/Projections.cs`
  or service-local inline shapes — apply filters and sorting on the projected shape (`SortMap`,
  `TextSearch`, `LocalDayRange`), and materialize only through `IQueryExecutor`. Public and catalog
  reads are wrapped in tag-based `HybridCache`. Queries never mutate, never commit, never invalidate.
- **Commands** load tracked entities (`FindAsync` / `GetForEditAsync`) or stage `Add`/`Remove`,
  commit once via `IUnitOfWork.SaveChangesAsync`, then invalidate the affected cache tags right
  after the commit. Commands that return a body re-read through the query path.
- **Two deliberate set-based escapes** commit immediately, outside the unit of work:
  `RemoveAsync` (`ExecuteDelete`, catalog deletes) and `SetFeaturedAsync` (`ExecuteUpdate`,
  exclusive featuring). They are only ever used in isolation — never combine them with staged
  changes that expect a single transaction.
- What this CQRS is **not**: no MediatR, no dispatcher or bus (controllers inject handlers
  directly), no separate read database, no event sourcing. Architecture tests
  (`UnitTests/Architecture/`) enforce the conventions by reflection: handler naming and
  namespaces, one handler per message, and the guarantee that query handlers never depend on
  `IUnitOfWork`, `ICacheInvalidator` or any email port.

### The Result / Error contract

Handlers never throw for expected failures. They return `Task<Result<TResponse>>` (or
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

- **Handlers** (`Application/<Aggregate>/Commands|Queries/`, one use case per file) use
  primary-constructor DI on repository interfaces — plus `IUnitOfWork`/`IClock` for mutations,
  `IQueryExecutor` for paged reads, and `IPasswordHasher` where needed; a command handler may
  inject a concrete query handler of its own aggregate for read-your-write responses, never the
  reverse. They **never** touch `DbContext`. Registration is explicit (`Add<Aggregate>Handlers`
  in `Composition/DependencyInjection.cs`); a wiring test fails if a handler is not registered.
- **DTOs** (`Application/DTOs/*Dtos.cs`) are records suffixed `...Request` / `...Response`, validated with
  DataAnnotations plus custom attributes (`NotBlank`, `JsonString`, …). Validation failures become an
  `ApiErrorResponse` with `ErrorCode.RequestValidationFailed`.
- **Mapping** is hand-written (`ToResponse()` extensions + `Expression` projections for DB-side `Select`).
  No AutoMapper.
- **Localization** (`Application/Resources/Localization/`): every user-facing backend string is a key in
  `AppStrings.resx`, read through the strongly-typed `AppStrings` accessor — the backend's counterpart to the
  frontend's `es.ts`. It lives in Application because that is the layer that *renders* text: **Domain
  deliberately carries no user-facing prose**, which is why `Error` holds only an `ErrorCode` and its Spanish
  copy lives in the frontend's `errors.*`. Seeded catalog text is content, not UI strings, and stays in the
  database. A second backend language is not a drop-in — see `backend/CLAUDE.md`.
- **Pagination**: list queries derive from `PageQuery` (page size clamped to 100) and return
  `PagedResult<T>` (no `Result` wrapper on lists).
- **Repositories** derive from `Repository<TEntity>`; reads are `AsNoTracking()` except `FindAsync`,
  which returns a tracked entity. Options are plain singletons built from configuration — **not** `IOptions<T>`.

### Data-carrier taxonomy

Every data-holding type has one home, so categories never mix:

| Category | Home |
| --- | --- |
| Persistent entities | `Domain/Entities/` |
| The `Result`/`Error` contract (`Result`, `Error`, `ErrorCode`, `PagedResult<T>`) | `Domain/Common/` — `ErrorCode` and `PagedResult` are also wire types on purpose: `Error` carries the code and `PagedResult` is the `IQueryExecutor` port's return type |
| Port contract types (`EmailMessage` and friends, `DashboardCounts`) | Domain, next to their port — `DashboardCounts`' shape is dictated by `DashboardRepository`'s raw SQL |
| Configuration options | **Never Domain.** `Application/Options/` when Application or API consumes them; Infrastructure (`Communication/`, `Storage/`) when only Infrastructure does. `FileStorageOptions` (storage root) and `FileUploadOptions` (upload limit) are split precisely along that line |
| Wire DTOs | `Application/DTOs/` — **only** `*Request`/`*Response` records, no `Stream` properties |
| Binding queries | `Application/Querying/` (`PageQuery`, the `*ListQuery` family, `DashboardAnalyticsQuery`) |
| CQRS messages | `Application/<Aggregate>/Commands\|Queries/`, colocated with their handler |
| Aggregate models that cross classes (`FileUpload`, `FileContent`, `EmailAttachmentUpload`) | The aggregate's folder |
| Projection rows / local read models | `private sealed record` with `{ get; init; }` nested in their handler — member-init only, never positional (a positional ctor stops EF translating) |
| Single-consumer carriers | Private nested types in their only consumer |

`UnitTests/Architecture/DataShapeTests.cs` pins the two rules that regress silently: the DTOs
namespace stays wire-only, and Domain never grows an `*Options` type again.

### Persistence

EF Core + Npgsql with **snake_case** naming (`FirstName` → `first_name`). Ids are client-generated `Guid`s;
entities extend base classes (`IdentifiableEntity`, `AuditableEntity`, `NamedEntity`, `IFeaturable`).
Closed value sets that are not admin-managed are domain enums stored as **strings**
(`HasConversion<string>()`, e.g. `User.Gender`), so the column reads the same as the JSON contract;
admin-managed lookups stay catalog tables instead.
On startup the API always applies migrations, then runs the idempotent `DatabaseSeeder` (lookup catalogs
keyed by fixed GUIDs in `DomainConstants.SeedIds`). Optional demo data is handled by a separate
`DemoDataSeeder` — see [DEPLOYMENT.md](DEPLOYMENT.md#demo-mode).

## Frontend — Feature-Sliced Design

Layers under `src/`; imports flow **downward only**, and slices in the same layer must not import each
other. The rules are enforced by **Steiger** (`npm run lint:fsd`), not ESLint.

| Layer         | Responsibility                                                                              |
| ------------- | ------------------------------------------------------------------------------------------ |
| **`app/`**    | Composition root: `config/` (Element Plus, TanStack Query), centralized router, layouts     |
| **`pages/`**  | One slice per route (public pages + admin under `pages/admin/`)                             |
| **`widgets/`** | Composite cross-page blocks (e.g. `content-entity-page`, the admin CRUD table widget)       |
| **`features/`** | User interactions: `auth`, `register`, `account`, `send-email`, admin `manage-*`             |
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
copy is resolved from `ErrorCode` to Spanish by `getErrorMessage()` in `src/shared/lib/api-error.ts`,
which looks the code up under the `errors.*` namespace of `src/shared/i18n/locales/es.ts`.

### Routing, state & theming

- **Routing**: per-route `beforeEnter` guards (`requireAuth`, `requireAdmin`, `redirectIfAuthenticated`);
  admin routes are lazy-imported and use the admin layout.
- **Session**: a module-level reactive singleton (no Pinia) that lazily resolves `GET /api/auth/me`.
  Auth is a server-set cookie; only the theme is stored in `localStorage`.
- **Theming**: custom light/dark via CSS variables (`:root` + `.ca-dark` on `<html>`, all `--ca-*` tokens),
  with Element Plus re-skinned by mapping `--el-*` tokens to `--ca-*`.

## How the two apps stay in sync (the API contract)

Any change that crosses the API boundary must be made on both sides in the same pass:

1. **DTOs / endpoints** change in the backend. Records suffixed `...Request`/`...Response` define the wire
   shape; enums serialize as strings.
2. **`frontend/swagger.json`** — the committed contract — is refreshed from the running backend's
   Development-only Swagger endpoint.
3. **`npm run api:generate`** (Orval) regenerates the typed client.
4. **Errors**: a new failure mode adds an `ErrorCode` member in the backend and a Spanish message under
   the frontend's `errors.*` i18n namespace (`src/shared/i18n/locales/es.ts`).
5. **Auth**: a session cookie plus a CSRF token from `GET /api/auth/csrf` (sent as `X-CSRF-TOKEN` on unsafe
   methods). Authorization is a boolean admin flag, not roles — details in [SECURITY.md](SECURITY.md).

The mechanical workflow for steps 1–4 is in [CONTRIBUTING.md](CONTRIBUTING.md#changing-the-api).
