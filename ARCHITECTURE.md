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

## Domain language

Documentation and the UI use these terms. The schema, the wire contract and part of the code name some of them
differently: `Assignment*`, `ParentId`, the `assign` and `children` routes, the `ActivityAssignment*` and
`UserChild*` codes, and use cases such as `AssignActivity` and `AddChild`. A participant role is not the
participant user type.

| Term      | Meaning                                                                                              | In code                                        | In the UI       |
| --------- | ---------------------------------------------------------------------------------------------------- | ---------------------------------------------- | --------------- |
| Event     | A dated gathering with categories, terms documents and activities                                    | `Event`                                        | evento          |
| Activity  | A scheduled session of an event with a desired number of people per role                             | `Activity`                                     | actividad       |
| Role      | What a person does in an activity: leader, volunteer or participant                                  | `ActivityRole`                                 | rol             |
| Signup    | A person's place in an activity with a role and a status: requested, confirmed, denied               | `Assignment`                                   | inscripción     |
| Attendee  | A person with a signup in an event; lists filter by status                                           | `EventAttendee*` (read side)                   | asistente       |
| Guardian  | An independent account that cares for dependents                                                     | `User`                                         | tutor           |
| Dependent | A person created as a minor and cared for by a guardian, who never signs in                          | `User` with `ParentId`, `UserStatus.Dependent` | menor           |
| Household | A guardian and their dependents                                                                      | `Household`                                    | unidad familiar |
| Account   | The credentials, second factor and sessions of a person who signs in                                 | `Accounts` feature                             | cuenta          |
| Notice    | A manual email about the recipient's account, signups or activities; reaches every confirmed address | `ManualEmailPurpose.Notice`                    | aviso           |
| News      | A manual email about upcoming events and news; reaches only confirmed addresses with news consent    | `ManualEmailPurpose.News`, `User.NewsConsent`  | novedades       |

## Backend

The backend follows Clean Architecture with a DDD domain model and a CQRS split, organized by feature in every
layer. It has one PostgreSQL database with a write model of aggregates and a read model of its own over the same
tables, domain events published in process after each commit, and no mediator, message bus, transactional
outbox for events or event sourcing.

### Project dependencies

| Project                       | Responsibility                                                                                                             | Direct project dependencies         |
| ----------------------------- | -------------------------------------------------------------------------------------------------------------------------- | ----------------------------------- |
| `CodigoActivo.Domain`         | Aggregates, typed identifiers, value objects, domain events and policies, repository ports, `Result`, `Error` and `DomainErrorCode` | None                                |
| `CodigoActivo.Application`    | Use cases with their validated messages, response contracts, the read model, the use case decorators and the ports they need       | Domain                              |
| `CodigoActivo.Infrastructure` | EF Core write and read contexts, repositories, the unit of work and event publisher, the caching decorator, file storage, Argon2id, HMAC-SHA256 one-time code hashing, TOTP (Otp.NET), the encrypted Data Protection key ring, SMTP, email templates, the clock and the log tables with their Serilog sinks | Domain, Application                 |
| `CodigoActivo.Composition`    | The composition root: registrations by feature, strict configuration-to-options mapping checked on start, data protection and database initialization | Domain, Application, Infrastructure |
| `CodigoActivo.API`            | HTTP controllers and request contracts by feature, the wire `ErrorCode`, middleware, claims and cookies, rate and concurrency limits, output caching, OpenAPI, SEO documents and startup | Composition                         |

The `ProjectReference` graph is the source of truth for these boundaries. The tests under
`tests/CodigoActivo.UnitTests/Architecture/` enforce the layer and feature dependencies (the API reaches neither
Infrastructure nor EF Core) and the conventions in [backend/AGENTS.md](backend/AGENTS.md).

### Domain and persistence

- `CodigoActivo.Domain` has one folder per feature (`Users/`, `Events/`, `Activities/`, `EventCategories/`,
  `TermsDocuments/`, `News/`, `Resources/`, `Partners/`, `Files/`) with its aggregates, typed identifiers, value
  objects, domain events, policies and repository ports; `Common/` holds `Result`, `Error`, `DomainErrorCode`, the
  base classes (`Entity<TId>`, `AggregateRoot<TId>`, `AuditableEntity<TId>`) and the shared value objects.
