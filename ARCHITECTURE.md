# Architecture

`<Codigoactivo/>` is one product composed of two applications:

- `backend/`: an ASP.NET Core Web API on .NET 10.
- `frontend/`: a Vue 3 and TypeScript single-page application built with Vite.

They are deployed same-origin. The browser calls relative `/api/...` URLs; Vite proxies them in local
development, nginx proxies them in Docker. `/sitemap.xml` and `/robots.txt` are also forwarded to API
endpoints that generate content from `APP_BASE_URL` and public data. There is no CORS configuration;
cross-origin access is not part of the design.

Runtime and environment details belong in [DEPLOYMENT.md](DEPLOYMENT.md). Authentication and trust boundaries
belong in [SECURITY.md](SECURITY.md).

## Backend

The backend combines Clean Architecture, pragmatic domain modeling and a lightweight CQRS split, with one
PostgreSQL database and no mediator, message bus, event sourcing or separate read store.

### Project dependencies

| Project                       | Responsibility                                                                    | Direct project dependencies         |
| ----------------------------- | --------------------------------------------------------------------------------- | ----------------------------------- |
| `CodigoActivo.Domain`         | Entities, repository and service ports, domain constants, `Result` and `Error`    | None                                |
| `CodigoActivo.Application`    | Use cases, DTOs, validation, mapping, querying and application services           | Domain                              |
| `CodigoActivo.Infrastructure` | EF Core, repositories, file storage, Argon2id, TOTP (Otp.NET), SMTP and the clock | Domain                              |
| `CodigoActivo.Composition`    | Dependency injection and configuration-to-options mapping                         | Domain, Application, Infrastructure |
| `CodigoActivo.API`            | HTTP controllers, middleware, authentication, caching, OpenAPI and startup        | Composition                         |

The `ProjectReference` graph is the source of truth for these boundaries. Tests under
`tests/CodigoActivo.UnitTests/Architecture/` enforce handler and data-shape conventions but do not replace
project-reference discipline.

### Domain and persistence

- Entities live in `CodigoActivo.Domain/Entities`; invariants are normally guard clauses in Application
  handlers, though `User` also owns account-state transitions.
- Repository interfaces live in `Domain/Repositories`. All repositories in one request share the scoped
  `CodigoActivoDbContext`; `IUnitOfWork.SaveChangesAsync` commits staged changes once.
- Pure service contracts (`IClock`, `IPasswordHasher`, `ITotpService`, `ISecretProtector`, email and file
  storage ports) live in Domain; application-specific contracts such as cache invalidation stay in
  Application.
- EF Core uses Npgsql and snake-case names. IDs are client-generated `Guid` values. Closed value sets are
  string enums; administrator-managed lookups are tables seeded with stable IDs from `SeedIds`.
- Startup locks the selected demo mode, applies migrations, seeds catalogs, creates the initial administrator
  when the user table is empty, and adds demo data when enabled.

### Commands and queries

Each use case is a message and a sealed handler in `CodigoActivo.Application/<Area>/Commands|Queries/`.
Controllers inject concrete handlers with `[FromServices]`; there is no MediatR dispatcher.

Queries start from no-tracking `IQueryable` sources, project to response shapes in the database, and
materialize through `IQueryExecutor`. They may use `HybridCache` only for immutable catalogs and expensive
non-personal aggregates, and never mutate, commit or invalidate caches.

Commands load tracked aggregates or stage additions/removals, commit through `IUnitOfWork` (normally once per
use case), and invalidate affected cache tags only after a successful commit. A command may call a query
handler for a read-after-write response; queries never call commands.

### Caching

- Every anonymous GET/HEAD declares a named output-cache policy or an explicit `no-store`; authenticated or
  user-specific responses are never output cached.
- `HybridCache` is limited to immutable catalogs and non-personal dashboard aggregates.
- Both in-memory cache layers are capped at 64 MiB and skip payloads larger than 1 MiB.
- Commands invalidate dependency tags only after commit, evicting both application and HTTP output entries.
- Both stores are process-local, assuming one API replica; scaling out needs a shared store and cross-instance
  invalidation.

`RemoveAsync` and `SetFeaturedAsync` are deliberate set-based operations that execute immediately; do not
combine them with other staged mutations expected to share a transaction. `IEventRatingRepository.SubmitAsync`
is the same kind of exception: it opens and commits its own transaction.

