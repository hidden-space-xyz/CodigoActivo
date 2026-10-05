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

The backend follows Clean Architecture with a DDD domain model and a CQRS split, organized by feature in every
layer. It has one PostgreSQL database with a write model of aggregates and a read model of its own over the same
tables, and no mediator, message bus or event sourcing.

### Project dependencies

| Project                       | Responsibility                                                                                                             | Direct project dependencies         |
| ----------------------------- | -------------------------------------------------------------------------------------------------------------------------- | ----------------------------------- |
| `CodigoActivo.Domain`         | Aggregates, value objects, domain policies, repository ports of the aggregates, `Result` and `Error`                       | None                                |
| `CodigoActivo.Application`    | Use cases, wire contracts, the read model and the ports they need                                                          | Domain                              |
| `CodigoActivo.Infrastructure` | EF Core write and read contexts, repositories, file storage, Argon2id, TOTP (Otp.NET), SMTP, email templates and the clock | Domain, Application                 |
| `CodigoActivo.Composition`    | Dependency injection, configuration-to-options mapping and database initialization                                         | Domain, Application, Infrastructure |
| `CodigoActivo.API`            | HTTP controllers, middleware, claims and cookies, caching, OpenAPI, SEO documents and startup                              | Composition                         |

The `ProjectReference` graph is the source of truth for these boundaries. Tests under
`tests/CodigoActivo.UnitTests/Architecture/` also check the layer references (the API reaches neither
Infrastructure nor EF Core), the handler and data-shape conventions, and that repositories exist only for
aggregate roots.

### Domain and persistence

- `CodigoActivo.Domain` has one folder per feature (`Users/`, `Events/`, `Activities/`, `EventCategories/`,
  `TermsDocuments/`, `News/`, `Resources/`, `Partners/`, `Files/`) with its aggregates, value objects, policies
  and repository ports; `Common/` holds `Result`, `Error`, `ErrorCode`, the base classes and `SeedIds`.
- The aggregates are `User` (a dependent is another `User` referenced by `ParentId`), `UserSession`,
  `DeletedAccount`, `Event` (with its categories and terms links), `Activity` (with its role capacities and
  signups), `EventTermsAcceptance`, `EventRating`, `NewsItem`, `Resource`, `Partner`, `TermsDocument`,
  `EventCategoryType` and `StoredFile`. Their state has private setters: factories create them, returning
  `Result<T>` when a rule can fail, and behavior methods own the invariants and the transitions
  (`User.Verify`, `User.RecordPasswordFailure`, `Activity.RequestAssignment`, `Activity.ChangeAssignmentStatus`,
  `Event.Feature`). Child entities change only through their root. For people, `CreateIndependent`,
  `CreateDependent` and `PlanProfileChange`/`ApplyProfileChange` decide which details an independent account or
  a dependent needs and answer each broken rule with one `ErrorCode`.
- Rules that span aggregates are domain policies (`SignupRoles`, `EarlySignup`, `TermsConsent`,
  `FeaturedSelection`, `AccountErasure`, `InitialAdministrator`, `LegalCopy`, `Household`). Handlers load,
  call the domain, persist and run the effects after the commit; the checks that need I/O are Application
  collaborators (`SignupGate`, `TermsGate`, `ActivityValidator`, `EventCategoryChecker`,
  `DisposableEmailChecker`).
- Aggregates reference each other by ID, never through navigations. The EF configuration keeps the foreign keys
  and cascades with `HasOne<T>().WithMany().HasForeignKey(...)`.
- The fixed catalogs (user status and type, activity role and modality, signup status and resource type) are
  reference data seeded with stable IDs from `SeedIds`, not aggregates: they have no repository, and commands
  check them through the read store.
- Repositories exist only for aggregate roots (`IRepository<T>` plus methods that name their intention), never
  expose `IQueryable` or predicates, and share the scoped `CodigoActivoDbContext`. `IUnitOfWork.SaveChangesAsync`
  commits staged changes once and turns a PostgreSQL unique violation into `UniqueConstraintViolationException`,
  naming the entity whose table rejected the commit, so a handler can answer a lost race instead of failing. A
  person holds at most one assignment per activity (unique index on `user_id`, `activity_id`); the signup
  commands answer a concurrent duplicate with `ActivityAssignmentAlreadyExists` (409). At most one event and one
  news item are featured (unique index on `featured`, filtered to featured rows). PostgreSQL checks it row by
  row and EF Core cannot order moving the flag between rows within one save, so `SetEventFeatured` and
  `SetNewsItemFeatured` save the unfeaturing of the current item before featuring the chosen one, in one
  transaction; a concurrent featuring that loses the race fails instead of leaving two items featured.