- Aggregates have a typed identifier (`UserId`, `EventId`, `ActivityId`, `PartnerId`, `StoredFileId`...), a
  `readonly record struct` over a `Guid` with no implicit conversion, so an identifier of one aggregate can never
  be passed for another; `EventTermsAcceptance` is keyed by event, user and document, and `DeletedAccount` by
  the former `UserId`. Values with rules are value objects: `EmailAddress`, `PhoneNumber`, `SpanishNationalId`,
  `RichText`, `DateRange` (the event calendar), `SignupWindow`, `ActivitySchedule`, `HandwrittenSignature`, and
  the security state of `User`: `OneTimeCode` (verification, password reset and login codes), `EmailChange`,
  `Authenticator`, `LoginChallenge`, `PasswordLockout` and `TwoFactorLockout`. EF Core maps them onto plain
  columns through conventions and converters (`Infrastructure/Database/Conversions`), the `User` ones as complex
  properties; the read model keeps plain `Guid` and primitive columns.
  `DateRange.InstantsIn` gives the UTC bounds of the event days that both `DateRange.Covers` (a new activity
  schedule) and `IActivityRepository.AnyOutsideRangeAsync` (a shortened event) compare activities against.
- The aggregates are `User` (a dependent is another `User` referenced by `ParentId`), `UserSession`,
  `DeletedAccount`, `Event` (with its categories and terms links), `Activity` (with its role capacities and
  signups), `EventTermsAcceptance`, `EventRating`, `NewsItem`, `Resource`, `Partner`, `TermsDocument`,
  `EventCategoryType` and `StoredFile`. Their state has private setters: factories create them, returning
  `Result<T>` when a rule can fail, and behavior methods own the invariants and the transitions
  (`User.Verify`, `User.RecordPasswordFailure`, `Activity.RequestAssignment`, `Activity.ChangeAssignmentStatus`,
  `Event.Feature`). Child entities change only through their root. For people, `CreateIndependent`,
  `CreateDependent` and `PlanProfileChange`/`ApplyProfileChange` decide which details an independent account or
  a dependent needs and answer each broken rule with one `DomainErrorCode`.
- Aggregates record domain events for the changes other parts react to (`PartnerUpdated`, `NewsItemDeleted`,
  `AssignmentStatusChanged`, `PasswordChanged`, `ContactDetailsReplaced`, `AccountErased`...). `UnitOfWork`
  collects them when it saves and publishes them only after the commit, or after the explicit transaction the
  save ran in commits; a failed commit publishes nothing and keeps them with the changes they describe. `ICommittedEventsHandler`s see the whole commit once
  (`CacheInvalidationOnCommit` evicts the tags `CacheTagsByEvent` maps; `ReleasedFilesCleanup` removes the files
  that events implementing `IReleasesFiles` stopped referencing, when nothing else references them) and then every
  `IDomainEventListener<T>` of each event runs (`AssignmentDecisionNotification`, `AccountSecurityNotifications`,
  `SessionsEndOnPasswordReplacedOrLocked`, `AccountLockoutLog`). Effects run in the request after the commit and to their end even
  when the client disconnects; a failing effect is logged and neither stops the others nor fails the committed
  request. A crash between the commit and an effect loses that effect, because there is no transactional outbox
  for events (emails themselves go through the email outbox).
- Rich text references a stored file only by its content URL `/api/files/{id}/content`, which saved documents
  keep and the frontend builds and checks; `RichTextFileReferences` in Domain owns it, so it cannot change
  without migrating saved documents.