Cross-handler behavior belongs in focused collaborators (`SignupGate`, `TermsGate`, `ActivityValidator`,
`AccountEmails`, `FileUploadValidator`, `ManualEmailDispatcher`) rather than controllers. An event can link
several terms documents (`event_terms_documents`, each `is_required`/`display_order`); the signup wire
contract (`AssignRequest`/`AssignHouseholdRequest`) carries a `TermsDecisions` list of
`{TermsDocumentId, Accepted}` instead of one boolean. `TermsGate` records each decision in
`event_terms_acceptances` (keyed by event, user and document) but never persists the rejection of a required
document, so signup blocks without excluding the user permanently; `GET /api/events/{eventId}/terms` returns
every linked document with the caller's current decision.

### Results, validation and HTTP errors

Expected failures are values, not exceptions: command and single-item query handlers return `Result` or
`Result<T>` with an `ErrorCode`; controllers translate them through `ApiControllerBase`. All client-visible
failures use `ApiErrorResponse(Title, Status, Code, TraceId)`, including model validation, authorization,
CSRF and unhandled exceptions. HTTP mappings: 400 bad request, 401 unauthenticated, 403 forbidden, 404 not
found, 409 conflict, 500 unexpected.

`ErrorCode` is serialized as a string and is part of the frontend contract. Request/response records live in
`Application/DTOs`; binding query models in `Application/Querying`. Mapping is handwritten. User-facing
backend text comes from `Application/Resources/Localization/AppStrings.resx`; logs and seeded content are not
UI localization resources.

### Email boundaries

Automatic messages (`IEmailSender`, throttled and queued through `ChannelEmailDispatcher`/`IEmailTransport`,
bounded and retry-less) and administrator-authored bulk mail (`ManualEmailDispatcher`/`IEmailTransport`
directly, so the HTTP response reports delivery results) follow separate paths. Operational values are in
[DEPLOYMENT.md](DEPLOYMENT.md#email-delivery); security properties are in
[SECURITY.md](SECURITY.md#email-abuse-controls).

## Frontend

The frontend follows Feature-Sliced Design. Imports flow downward, and slices at the same layer do not import
one another. Steiger enforces the structure through `npm run lint:fsd`.

| Layer       | Responsibility                                                                  |
| ----------- | ------------------------------------------------------------------------------- |
| `app/`      | Application bootstrap, router, layouts and global configuration                 |
| `pages/`    | Route-level composition, including the admin pages                              |
| `widgets/`  | Reusable page sections composed from lower layers                               |
| `features/` | User workflows such as authentication, registration and administration          |
| `entities/` | Domain-facing types, API adapters, query state and entity UI                    |
| `shared/`   | Generated client, HTTP client, UI primitives, utilities, configuration and i18n |

Slices expose a public API through `index.ts`; `@` maps to `src`. The generated API client is the only
intentional deep-import area.

### API and server state

`frontend/swagger.json` is the committed API contract. Orval generates
`src/shared/api/generated/{endpoints,models}` using `src/shared/api/http-client.ts` as its request mutator.
Generated files are disposable and must never be edited manually.

Handwritten `api/requests.ts` modules isolate generated functions. Entity modules map wire DTOs to frontend
models and expose TanStack Query composables; query keys come from each entity's `api/query-keys.ts` factory.
Entity-scoped reads/mutations stay with the entity; workflows depending on session state, multiple entities or
user interaction belong to a feature. Pages contain route composition and view-shaping only.

### Routing, session and presentation

- Route guards resolve authentication and administrator access before entering protected pages.
- Session state is a module-level reactive singleton backed by `GET /api/auth/me`; Pinia is not used.
- Login is two pages: the password form navigates to `/login/verify`, which reads the pending challenge from
  `GET /api/auth/login/two-factor` (held in a cookie, so a reload survives) and only stores the user in the
  session once the second factor is accepted.
- The HTTP client sends cookies, obtains the CSRF token when needed and turns failed responses into
  `ApiError`. `ErrorCode` values map to Spanish messages under `errors.*` in
  `src/shared/i18n/locales/es.json`.
- Light and dark themes use `--ca-*` CSS variables; Element Plus tokens map onto them.
- User-facing frontend copy must go through Vue I18n. Spanish is currently the only locale.

## Changing the API contract

An API change is complete only when both applications agree:

1. Change backend endpoints, DTOs and any `ErrorCode` values.
2. Run the backend in Development and replace `frontend/swagger.json` with the document from
   `http://localhost:5150/swagger/v1/swagger.json`.
3. From `frontend/`, run `npm run api:generate`.
4. Add translations for new error codes in `src/shared/i18n/locales/es.json`.
5. Run backend tests and `npm run check`.

`npm run api:check` generates the client in a temporary location and fails if committed output differs from
`swagger.json`. See [CONTRIBUTING.md](CONTRIBUTING.md#changing-the-api) for the command-level checklist.
