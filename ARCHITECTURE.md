# Architecture

`<Codigoactivo/>` is one product composed of two applications:

- `backend/`: an ASP.NET Core Web API on .NET 10.
- `frontend/`: a Vue 3 and TypeScript single-page application built with Vite.

They are deployed as a same-origin system. The browser calls relative `/api/...` URLs; Vite proxies them
during local development, while nginx serves the built SPA and proxies them in Docker. `/sitemap.xml` and
`/robots.txt` are also forwarded to API endpoints that generate content from `APP_BASE_URL` and public data.
The application has no CORS configuration because cross-origin access is not part of its design.

Runtime and environment details belong in [DEPLOYMENT.md](DEPLOYMENT.md). Authentication and trust
boundaries belong in [SECURITY.md](SECURITY.md).

## Backend

The backend combines Clean Architecture, pragmatic domain modeling and a lightweight CQRS split. It uses one
PostgreSQL database and no mediator, message bus, event sourcing or separate read store.

### Project dependencies

| Project                       | Responsibility                                                                 | Direct project dependencies         |
| ----------------------------- | ------------------------------------------------------------------------------ | ----------------------------------- |
| `CodigoActivo.Domain`         | Entities, repository and service ports, domain constants, `Result` and `Error` | None                                |
| `CodigoActivo.Application`    | Use cases, DTOs, validation, mapping, querying and application services        | Domain                              |
| `CodigoActivo.Infrastructure` | EF Core, repositories, file storage, Argon2id, TOTP (Otp.NET), SMTP and the clock | Domain                            |
| `CodigoActivo.Composition`    | Dependency injection and configuration-to-options mapping                      | Domain, Application, Infrastructure |
| `CodigoActivo.API`            | HTTP controllers, middleware, authentication, caching, OpenAPI and startup     | Composition                         |

The `ProjectReference` graph is the source of truth for these boundaries. Tests under
`tests/CodigoActivo.UnitTests/Architecture/` enforce handler and data-shape conventions, but they do not
replace project-reference discipline.

### Domain and persistence

- Entities and aggregate children live in `CodigoActivo.Domain/Entities`. Business invariants are normally
  expressed as guard clauses in Application handlers; `User` also owns account-state transitions.
- Repository interfaces live in `Domain/Repositories`. All repositories in one request share the scoped
  `CodigoActivoDbContext`; `IUnitOfWork.SaveChangesAsync` commits staged changes once.
- Pure service contracts such as `IClock`, `IPasswordHasher`, `ITotpService`, `ISecretProtector`, email
  ports and file storage ports live in Domain. Application-specific contracts such as cache invalidation
  remain in Application.
- EF Core uses Npgsql and snake-case names. IDs are client-generated `Guid` values. Closed value sets are
  enums stored as strings; administrator-managed lookups are tables seeded with stable IDs from `SeedIds`.
- Startup locks the selected demo mode, applies migrations, seeds catalogs, creates the initial administrator
  when the user table is empty, and finally adds demo data when enabled.

Entities are intentionally lightweight. The project does not currently use domain events or general-purpose
value objects, so new work should follow the existing handler-based invariant style unless the architecture is
changed deliberately.

### Commands and queries

Each use case is a message and a sealed handler in
`CodigoActivo.Application/<Area>/Commands|Queries/`. Controllers inject concrete handlers with
`[FromServices]`; there is no MediatR dispatcher.

Queries:

- Start from no-tracking `IQueryable` sources.
- Project to response shapes in the database, using shared expressions or handler-local projections.
- Materialize through `IQueryExecutor`.
- May use `HybridCache` only for immutable catalogs and expensive non-personal aggregates. Public HTTP
  response caching belongs to the API layer.
- Never mutate data, save changes or invalidate caches.

Commands:

- Load tracked aggregates or stage additions and removals.
- Commit through `IUnitOfWork`, normally once per use case.
- Invalidate affected cache tags only after a successful commit.
- May call an aggregate query handler to return a fresh read-after-write response; queries never call
  commands.

### Caching

- HTTP output caching is restricted to read-only public endpoints. Every anonymous GET or HEAD declares
  either a named output-cache policy or an explicit `no-store` decision; endpoints that require
  authentication and user-specific responses are never output cached.
- `HybridCache` is limited to immutable catalogs and non-personal dashboard aggregates. Public list,
  detail and SEO queries are not cached a second time inside Application.
- Both in-memory cache layers are capped at 64 MiB and skip individual payloads larger than 1 MiB. This
  keeps large file responses and unexpectedly large projections from consuming the cache.