- Rules that span aggregates are domain policies (`SignupEligibility`, `SignupRoles`, `EarlySignup`,
  `EventTimeline`, `AssignmentDecisions`, `TermsConsent`, `FeaturedSelection`, `AccountErasure`,
  `InitialAdministrator`, `LegalCopy`, `Household`); `EventTimeline` also decides the stage the read side
  shows, so it always agrees with signup.
  Handlers load and call the domain; the commit and the effects that follow it belong to the decorators and the
  domain events; Application collaborators only load what a policy needs or run the checks that need I/O
  (`SignupGate`, `TermsGate`, `ActivityValidator`, `EventCategoryChecker`, `DisposableEmailChecker`).
- Aggregates reference each other by ID, never through navigations. The EF configuration keeps the foreign keys
  and cascades with `HasOne<T>().WithMany().HasForeignKey(...)`; a foreign key whose cascade would change
  another aggregate's children restricts instead, so those children change through their root.
- The closed value sets (user status and type, activity role and modality, signup status and resource type) are
  domain enums (`UserStatus`, `UserType`, `ActivityRole`, `ActivityModality`, `AssignmentStatus`,
  `ResourceType`). Their catalog tables stay only for the names, colors and descriptions shown to people; each
  member keeps the stable `Guid` of its row through `CatalogIds` (`Application/Common/Catalogs`), which the EF
  converters use to store the enum in the foreign key column and the API uses to accept and return catalog
  identifiers. A request whose identifier names no member is invalid (`RequestValidationFailed`), and
  the API maps the identifier to the enum before building the command, so commands never carry catalog
  identifiers.
- Repositories exist only for aggregate roots (`IRepository<T>` plus methods that name their intention), never
  expose `IQueryable` or predicates, and share the scoped `CodigoActivoDbContext`. `AddAsync` and `Remove` only
  stage changes; the few methods that write immediately, without waiting for the unit of work, say so by their
  verb (`Save…`, `End…`, `Purge…`). `IUnitOfWork.SaveChangesAsync` commits staged changes once and turns a
  PostgreSQL unique violation into `UniqueConstraintViolationException`, naming the entity whose table rejected
  the commit, so a handler can answer a lost race instead of failing. A save that finds a row it updates or
  deletes changed or removed meanwhile, for any entity, throws `ConcurrentUpdateException` instead, which the
  API answers with 409 `ConcurrentUpdate`. Users carry a concurrency token mapped onto the `xmin` system column
  (no column of their own), so a save or deletion of a user that another transaction wrote since it was read
  is refused instead of mixing both writes. The credential, code and challenge flows of an account
  (`AccountLock`) and the claim of an unverified holder's email decide on the account as loaded and, only when
  they will act, lock and reload it and check again before they write, never hashing a password with the row
  locked, so concurrent requests for it run one after another and the token only backs them up. A
  person holds at most one signup per activity (primary key on `user_id`, `activity_id`); the signup commands
  answer a concurrent duplicate with `ActivityAssignmentAlreadyExists` (409). At most one event and one news
  item are featured (unique index on `featured`, filtered to featured rows). PostgreSQL checks it row by row and
  EF Core cannot order moving the flag between rows within one save, so `SetEventFeatured` and
  `SetNewsItemFeatured` save the unfeaturing of the current item before featuring the chosen one, in one
  transaction; a concurrent featuring that loses the race fails instead of leaving two items featured.
- `IUnitOfWork.ExecuteInTransactionAsync` runs work in one explicit transaction and, when PostgreSQL aborts it
  to resolve a deadlock, runs it again (three attempts in total) from the state the failed attempt started
  from: entities tracked since are dropped, the others get back their values and state, and the domain events
  raised during the attempt are discarded. A `ConcurrentUpdateException` is not retried: that state still holds
  the values read before the concurrent write, and the checks made before the transaction would not run again.
  `PasswordAttemptGuard` counts a wrong password inside it with the account row locked (`FOR UPDATE`), so
  parallel attempts lose no increment and only one of them sees the account lock. `SavePasswordStateAsync` writes
  only the lockout and login challenge columns (a lock closes the challenge), so it hands just the `PasswordLocked` event to the unit of work, which publishes it
  when that transaction commits; the account's other changes and events wait for their own commit, and it
  advances the account's token only when no other transaction wrote the row since it was loaded. Second-factor
  checks (the handler locks and reloads the account, token included, before `SecondFactorGuard` counts the code)
  and adding a dependent lock and reload the account the same way (`IUserRepository.LockAsync`).
  Deleting an event category locks the category (`FOR UPDATE`, which also holds back tagging an event with it) and
  then the events tagged with it in identifier order, and `UpdateEvent` locks its event before loading it
  (`FOR NO KEY UPDATE`, which leaves signups, ratings and activities referencing the event free), so no event loses
  its last category.