- `IUnitOfWork.ExecuteInTransactionAsync` runs work in one explicit transaction and, when PostgreSQL aborts it
  to resolve a deadlock, runs it again (three attempts in total) after discarding what the failed attempt
  staged. `PasswordAttemptGuard` counts a wrong password inside it with the account row locked (`FOR UPDATE`),
  so parallel attempts lose no increment and only one of them sees the account lock. Second-factor checks and
  adding a dependent lock and reload the account the same way (`IUserRepository.LockAsync`).
- Users are deleted only through `AccountEraser`, used by `DeleteUser`, `DeleteOwnAccount` and `EmailClaims`,
  which replaces an unverified account whose email a registration or a confirmed email change takes. In one
  such transaction it locks the household, stores the legal copy (composed by `LegalCopy` and serialized by
  Infrastructure as JSON with `SchemaVersion` 1), hands the content credited to the household over to the
  initial administrator and removes the account; any other change staged in the unit of work commits with it.
  `DeletedAccountGuard`, a `SaveChangesInterceptor` that `CodigoActivoDbContext` adds to itself, is the safety
  net that refuses any other user deletion and any deletion of the initial administrator. The
  `PurgeDeletedAccounts` and `RemoveExpiredSessions` commands delete legal copies past their retention and
  expired sessions; hosted services in Infrastructure only run them on a schedule. What the copy holds and how
  long it is kept is in [SECURITY.md](SECURITY.md#two-factor-authentication).
- Technical ports (`IClock`, `IPasswordHasher`, `ITotpService`, `ISecretProtector`, `IFileStorage`, the email,
  persistence and read ports) live in `Application/Abstractions` or in the feature that consumes them. Domain
  declares only the repositories of its aggregates.
- EF Core uses Npgsql and snake-case names. IDs are client-generated `Guid` values. Closed value sets are
  string enums; fixed catalogs are tables seeded with stable IDs from `SeedIds`.
- Startup locks the selected demo mode, applies migrations, seeds catalogs, creates the initial administrator
  (`SeedIds.Users.InitialAdministrator`) when the user table is empty, refuses a database that has users but
  not that account, and adds demo data when enabled.

### Commands and queries

Each use case is a message and a sealed handler in `CodigoActivo.Application/<Feature>/Commands|Queries/`, with
its wire contracts in `<Feature>/Contracts/`. Controllers inject concrete handlers with `[FromServices]`; there
is no MediatR dispatcher.

Queries read only through `IReadStore`: no-tracking `IQueryable` sources of read rows
(`Application/Abstractions/Querying/ReadModel`) that `CodigoActivoReadDbContext` maps onto the same tables,
excluded from migrations. They project to response shapes in the database, materialize through
`IQueryExecutor`, and never depend on repositories or domain entities. They may use `HybridCache` only for
immutable catalogs and expensive non-personal aggregates, and never mutate, commit or invalidate caches.

Commands load aggregates through repositories, call the domain, commit through `IUnitOfWork` (normally once per
use case), and invalidate affected cache tags only after a successful commit. They return `Result`, or
`Result<Guid>` with the ID of what they create; they return data only when it exists nowhere else after the
operation, such as the authenticator secret of `BeginAuthenticatorSetup` or the recipient count of
`SendEmail*`. The controller then runs the query that builds the HTTP response. Commands never call query
handlers; they may read the read store only for lookups that change nothing: fixed catalogs, email audiences
and the names the signup notifier shows.

An event can link several terms documents (`event_terms_documents`, each `is_required`/`display_order`); the
signup wire contract (`AssignRequest`/`AssignHouseholdRequest`) carries a `TermsDecisions` list of
`{TermsDocumentId, Accepted}`. `TermsGate` applies `TermsConsent` and records each decision as an
`EventTermsAcceptance` (keyed by event, user and document) but never persists the rejection of a required
document, so signup blocks without excluding the user permanently; `GET /api/events/{eventId}/terms` returns
every linked document with the caller's current decision.

### Caching

- Every anonymous GET/HEAD declares a named output-cache policy or an explicit `no-store`; authenticated or
  user-specific responses are never output cached.
- `HybridCache` is limited to immutable catalogs and non-personal dashboard aggregates.
- Both in-memory cache layers are capped at 64 MiB and skip payloads larger than 1 MiB.
- Commands invalidate dependency tags only after commit, evicting both application and HTTP output entries.
- Both stores are process-local, assuming one API replica; scaling out needs a shared store and cross-instance
  invalidation.
- nginx does not cache API responses. It adds `Cache-Control: no-store` to every proxied response that sets
  none, so browsers and intermediaries never store API data; file content keeps the API's own
  `private, no-cache` and revalidates by `ETag`.
- Static files: hashed `/assets/` are `immutable` for a year, the stable-named icons last a day, and
  `index.html` with every SPA route is `no-cache`, so a deployment is picked up on the next navigation.
  Errors and any response without an explicit policy are `no-store`. A tab left open across a deployment
  reloads once when a lazy chunk of the previous build is gone (`stale-build-reload.ts`).
- The SPA holds no data cache: TanStack Query refetches on every mount, focus and reconnect and drops data
  as soon as no view observes it, so freshness is decided only by the API. Retaining query data needs every
  consumer to handle retained values and failed refetches first.

### Results, validation and HTTP errors

Expected failures are values, not exceptions: command and single-item query handlers return `Result` or
`Result<T>` with an `ErrorCode`; controllers translate them through `ApiControllerBase`. All client-visible
failures use `ApiErrorResponse(Title, Status, Code, TraceId)`, including model validation, authorization,
CSRF and unhandled exceptions. HTTP mappings: 400 validation, 401 unauthenticated, 403 forbidden, 404 not
found, 409 conflict, 500 unexpected. A path under `/api` that matches no endpoint answers 404
`EndpointNotFound` to anyone, and every create answers 201 with the new resource and its `Location`.

`ErrorCode` is serialized as a string and is part of the frontend contract; the API registers the string enum
converter for MVC and for the responses it writes directly, since the Domain carries no serialization
attributes. Request/response records live in each feature's `Contracts/`; the shared binding primitives
(`PageQuery`, `SortMap`, `TextSearch`, `LocalDayRange`) in `Application/Common/Querying`. Mapping is
handwritten. User-facing backend text comes from `Application/Common/Localization/AppStrings.resx`; logs and
seeded content are not UI localization resources.

### Email boundaries

Application decides which email is sent, to whom and with which data, through typed models and the composer
ports `IAccountEmailComposer`, `ISignupEmailComposer` and `IManualEmailComposer`; Infrastructure renders the
HTML, the branding and the links to the SPA (`Infrastructure/Communication/Templates`).

Automatic messages (`IEmailSender`/`ThrottledEmailSender`, rate-limited per recipient and globally) and
administrator-authored bulk mail (`ManualEmailDispatcher`) both store their batch through `IEmailOutbox` in
a PostgreSQL outbox, in a unit of work of its own committed before the call returns; a background
`EmailOutboxProcessor` hosted service claims due rows and delivers them through `IEmailTransport`
(`SmtpEmailSender`), with retries and an attempt limit. `ManualEmailDispatcher` and the audience-preview
queries in `Application/Emails/Queries` share recipient selection through `ManualEmailAudience`, so a
preview always matches what the corresponding send would reach. Operational values are in
[DEPLOYMENT.md](DEPLOYMENT.md#email-delivery); security properties are in
[SECURITY.md](SECURITY.md#email-abuse-controls).

Registration and email changes ask `DisposableEmailChecker` whether an address belongs to a disposable
mailbox provider; it looks the domain and its parent domains (`EmailDomains`) up in
`disposable_email_domains` through `IDisposableEmailDomainRepository`. The `DisposableEmailDomainRefresher`
hosted service keeps that table filled from an external list, replacing it only with a download that
`DisposableEmailDomainList` validated ([DEPLOYMENT.md](DEPLOYMENT.md#disposable-email-domains)).

## Frontend

The frontend follows Feature-Sliced Design 2.1, pages first. Imports flow downward, slices at the same layer do
not import one another and each slice exposes its public API through `index.ts`; `@` maps to `src`. Code starts
in the page that uses it and moves down to a feature or entity only when a second slice needs it. Steiger
(`npm run lint:fsd`) enforces the structure, including that no slice has a single consumer.

| Layer       | Responsibility                                                                       |
| ----------- | ------------------------------------------------------------------------------------ |
| `app/`      | Bootstrap, route table and guards, layouts and global styles                         |
| `pages/`    | One slice per route: its views, view models, single-page API access and helpers      |
| `widgets/`  | Page chrome shared by several pages (site header and footer, admin shell)            |
| `features/` | User actions reused by several pages (logout, emailing users or attendees)           |
| `entities/` | Business models with their API adapters, query and mutation options, rules and UI    |
| `shared/`   | Generated client, HTTP client, UI primitives, utilities, typed routes, config, i18n  |

Segments mean the same in every slice: `ui` components, `model` state and pure rules, `api` server access,
`lib` pure helpers and `config` static content.

### API and server state

`frontend/swagger.json` is the committed API contract. Orval generates
`src/shared/api/generated/{endpoints,models}` using `src/shared/api/http-client.ts` as its request mutator.
Generated files are disposable and must never be edited manually.

The generated client is an anti-corruption boundary: only `api` segments import it, and their `mapper.ts`
turns wire DTOs into the slice's own `model/types.ts`. Dates stay ISO strings; views format them. Entities
expose query keys (`xKeys`), `queryOptions` factories (`xQueries`), paged sources for `useServerTable` and
`usePagedList`, and `mutationOptions` factories (`xMutations`) that list the keys they refresh in
`meta.invalidates`; the query client invalidates them after a successful mutation, and a page adds keys of
another slice with `alsoInvalidates`. Reports read by a single page (event statistics, attendees, ratings,
badges, roster and dashboard analytics) live in that page's `api` segment.

Person field rules mirror the backend `User` rules in one place, `entities/user`: `usePersonForm` (and the
`parseIndependentPerson`/`parseDependentPerson` functions behind it) validates and normalizes an independent
account or a dependent and decides when a change needs the caller's password; the registration, profile,
minors and admin user forms only bind fields and show its messages.

### Views, forms, routing and session

- Views are humble: `.vue` files bind to `model` composables and never create mutations, touch the query cache
  or inspect API errors. Business rules are pure functions in `model` or `lib`.
- Forms use `useForm` (`shared/lib/form`) around a pure `read(draft)` that returns the problem of each field
  (a translation key in a `form.problems` group, or `true` for a red border only) and, without problems, the
  value to send. Admin create/edit dialogs follow the `useCrudDialog` state machine.
- Route names and params are typed in `shared/routes`. Route guards resolve authentication and administrator
  access before entering protected pages; detail pages receive their path params as props.
- The session is the `GET /api/auth/me` query in the TanStack Query cache, read through `useSession`; ending it
  clears every other cached query. Pinia is not used. A query or mutation the API refuses because the
  session is gone (`AuthenticationRequired`, `CurrentUserNotFound`) ends it too, and protected pages then
  return to login (`app/router/session-expiry.ts`).
- Login is two pages: the password form navigates to `/login/verify`, which reads the pending challenge from
  `GET /api/auth/login/two-factor` (held in a cookie, so a reload survives) and only stores the user in the
  session once the second factor is accepted.
- The HTTP client sends cookies, obtains the CSRF token when needed and turns failed responses into
  `ApiError`. `ErrorCode` values map to Spanish messages under `errors.*` in
  `src/shared/i18n/locales/es.json`.
- Light and dark themes use `--ca-*` CSS variables; Element Plus tokens map onto them.
- User-facing frontend copy must go through Vue I18n. Spanish is currently the only locale. Only `shared` and
  `app` use the global i18n instance; components call `useI18n` and pure helpers receive a `Translate`.
- ESLint turns these boundaries into build failures: the generated client outside `api` segments, the global
  i18n instance or Element Plus imperative feedback in pages, widgets, features or entities, and mutations,
  cache access or API errors in views.

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