- Commands invalidate dependency tags only after a successful commit. The invalidator evicts both
  application entries and HTTP output entries for those tags.
- Both stores are process-local and the current deployment assumes one API replica. A multi-replica
  deployment must add a shared cache store and cross-instance invalidation before scaling the API out.

`RemoveAsync` and `SetFeaturedAsync` are deliberate set-based operations that execute immediately. Do not
combine either with other staged mutations that are expected to share one transaction.

Cross-handler behavior belongs in focused collaborators such as `SignupGate`, `TermsGate`,
`ActivityValidator`, `AccountEmails`, `FileUploadValidator` and `ManualEmailDispatcher` rather than in
controllers.

### Results, validation and HTTP errors

Expected failures are values, not exceptions. Command and single-item query handlers return `Result` or
`Result<T>` with an `ErrorCode`; controllers translate those values through `ApiControllerBase`.

All client-visible failures use:

```text
ApiErrorResponse(Title, Status, Code, TraceId)
```

`ErrorCode` is serialized as a string and is part of the frontend contract. Model validation, authorization,
CSRF failures and unhandled exceptions use the same response shape. HTTP mappings are 400 for bad requests,
401 for unauthenticated callers, 403 for forbidden actions, 404 for missing resources, 409 for conflicts and
500 for unexpected failures.

Request and response records live in `Application/DTOs`; binding query models live in
`Application/Querying`. Mapping is handwritten. User-facing backend text comes from
`Application/Resources/Localization/AppStrings.resx`; logs, exception diagnostics and seeded content are not
UI localization resources.

### Email boundaries

Automatic mail and administrator-authored mail deliberately follow different paths:

- Automatic account and activity messages, including the login codes of the mandatory second factor, use
  `IEmailSender`. `ThrottledEmailSender` consumes an in-memory rate-limit budget and enqueues accepted
  messages in `ChannelEmailDispatcher`.
- Queue workers deliver through `IEmailTransport`. The queue is bounded, volatile and has no retry.
- Administrator-authored bulk mail uses `ManualEmailDispatcher` and `IEmailTransport` directly so the HTTP
  response can report delivery results. It remains subject to recipient and attachment limits.

The wiring is protected by unit tests. Operational values and failure behavior are documented in
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

Slices expose a public API through `index.ts`; other slices import from that public API. `@` maps to `src`.
The generated API client is the only intentional deep-import area.

### API and server state

`frontend/swagger.json` is the committed API contract. Orval generates
`src/shared/api/generated/{endpoints,models}` and uses `src/shared/api/http-client.ts` as its request mutator.
Generated files are disposable and must never be edited manually.

Handwritten `api/requests.ts` modules isolate generated functions. Entity modules then map wire DTOs to
frontend models and expose TanStack Query composables. Query keys come from each entity's `api/query-keys.ts`
factory; call sites do not assemble key arrays themselves.

Entity-scoped reads and mutations stay with the entity. Workflows that depend on session state, multiple
entities or a user interaction belong to a feature. Pages should contain route composition and view-shaping,
not reusable server-state logic.

### Routing, session and presentation

- Route guards resolve authentication and administrator access before entering protected pages.
- Session state is a module-level reactive singleton backed by `GET /api/auth/me`; Pinia is not used.
- Login is two pages: the password form navigates to `/login/verify`, which reads the pending challenge from
  `GET /api/auth/login/two-factor` (held in a cookie, so a reload survives) and only stores the user in the
  session once the second factor is accepted.
- The HTTP client sends cookies, obtains the CSRF token when needed and turns failed responses into `ApiError`.
- `ErrorCode` values map to Spanish messages under `errors.*` in
  `src/shared/i18n/locales/es.json`.
- Light and dark themes use `--ca-*` CSS variables; Element Plus tokens map onto them.
- User-facing frontend copy must go through Vue I18n. Spanish is currently the only locale.

## Changing the API contract

An API change is complete only when both applications agree:

1. Change backend endpoints, DTOs and any `ErrorCode` values.
2. Run the backend in Development and refresh `frontend/swagger.json` from its Swagger document.
3. From `frontend/`, run `npm run api:generate`.
4. Add translations for new error codes in `src/shared/i18n/locales/es.json`.
5. Run backend tests and `npm run check`.

`npm run api:check` generates the client in a temporary location and fails if committed output differs from
`swagger.json`. The command-level workflow is in [CONTRIBUTING.md](CONTRIBUTING.md#changing-the-api).