- Users are deleted only through `AccountEraser`, used by `DeleteUser`, `DeleteOwnAccount` and `EmailClaims`,
  which replaces an unverified account whose email a registration or a confirmed email change takes. In one
  such transaction it locks the household, stores the legal copy (composed by `LegalCopy` and serialized by
  Infrastructure as JSON with `SchemaVersion` 5), hands the content credited to the household over to the
  initial administrator and removes the account; any other change staged in the unit of work commits with it.
  `DeletedAccountGuard`, a `SaveChangesInterceptor` that `CodigoActivoDbContext` adds to itself, is the safety
  net that refuses any other user deletion, any deletion of the initial administrator and any change to a
  stored copy. The
  `PurgeDeletedAccounts`, `PurgeUnverifiedAccounts`, `PurgeAdultDependents`, `PurgeUnusedFiles` and
  `RemoveExpiredSessions` commands delete legal copies past their retention, accounts nobody verified within
  24 hours and dependents who came of age (both through `AccountEraser`), uploads no content used within 7 days
  (through `IOrphanFileCleaner`) and expired sessions, and `SendComingOfAgeNotices` warns guardians 30 days
  before; hosted services in Infrastructure only run them on a schedule. What the copy holds and how
  long it is kept is in [SECURITY.md](SECURITY.md#two-factor-authentication).
- Technical ports (`IClock`, `IPasswordHasher`, `IOneTimeCodeHasher`, `ITotpService`, `ISecretProtector`,
  `IFileStorage`, `IAuditTrail`, `IRequestOrigin`, the email, persistence and read ports) live in
  `Application/Abstractions` or in the feature
  that consumes them. Domain declares only the repositories of its aggregates.
- EF Core uses Npgsql and snake-case names. IDs are client-generated `Guid` values wrapped in typed
  identifiers. Closed value sets are domain enums stored as strings or as the `Guid` of their catalog row.
- Startup validates the configuration, applies migrations, seeds catalogs, creates the initial administrator
  (`InitialAdministrator.Id`) when the user table is empty, locks the selected demo mode only once that
  succeeded, refuses a database that has users but not that
  account, and adds demo data and an invented log history when enabled.
- Infrastructure is organized by feature too: each feature folder (`Users/`, `Events/`, `Activities/`, `News/`,
  `Files/`...) holds its EF configurations, repositories and background services; `Database/` keeps the
  contexts, conventions, catalogs, shared configurations and repository base, the unit of work, the query
  executor, seeders and migrations; `Communication/` holds the email delivery shared by every feature, and
  `Caching/`, `Messaging/`, `Security/`, `Time/`, `Diagnostics/` and `Logs/` (the partitioned log tables, their
  own connection pool and the Serilog sinks that write them) the other technical services.

### Commands and queries

Each use case is a message and a sealed handler in `CodigoActivo.Application/<Feature>/Commands|Queries/`.
`Accounts` groups its supporting types by concern: `Passwords/`, `SecondFactor/`, `Sessions/`, `EmailAddresses/`,
`Notifications/` and `Erasure/`; `AccountLock`, `AccountLockoutLog` and `SecurityLog` sit at the `Accounts`
root.
`UseCaseRegistration` (Composition) registers every `ICommandHandler<,>`, `IQueryHandler<,>` and
`IDomainEventListener<>` it finds by the contract it implements, and wraps the handlers in decorators: a command
runs through `LoggingCommandDecorator` (warns about slow use cases), `AuditCommandDecorator` (records it in the
audit log), `ValidationCommandDecorator` and `UnitOfWorkCommandDecorator` (commits once the handler succeeds); a
query through `LoggingQueryDecorator`, `ValidationQueryDecorator` and `CachingQueryDecorator`. Controllers inject the handler contract
(`ICommandHandler<TCommand, TResult>`) with `[FromServices]`, the only way an action receives a service
(implicit service binding is disabled); there is no MediatR dispatcher.

Commands and queries are application messages, not HTTP contracts: they carry typed identifiers and domain
values, and their fields carry the DataAnnotations of their rules (`[property: Required, MaxLength(200),
NotBlank]`), which `MessageValidator` checks, nested messages included, before any command handler runs and
before any query handler whose result can report the failure (`Result`); the criteria of list queries, which
cannot, are checked by MVC when the request binds them. The current day
reaches the date rules through the validation context, never through a service locator. The HTTP request
records live in the API (`API/<Feature>/Contracts/`) with only the standard attributes the OpenAPI document
describes, plus the catalog membership check (`IValidatableObject` through `CatalogIdentifiers`), and map
themselves to the command (`request.ToCommand(id)`). Response records and list criteria stay
in `Application/<Feature>/Contracts/` as the read contracts of the queries. `Gender` and `TwoFactorMethod` are
contract enums that mirror the domain ones (`ContractEnums`); they live with the read rows that store them, so
the read model depends on no feature.

Queries read the database through `IReadStore`: no-tracking `IQueryable` sources of read rows
(`Application/Abstractions/Querying/ReadModel`) that `CodigoActivoReadDbContext` maps onto the same tables,
excluded from migrations. They project to response shapes in the database, materialize through
`IQueryExecutor`, and never depend on repositories or domain entities. There are two exceptions: the dashboard
port `IDashboardCountsReader` (`Application/Reports`, plain SQL in `Infrastructure/Reports`), which counts
whole tables in one round trip and creation times per chart bucket with `width_bucket`, and the log port
`ILogEntryReader` (`Application/Logs`, `Infrastructure/Logs/LogEntryReader`), which reads the partitioned log
tables through read rows of its own in the read context, filters them on their trigram, containment and B-tree
indexes and looks up the names of the accounts and things an audit page mentions, one query per kind. A
query that may be cached implements `ICachedQuery` (duration, tags and key) and `CachingQueryDecorator`
keeps its result in `HybridCache`; only immutable catalogs and expensive non-personal aggregates do. Queries
never mutate, commit or invalidate caches.

Commands load aggregates through repositories, call the domain and stage the changes; the decorator commits them
and the committed domain events evict caches and run the effects. A handler saves on its own only when it must
react to the outcome of the save (a unique violation answered as a conflict, file content to compensate) or
when several saves form one transaction. `ICommand<TResult>` requires a `Result`, so the decorators can refuse
and commit every command alike. Commands return `Result`, or `Result<TId>` with the typed ID of what they
create; they return data only when it exists nowhere else after the operation, such as the authenticator secret
of `BeginAuthenticatorSetup`, the recipient count of `SendEmail*` or the number of rows a scheduled purge
removed. The controller then runs the query that builds the HTTP response. Commands never call query handlers; they may read
the read store only for lookups that change nothing: email audiences, the names the signup notifier shows and
the guardianship `ActingUserPolicy` checks.

`AuditCommandDecorator` hands every command to `IAuditTrail` (`Application/Abstractions/Auditing`) as an
`AuditRecord`: the actor (`ICurrentUser`, or the system for an `IScheduledCommand`), the caller address
(`IRequestOrigin`, implemented by `HttpRequestOrigin` in the API), the `AuditAction` named after the command,
the main entity (the one the handler named through `IAuditSubject`, as login does with the account it found by
email, else the typed ID a `Result<TId>` created, else the first typed ID of the command), the other typed IDs
as related entities and the outcome. `CommandAudit` reads only the typed IDs, never another value. `IUnaudited` keeps a command out (only `SaveEventRating`), a scheduled command is recorded
only when it changed something, and a failure to record never changes the command's result.
`SerilogAuditTrail` (Infrastructure) queues the record to `audit_log_entries`.

Use cases decide who may run them on whose behalf. `ICurrentUser` (implemented by `HttpCurrentUser` in the API)
tells who is signed in; commands and queries take the author, the acting user and the administrator flag from
it, never from their input. `GetAccountQuery` is the one query that reads the caller's own account by its id:
email verification and two-factor login completion answer before the session exists. `ActingUserPolicy` lets
the signed-in user act for themselves or one of their dependents, and an administrator for anyone, reading the guardianship from the read side so commands and queries share it, and
answers anything else with `ActingForAnotherUserForbidden` (403
`AccessDenied` on the wire); signing up or withdrawing a person, checking their agenda and deleting a user
check it. Personal data is narrower: updating a user and adding a dependent accept only the holder or their
guardian, administrators included (`EnsureMayManagePersonalDataAsync`), and changing a password only the
holder. Signing up a household is the exception: it only
takes the signed-in user and their own dependents (`Household.Includes`), administrators included, and answers
anyone else with `ActivityHouseholdMemberNotAllowed`. The `[AllowOnlySelf]` and `[AllowOnlyAdmin]` attributes
refuse early at the HTTP edge; `[AllowOnlySelf]` asks the same policy.

An event can link several terms documents (`event_terms_documents`, each `is_required`/`display_order`), and a
document may ask for a handwritten signature (`requires_signature`). The signup wire contract
(`AssignRequest`/`AssignHouseholdRequest`) carries a `TermsDecisions` list of `{TermsDocumentId, Accepted,
Signature}`, the signature a base64 PNG drawn in the SPA with `signature_pad`. `TermsGate` applies `TermsConsent`
and records each decision as an `EventTermsAcceptance`; `GET /api/events/{eventId}/terms` returns every linked
document with the caller's current decision, and administrators list the decisions of an event under
`/api/events/{eventId}/terms/decisions`. Consent rules are in [SECURITY.md](SECURITY.md#privacy-and-third-parties).

### Caching

- Every anonymous GET/HEAD declares a named output-cache policy or an explicit `no-store`; authenticated or
  user-specific responses are never output cached.
- `HybridCache` is limited to immutable catalogs and non-personal dashboard aggregates; only
  `CachingQueryDecorator` reads and writes it, and only `HttpCacheInvalidator` evicts its tags.
- Both in-memory cache layers are capped at 64 MiB and skip payloads larger than 1 MiB.
- Committed domain events invalidate the tags `CacheTagsByEvent` maps them to, evicting both application and
  HTTP output entries; commands never invalidate caches themselves.
- Both stores are process-local and invalidated in process, which assumes one API replica.
- nginx does not cache API responses. It adds `Cache-Control: no-store` to every proxied response that sets
  none, so browsers and intermediaries never store API data. File content keeps the API's own
  `public, max-age` of a year, `immutable`: a stored file's content never changes (there is no replace
  endpoint; a new image is uploaded as a new file and the one it replaces is removed once unreferenced), so
  a browser that already holds a deleted file may keep showing it from its cache.
- Static files: hashed `/assets/` are `immutable` for a year, the stable-named icons last a day, and
  `index.html` with every SPA route is `no-cache`, so a deployment is picked up on the next navigation.
  Errors and any response without an explicit policy are `no-store`. A tab left open across a deployment
  reloads once when a lazy chunk of the previous build is gone (`stale-build-reload.ts`).
- The SPA holds no data cache: TanStack Query refetches on every mount, focus and reconnect and drops data
  as soon as no view observes it, so freshness is decided only by the API. The one exception is the session
  query (`GET /api/auth/me`), kept until a mutation invalidates it or the session ends.

### Results, validation and HTTP errors

Expected failures are values, not exceptions: command and route-addressed query handlers return `Result` or
`Result<T>` with an `Error` whose code is a `DomainErrorCode` (rules of the model) or an `ApplicationErrorCode`
(use case outcomes such as `UserNotFound`); neither knows HTTP. A query addressed to a resource by route, lists
of its children included, answers that resource's `NotFound` when it does not exist; a filter such as
`?eventId=` only narrows a list. The API owns the wire `ErrorCode` (`API/Errors`): `WireErrorCodes` translates
each code to the wire member of the same name or to an explicit rename (malformed input becomes
`RequestValidationFailed`, acting for another user `AccessDenied`), and `WireErrorCodesTests` fails when a code
has no translation or a wire code is reached by none. Controllers
translate failures through `ApiControllerBase`. All client-visible
failures use `ApiErrorResponse(Title, Status, Code, TraceId)`, including model validation, authorization,
CSRF, rate-limit rejections (`TooManyRequests` with `Retry-After`) and unhandled exceptions. HTTP
mappings: 400 validation, 401 unauthenticated, 403 forbidden, 404 not found, 409 conflict, 429 too many
requests, 500 unexpected. A path under `/api` that matches no endpoint answers 404
`EndpointNotFound` to anyone, and every create answers 201 with the new resource and its `Location`.

`ErrorCode` is serialized as a string and is part of the frontend contract; the API registers the string enum
converter for MVC and for the responses it writes directly, since the Domain carries no serialization
attributes. Request records live in `API/<Feature>/Contracts/`, response records in
`Application/<Feature>/Contracts/`; the shared binding primitives (`PageQuery`, `SortMap`, `TextSearch`) in
`Application/Common/Querying`; day filters turn a local day into UTC instants with `LocalDayRange` (Domain).
Mapping is handwritten.

Each kind of value has one home. Deployment configuration (addresses, credentials, SMTP, the demo choice and
the association's legal identity and contact details, which stay out of the public repository) is the flat
uppercase variables of `.env` and `secrets/`, read once in the composition root (`AddCodigoActivo`) into
options registered with `AddValidatedOptions` and `ValidateOnStart`: a missing setting takes its default, and
a setting that is present but unusable stops the application at start with a message naming the key. Security,
abuse and retention values are fixed in their `*Options` classes, registered with `AddFixedOptions` so no
setting reaches them (tests replace the instance). The association's public identity is `Organization`
(Domain) and `shared/config/organization.ts` (frontend); user-facing text is in the i18n resources. Data protection is registered there once, with the
keys protected at rest in production. User-facing backend text comes from `Application/Common/Localization/AppStrings.resx`; logs and
seeded content are not UI localization resources.

### Email boundaries

Application decides which email is sent, to whom and with which data, through typed models and the composer
ports `IAccountEmailComposer`, `ISignupEmailComposer` and `IManualEmailComposer`; Infrastructure renders the
HTML, the branding and the links to the SPA (`Infrastructure/Communication/Templates`).

Automatic messages (`IEmailSender`/`ThrottledEmailSender`, rate-limited per recipient and globally) and
administrator-authored bulk mail (`ManualEmailDispatcher`) both store their batch through `IEmailOutbox` in
a PostgreSQL outbox, in a unit of work of its own committed before the call returns; a background
`EmailOutboxProcessor` hosted service claims due rows and delivers them through `IEmailTransport`
(`SmtpEmailSender`), with retries and an attempt limit. The send commands (`SendEmailToUsers`,
`SendEmailToEventAttendees`) and the audience-preview queries in `Application/Emails/Queries` share recipient
selection through `ManualEmailAudience`, so a preview always matches what the corresponding send would reach;
the send's `ManualEmailPurpose` decides whether recipients without news consent are left out. A
news batch keeps one shared content whose footer leaves a slot for the unsubscribe link:
`ManualEmailComposer` gives each `EmailRecipient` its own link (a token from `IUnsubscribeTokens`), the outbox
row stores it, and `EmailLayout.LinkUnsubscribe` puts it in place at delivery. The same token also forms the
recipient's one-click unsubscribe address (RFC 8058), which travels on the outbox row to
`EmailMessage.OneClickUnsubscribeUrl` and which `SmtpEmailSender` sends in the `List-Unsubscribe` and
`List-Unsubscribe-Post` headers; both the SPA page and the mail client end in `UnsubscribeCommand`, which
records which of them withdrew the consent. Operational values are in
[DEPLOYMENT.md](DEPLOYMENT.md#email-delivery); security properties are in
[SECURITY.md](SECURITY.md#email-abuse-controls).

Registration and email changes ask `DisposableEmailChecker` whether an address belongs to a disposable
mailbox provider; it looks the domain and its parent domains (`EmailDomains`) up in
`disposable_email_domains` through `IDisposableEmailDomainLookup`. The `DisposableEmailDomainRefresher`
hosted service keeps that table filled from an external list, replacing it only with a download that
`DisposableEmailDomainList` validated ([DEPLOYMENT.md](DEPLOYMENT.md#disposable-email-domains)).

## Frontend

The frontend follows Feature-Sliced Design 2.1, pages first. Imports flow downward, slices at the same layer do
not import one another (the only exception is the `@x` cross-import `entities/user/@x/account.ts`) and each
slice exposes its public API through `index.ts`; `@` maps to `src`. Code starts in the page that uses it and
moves down to a feature or entity only when a second slice needs it. Steiger (`npm run lint:fsd`) enforces the
structure, including that a widget, feature or entity slice is used by at least two slices or by `app`. The
coding rules and their ESLint enforcement are in [frontend/AGENTS.md](frontend/AGENTS.md).

| Layer       | Responsibility                                                                                |
| ----------- | --------------------------------------------------------------------------------------------- |
| `app/`      | Bootstrap, route table and guards, layouts and global styles                                  |
| `pages/`    | One slice per route: its views, view models, single-page API access and helpers               |
| `widgets/`  | Page chrome composed by the app layouts (site header and footer, admin shell)                 |
| `features/` | User actions reused by several pages (logout, emailing users or attendees)                    |
| `entities/` | Business models with their API adapters, query and mutation options, rules and UI             |
| `shared/`   | Generated client, HTTP client, UI primitives, utilities, typed routes, config, i18n, branding |

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
badges, roster, dashboard analytics and the diagnostic and audit logs) live in that page's `api` segment.

Person field rules mirror the backend `User` rules in one place, `entities/user`: `usePersonForm` (and the
`parseIndependentPerson`/`parseDependentPerson` functions behind it) validates and normalizes an independent
account or a dependent and decides when a change needs the caller's password; the registration, profile,
dependents and admin user forms only bind fields and show its messages.

### Views, forms, routing and session

- Forms use `useForm` (`shared/lib/form`) around a pure `read(draft)` that returns the problem of each field
  (a translation key in a `form.problems` group, or `true` for a red border only) and, without problems, the
  value to send. Admin create/edit dialogs follow the `useCrudDialog` state machine.
- Admin tables render through `shared/ui/data-table`: a card with a toolbar (an optional search box bound to
  a column filter, filter pills, the result count and page actions), a chip per active filter except the
  search box, with «Limpiar filtros», the table and a pager hidden while every row fits on the smallest page.
  Header and toolbar filter components register their chips with the table they sit in; page-owned filters
  go in `extraFilters`. Row actions use `shared/ui/row-actions`: inline icon buttons plus a labelled menu.
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
  `src/shared/i18n/locales/es.json`, the only locale.
- Light and dark themes use `--ca-*` CSS variables; Element Plus tokens map onto them.

## Changing the API contract

An API change is complete only when both applications agree:

1. Change backend endpoints, DTOs and any `ErrorCode` values.
2. Run the backend in Development and replace `frontend/swagger.json` with the document from
   `http://localhost:5150/swagger/v1/swagger.json`.
3. From `frontend/`, run `npm run api:generate`.
4. Add translations for new error codes in `src/shared/i18n/locales/es.json`.
5. Run `dotnet test --solution CodigoActivo.slnx` from `backend/` and `npm run check` from `frontend/`.

`npm run api:check` generates the client in a temporary location and fails if committed output differs from
`swagger.json`.
